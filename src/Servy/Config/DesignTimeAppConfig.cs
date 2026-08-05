using System.ComponentModel;

namespace Servy.Config
{
    /// <summary>
    /// A lightweight implementation of <see cref="IAppConfiguration"/> specifically for
    /// the XAML designer to prevent constructor-chaining failures.
    /// </summary>
    public class DesignTimeAppConfig : IAppConfiguration
    {
        public bool IsManagerAppAvailable => true;

        public string? ManagerAppPublishPath => Core.Config.AppConfig.DefaultManagerAppPublishPath;

        public bool ForceSoftwareRendering => false;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }
}
