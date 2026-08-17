using Servy.Core.DTOs;
using System.Reflection;

namespace Servy.Core.Validation
{
    /// <summary>
    /// Shared reflection helper for discovering and evaluating <see cref="ServicePathAttribute"/> properties across DTOs and options objects.
    /// </summary>
    public static class ServicePathValidator
    {
        /// <summary>
        /// Evaluates all properties decorated with <see cref="ServicePathAttribute"/> on <paramref name="target"/>
        /// and returns the first violation encountered, or <c>null</c> if all paths pass.
        /// </summary>
        /// <typeparam name="T">The type containing path properties to validate.</typeparam>
        /// <param name="target">The instance whose path properties should be inspected.</param>
        /// <param name="validatePath">A delegate function that verifies whether a path string exists/is valid for files or directories.</param>
        /// <returns>A <see cref="ServicePathViolation"/> detailing the failure, or <c>null</c> if valid.</returns>
        public static ServicePathViolation? FindFirstViolation<T>(T target, Func<string?, bool, bool> validatePath)
           => FindAllViolations(target, validatePath).FirstOrDefault();

        /// <summary>
        /// Evaluates all properties decorated with <see cref="ServicePathAttribute"/> on <paramref name="target"/>
        /// and returns all violations encountered.
        /// </summary>
        /// <typeparam name="T">The type containing path properties to validate.</typeparam>
        /// <param name="target">The instance whose path properties should be inspected.</param>
        /// <param name="validatePath">A delegate function that verifies whether a path string exists/is valid for files or directories.</param>
        /// <returns>A collection of <see cref="ServicePathViolation"/> detailing all failures.</returns>
        public static IEnumerable<ServicePathViolation> FindAllViolations<T>(T target, Func<string?, bool, bool> validatePath)
        {
            if (target == null) yield break;

            foreach (var property in target.GetType().GetProperties().OrderBy(p => p.MetadataToken))
            {
                var attr = property.GetCustomAttribute<ServicePathAttribute>();
                if (attr == null) continue;

                var value = property.GetValue(target) as string;
                bool isEmpty = string.IsNullOrWhiteSpace(value);

                // 1. Mandatory presence check
                if (attr.Required && isEmpty)
                {
                    yield return new ServicePathViolation(property, attr, value, isMissing: true);
                }

                // 2. File/Directory existence and syntax validity check
                if (!isEmpty && !validatePath(value, attr.IsFile))
                {
                    yield return new ServicePathViolation(property, attr, value, isMissing: false);
                }
            }
        }
    }
}
