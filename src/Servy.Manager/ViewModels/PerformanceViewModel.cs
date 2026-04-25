using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.Constants;
using Servy.UI.Services;
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

        private bool _hadSelectedService;
        private bool _disposedValue;
        private readonly IAppConfiguration _appConfig;

        private Queue<double> _cpuValues = new Queue<double>();
        private Queue<double> _ramValues = new Queue<double>();

        #endregion

        #region Fields - Optimized Double Buffering

        // Pre-allocated collections to avoid GC pressure
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

                ResetGraphs(true);

                StopMonitoring(false); // Pass false so we don't clear the zeros we just added
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

        public double GraphWidth { get; } = 400;
        public double GraphHeight { get; } = 200;

        private string _pid = UiConstants.NotAvailable;
        public string Pid
        {
            get => _pid;
            set => Set(ref _pid, value);
        }

        private string _cpuUsage = UiConstants.NotAvailable;
        public string CpuUsage
        {
            get => _cpuUsage;
            set => Set(ref _cpuUsage, value);
        }

        private string _ramUsage = UiConstants.NotAvailable;
        public string RamUsage
        {
            get => _ramUsage;
            set => Set(ref _ramUsage, value);
        }

        #endregion

        #region Commands

        public IAsyncCommand CopyPidCommand { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceViewModel"/> class.
        /// </summary>
        /// <param name="serviceRepository">Repository for service data access.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        /// <param name="appConfig">Application configuration settings.</param>
        /// <param name="cursorService">Service used to control the cursor state.</param>
        public PerformanceViewModel(
            IServiceRepository serviceRepository,
            IServiceCommands serviceCommands,
            IAppConfiguration appConfig,
            ICursorService cursorService
            ) : base(cursorService)
        {
            _serviceRepository = serviceRepository;
            ServiceCommands = serviceCommands;
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null);

            InitTimer();
        }

        #endregion

        #region MonitoringViewModelBase Implementation

        /// <inheritdoc/>
        protected override int RefreshIntervalMs => _appConfig.PerformanceRefreshIntervalInMs;

        /// <inheritdoc/>
        protected override ServiceItemBase CreateServiceItem(Service service)
        {
            return new PerformanceService { Name = service.Name, Pid = service.Pid };
        }

        /// <inheritdoc/>
        protected override async Task OnTickAsync()
        {
            var token = _monitoringCts?.Token ?? CancellationToken.None;

            try
            {
                var currentSelection = SelectedService;
                if (currentSelection == null)
                {
                    if (_hadSelectedService)
                    {
                        ResetGraphs(true);
                        _hadSelectedService = false;
                        CopyPidCommand?.RaiseCanExecuteChanged();
                    }
                    return;
                }
                _hadSelectedService = true;

                var currentPid = await _serviceRepository.GetServicePidAsync(currentSelection.Name, token);
                if (!currentPid.HasValue)
                {
                    ResetGraphs(true);
                    SelectedService.Pid = null;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                    return;
                }

                if (currentSelection.Pid != currentPid)
                {
                    currentSelection.Pid = currentPid;
                    ResetGraphs(true);
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }

                int pid = currentSelection.Pid.Value;
                SetPidText(currentSelection);

                var processMetrics = await Task.Run(() =>
                {
                    ProcessHelper.MaintainCache();
                    return ProcessHelper.GetProcessTreeMetrics(pid);
                });

                double rawRamMb = processMetrics.RamUsage / 1024d / 1024d;

                CpuUsage = ProcessHelper.FormatCpuUsage(processMetrics.CpuUsage);
                RamUsage = ProcessHelper.FormatRamUsage(processMetrics.RamUsage);

                AddPoint(_cpuValues, processMetrics.CpuUsage, MetricType.Cpu);
                AddPoint(_ramValues, rawRamMb, MetricType.Ram);
            }
            catch (OperationCanceledException)
            {
                // Expected during app shutdown or when the ViewModel is deactivated.
                // No logging required as this is a normal lifecycle event.
            }
            catch (Exception ex)
            {
                // Log the error so it's visible in 'Servy.Manager.log'
                // This ensures developers can diagnose why the UI stopped updating.
                Logger.Error($"Background tick failed in {GetType().Name}", ex);
            }
        }

        #endregion

        #region Private Methods - Logic & Calculation

        /// <summary>
        /// Resets all graph-related display values and data collections to their initial state.
        /// </summary>
        /// <remarks>Call this method to clear existing CPU and RAM usage data and prepare the graphs for
        /// fresh input. This is typically used when reinitializing the display or after a data source change.</remarks>
        private void ResetGraphs(bool resetLabels)
        {
            // 1. Reset display values
            if (resetLabels)
            {
                Pid = UiConstants.NotAvailable;
                CpuUsage = UiConstants.NotAvailable;
                RamUsage = UiConstants.NotAvailable;
            }

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
        /// Updates the PID display text based on the selected service's current state.
        /// </summary>
        /// <param name="service">Service model.</param>
        private void SetPidText(ServiceItemBase service)
        {
            var pidTxt = service.Pid?.ToString() ?? UiConstants.NotAvailable;
            if (Pid != pidTxt) Pid = pidTxt;
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

            // --- REPLACED LINQ .Max() WITH ALLOCATION-FREE FOREACH ---
            double currentMax = 0;
            if (!isCpu && valueHistory.Count > 0)
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
            // CPU is always 0-100%, RAM scale is dynamic based on usage
            double displayMax = isCpu ? 100.0 : Math.Max(currentMax * 1.2, _ramDisplayMax);

            var lineBuffer = isCpu ? _cpuBuffer : _ramBuffer;
            var fillBuffer = isCpu ? _cpuFillBuffer : _ramFillBuffer;

            lineBuffer.Clear();
            fillBuffer.Clear();

            double stepX = GraphWidth / 100.0;
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

        /// <summary>
        /// Asynchronously copies the process identifier (PID) of the selected service to the clipboard or a designated
        /// destination.
        /// </summary>
        /// <remarks>The method performs no action if no service is selected or if the selected service
        /// does not have a PID.</remarks>
        /// <param name="parameter">An optional parameter that can be used to pass additional data for the copy operation. This parameter is not
        /// used by the method.</param>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        private async Task CopyPidAsync(object parameter)
        {
            if (SelectedService?.Pid != null)
            {
                var service = ServiceMapper.ToModel(SelectedService);
                await ServiceCommands.CopyPid(service);
            }
        }


        #endregion

        #region Public Methods - Control

        /// <summary>
        /// Stops the performance monitoring timer and optionally clears existing graph data.
        /// </summary>
        /// <param name="clearPoints">True to reset the graph visualizations.</param>
        public override void StopMonitoring(bool clearPoints)
        {
            base.StopMonitoring(clearPoints);


            // Clear points
            if (clearPoints)
            {
                CpuPointCollection = new PointCollection();
                CpuFillPoints = new PointCollection();
                RamPointCollection = new PointCollection();
                RamFillPoints = new PointCollection();

                _cpuValues.Clear();
                _ramValues.Clear();
            }
        }

        /// <summary>
        /// Cleans up resources, cancels background tasks, and explicitly unsubscribes 
        /// from timer events to prevent memory leaks.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Cancel and dispose the Monitoring CTS
                    var oldMonitoringCts = Interlocked.Exchange(ref _monitoringCts, null);
                    if (oldMonitoringCts != null)
                    {
                        oldMonitoringCts.Cancel();
                        oldMonitoringCts.Dispose();
                    }
                }

                base.Dispose(disposing);
                _disposedValue = true;
            }
        }

        #endregion

    }
}