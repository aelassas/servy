using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Servy.Testing
{
    /// <summary>
    /// Reflection helper methods for tests to access public and non-public fields, properties, constructors, and methods.
    /// </summary>
    public static class TestReflection
    {
        private const BindingFlags PrivateInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PrivateStaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PublicStaticFlags = BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Creates an instance of <typeparamref name="T"/> using a non-public constructor that matches the provided argument types.
        /// </summary>
        /// <typeparam name="T">The target type to instantiate.</typeparam>
        /// <param name="args">The arguments to pass to the non-public constructor.</param>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when a matching non-public constructor cannot be found.</exception>
        public static T CreateInstanceNonPublic<T>(params object[] args)
        {
            var type = typeof(T);
            var paramTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? Type.EmptyTypes;

            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            ConstructorInfo targetCtor = null;
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != (args?.Length ?? 0)) continue;

                bool isMatch = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var arg = args[i];
                    if (arg != null && !parameters[i].ParameterType.IsAssignableFrom(arg.GetType()))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    targetCtor = ctor;
                    break;
                }
            }

            if (targetCtor == null)
            {
                throw new ArgumentException($"Matching constructor could not be found on type {type.Name}.");
            }

            try
            {
                return (T)targetCtor.Invoke(args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable
            }
        }

        /// <summary>
        /// Gets the value of a non-public instance field, searching base types if needed.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="obj">The object instance containing the field.</param>
        /// <param name="fieldName">The name of the non-public instance field.</param>
        /// <returns>The value of the field cast to <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not found on <paramref name="obj"/> or its base classes.</exception>
        public static T GetField<T>(object obj, string fieldName)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();
            FieldInfo fieldInfo = null;

            while (type != null && fieldInfo == null)
            {
                fieldInfo = type.GetField(fieldName, PrivateInstanceFlags);
                type = type.BaseType;
            }

            if (fieldInfo == null)
            {
                throw new ArgumentException($"Field '{fieldName}' could not be found on type {obj.GetType().Name} or its base classes.");
            }

            return (T)fieldInfo.GetValue(obj);
        }

        /// <summary>
        /// Gets the value of a non-public static field on the specified type, searching base types if needed.
        /// </summary>
        /// <typeparam name="T">The expected type of the static field value.</typeparam>
        /// <param name="type">The target type.</param>
        /// <param name="fieldName">The name of the non-public static field.</param>
        /// <returns>The value of the static field cast to <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not found on <paramref name="type"/> or its base classes.</exception>
        public static T GetFieldStatic<T>(Type type, string fieldName)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var currentType = type;
            FieldInfo fieldInfo = null;

            while (currentType != null && fieldInfo == null)
            {
                fieldInfo = currentType.GetField(fieldName, PrivateStaticFlags);
                currentType = currentType.BaseType;
            }

            if (fieldInfo == null)
            {
                throw new ArgumentException($"Static field '{fieldName}' could not be found on type {type.Name} or its base classes.");
            }

            return (T)fieldInfo.GetValue(null);
        }

        /// <summary>
        /// Sets a non-public instance field, searching base types if needed.
        /// </summary>
        /// <param name="obj">The object instance containing the field.</param>
        /// <param name="fieldName">The name of the non-public instance field.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not found on <paramref name="obj"/> or its base classes.</exception>
        public static void SetField(object obj, string fieldName, object value)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();
            FieldInfo fieldInfo = null;

            while (type != null && fieldInfo == null)
            {
                fieldInfo = type.GetField(fieldName, PrivateInstanceFlags);
                type = type.BaseType;
            }

            if (fieldInfo == null)
            {
                throw new ArgumentException($"Field '{fieldName}' could not be found on type {obj.GetType().Name} or its base classes.");
            }

            fieldInfo.SetValue(obj, value);
        }

        /// <summary>
        /// Sets a non-public static field on the specified type, searching base types if needed.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="fieldName">The name of the non-public static field.</param>
        /// <param name="value">The value to assign to the static field.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not found on <paramref name="type"/> or its base classes.</exception>
        public static void SetFieldStatic(Type type, string fieldName, object value)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var currentType = type;
            FieldInfo fieldInfo = null;

            while (currentType != null && fieldInfo == null)
            {
                fieldInfo = currentType.GetField(fieldName, PrivateStaticFlags);
                currentType = currentType.BaseType;
            }

            if (fieldInfo == null)
            {
                throw new ArgumentException($"Static field '{fieldName}' could not be found on type {type.Name} or its base classes.");
            }

            fieldInfo.SetValue(null, value);
        }

        /// <summary>
        /// Invokes a non-public instance method, searching base types if needed, and unwraps <see cref="TargetInvocationException"/>.
        /// </summary>
        /// <param name="obj">The target object instance.</param>
        /// <param name="methodName">The name of the non-public instance method.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        /// <returns>The return value of the method, or null if void.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="methodName"/> is not found on <paramref name="obj"/> or its base classes.</exception>
        public static object InvokeNonPublic(object obj, string methodName, params object[] args)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();
            MethodInfo method = null;

            // Traverse the inheritance hierarchy to find private methods on base classes
            while (type != null && method == null)
            {
                method = type.GetMethod(methodName, PrivateInstanceFlags);
                type = type.BaseType;
            }

            if (method == null)
            {
                throw new ArgumentException($"Method '{methodName}' could not be found on type {obj.GetType().Name} or its base classes.");
            }

            return InvokeUnwrapped(method, obj, args);
        }

        /// <summary>
        /// Invokes a non-public static method on the specified type, searching base types if needed, and unwraps <see cref="TargetInvocationException"/>.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="methodName">The name of the non-public static method.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        /// <returns>The return value of the static method, or null if void.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="methodName"/> is not found on <paramref name="type"/> or its base classes.</exception>
        public static object InvokeNonPublicStatic(Type type, string methodName, params object[] args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var currentType = type;
            MethodInfo method = null;

            while (currentType != null && method == null)
            {
                method = currentType.GetMethod(methodName, PrivateStaticFlags);
                currentType = currentType.BaseType;
            }

            if (method == null)
            {
                throw new ArgumentException($"Static method '{methodName}' could not be found on type {type.Name} or its base classes.");
            }

            return InvokeUnwrapped(method, null, args);
        }

        /// <summary>
        /// Invokes a public static method on the specified type, searching base types if needed, and unwraps <see cref="TargetInvocationException"/>.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="methodName">The name of the public static method.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        /// <returns>The return value of the static method, or null if void.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="methodName"/> is not found on <paramref name="type"/> or its base classes.</exception>
        public static object InvokePublicStatic(Type type, string methodName, params object[] args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var currentType = type;
            MethodInfo method = null;

            while (currentType != null && method == null)
            {
                method = currentType.GetMethod(methodName, PublicStaticFlags);
                currentType = currentType.BaseType;
            }

            if (method == null)
            {
                throw new ArgumentException($"Public static method '{methodName}' could not be found on type {type.Name} or its base classes.");
            }

            return InvokeUnwrapped(method, null, args);
        }

        /// <summary>
        /// Invokes the specified method on the target instance or type and unwraps <see cref="TargetInvocationException"/>.
        /// </summary>
        private static object InvokeUnwrapped(MethodInfo method, object target, object[] args)
        {
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable
            }
        }

        /// <summary>
        /// Returns the readable public instance properties of <typeparamref name="T"/>, excluding any specified in <paramref name="excludedProperties"/>.
        /// </summary>
        /// <typeparam name="T">The type whose properties to retrieve.</typeparam>
        /// <param name="excludedProperties">An optional collection of property names to exclude.</param>
        /// <returns>A collection of readable public instance properties for <typeparamref name="T"/>.</returns>
        public static IEnumerable<PropertyInfo> GetMappedProperties<T>(IEnumerable<string> excludedProperties = null)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            return properties.Where(p => p.CanRead && !(excludedProperties?.Contains(p.Name) ?? false)).ToList();
        }
    }
}
