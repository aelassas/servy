using CommandLine;
using Servy.CLI.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;

namespace Servy.CLI.Helpers
{
    internal static class Helper
    {
        /// <summary>
        /// Gets the verb name defined by the <see cref="VerbAttribute"/> on the specified options class.
        /// </summary>
        /// <typeparam name="T">The options class decorated with <see cref="VerbAttribute"/>.</typeparam>
        /// <returns>The verb name defined in the <see cref="VerbAttribute"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the class does not have a <see cref="VerbAttribute"/>.</exception>
        public static string GetVerbName<T>()
        {
            var verbAttr = typeof(T).GetCustomAttribute<VerbAttribute>();
            if (verbAttr == null)
                throw new InvalidOperationException($"Class {typeof(T).Name} does not have a VerbAttribute.");

            return verbAttr.Name;
        }

        /// <summary>
        /// Gets all verb names defined by <see cref="VerbAttribute"/> on all types in the current assembly.
        /// </summary>
        /// <returns>An array of verb names.</returns>
        public static string[] GetVerbs()
        {
            var verbs = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Select(t => t.GetCustomAttribute<VerbAttribute>())
                .Where(attr => attr != null)
                .Select(attr => attr.Name.ToLowerInvariant())
                .ToList();

            verbs.Add("version");
            verbs.Add("--version");

            return verbs.ToArray();
        }

        /// <summary>
        /// Executes the output handling for a command result by printing its message to the console
        /// and returning its exit code.
        /// </summary>
        /// <param name="result">The <see cref="CommandResult"/> containing the message and exit code from the executed command.</param>
        /// <returns>The integer exit code associated with the command result.</returns>
        public static int PrintAndReturn(CommandResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
                Console.WriteLine(result.Message);

            return result.ExitCode;
        }

        /// <summary>
        /// Awaits the execution of a <see cref="CommandResult"/>-returning task,
        /// prints its message to the console, and returns an appropriate exit code.
        /// </summary>
        /// <param name="task">The task that produces a <see cref="CommandResult"/>.</param>
        /// <returns>
        /// A <see cref="Task{Int32}"/> representing the asynchronous operation,
        /// with 0 if <see cref="CommandResult.Success"/> is <c>true</c>, or 1 otherwise.
        /// </returns>
        public static async Task<int> PrintAndReturnAsync(Task<CommandResult> task)
        {
            var result = await task;
            Console.WriteLine(result.Message);
            return result.Success ? 0 : 1;
        }

    }
}
