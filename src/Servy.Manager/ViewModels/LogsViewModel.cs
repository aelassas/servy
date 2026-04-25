using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.UI.Commands;
using Servy.UI.Helpers;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the Logs tab in Servy Manager.
    /// Provides filtering, searching, and displaying log entries from the event log.
    /// </summary>
    public class LogsViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Private Fields

        private readonly IEventLogService _eventLogService;
        private readonly ICursorService _cursorService;
        private bool _isBusy;
        private string _searchButtonText = Strings.Button_Search;
        private LogEntryModel _selectedLog;
        private DateTime? _fromDate;
        private DateTime? _fromDateMaxDate;
        private DateTime? _toDate;
        private DateTime? _toDateMinDate;
        private string _keyword = string.Empty;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ObservableCollection<LogEntryModel> _logs = new ObservableCollection<LogEntryModel>();
        private string _selectedLogMessage;
        private string _footerText;
        private bool _disposedValue;

        #endregion

        #region Events

        /// <summary>
        /// Scroll DataGrid to top event.
        /// </summary>
        public event Action ScrollLogsToTopRequested;

        /// <summary>
        /// Occurs when a property value changes.
        /// Used to update data bindings in the UI.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the given property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Collection of logs displayed in the DataGrid.
        /// </summary>
        public ICollectionView LogsView { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether a background operation is in progress.
        /// Used to show a busy indicator in the UI.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets footer text displayed in the UI.
        /// </summary>
        public string FooterText
        {
            get => _footerText;
            set
            {
                if (_footerText != value)
                {
                    _footerText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the text displayed on the search button.
        /// Defaults to "Search" and changes to "Searching..." while a query is running.
        /// </summary>
        public string SearchButtonText
        {
            get => _searchButtonText;
            set
            {
                if (_searchButtonText != value)
                {
                    _searchButtonText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the starting date for filtering log entries.
        /// Setting this updates <see cref="ToDateMinDate"/> to prevent invalid ranges.
        /// </summary>
        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (_fromDate != value)
                {
                    _fromDate = value;
                    ToDateMinDate = value?.Date;
                    OnPropertyChanged(nameof(FromDate));
                    OnPropertyChanged(nameof(ToDateMinDate));
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum allowed date for <see cref="FromDate"/>.
        /// Updated when <see cref="ToDate"/> changes.
        /// </summary>
        public DateTime? FromDateMaxDate
        {
            get => _fromDateMaxDate;
            set
            {
                if (_fromDateMaxDate != value)
                {
                    _fromDateMaxDate = value;
                    OnPropertyChanged(nameof(FromDateMaxDate));
                }
            }
        }

        /// <summary>
        /// Gets or sets the ending date for filtering log entries.
        /// Setting this updates <see cref="FromDateMaxDate"/> to prevent invalid ranges.
        /// </summary>
        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (_toDate != value)
                {
                    _toDate = value;
                    FromDateMaxDate = value?.Date;
                    OnPropertyChanged(nameof(ToDate));
                    OnPropertyChanged(nameof(FromDateMaxDate));
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum allowed date for <see cref="ToDate"/>.
        /// Updated when <see cref="FromDate"/> changes.
        /// </summary>
        public DateTime? ToDateMinDate
        {
            get => _toDateMinDate;
            set
            {
                if (_toDateMinDate != value)
                {
                    _toDateMinDate = value;
                    OnPropertyChanged(nameof(ToDateMinDate));
                }
            }
        }

        /// <summary>
        /// Gets or sets the keyword used to filter log messages.
        /// Case-insensitive search is applied.
        /// </summary>
        public string Keyword
        {
            get => _keyword;
            set
            {
                if (_keyword != value)
                {
                    _keyword = value;
                    OnPropertyChanged(nameof(Keyword));
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently selected log entry in the UI.
        /// </summary>
        public LogEntryModel SelectedLog
        {
            get => _selectedLog;
            set
            {
                _selectedLog = value;
                SelectedLogMessage = value?.Message ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the message of the currently selected log entry.
        /// Returns an empty string if none is selected.
        /// </summary>
        public string SelectedLogMessage
        {
            get => _selectedLogMessage;
            set
            {
                if (_selectedLogMessage != value)
                {
                    _selectedLogMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private EventLogLevel? _selectedLevel = EventLogLevel.All;

        /// <summary>
        /// Gets or sets the currently selected log level filter.
        /// Defaults to <see cref="EventLogLevel.All"/>.
        /// </summary>
        public EventLogLevel? SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged(nameof(SelectedLevel));
            }
        }

        /// <summary>
        /// Gets the list of all available log levels for filtering.
        /// Used to populate a dropdown in the UI.
        /// </summary>
        public static List<EventLogLevel> LogLevels => GetLogLevels();

        #endregion

        #region Commands

        /// <summary>
        /// Command that executes a log search with the current filter values.
        /// </summary>
        public IAsyncCommand SearchCommand { get; }

        /// <summary>
        /// Command triggered when a log row is clicked in the UI.
        /// Updates the <see cref="SelectedLog"/>.
        /// </summary>
        public ICommand RowClickCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LogsViewModel"/> class.
        /// </summary>
        /// <param name="eventLogService">Service used to fetch event logs.</param>
        /// <param name="cursorService">Service used to control the cursor state.</param>
        public LogsViewModel(IEventLogService eventLogService, ICursorService cursorService)
        {
            _eventLogService = eventLogService;
            _cursorService = cursorService ?? throw new ArgumentNullException(nameof(cursorService));

            FromDate = DateTime.Now.AddDays(-3); // Default to last 3 days
            ToDate = DateTime.Now; // Default to now

            LogsView = new ListCollectionView(_logs);
            SearchCommand = new AsyncCommand(Search);
            RowClickCommand = new RelayCommand<object>(OnRowClick);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Executes a search for logs based on the selected filters.
        /// Updates <see cref="Logs"/> with the results.
        /// </summary>
        /// <param name="parameter">Optional command parameter (not used).</param>
        private async Task Search(object parameter)
        {
            var stopwatch = Stopwatch.StartNew();

            // 1. Create the new token for THIS specific search
            var newCts = new CancellationTokenSource();

            // 2. Atomic swap: the "Lazy" part. 
            // If _cancellationTokenSource was null, oldCts is null.
            // If a search was already running, oldCts is the previous one.
            var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, newCts);

            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var token = newCts.Token;

            try
            {
                FooterText = string.Empty; // Clear footer text before search

                // Step 1: show "Searching..." immediately
                _cursorService.SetWaitCursor();
                SearchButtonText = Strings.Button_Searching;
                IsBusy = true;

                // Step 2: Run search in background
                var results = await _eventLogService.SearchAsync(SelectedLevel, FromDate, ToDate, Keyword, token);

                // Step 3: Update UI safely
                token.ThrowIfCancellationRequested();

                _logs.Clear();

                foreach (var result in results)
                {
                    var entry = new LogEntryModel
                    {
                        Time = result.Time.LocalDateTime,
                        Level = result.Level.ToString(),
                        EventId = result.EventId,
                        Message = result.Message,
                    };

                    _logs.Add(entry);
                }

                // Scroll DataGrid to Top
                ScrollLogsToTopRequested?.Invoke();

                stopwatch.Stop();
                FooterText = Helper.GetRowsInfo(_logs.Count, stopwatch.Elapsed, Strings.Footer_LogRowText);
            }
            catch (OperationCanceledException)
            {
                // Search was canceled; no action needed
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to search logs.", ex);
            }
            finally
            {
                // Step 4: restore button text and IsBusy
                _cursorService.ResetCursor();
                SearchButtonText = Strings.Button_Search;
                IsBusy = false;
            }
        }

        /// <summary>
        /// Handles row click events in the Logs DataGrid.
        /// Updates the <see cref="SelectedLog"/>.
        /// </summary>
        /// <param name="parameter">The clicked row's bound model.</param>
        private void OnRowClick(object parameter)
        {
            if (parameter is LogEntryModel model)
            {
                SelectedLog = model;
            }
        }

        /// <summary>
        /// Gets the list of all available log levels for filtering.
        /// </summary>
        /// <returns></returns>
        private static List<EventLogLevel> GetLogLevels() => Enum.GetValues(typeof(EventLogLevel))
                                                            .Cast<EventLogLevel>()
                                                            .ToList();

        #endregion

        #region Public Methods & IDisposable Implementation

        /// <summary>
        /// Cancels any ongoing search and cleans up the cancellation token.
        /// Maintained as an alias for <see cref="Dispose()"/> to ensure backward compatibility.
        /// </summary>
        public void Cleanup()
        {
            Dispose();
        }

        /// <summary>
        /// Safely disposes of resources, specifically cancelling and disposing the internal 
        /// <see cref="CancellationTokenSource"/> to prevent memory leaks.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Use Interlocked to ensure we only dispose the token once
                    var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, null);
                    if (oldCts != null)
                    {
                        try
                        {
                            oldCts.Cancel();
                        }
                        catch (ObjectDisposedException) { /* Already gone */ }
                        finally
                        {
                            oldCts.Dispose();
                        }
                    }
                }

                _disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }
}