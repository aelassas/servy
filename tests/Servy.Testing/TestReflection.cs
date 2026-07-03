using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Servy.Testing
{
    /// <summary>
    /// Centralized test infrastructure reflection engine to interact securely with public and non-public
    /// fields, properties, and methods without copy-pasting raw BindingFlags boilerplate.
    /// </summary>
    public static class TestReflection
    {
        private const BindingFlags PrivateInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PrivateStaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PublicStaticFlags = BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Safely fetches the instance value of an internal or private field, traversing parent base types if required.
        /// </summary>
        /// <typeparam name="T">The expected data type of the field being extracted.</typeparam>
        /// <param name="obj">The target object instance containing the field.</param>
        /// <param name="fieldName">The exact string identifier name of the non-public instance field.</param>
        /// <returns>The extracted casted value typed as <typeparamref name="T"/> from the object field context.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not discovered anywhere within the inheritance lookup traversal hierarchy.</exception>
        public static T GetField<T>(object obj, string fieldName)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();
            FieldInfo? fieldInfo = null;

            while (type != null && fieldInfo == null)
            {
                fieldInfo = type.GetField(fieldName, PrivateInstanceFlags);
                type = type.BaseType;
            }

            if (fieldInfo == null)
            {
                throw new ArgumentException($"Field '{fieldName}' could not be found on type {obj.GetType().Name} or its base classes.");
            }

            return (T)fieldInfo.GetValue(obj)!;
        }

        /// <summary>
        /// Safely fetches the value of an internal or private STATIC field from the targeted type context.
        /// </summary>
        /// <typeparam name="T">The expected data type of the static field being extracted.</typeparam>
        /// <param name="type">The declarative <see cref="Type"/> context metadata layer of the static target.</param>
        /// <param name="fieldName">The exact string identifier name of the non-public static field.</param>
        /// <returns>The extracted casted value typed as <typeparamref name="T"/> from the type system domain.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not located on the target class definition.</exception>
        public static T GetFieldStatic<T>(Type type, string fieldName)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var fieldInfo = type.GetField(fieldName, PrivateStaticFlags);
            if (fieldInfo == null)
            {
                throw new ArgumentException($"Static field '{fieldName}' could not be found on type {type.Name}.");
            }

            return (T)fieldInfo.GetValue(null)!;
        }

        /// <summary>
        /// Directly alters a non-public instance field state.
        /// </summary>
        /// <param name="obj">The target object instance containing the field to modify.</param>
        /// <param name="fieldName">The exact string identifier name of the non-public instance field.</param>
        /// <param name="value">The raw input assignment reference data state payload to inject into the field slot.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is not discovered inside the inheritance lookup traversal hierarchy.</exception>
        public static void SetField(object obj, string fieldName, object? value)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();
            FieldInfo? fieldInfo = null;

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
        /// Directly alters a non-public STATIC field state on the specified type context.
        /// </summary>
        /// <param name="type">The declarative <see cref="Type"/> context metadata layer of the static target.</param>
        /// <param name="fieldName">The exact string identifier name of the non-public static field to alter.</param>
        /// <param name="value">The raw input assignment reference data state payload to inject into the type slot.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is missing from the designated target class.</exception>
        public static void SetFieldStatic(Type type, string fieldName, object? value)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var fieldInfo = type.GetField(fieldName, PrivateStaticFlags);
            if (fieldInfo == null)
            {
                throw new ArgumentException($"Static field '{fieldName}' could not be found on type {type.Name}.");
            }

            fieldInfo.SetValue(null, value);
        }

        /// <summary>
        /// Safely executes an internal or private instance method on the target object, cleanly unwrapping TargetInvocationException lines.
        /// </summary>
        /// <param name="obj">The execution host runtime instance context.</param>
        /// <param name="methodName">The exact string signature identifier mapping of the non-public target method.</param>
        /// <param name="args">An optional array vector containing arguments passed sequentially down into the invocation layer.</param>
        /// <returns>The functional return type payload evaluation block from the invoked target, or null if void.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="methodName"/> cannot be bound inside the target object's type or its inheritance hierarchy.</exception>
        public static object? InvokeNonPublic(object obj, string methodName, params object?[]? args)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();
            MethodInfo? method = null;

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

            try
            {
                return method.Invoke(obj, args);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
                throw;
            }
        }

        /// <summary>
        /// Safely executes an internal or private static method on the specified target type, cleanly unwrapping TargetInvocationException lines.
        /// </summary>
        /// <param name="type">The declarative <see cref="Type"/> token context architecture structure definition metadata layer.</param>
        /// <param name="methodName">The exact string signature identifier mapping of the non-public static method target.</param>
        /// <param name="args">An optional array vector containing arguments passed sequentially down into the invocation layer.</param>
        /// <returns>The functional return type payload evaluation block from the invoked static target, or null if void.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="methodName"/> cannot be bound onto the target class framework metadata description.</exception>
        public static object? InvokeNonPublicStatic(Type type, string methodName, params object?[]? args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var method = type.GetMethod(methodName, PrivateStaticFlags);
            if (method == null)
            {
                throw new ArgumentException($"Static method '{methodName}' could not be found on type {type.Name}.");
            }

            try
            {
                return method.Invoke(null, args);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
                throw;
            }
        }

        /// <summary>
        /// Safely executes a PUBLIC static method on the specified target type, cleanly unwrapping TargetInvocationException lines.
        /// </summary>
        /// <param name="type">The declarative <see cref="Type"/> token context definition metadata layer.</param>
        /// <param name="methodName">The exact string signature identifier mapping of the public static method target.</param>
        /// <param name="args">An optional array vector containing arguments passed sequentially down into the invocation layer.</param>
        /// <returns>The functional return type payload evaluation block from the invoked public static target, or null if void.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="methodName"/> cannot be bound onto the public static metadata layout.</exception>
        public static object? InvokeStatic(Type type, string methodName, params object?[]? args)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var method = type.GetMethod(methodName, PublicStaticFlags);
            if (method == null)
            {
                throw new ArgumentException($"Public static method '{methodName}' could not be found on type {type.Name}.");
            }

            try
            {
                return method.Invoke(null, args);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
                throw;
            }
        }
    }
}