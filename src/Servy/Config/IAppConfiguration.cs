using System.ComponentModel;

namespace Servy.Config
{
    /// <summary>
    /// Provides configuration settings and dynamic state properties for the Servy application.
    /// </summary>
    public interface IAppConfiguration : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets a value indicating whether the companion Servy Manager application is available on the system.
        /// </summary>
        bool IsManagerAppAvailable { get; }

        /// <summary>
        /// Gets the file path to the published Servy Manager application executable.
        /// </summary>
        string? ManagerAppPublishPath { get; }

        /// <summary>
        /// Gets a value indicating whether WPF software rendering is forced for the current session.
        /// </summary>
        bool ForceSoftwareRendering { get; }

        /// <summary>
        /// Gets the display language saved for this application, as a <c>--lang</c> value
        /// (<c>system</c>, <c>en</c>, <c>zh-CN</c>, <c>fr</c>, <c>de</c>, <c>es</c>, <c>ja</c>, <c>it</c>, <c>pt-PT</c> or <c>pt-BR</c>),
        /// or <see langword="null"/> when it is not known.
        /// </summary>
        /// <remarks>
        /// Passed to Servy.Manager.exe when this application launches it, so both applications show the same language
        /// even when their settings files are not in the same folder.
        /// </remarks>
        string? LanguageCode { get; }
    }
}
