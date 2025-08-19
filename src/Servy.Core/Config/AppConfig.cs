﻿

using System;
using System.IO;

namespace Servy.Core.Config
{
    /// <summary>
    /// Provides application-wide configuration.
    /// </summary>
    public static class AppConfig
    {

        #region Constants

        /// <summary>
        /// Servy's current version.
        /// </summary>
        public static readonly string Version = "1.0.0";

        /// <summary>
        /// The file name of the Servy.Core assembly (without extension).
        /// Used when copying or loading the core library dynamically.
        /// </summary>
        public static readonly string ServyCoreDllName = "Servy.Core";

        /// <summary>
        /// The base file name (without extension) of the Servy Service UI executable.
        /// </summary>
        public static readonly string ServyServiceUIFileName = "Servy.Service.Net48";

        /// <summary>
        /// The full file name (with extension) of the Servy Service UI executable.
        /// </summary>
        public static readonly string ServyServiceUIExe = $"{ServyServiceUIFileName}.exe";

        /// <summary>
        /// Servy Service Debug Folder.
        /// </summary>
        public static readonly string ServyServiceUIDebugFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Servy\bin\Debug\");

        /// <summary>
        /// Servy Configuration App Publish Path.
        /// </summary>
        public static readonly string ConfigrationAppPublishDebugPath = Path.Combine(ServyServiceUIDebugFolder, "Servy.exe");

        /// <summary>
        /// Default Servy Configuration App Publish Path (Release).
        /// </summary>
        public static readonly string DefaultConfigrationAppPublishPath = @".\Servy.exe";

        /// <summary>
        /// Servy Manager Release Folder.
        /// </summary>
        public static readonly string ServyManagerReleaseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Servy.Manager\bin\Debug\");

        /// <summary>
        /// Servy Manager App Publish Path.
        /// </summary>
        public static readonly string ManagerAppPublishDebugPath = Path.Combine(ServyManagerReleaseFolder, "Servy.Manager.exe");

        /// <summary>
        /// Default Servy Manager App Publish Path (Release).
        /// </summary>
        public static readonly string DefaultManagerAppPublishPath = @".\Servy.Manager.exe";

        /// <summary>
        /// The base file name (without extension) of the Servy Service CLI executable.
        /// </summary>
        public static readonly string ServyServiceCLIFileName = "Servy.Service.Net48.CLI";

        /// <summary>
        /// The full file name (with extension) of the Servy Service CLI executable.
        /// </summary>
        public static readonly string ServyServiceCLIExe = $"{ServyServiceCLIFileName}.exe";

        /// <summary>
        /// Default services refresh interval when not set in appsettings. Default is 5 seconds.
        /// </summary>
        public static readonly int DefaultRefreshIntervalInSeconds = 5;

        /// <summary>
        /// Servy's official documentation link.
        /// </summary>
        public static readonly string DocumentationLink = "https://github.com/aelassas/servy/wiki";

        /// <summary>
        /// Latest GitHub release link.
        /// </summary>
        public static readonly string LatestReleaseLink = "https://github.com/aelassas/servy/releases/latest";

        /// <summary>
        /// The root folder name under ProgramData.
        /// </summary>
        public static readonly string AppFolderName = "Servy";

        /// <summary>
        /// The full root path under ProgramData where Servy stores its data.
        /// </summary>
        public static readonly string ProgramDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderName);

        /// <summary>
        /// Path to the database folder.
        /// </summary>
        public static readonly string DbFolderPath = Path.Combine(ProgramDataPath, "db");

        /// <summary>
        /// Path to the security folder containing AES key/IV files.
        /// </summary>
        public static readonly string SecurityFolderPath = Path.Combine(ProgramDataPath, "security");

        /// <summary>
        /// Default SQLite connection string pointing to Servy.db in the database folder.
        /// </summary>
        public static readonly string DefaultConnectionString = $@"Data Source={Path.Combine(DbFolderPath, "Servy.db")};Journal Mode=WAL;";

        /// <summary>
        /// Default AES key file path.
        /// </summary>
        public static readonly string DefaultAESKeyPath = Path.Combine(SecurityFolderPath, "aes_key.dat");

        /// <summary>
        /// Default AES IV file path.
        /// </summary>
        public static readonly string DefaultAESIVPath = Path.Combine(SecurityFolderPath, "aes_iv.dat");

        /// <summary>
        /// Default log rotation size in bytes. Default is 10 MB.
        /// </summary>
        public static readonly int DefaultRotationSize = 10 * 1024 * 1024;

        /// <summary>
        /// Default heartbeat interval in seconds. Default is 30 seconds.
        /// </summary>
        public static readonly int DefaultHeartbeatInterval = 30;

        /// <summary>
        /// Default maximum number of failed health checks before triggering an action. Default is 3 attempts.
        /// </summary>
        public static readonly int DefaultMaxFailedChecks = 3;

        /// <summary>
        /// Default maximum number of restart attempts after a service failure. Default is 3 attempts.
        /// </summary>
        public static readonly int DefaultMaxRestartAttempts = 3;

        /// <summary>
        /// Default timeout in seconds before considering a pre-launch task as failed. Default is 30 seconds.
        /// </summary>
        public static readonly int DefaultPreLaunchTimeoutSeconds = 30;

        /// <summary>
        /// Default number of retry attempts for pre-launch tasks. Default is 0 attempts.
        /// </summary>
        public static readonly int DefaultPreLaunchRetryAttempts = 0;

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the absolute path to the Servy CLI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>ServyServiceCLI.exe</c>.</returns>
        public static string GetServyCLIServicePath()
        {
#if DEBUG
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppConfig.ServyServiceCLIFileName}.exe");
#else
            var wrapperExePath = Path.Combine(AppConfig.ProgramDataPath, $"{AppConfig.ServyServiceCLIFileName}.exe");
#endif
            return wrapperExePath;
        }

        /// <summary>
        /// Gets the absolute path to the Servy UI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>ServyServiceUI.exe</c>.</returns>
        public static string GetServyUIServicePath()
        {
#if DEBUG
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppConfig.ServyServiceUIFileName}.exe");
#else
            var wrapperExePath = Path.Combine(AppConfig.ProgramDataPath, $"{AppConfig.ServyServiceUIFileName}.exe");
#endif
            return wrapperExePath;
        }


        #endregion

    }
}
