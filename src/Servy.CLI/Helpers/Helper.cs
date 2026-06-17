using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Servy.CLI.Helpers
{
    public static class Helper
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
        [UnconditionalSuppressMessage("Trimming", 
            "IL2026:Members annotated with 'RequiresUnreferencedCode' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "Verb types are manually preserved via the main Parser configuration.")]
        public static string[] GetVerbs()
        {
            var verbs = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Select(t => t.GetCustomAttribute<VerbAttribute>())
                .Where(attr => attr != null)
                .Select(attr => attr!.Name.ToLowerInvariant())
                .ToList();

            verbs.Add("version");
            verbs.Add("--version");

            return verbs.ToArray();
        }

        /// <summary>
        /// Prints the message from a <see cref="CommandResult"/> to the console 
        /// and returns its defined exit code.
        /// </summary>
        /// <param name="result">The command result to process.</param>
        /// <returns>The exit code defined in the <paramref name="result"/>.</returns>
        public static int PrintAndReturn(CommandResult result)
        {
            if (result == null) return 1;

            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(result.Message);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(result.Message);
                }
                Console.ResetColor();
            }

            return result.ExitCode;
        }

        /// <summary>
        /// Awaits the execution of a <see cref="CommandResult"/>-returning task,
        /// prints its message to the console, and returns its defined exit code.
        /// </summary>
        /// <param name="task">The task that produces a <see cref="CommandResult"/>.</param>
        /// <returns>
        /// A <see cref="Task{Int32}"/> representing the asynchronous operation,
        /// containing the exit code defined in the resulting <see cref="CommandResult"/>.
        /// </returns>
        /// <remarks>
        /// Respects <see cref="CommandResult.ExitCode"/> and skips printing for null 
        /// or whitespace messages, matching the sync variant.
        /// </remarks>
        public static async Task<int> PrintAndReturnAsync(Task<CommandResult> task)
        {
            var result = await task;

            // Re-use the sync logic to ensure behavior is identical across both paths
            return PrintAndReturn(result);
        }

        /// <summary>
        /// Tries to parse the string input into a <see cref="ConfigFileType"/>.
        /// </summary>
        /// <param name="input">The input string (xml or json).</param>
        /// <param name="fileType">The parsed <see cref="ConfigFileType"/> if successful.</param>
        /// <param name="error">Error message if parsing fails.</param>
        /// <returns>True if parsing succeeds; otherwise false.</returns>
        public static bool TryParseFileType(string? input, out ConfigFileType fileType, out string error)
        {
            var trimmed = input?.Trim();
            if (string.IsNullOrEmpty(trimmed)
                || trimmed.IndexOf(',') >= 0
                || char.IsDigit(trimmed[0])
                || !Enum.TryParse(trimmed, true, out fileType)
                || !Enum.IsDefined(typeof(ConfigFileType), fileType))
            {
                fileType = default;
                error = Strings.Msg_InvalidConfigFileType;
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
