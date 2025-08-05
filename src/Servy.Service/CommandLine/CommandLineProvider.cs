using System;

namespace Servy.Service
{
    /// <inheritdoc cref="ICommandLineProvider"/>
    public class CommandLineProvider : ICommandLineProvider
    {
        /// <inheritdoc />
        public string[] GetArgs() => Environment.GetCommandLineArgs();
    }
}
