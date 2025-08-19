namespace Servy.UI.Services
{
    /// <summary>
    /// A mock implementation of <see cref="IFileDialogService"/> used for design-time support.
    /// Returns static paths instead of opening real file/folder dialogs.
    /// </summary>
    public class DesignTimeFileDialogService : IFileDialogService
    {
        /// <summary>
        /// Simulates selecting an executable file by returning a static path.
        /// </summary>
        /// <returns>A sample executable path.</returns>
        public string OpenExecutable()
        {
            return @"C:\DesignTime\Servy.exe";
        }

        /// <summary>
        /// Simulates selecting an executable file by returning a static path.
        /// </summary>
        /// <returns>A sample executable path.</returns>
        public string OpenXml()
        {
            return @"C:\DesignTime\Service.xml";
        }

        /// <summary>
        /// Simulates selecting an executable file by returning a static path.
        /// </summary>
        /// <returns>A sample executable path.</returns>
        public string OpenJson()
        {
            return @"C:\DesignTime\Service.json";
        }

        /// <summary>
        /// Simulates selecting a startup directory by returning a static path.
        /// </summary>
        /// <returns>A sample folder path.</returns>
        public string OpenFolder()
        {
            return @"C:\DesignTime";
        }

        /// <summary>
        /// Simulates saving a file by returning a static path.
        /// </summary>
        /// <param name="title">The title of the (simulated) dialog.</param>
        /// <returns>A sample file path.</returns>
        public string SaveFile(string title)
        {
            return $@"C:\DesignTime\{title.Replace(" ", "_").ToLowerInvariant()}.log";
        }

        /// <summary>
        /// Simulates saving an XML configuration file by returning a static path.
        /// </summary>
        /// <param name="title">The title of the (simulated) dialog.</param>
        /// <returns>A sample file path.</returns>
        public string SaveXml(string title)
        {
            return @"C:\DesignTime\Service.xml";
        }

        /// <summary>
        /// Simulates saving a JSON configuration file by returning a static path.
        /// </summary>
        /// <param name="title">The title of the (simulated) dialog.</param>
        /// <returns>A sample file path.</returns>
        public string SaveJson(string title)
        {
            return @"C:\DesignTime\Service.json";
        }
    }
}
