using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel responsible for monitoring and visualizing real-time performance data (CPU and RAM) 
    /// for selected Windows services.
    /// </summary>
    public class PerformanceViewModel : ViewModelBase
    {
        #region Constants

        private const string NotAvailableText = "N/A";

        #endregion

        #region Fields

        private readonly IServiceRepository _serviceRepository;
        private readonly DispatcherTimer _timer;
        private readonly double _graphWidth = 400;
        private readonly double _graphHeight = 200;

        private List<double> _cpuValues = new List<double>();
        private List<double> _ramValues = new List<double>();
        private readonly List<double> _cpuSmoothingBuffer = new List<double>();
        private readonly List<double> _ramSmoothingBuffer = new List<double>();

        #endregion

        #region Properties - Service Data

        /// <summary>
        /// Collection of services available for performance monitoring.
        /// </summary>
        public ObservableCollection<PerformanceService> Services { get; } = new ObservableCollection<PerformanceService>();

        private PerformanceService _selectedService;
        /// <summary>
        /// Gets or sets the currently selected service for monitoring.
        /// Resets and restarts monitoring upon change.
        /// </summary>
        public PerformanceService SelectedService
        {
            get => _selectedService;
            set
            {
                if (ReferenceEquals(_selectedService, value)) return;
                _selectedService = value;
                OnPropertyChanged(nameof(SelectedService));

                // Reset display values
                Pid = NotAvailableText;
                CpuUsage = NotAvailableText;
                RamUsage = NotAvailableText;

                // Reset data history
                _cpuValues = new List<double>();
                CpuPointCollection = new PointCollection();
                _ramValues = new List<double>();
                RamPointCollection = new PointCollection();

                StopMonitoring(true);
                StartMonitoring();
            }
        }

        #endregion

        #region Properties - Graph Collections

        private PointCollection _cpuPointCollection = new PointCollection();
        /// <summary>
        /// Collection of points representing the CPU usage line.
        /// </summary>
        public PointCollection CpuPointCollection { get => _cpuPointCollection; set => Set(ref _cpuPointCollection, value); }

        private PointCollection _ramPointCollection = new PointCollection();
        /// <summary>
        /// Collection of points representing the RAM usage line.
        /// </summary>
        public PointCollection RamPointCollection { get => _ramPointCollection; set => Set(ref _ramPointCollection, value); }

        private PointCollection _ramFillPoints = new PointCollection();
        /// <summary>
        /// Collection of points representing the filled area beneath the RAM usage line.
        /// </summary>
        public PointCollection RamFillPoints
        {
            get => _ramFillPoints;
            set => Set(ref _ramFillPoints, value);
        }

        #endregion

        #region Properties - Grid Lines

        public ObservableCollection<Line> CpuHorizontalGridLines { get; } = new ObservableCollection<Line>();
        public ObservableCollection<Line> CpuVerticalGridLines { get; } = new ObservableCollection<Line>();
        public ObservableCollection<Line> RamHorizontalGridLines { get; } = new ObservableCollection<Line>();
        public ObservableCollection<Line> RamVerticalGridLines { get; } = new ObservableCollection<Line>();

        #endregion

        #region Properties - UI State & Search

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set => Set(ref _searchText, value);
        }

        private string _searchButtonText;
        public string SearchButtonText
        {
            get => _searchButtonText;
            set => Set(ref _searchButtonText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        private string _pid = NotAvailableText;
        public string Pid
        {
            get => _pid;
            set => Set(ref _pid, value);
        }

        private string _cpuUsage = NotAvailableText;
        public string CpuUsage
        {
            get => _cpuUsage;
            set => Set(ref _cpuUsage, value);
        }

        private string _ramUsage = NotAvailableText;
        public string RamUsage
        {
            get => _ramUsage;
            set => Set(ref _ramUsage, value);
        }

        #endregion

        #region Commands

        public IServiceCommands ServiceCommands { get; set; }
        public IAsyncCommand SearchCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceViewModel"/> class.
        /// </summary>
        /// <param name="serviceRepository">Repository for service data access.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        public PerformanceViewModel(IServiceRepository serviceRepository, IServiceCommands serviceCommands)
        {
            _serviceRepository = serviceRepository;
            ServiceCommands = serviceCommands;
            SearchCommand = new AsyncCommand(SearchServicesAsync);

            var app = (App)Application.Current;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(app.PerformanceRefreshIntervalInMs) };
            _timer.Tick += OnTick;

            GenerateGridLines();
        }

        #endregion

        #region Private Methods - Logic & Calculation

        /// <summary>
        /// Updates the PID display text based on the selected service.
        /// </summary>
        private void SetPidText()
        {
            var pidTxt = SelectedService.Pid?.ToString() ?? NotAvailableText;
            if (Pid != pidTxt) Pid = pidTxt;
        }

        /// <summary>
        /// Event handler for the monitoring timer. Refreshes performance metrics.
        /// </summary>
        private async void OnTick(object sender, EventArgs e)
        {
            if (SelectedService == null) return;

            var serviceDto = await _serviceRepository.GetByNameAsync(SelectedService.Name);

            if (serviceDto == null || serviceDto.Pid == null)
            {
                Pid = NotAvailableText;
                CpuUsage = NotAvailableText;
                RamUsage = NotAvailableText;
                // Clear buffers when service stops/disappears
                _cpuSmoothingBuffer.Clear();
                _ramSmoothingBuffer.Clear();
                return;
            }

            if (SelectedService.Pid != serviceDto.Pid)
            {
                SelectedService.Pid = serviceDto.Pid;
            }

            int pid = SelectedService.Pid.Value;
            SetPidText();

            // Fetch raw metrics
            double rawCpu = ProcessHelper.GetCpuUsage(pid);
            long ramBytes = ProcessHelper.GetRamUsage(pid);
            double rawRamMb = ramBytes / 1024d / 1024d;

            // Update UI Texts
            CpuUsage = ProcessHelper.FormatCpuUsage(rawCpu);
            RamUsage = ProcessHelper.FormatRamUsage(ramBytes);

            // Update Graphs with Smoothing
            AddPoint(_cpuValues, rawCpu, "CpuPointCollection", _cpuSmoothingBuffer);
            AddPoint(_ramValues, rawRamMb, "RamPointCollection", _ramSmoothingBuffer);
        }

        private void AddPoint(List<double> valueHistory, double newValue, string propertyName, List<double> smoothingBuffer)
        {
            // 1. Increased Smoothing Buffer for higher frequency
            smoothingBuffer.Add(newValue);
            if (smoothingBuffer.Count > 10) smoothingBuffer.RemoveAt(0);
            double smoothedValue = smoothingBuffer.Average();

            valueHistory.Add(smoothedValue);

            // Always keep exactly 100 points to maintain the "scrolling" effect
            if (valueHistory.Count > 100) valueHistory.RemoveAt(0);

            // 2. Consistent Scale
            double displayMax = (propertyName == "CpuPointCollection")
                ? 100.0
                : Math.Max(valueHistory.Max() * 1.2, 50);

            PointCollection pc = new PointCollection();
            for (int i = 0; i < valueHistory.Count; i++)
            {
                // Use i directly. Since history is always 100 points, it always fills 0 to _graphWidth
                double x = i * (_graphWidth / 99.0);

                double ratio = Math.Min(Math.Max(valueHistory[i] / displayMax, 0), 1);
                double y = _graphHeight - (ratio * _graphHeight);

                pc.Add(new Point(x, y));
            }

            // Assign collections
            if (propertyName == "CpuPointCollection")
                CpuPointCollection = pc;
            else
            {
                RamPointCollection = pc;
                var fillPc = new PointCollection(pc);
                if (fillPc.Count > 0)
                {
                    fillPc.Add(new Point(fillPc[fillPc.Count - 1].X, _graphHeight));
                    fillPc.Add(new Point(fillPc[0].X, _graphHeight));
                }
                RamFillPoints = fillPc;
            }
        }

        /// <summary>
        /// Generates the static horizontal and vertical background grid lines for the charts.
        /// </summary>
        private void GenerateGridLines()
        {
            int rows = 10;
            int cols = 10;
            Brush gridBrush = new SolidColorBrush(Color.FromRgb(0, 45, 0));
            double thickness = 0.5;

            CpuHorizontalGridLines.Clear();
            CpuVerticalGridLines.Clear();
            RamHorizontalGridLines.Clear();
            RamVerticalGridLines.Clear();

            for (int i = 0; i <= rows; i++)
            {
                double y = (i / (double)rows) * _graphHeight;
                CpuHorizontalGridLines.Add(new Line { X1 = 0, X2 = _graphWidth, Y1 = y, Y2 = y, Stroke = gridBrush, StrokeThickness = thickness });
                RamHorizontalGridLines.Add(new Line { X1 = 0, X2 = _graphWidth, Y1 = y, Y2 = y, Stroke = gridBrush, StrokeThickness = thickness });
            }

            for (int i = 0; i <= cols; i++)
            {
                double x = (i / (double)cols) * _graphWidth;
                CpuVerticalGridLines.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = _graphHeight, Stroke = gridBrush, StrokeThickness = thickness });
                RamVerticalGridLines.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = _graphHeight, Stroke = gridBrush, StrokeThickness = thickness });
            }
        }

        /// <summary>
        /// Asynchronously searches for services matching the SearchText.
        /// </summary>
        private async Task SearchServicesAsync(object parameter)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                IsBusy = true;
                SearchButtonText = Strings.Button_Searching;

                var results = await ServiceCommands.SearchServicesAsync(SearchText);

                Services.Clear();
                foreach (var s in results)
                    Services.Add(new PerformanceService { Name = s.Name, Pid = s.Pid });
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
                SearchButtonText = Strings.Button_Search;
            }
        }

        #endregion

        #region Public Methods - Control

        /// <summary>
        /// Starts the performance monitoring timer.
        /// </summary>
        public void StartMonitoring()
        {
            _timer.Start();
        }

        /// <summary>
        /// Stops the performance monitoring timer and optionally clears existing graph data.
        /// </summary>
        /// <param name="clearPoints">True to reset the graph visualizations.</param>
        public void StopMonitoring(bool clearPoints)
        {
            _timer.Stop();

            if (clearPoints)
            {
                CpuPointCollection = new PointCollection();
                RamPointCollection = new PointCollection();
                RamFillPoints = new PointCollection();

                _cpuValues.Clear();
                _ramValues.Clear();
            }
        }

        /// <summary>
        /// Explicit trigger for when the selected service is updated.
        /// </summary>
        public void OnSelectedServiceChanged()
        {
            StopMonitoring(true);
            if (SelectedService?.Pid != null)
                StartMonitoring();
        }

        #endregion
    }
}