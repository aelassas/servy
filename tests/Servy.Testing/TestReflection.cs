using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Servy.Testing
{
    /// <summary>
    /// Reflection helpers for tests: non-public instance and static fields (get and set),
    /// public and non-public constructors, non-public instance and static methods,
    /// public static methods, and enumeration of public instance properties.
    /// </summary>
    /// <remarks>
    /// Field access is non-public only, so a public field is reported as not found. The property
    /// helper enumerates public instance properties and does not read or write their values, and
    /// there is no invoker for a public instance method. Lookups walk the base-type chain and throw
    /// <see cref="ArgumentException"/> rather than returning null, so a renamed member fails the
    /// test loudly instead of silently no-opping.
    /// </remarks>
    public static class TestReflection
    {
        private const BindingFlags PrivateInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PrivateStaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PublicStaticFlags = BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Creates an instance of <typeparamref name="T"/> using a constructor (public or non-public) that matches the provided argument types.
        /// </summary>
        /// <typeparam name="T">The target type to instantiate.</typeparam>
        /// <param name="args">The arguments to pass to the constructor.</param>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when a matching constructor cannot be found.</exception>
        public static T CreateInstance<T>(params object?[]? args)
        {
            var type = typeof(T);

            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            ConstructorInfo? targetCtor = null;
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != (args?.Length ?? 0)) continue;

                bool isMatch = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var arg = args![i];
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

            return (T)InvokeUnwrapped(targetCtor, null, args)!;
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

            var fieldInfo = FindInHierarchy(obj.GetType(), fieldName, "Field",
                t => t.GetField(fieldName, PrivateInstanceFlags));

            return (T)fieldInfo.GetValue(obj)!;
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

            var fieldInfo = FindInHierarchy(type, fieldName, "Static field",
                t => t.GetField(fieldName, PrivateStaticFlags));

            return (T)fieldInfo.GetValue(null)!;
        }

        /// <summary>
        /// Sets a non-public instance field, searching base types if needed.
        /// </summary>
        /// <param name="obj">The object instance containing the field.</param>
        /// <param name="fieldName">The name of the non-public instance field.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not found on <paramref name="obj"/> or its base classes.</exception>
        public static void SetField(object obj, string fieldName, object? value)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var fieldInfo = FindInHierarchy(obj.GetType(), fieldName, "Field",
                t => t.GetField(fieldName, PrivateInstanceFlags));

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
        public static void SetFieldStatic(Type type, string fieldName, object? value)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var fieldInfo = FindInHierarchy(type, fieldName, "Static field",
                t => t.GetField(fieldName, PrivateStaticFlags));

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
        public static object? InvokeNonPublic(object obj, string methodName, params object?[]? args)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var method = FindInHierarchy(obj.GetType(), methodName, "Method",
                t => t.GetMethod(methodName, PrivateInstanceFlags));

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
        public static object? InvokeNonPublicStatic(Type type, string methodName, params object?[]? args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var method = FindInHierarchy(type, methodName, "Static method",
                t => t.GetMethod(methodName, PrivateStaticFlags));

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
        public static object? InvokePublicStatic(Type type, string methodName, params object?[]? args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var method = FindInHierarchy(type, methodName, "Public static method",
                t => t.GetMethod(methodName, PublicStaticFlags));

            return InvokeUnwrapped(method, null, args);
        }

        /// <summary>
        /// Walks <paramref name="type"/> and its base types applying <paramref name="lookup"/>, returning the
        /// first non-null result, or throwing <see cref="ArgumentException"/> naming <paramref name="memberKind"/>.
        /// </summary>
        /// <typeparam name="TMember">The kind of member being looked up.</typeparam>
        /// <param name="type">The type to start the walk from.</param>
        /// <param name="memberName">The name of the member, used in the not-found message.</param>
        /// <param name="memberKind">A noun describing the member kind, used in the not-found message.</param>
        /// <param name="lookup">The per-type lookup to apply, returning null when the type does not declare the member.</param>
        /// <returns>The first member found on <paramref name="type"/> or one of its base types.</returns>
        /// <exception cref="ArgumentException">Thrown when no type in the chain declares the member.</exception>
        private static TMember FindInHierarchy<TMember>(
            Type type, string memberName, string memberKind, Func<Type, TMember?> lookup)
            where TMember : MemberInfo
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var found = lookup(current);
                if (found != null)
                {
                    return found;
                }
            }

            throw new ArgumentException($"{memberKind} '{memberName}' could not be found on type {type.Name} or its base classes.");
        }

        /// <summary>
        /// Invokes the specified method or constructor on the target instance or type and unwraps <see cref="TargetInvocationException"/>.
        /// </summary>
        private static object? InvokeUnwrapped(MethodBase method, object? target, object?[]? args)
        {
            try
            {
                if (method is ConstructorInfo ctor)
                {
                    return ctor.Invoke(args);
                }

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
        public static IEnumerable<PropertyInfo> GetMappedProperties<T>(IEnumerable<string>? excludedProperties = null)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            return properties.Where(p => p.CanRead && !(excludedProperties?.Contains(p.Name) ?? false)).ToList();
        }
    }
}
