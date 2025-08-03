using Servy.Core;
using Servy.Resources;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;
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

        private string _serviceName;
        private string _serviceDescription;
        private string _processPath;
        private string _startupDirectory;
        private string _processParameters;
        private string _selectedStartupType;
        private string _language;

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

        public string SelectedStartupType
        {
            get => _selectedStartupType;
            set { _selectedStartupType = value; OnPropertyChanged(); }
        }

        public string Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    OnPropertyChanged();
                    SwitchLanguage(value);
                }
            }
        }

        public string[] StartupTypes { get; } = new[] { "Automatic", "Manual", "Disabled" };
        public string[] Languages { get; } = new[] { "en", "fr" };

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
            _selectedStartupType = StartupTypes[0];
            _language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            InstallCommand = new RelayCommand(InstallService);
            UninstallCommand = new RelayCommand(UninstallService);
            StartCommand = new RelayCommand(StartService);
            StopCommand = new RelayCommand(StopService);
            RestartCommand = new RelayCommand(RestartService);
            ClearCommand = new RelayCommand(ClearForm);
            Language = _language;
        }

        private void InstallService()
        {
            if (string.IsNullOrWhiteSpace(ServiceName)
                || string.IsNullOrWhiteSpace(ProcessPath)
                || string.IsNullOrWhiteSpace(SelectedStartupType))
            {
                System.Windows.MessageBox.Show(Strings.Msg_ValidationError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(ProcessPath))
            {
                System.Windows.MessageBox.Show(Strings.Msg_InvalidPath, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Servy.Service.exe");

            if (!File.Exists(wrapperExePath))
            {
                System.Windows.MessageBox.Show("Service wrapper executable not found.", "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            ServiceStartType type;

            switch (SelectedStartupType)
            {
                case "Automatic":
                    type = ServiceStartType.Automatic;
                    break;
                case "Manual":
                    type = ServiceStartType.Manual;
                    break;
                case "Disabled":
                    type = ServiceStartType.Disabled;
                    break;
                default:
                    type = ServiceStartType.Manual;
                    break;
            }

            try
            {
                var success = ServiceManager.InstallService(
                    ServiceName,
                    ServiceDescription,
                    wrapperExePath, // wrapper exe
                    ProcessPath,    // real exe path
                    StartupDirectory,
                    ProcessParameters,
                    type
                );

                if (success)
                {
                    System.Windows.MessageBox.Show(Strings.Msg_ServiceCreated, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Windows.MessageBox.Show(Strings.Msg_AdminRightsRequired, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void UninstallService()
        {
            if (string.IsNullOrWhiteSpace(ServiceName))
            {
                System.Windows.MessageBox.Show(Strings.Msg_ValidationError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool success = ServiceManager.UninstallService(ServiceName);

                if (success)
                {
                    System.Windows.MessageBox.Show(Strings.Msg_ServiceRemoved, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Windows.MessageBox.Show(Strings.Msg_AdminRightsRequired, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void StartService()
        {
            try
            {
                bool success = ServiceManager.StartService(ServiceName);

                if (success)
                {
                    System.Windows.MessageBox.Show(Strings.Msg_ServiceStarted, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void StopService()
        {
            try
            {
                bool success = ServiceManager.StopService(ServiceName);

                if (success)
                {
                    System.Windows.MessageBox.Show(Strings.Msg_ServiceStopped, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void RestartService()
        {
            try
            {
                bool success = ServiceManager.RestartService(ServiceName);

                if (success)
                {
                    System.Windows.MessageBox.Show(Strings.Msg_ServiceRestarted, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(Strings.Msg_UnexpectedError, "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            ServiceName = string.Empty;
            ServiceDescription = string.Empty;
            ProcessPath = string.Empty;
            StartupDirectory = string.Empty;
            ProcessParameters = string.Empty;
            SelectedStartupType = StartupTypes[0];
        }

        private void SwitchLanguage(string lang)
        {
            try
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);
                Thread.CurrentThread.CurrentCulture = new CultureInfo(lang);
                System.Windows.MessageBox.Show("Language changed. Please restart the app.", "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch
            {
                System.Windows.MessageBox.Show("Failed to switch language.", "Servy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        public void ApplyLanguage(CultureInfo culture)
        {
            _culture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
