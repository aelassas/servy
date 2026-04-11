using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.Constants;
using Servy.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

        /// <summary>
        /// The number of data points maintained in the performance history buffers.
        /// A value of 101 ensures the graph line spans the entire horizontal width 
        /// of the UI control (0 to 100 on the X-axis) immediately upon initialization.
        /// </summary>
        private const int PerformanceHistoryCapacity = 101;

        #endregion

        #region Fields

        private readonly IServiceRepository _serviceRepository;
        private DispatcherTimer _timer;
        private readonly double _ramDisplayMax = 10; // Minimum RAM scale (MB) to avoid flat graphs for small processes
        private CancellationTokenSource _cts;
        private bool _hadSelectedService;
        private int _isMonitoringFlag = 0; // 0 = Stopped, 1 = Monitoring
        private int _isTickRunningFlag = 0; // 0 = Idle, 1 = Processing

        private List<double> _cpuValues = new List<double>();
        private List<double> _ramValues = new List<double>();

        #endregion

        #region Fields - Optimized Double Buffering

        // Pre-allocated collections to avoid GC pressure
        private readonly PointCollection _cpuBuffer = new PointCollection(PerformanceHistoryCapacity);
        private readonly PointCollection _cpuFillBuffer = new PointCollection(PerformanceHistoryCapacity + 2);
        private readonly PointCollection _ramBuffer = new PointCollection(PerformanceHistoryCapacity);
        private readonly PointCollection _ramFillBuffer = new PointCollection(PerformanceHistoryCapacity + 2);

        #endregion

        #region Properties - Service Data

        /// <summary>
        /// Collection of services available for performance monitoring.
        /// </summary>
        public ObservableCollection<ServiceItemBase> Services { get; } = new ObservableCollection<ServiceItemBase>();

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

        public IServiceCommands ServiceCommands { get; set; }
        public IAsyncCommand SearchCommand { get; }
        public IAsyncCommand CopyPidCommand { get; set; }

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
            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null);
            
            InitTimer();
        }

        #endregion

        #region Private Methods - Logic & Calculation

        /// <summary>
        /// Configures the <see cref="DispatcherTimer"/> used to poll process performance metrics.
        /// </summary>
        /// <remarks>
        /// The timer interval is retrieved from the global <see cref="App.PerformanceRefreshIntervalInMs"/> configuration.
        /// This method hooks the <see cref="OnTick"/> event handler, which is responsible for triggering 
        /// the asynchronous update of CPU and RAM counters.
        /// </remarks>
        private void InitTimer()
        {
            if (_timer == null)
            {
                var app = (App)Application.Current;
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(app.PerformanceRefreshIntervalInMs) };
                _timer.Tick += OnTick;
            }
        }

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
            _cpuValues = Enumerable.Repeat(0.0, PerformanceHistoryCapacity).ToList();
            _ramValues = Enumerable.Repeat(0.0, PerformanceHistoryCapacity).ToList();

            // 3. Reset the UI collections to empty (they will update on next tick)
            CpuPointCollection = new PointCollection();
            CpuFillPoints = new PointCollection();
            RamPointCollection = new PointCollection();
            RamFillPoints = new PointCollection();
        }

        /// <summary>
        /// Updates the PID display text based on the selected service.
        /// </summary>
        private void SetPidText()
        {
            var pidTxt = SelectedService.Pid?.ToString() ?? UiConstants.NotAvailable;
            if (Pid != pidTxt) Pid = pidTxt;
        }

        /// <summary>
        /// Event handler for the monitoring timer. Refreshes performance metrics.
        /// </summary>
        private async void OnTick(object sender, EventArgs e)
        {
            // 1. Atomic Guard: Must be monitoring AND not already running a tick
            // Interlocked.CompareExchange ensure we see the latest state
            if (Interlocked.CompareExchange(ref _isMonitoringFlag, 1, 1) == 0 ||
                Interlocked.CompareExchange(ref _isTickRunningFlag, 1, 0) == 1)
            {
                return;
            }

            _timer?.Stop();

            try
            {
                await OnTickAsync();
            }
            finally
            {
                // Release the flag
                Interlocked.Exchange(ref _isTickRunningFlag, 0);

                // 2. The safety check: Only restart if we are STILL supposed to be monitoring
                // This prevents the timer from "resurrecting" after StopMonitoring was called.
                if (Interlocked.CompareExchange(ref _isMonitoringFlag, 1, 1) == 1)
                {
                    _timer?.Start();
                }
            }
        }

        /// <summary>
        /// Core logic for performance polling.
        /// </summary>
        private async Task OnTickAsync()
        {
            try
            {
                // Capture selection locally to prevent race conditions during the async flow
                var currentSelection = SelectedService;

                // Only reset graphs if selection changed
                if (currentSelection == null)
                {
                    if (_hadSelectedService)
                    {
                        ResetGraphs(true);
                        _hadSelectedService = false;

                        // Notify that the command can no longer execute because selection is gone
                        CopyPidCommand?.RaiseCanExecuteChanged();
                    }
                    return;
                }
                _hadSelectedService = true;

                var currentPid = await _serviceRepository.GetServicePidAsync(currentSelection.Name, _cts.Token);

                if (!currentPid.HasValue)
                {
                    ResetGraphs(true);

                    SelectedService.Pid = null;

                    // IMPORTANT: Tell the command the PID is now available (or gone)
                    CopyPidCommand?.RaiseCanExecuteChanged();
                    return;
                }

                if (currentSelection.Pid != currentPid)
                {
                    currentSelection.Pid = currentPid;   // write to captured local, not SelectedService
                    ResetGraphs(true);

                    // IMPORTANT: Tell the command the PID is now available (or gone)
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }

                int pid = currentSelection.Pid.Value;
                SetPidText();

                // Fetch raw metrics
                var processMetrics = await Task.Run((Func<ProcessMetrics>)(() =>
                {
                    // 1. Perform background maintenance on the PID cache
                    ProcessHelper.MaintainCache();

                    // 2. Retrieve tree-wide metrics
                    return ProcessHelper.GetProcessTreeMetrics((int)pid);
                }));
                double rawRamMb = processMetrics.RamUsage / 1024d / 1024d;

                // Update UI Texts
                CpuUsage = ProcessHelper.FormatCpuUsage(processMetrics.CpuUsage);
                RamUsage = ProcessHelper.FormatRamUsage(processMetrics.RamUsage);

                // Update Graphs
                AddPoint(_cpuValues, processMetrics.CpuUsage, nameof(CpuPointCollection));
                AddPoint(_ramValues, rawRamMb, nameof(RamPointCollection));
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

        /// <summary>
        /// Processes new performance data and updates the point collections using an optimized 
        /// double-buffering approach to minimize GC allocations.
        /// </summary>
        /// <param name="valueHistory">The historical list of data points for the specific metric.</param>
        /// <param name="newValue">The latest raw value captured from the process.</param>
        /// <param name="propertyName">The name of the property being updated (used to distinguish CPU vs RAM logic).</param>
        private void AddPoint(List<double> valueHistory, double newValue, string propertyName)
        {
            var isCpu = propertyName == nameof(CpuPointCollection);
            valueHistory.Add(newValue);

            if (valueHistory.Count > PerformanceHistoryCapacity)
                valueHistory.RemoveAt(0);

            double currentMax = valueHistory.Count > 0 ? valueHistory.Max() : 0;
            double displayMax = isCpu ? 100.0 : Math.Max(currentMax * 1.2, _ramDisplayMax);

            var lineBuffer = isCpu ? _cpuBuffer : _ramBuffer;
            var fillBuffer = isCpu ? _cpuFillBuffer : _ramFillBuffer;

            lineBuffer.Clear();
            fillBuffer.Clear();

            double stepX = GraphWidth / 100.0;
            for (int i = 0; i < valueHistory.Count; i++)
            {
                double x = i * stepX;
                double ratio = Math.Min(Math.Max(valueHistory[i] / displayMax, 0), 1);
                double y = GraphHeight - (ratio * GraphHeight);

                var point = new Point(x, y);
                lineBuffer.Add(point);
                fillBuffer.Add(point);
            }

            // Close the fill polygon
            // Use valueHistory.Count to satisfy static analysis; if we have data, we have points
            if (valueHistory.Count > 0)
            {
                fillBuffer.Add(new Point(fillBuffer[fillBuffer.Count - 1].X, GraphHeight));
                fillBuffer.Add(new Point(fillBuffer[0].X, GraphHeight));
            }

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
        /// Asynchronously searches for services. Consolidates background work and 
        /// handles cancellation to keep the UI responsive.
        /// </summary>
        private async Task SearchServicesAsync(object parameter)
        {
            // Cancel previous search
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // Show "Searching..." immediately
                Mouse.OverrideCursor = Cursors.Wait;
                IsBusy = true;
                SearchButtonText = Strings.Button_Searching;

                // Allow WPF to repaint the button and show progress bar
                // Only execute if we are in a real UI context with a running dispatcher frame
                if (Application.Current?.Dispatcher != null && !Helper.IsRunningInUnitTest())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }

                // Async I/O
                var results = await ServiceCommands.SearchServicesAsync(SearchText, false, token);

                // Mapping is cheap; do it on UI thread
                Services.Clear();
                foreach (var s in results)
                {
                    Services.Add(new PerformanceService { Name = s.Name, Pid = s.Pid });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected: user triggered a new search
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to search services from performance tab.", ex);
            }
            finally
            {
                // Restore button text and IsBusy
                Mouse.OverrideCursor = null;
                IsBusy = false;
                SearchButtonText = Strings.Button_Search;
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
        /// Starts the performance monitoring timer.
        /// </summary>
        public void StartMonitoring()
        {
            // Atomically signal start
            Interlocked.Exchange(ref _isMonitoringFlag, 1);

            // Start timer
            InitTimer();
            _timer?.Start();
        }

        /// <summary>
        /// Stops the performance monitoring timer and optionally clears existing graph data.
        /// </summary>
        /// <param name="clearPoints">True to reset the graph visualizations.</param>
        public void StopMonitoring(bool clearPoints)
        {
            // cancel any in-progress async work
            _cts?.Cancel();

            // Atomically signal stop
            Interlocked.Exchange(ref _isMonitoringFlag, 0);

            // Stop timer
            _timer?.Stop();

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
        /// from timer events to prevent memory leaks during tab navigation.
        /// </summary>
        public void Cleanup()
        {
            // 1. Cancel and dispose the CancellationTokenSource
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            // 2. Stop the timer, unsubscribe from the Tick event, and release the reference
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick; // CRITICAL: Prevents the Dispatcher from keeping the VM alive
                _timer = null;
            }
        }

        #endregion

    }
}