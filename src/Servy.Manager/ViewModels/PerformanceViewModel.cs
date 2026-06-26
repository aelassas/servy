using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Design;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.UI.Constants;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel responsible for monitoring and visualizing real-time performance data (CPU and RAM) 
    /// for selected Windows services.
    /// </summary>
    public class PerformanceViewModel : MonitoringViewModelBase
    {
        #region Constants

        /// <summary>
        /// The number of data points maintained in the performance history buffers.
        /// A value of 101 ensures the graph line spans the entire horizontal width 
        /// of the UI control (0 to 100 on the X-axis) immediately upon initialization.
        /// </summary>
        private const int PerformanceHistoryCapacity = 101;

        #endregion

        #region Fields

        private readonly IServiceRepository _serviceRepository;
        private readonly double _ramDisplayMax = 10; // Minimum RAM scale (MB) to avoid flat graphs for small processes

        private readonly IAppConfiguration _appConfig;
        private readonly IProcessHelper _processHelper;

        private Queue<double> _cpuValues = new Queue<double>();
        private Queue<double> _ramValues = new Queue<double>();

        #endregion

        #region Fields - Optimized Double Buffering

        // Working buffers reused across ticks; final snapshots cloned for thread-safe property exposure
        private readonly PointCollection _cpuBuffer = new PointCollection(PerformanceHistoryCapacity);
        private readonly PointCollection _cpuFillBuffer = new PointCollection(PerformanceHistoryCapacity + 2);
        private readonly PointCollection _ramBuffer = new PointCollection(PerformanceHistoryCapacity);
        private readonly PointCollection _ramFillBuffer = new PointCollection(PerformanceHistoryCapacity + 2);

        #endregion

        #region Properties - Service Data

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

                CopyPidCommand?.RaiseCanExecuteChanged();

                ResetGraphs();

                StopMonitoring();
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

        private PointCollection _cpuFillPoints = new PointCollection();
        /// <summary>
        /// Collection of points representing the filled area beneath the CPU usage line.
        /// </summary>
        public PointCollection CpuFillPoints
        {
            get => _cpuFillPoints;
            set => Set(ref _cpuFillPoints, value);
        }

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

        #region Properties - UI State & Search

        /// <summary>
        /// Gets the fixed width of the performance graph drawing surface in device-independent pixels.
        /// </summary>
        public double GraphWidth { get; } = 400;

        /// <summary>
        /// Gets the fixed height of the performance graph drawing surface in device-independent pixels.
        /// </summary>
        public double GraphHeight { get; } = 200;

        private string _cpuUsage = UiConstants.NotAvailable;

        /// <summary>
        /// Gets or sets the formatted, localized CPU usage percentage string displayed in the view template.
        /// </summary>
        public string CpuUsage
        {
            get => _cpuUsage;
            set => Set(ref _cpuUsage, value);
        }

        private string _ramUsage = UiConstants.NotAvailable;

        /// <summary>
        /// Gets or sets the formatted, localized RAM usage metrics string (e.g., "45.2 MB") displayed in the view template.
        /// </summary>
        public string RamUsage
        {
            get => _ramUsage;
            set => Set(ref _ramUsage, value);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceViewModel"/> class.
        /// </summary>
        /// <param name="serviceRepository">Repository for service data access.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        /// <param name="appConfig">Application configuration settings.</param>
        /// <param name="cursorService">Service used to control the cursor state.</param>
        /// <param name="processHelper">The process helper used to format process commands.</param>
        /// <param name="uiDispatcher">Dispatcher for UI thread operations.</param>
        public PerformanceViewModel(
            IServiceRepository serviceRepository,
            IServiceCommands serviceCommands,
            IAppConfiguration appConfig,
            ICursorService cursorService,
            IProcessHelper processHelper,
            IUiDispatcher uiDispatcher
            ) : base(cursorService, uiDispatcher, serviceCommands)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));

            InitTimer();
        }

        /// <summary>
        /// Design-Time constructor.
        /// </summary>
        public PerformanceViewModel() : this(
            new UI.Design.DesignTimeServiceRepository(),
            new DesignTimeServiceCommands(),
            new DesignTimeAppConfig(),
            new UI.Design.DesignTimeCursorService(),
            new UI.Design.DesignTimeProcessHelper(),
            new UI.Design.DesignTimeUiDispatcher()
            )
        { }

        #endregion

        #region MonitoringViewModelBase Implementation

        /// <inheritdoc/>
        protected override ServiceItemBase SelectedServiceItem => SelectedService;

        /// <inheritdoc/>
        protected override int RefreshIntervalMs => _appConfig.PerformanceRefreshIntervalInMs;

        /// <inheritdoc/>
        protected override ServiceItemBase CreateServiceItem(Service service)
        {
            return new PerformanceService { Name = service?.Name, Pid = service?.Pid };
        }

        /// <inheritdoc/>
        protected override void ResetMonitoringState()
        {
            ResetGraphs();
        }

        /// <inheritdoc/>
        protected override async Task ApplyTickAsync(ServiceItemBase selection, CancellationToken token)
        {
            var currentSelection = (PerformanceService)selection;
            var currentPid = await _serviceRepository.GetServicePidAsync(currentSelection.Name, token);

            // Drop this tick if the user switched services while we were awaiting the DB call.
            if (!ReferenceEquals(currentSelection, _selectedService) || token.IsCancellationRequested) return;

            if (!currentPid.HasValue)
            {
                if (currentSelection.Pid != null)      // only act on the running -> stopped transition
                {
                    currentSelection.Pid = null;
                    ResetGraphs();
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }
                return;
            }

            if (currentSelection.Pid != currentPid)
            {
                currentSelection.Pid = currentPid;
                ResetGraphs();
                CopyPidCommand?.RaiseCanExecuteChanged();
            }

            int pid = currentSelection.Pid.Value;
            SetPidText(currentSelection);

            var processMetrics = await Task.Run(() =>
            {
                _processHelper.MaintainCache();
                return _processHelper.GetProcessTreeMetrics(pid);
            }, token);

            if (token.IsCancellationRequested) return;
            if (!ReferenceEquals(currentSelection, _selectedService)) return;

            double rawRamMb = processMetrics.RamUsage / 1024d / 1024d;

            CpuUsage = _processHelper.FormatCpuUsage(processMetrics.CpuUsage);
            RamUsage = _processHelper.FormatRamUsage(processMetrics.RamUsage);

            AddPoint(_cpuValues, processMetrics.CpuUsage, MetricType.Cpu);
            AddPoint(_ramValues, rawRamMb, MetricType.Ram);
        }

        #endregion

        #region Private Methods - Logic & Calculation

        /// <summary>
        /// Resets all graph-related display values and data collections to their initial state.
        /// </summary>
        /// <remarks>Call this method to clear existing CPU and RAM usage data and prepare the graphs for
        /// fresh input. This is typically used when reinitializing the display or after a data source change.</remarks>
        private void ResetGraphs()
        {
            // 1. Reset display values
            Pid = UiConstants.NotAvailable;
            CpuUsage = UiConstants.NotAvailable;
            RamUsage = UiConstants.NotAvailable;

            // 2. Clear and SEED the data history with PerformanceHistoryCapacity zeros
            // This ensures the graph line spans the whole width immediately
            _cpuValues = new Queue<double>(Enumerable.Repeat(0.0, PerformanceHistoryCapacity));
            _ramValues = new Queue<double>(Enumerable.Repeat(0.0, PerformanceHistoryCapacity));

            // 3. Reset the UI collections to empty (they will update on next tick)
            CpuPointCollection = new PointCollection();
            CpuFillPoints = new PointCollection();
            RamPointCollection = new PointCollection();
            RamFillPoints = new PointCollection();
        }

        /// <summary>
        /// Processes new performance data and updates the point collections using an optimized 
        /// double-buffering approach to minimize GC allocations.
        /// </summary>
        /// <param name="valueHistory">The historical list of data points for the specific metric.</param>
        /// <param name="newValue">The latest raw value captured from the process.</param>
        /// <param name="metricType">The type of metric (CPU or RAM) being updated.</param>
        private void AddPoint(Queue<double> valueHistory, double newValue, MetricType metricType)
        {
            var isCpu = metricType == MetricType.Cpu;
            valueHistory.Enqueue(newValue);

            if (valueHistory.Count > PerformanceHistoryCapacity)
            {
                valueHistory.Dequeue();
            }

            double currentMax = 0;
            if (valueHistory.Count > 0)
            {
                // A foreach over a Queue<T> uses a struct enumerator (0 bytes allocated).
                // This is microscopically fast for 100 items and guarantees perfect accuracy.
                foreach (var val in valueHistory)
                {
                    if (val > currentMax)
                    {
                        currentMax = val;
                    }
                }
            }

            // Scale dynamically if multi-process trees breach normalized boundaries, maintaining rational floor metrics
            double displayMax = isCpu
                ? Math.Max(currentMax * 1.2, 100.0)
                : Math.Max(currentMax * 1.2, _ramDisplayMax);

            var lineBuffer = isCpu ? _cpuBuffer : _ramBuffer;
            var fillBuffer = isCpu ? _cpuFillBuffer : _ramFillBuffer;

            lineBuffer.Clear();
            fillBuffer.Clear();

            double stepX = GraphWidth / (PerformanceHistoryCapacity - 1);
            int i = 0;

            foreach (var val in valueHistory)
            {
                double x = i * stepX;
                double ratio = Math.Min(Math.Max(val / displayMax, 0), 1);
                double y = GraphHeight - (ratio * GraphHeight);

                var point = new Point(x, y);
                lineBuffer.Add(point);
                fillBuffer.Add(point);

                i++;
            }

            // Close the fill polygon
            // Use valueHistory.Count to satisfy static analysis; if we have data, we have points
            if (valueHistory.Count > 0)
            {
                fillBuffer.Add(new Point(fillBuffer[fillBuffer.Count - 1].X, GraphHeight));
                fillBuffer.Add(new Point(fillBuffer[0].X, GraphHeight));
            }

            // Update the UI-bound collections
            if (isCpu)
            {
                CpuPointCollection = lineBuffer.Clone();
                CpuFillPoints = fillBuffer.Clone();
            }
            else
            {
                RamPointCollection = lineBuffer.Clone();
                RamFillPoints = fillBuffer.Clone();
            }
        }

        #endregion
    }
}