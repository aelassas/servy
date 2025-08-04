using Servy.Core;
using Servy.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Servy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private const int DefaultRotationSize = 10 * 1024 * 1024; // Default to 10 MB
        private const int MinRotationSize = 1 * 1024 * 1024; // 1 MB

        private string _serviceName;
        private string _serviceDescription;
        private string _processPath;
        private string _startupDirectory;
        private string _processParameters;
        private ServiceStartType _selectedStartupType;
        private ProcessPriority _selectedProcessPriority;
        private string _stdoutPath;
        private string _stderrPath;
        private bool _enableRotation;
        private string _rotationSize;

        public string ServiceName
        {
            get => _serviceName;
            set { _serviceName = value; OnPropertyChanged(); }
        }

        public string ServiceDescription
        {
            get => _serviceDescription;
            set { _serviceDescription = value; OnPropertyChanged(); }
        }

        public string ProcessPath
        {
            get => _processPath;
            set { _processPath = value; OnPropertyChanged(); }
        }

        public string StartupDirectory
        {
            get => _startupDirectory;
            set { _startupDirectory = value; OnPropertyChanged(); }
        }

        public string ProcessParameters
        {
            get => _processParameters;
            set { _processParameters = value; OnPropertyChanged(); }
        }

        public ServiceStartType SelectedStartupType
        {
            get => _selectedStartupType;
            set { _selectedStartupType = value; OnPropertyChanged(); }
        }


        public ProcessPriority SelectedProcessPriority
        {
            get => _selectedProcessPriority;
            set { _selectedProcessPriority = value; OnPropertyChanged(); }
        }

        public List<StartupTypeItem> StartupTypes { get; } = new List<StartupTypeItem>
        {
            new StartupTypeItem { StartupType = ServiceStartType.Automatic, DisplayName = Strings.StartupType_Automatic },
            new StartupTypeItem { StartupType = ServiceStartType.Manual, DisplayName = Strings.StartupType_Manual },
            new StartupTypeItem { StartupType = ServiceStartType.Disabled, DisplayName = Strings.StartupType_Disabled },
        };

        public List<ProcessPriorityItem> ProcessPriorities { get; } = new List<ProcessPriorityItem>
        {
            new ProcessPriorityItem { Priority = ProcessPriority.Idle, DisplayName = Strings.ProcessPriority_Idle },
            new ProcessPriorityItem { Priority = ProcessPriority.BelowNormal, DisplayName = Strings.ProcessPriority_BelowNormal },
            new ProcessPriorityItem { Priority = ProcessPriority.Normal, DisplayName = Strings.ProcessPriority_Normal },
            new ProcessPriorityItem { Priority = ProcessPriority.AboveNormal, DisplayName = Strings.ProcessPriority_AboveNormal },
            new ProcessPriorityItem { Priority = ProcessPriority.High, DisplayName = Strings.ProcessPriority_High },
            new ProcessPriorityItem { Priority = ProcessPriority.RealTime, DisplayName = Strings.ProcessPriority_RealTime },
        };

        public string StdoutPath
        {
            get => _stdoutPath;
            set { _stdoutPath = value; OnPropertyChanged(); }
        }

        public string StderrPath
        {
            get => _stderrPath;
            set { _stderrPath = value; OnPropertyChanged(); }
        }

        public bool EnableRotation
        {
            get => _enableRotation;
            set { _enableRotation = value; OnPropertyChanged(); }
        }

        public string RotationSize
        {
            get => _rotationSize;
            set { _rotationSize = value; OnPropertyChanged(); }
        }

        public ICommand InstallCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RestartCommand { get; }
        public ICommand ClearCommand { get; }

        private ResourceManager _resourceManager = Strings.ResourceManager;
        private CultureInfo _culture = CultureInfo.CurrentUICulture;

        public string Menu_Language => _resourceManager.GetString(nameof(Menu_Language), _culture) ?? string.Empty;
        public string Label_ServiceName => _resourceManager.GetString(nameof(Label_ServiceName), _culture) ?? string.Empty;
        public string Label_ServiceDescription => _resourceManager.GetString(nameof(Label_ServiceDescription), _culture) ?? string.Empty;
        public string Label_ProcessPath => _resourceManager.GetString(nameof(Label_ProcessPath), _culture) ?? string.Empty;
        public string Label_StartupDirectory => _resourceManager.GetString(nameof(Label_StartupDirectory), _culture) ?? string.Empty;
        public string Label_ProcessParameters => _resourceManager.GetString(nameof(Label_ProcessParameters), _culture) ?? string.Empty;

        public string Label_StartupType => _resourceManager.GetString(nameof(Label_StartupType), _culture) ?? string.Empty;
        public string StartupType_Automatic => _resourceManager.GetString(nameof(StartupType_Automatic), _culture) ?? string.Empty;
        public string StartupType_Manual => _resourceManager.GetString(nameof(StartupType_Manual), _culture) ?? string.Empty;
        public string StartupType_Disabled => _resourceManager.GetString(nameof(StartupType_Disabled), _culture) ?? string.Empty;

        public string Label_ProcessPriority => _resourceManager.GetString(nameof(Label_ProcessPriority), _culture) ?? string.Empty;
        public string ProcessPriority_Idle => _resourceManager.GetString(nameof(ProcessPriority_Idle), _culture) ?? string.Empty;
        public string ProcessPriority_BelowNormal => _resourceManager.GetString(nameof(ProcessPriority_BelowNormal), _culture) ?? string.Empty;
        public string ProcessPriority_Normal => _resourceManager.GetString(nameof(ProcessPriority_Normal), _culture) ?? string.Empty;
        public string ProcessPriority_AboveNormal => _resourceManager.GetString(nameof(ProcessPriority_AboveNormal), _culture) ?? string.Empty;
        public string ProcessPriority_High => _resourceManager.GetString(nameof(ProcessPriority_High), _culture) ?? string.Empty;
        public string ProcessPriority_RealTime => _resourceManager.GetString(nameof(ProcessPriority_RealTime), _culture) ?? string.Empty;

        public string Label_StdoutPath => _resourceManager.GetString(nameof(Label_StdoutPath), _culture) ?? string.Empty;
        public string Label_StderrPath => _resourceManager.GetString(nameof(Label_StderrPath), _culture) ?? string.Empty;
        public string Label_EnableRotation => _resourceManager.GetString(nameof(Label_EnableRotation), _culture) ?? string.Empty;
        public string Label_RotationSize => _resourceManager.GetString(nameof(Label_RotationSize), _culture) ?? string.Empty;
        public string Label_RotationSizeUnity => _resourceManager.GetString(nameof(Label_RotationSizeUnity), _culture) ?? string.Empty;

        public string Button_Install => _resourceManager.GetString(nameof(Button_Install), _culture) ?? string.Empty;
        public string Button_Uninstall => _resourceManager.GetString(nameof(Button_Uninstall), _culture) ?? string.Empty;
        public string Button_Start => _resourceManager.GetString(nameof(Button_Start), _culture) ?? string.Empty;
        public string Button_Stop => _resourceManager.GetString(nameof(Button_Stop), _culture) ?? string.Empty;
        public string Button_Restart => _resourceManager.GetString(nameof(Button_Restart), _culture) ?? string.Empty;
        public string Button_Browse => _resourceManager.GetString(nameof(Button_Browse), _culture) ?? string.Empty;


        public MainViewModel()
        {
            _serviceName = string.Empty;
            _serviceDescription = string.Empty;
            _processPath = string.Empty;
            _startupDirectory = string.Empty;
            _processParameters = string.Empty;
            _selectedStartupType = ServiceStartType.Automatic; // Default to Automatic startup type
            _selectedProcessPriority = ProcessPriority.Normal; // Default to Normal priority
            _enableRotation = false; // Default to no rotation
            _rotationSize = DefaultRotationSize.ToString(); // Default to 10 MB rotation size

            InstallCommand = new RelayCommand(InstallService);
            UninstallCommand = new RelayCommand(UninstallService);
            StartCommand = new RelayCommand(StartService);
            StopCommand = new RelayCommand(StopService);
            RestartCommand = new RelayCommand(RestartService);
            ClearCommand = new RelayCommand(ClearForm);
        }

        private void InstallService()
        {
            if (string.IsNullOrWhiteSpace(ServiceName) || string.IsNullOrWhiteSpace(ProcessPath))
            {
                MessageBox.Show(Strings.Msg_ValidationError, "Servy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Helper.IsValidPath(ProcessPath) || !File.Exists(ProcessPath))
            {
                MessageBox.Show(Strings.Msg_InvalidPath, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servy.Service.exe");

            if (!File.Exists(wrapperExePath))
            {
                MessageBox.Show(Strings.Msg_InvalidWrapperExePath, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(StartupDirectory) && (!Helper.IsValidPath(StartupDirectory) || !Directory.Exists(StartupDirectory)))
            {
                MessageBox.Show(Strings.Msg_InvalidStartupDirectory, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(StdoutPath) && (!Helper.IsValidPath(StdoutPath) || !Helper.CreateParentDirectory(StdoutPath)))
            {
                MessageBox.Show(Strings.Msg_InvalidStdoutPath, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(StderrPath) && (!Helper.IsValidPath(StderrPath) || !Helper.CreateParentDirectory(StderrPath)))
            {
                MessageBox.Show(Strings.Msg_InvalidStderrPath, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var rotationSize = 0;
            if (EnableRotation)
            {
                if (!int.TryParse(RotationSize, out rotationSize) || rotationSize < MinRotationSize)
                {
                    MessageBox.Show(Strings.Msg_InvalidRotationSize, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                var success = ServiceManager.InstallService(
                    ServiceName,                      // service name
                    ServiceDescription,               // service description
                    wrapperExePath,                   // wrapper exe
                    ProcessPath,                      // real exe path
                    StartupDirectory,                 // startup directory
                    ProcessParameters,                // process parameters
                    SelectedStartupType,              // startup type
                    SelectedProcessPriority,          // process priority
                    StdoutPath,                       // standard output path 
                    StderrPath,                       // standard error path
                    rotationSize                      // rotation size in bytes, O if rotation is disabled
                );

                if (success)
                {
                    MessageBox.Show(Strings.Msg_ServiceCreated, "Servy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(Strings.Msg_AdminRightsRequired, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception)
            {
                MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UninstallService()
        {
            if (string.IsNullOrWhiteSpace(ServiceName))
            {
                MessageBox.Show(Strings.Msg_ValidationError, "Servy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool success = ServiceManager.UninstallService(ServiceName);

                if (success)
                {
                    MessageBox.Show(Strings.Msg_ServiceRemoved, "Servy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(Strings.Msg_AdminRightsRequired, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception)
            {
                MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartService()
        {
            try
            {
                bool success = ServiceManager.StartService(ServiceName);

                if (success)
                {
                    MessageBox.Show(Strings.Msg_ServiceStarted, "Servy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopService()
        {
            try
            {
                bool success = ServiceManager.StopService(ServiceName);

                if (success)
                {
                    MessageBox.Show(Strings.Msg_ServiceStopped, "Servy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestartService()
        {
            try
            {
                bool success = ServiceManager.RestartService(ServiceName);

                if (success)
                {
                    MessageBox.Show(Strings.Msg_ServiceRestarted, "Servy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            ServiceName = string.Empty;
            ServiceDescription = string.Empty;
            ProcessPath = string.Empty;
            StartupDirectory = string.Empty;
            ProcessParameters = string.Empty;
            SelectedStartupType = ServiceStartType.Automatic;
            SelectedProcessPriority = ProcessPriority.Normal;
            EnableRotation = false;
            RotationSize = DefaultRotationSize.ToString();
            StdoutPath = string.Empty;
            StderrPath = string.Empty;
        }

    }
}
