using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Design;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the Logs tab in Servy Manager.
    /// Provides filtering, searching, and displaying log entries from the event log.
    /// </summary>
    public class LogsViewModel : SearchableViewModelBase
    {
        #region Private Fields

        private readonly IAppConfiguration _appConfig;
        private readonly IEventLogService _eventLogService;
        private readonly IMessageBoxService _messageBoxService;
        private LogEntryModel _selectedLog;
        private DateTime? _fromDate;
        private DateTime? _fromDateMaxDate;
        private DateTime? _toDate;
        private DateTime? _toDateMinDate;
        private string _keyword = string.Empty;
        private readonly BulkObservableCollection<LogEntryModel> _logs = new BulkObservableCollection<LogEntryModel>();
        private EventLogLevel? _selectedLevel = EventLogLevel.All;
        private string _selectedLogMessage;

        #endregion

        #region Events

        /// <summary>
        /// Scroll DataGrid to top event.
        /// </summary>
        public event Action ScrollLogsToTopRequested;

        #endregion

        #region Properties

        /// <summary>
        /// Collection of logs displayed in the DataGrid.
        /// </summary>
        public ICollectionView LogsView { get; }

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
                if (!ReferenceEquals(_selectedLog, value))
                {
                    _selectedLog = value;
                    SelectedLogMessage = value?.Message ?? string.Empty;
                    OnPropertyChanged(nameof(SelectedLog));
                }
            }
        }

        /// <summary>
        /// Gets or sets the message of the currently selected log entry.
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
                    OnPropertyChanged(nameof(SelectedLogMessage));
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently selected log level filter.
        /// Defaults to <see cref="EventLogLevel.All"/>.
        /// </summary>
        public EventLogLevel? SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (_selectedLevel != value)
                {
                    _selectedLevel = value;
                    OnPropertyChanged(nameof(SelectedLevel));
                }
            }
        }

        /// <summary>
        /// Gets the list of all available log levels for filtering.
        /// Used to populate a dropdown in the UI.
        /// </summary>
        public static IReadOnlyList<EventLogLevel> LogLevels { get; } = GetLogLevels().AsReadOnly();

        #endregion

        #region Commands

        /// <summary>
        /// Command that executes a log search with the current filter values.
        /// </summary>
        public IAsyncCommand SearchCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Design-Time constructor.
        /// </summary>
        public LogsViewModel() : this(
            appConfig: new DesignTimeAppConfig(),
            eventLogService: new DesignTimeEventLogService(),
            cursorService: new DesignTimeCursorService(),
            messageBoxService: new DesignTimeMessageBoxService())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogsViewModel"/> class.
        /// </summary>
        /// <param name="appConfig">Application configuration settings.</param>
        /// <param name="eventLogService">Service used to fetch event logs.</param>
        /// <param name="cursorService">Service used to control the cursor state.</param>
        /// <param name="messageBoxService">Service used to display modal dialogs (e.g. error popups).</param>
        public LogsViewModel(
            IAppConfiguration appConfig,
            IEventLogService eventLogService,
            ICursorService cursorService,
            IMessageBoxService messageBoxService) : base(cursorService)
        {
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            ResetDateWindowToNow();

            LogsView = new ListCollectionView(_logs);
            SearchCommand = new AsyncCommand(Search, name: nameof(SearchCommand));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Executes a search for logs based on the selected filters.
        /// Updates <see cref="LogsView"/> with the results.
        /// </summary>
        /// <param name="parameter">Optional command parameter (not used).</param>
        private async Task Search(object parameter)
        {
            await ExecuteSearchPipelineAsync(
                async (token) =>
                {
                    // Run search in background
                    var results = await _eventLogService.SearchAsync(SelectedLevel, FromDate, ToDate, Keyword, token);

                    // Update UI safely
                    token.ThrowIfCancellationRequested();

                    // Materialize the search results and construct the data model in a single pass
                    // to eliminate multiple enumeration overhead on potentially deferred underlying sequences.
                    var batch = results.Select(result => new LogEntryModel
                    {
                        Time = result.Time.LocalDateTime,
                        Level = result.Level,
                        EventId = result.EventId,
                        Message = result.Message,
                    }).ToList();

                    _logs.Clear();
                    _logs.AddRange(batch);

                    // Scroll DataGrid to Top
                    ScrollLogsToTopRequested?.Invoke();

                    return _logs.Count;
                },
                noneFormat: Strings.Footer_Log_None,
                oneFormat: Strings.Footer_Log_One,
                manyFormat: Strings.Footer_Log_Many);
        }

        /// <summary>
        /// Derived explicit error hook to pop alerts securely when errors occur.
        /// </summary>
        protected override async Task HandleSearchExceptionAsync(Exception ex)
        {
            var message = ex is SecurityException || ex is InvalidOperationException
                ? ex.Message
                : Strings.Msg_UnexpectedError;

            await _messageBoxService.ShowErrorAsync(message, UiAppConfig.Caption);
        }

        /// <summary>
        /// Gets the list of available log levels for filtering.
        /// Levels exposed in the filter dropdown - excludes Critical/Verbose because Servy itself never emits them
        /// </summary>
        /// <returns>Log levels.</returns>
        private static List<EventLogLevel> GetLogLevels() => Enum.GetValues(typeof(EventLogLevel))
                                                                    .Cast<EventLogLevel>()
                                                                    .Where(logLevel => logLevel != EventLogLevel.Critical && logLevel != EventLogLevel.Verbose)
                                                                    .ToList();

        #endregion

        #region Public Methods & IDisposable Implementation

        /// <summary>
        /// Cancels any in-flight search and restores UI state (cursor, busy flag, button text).
        /// Does not dispose the view model - see <see cref="Dispose()"/> for full teardown.
        /// </summary>
        public void CancelSearch()
        {
            ClearActiveSearchContext(); // Triggers atomic infrastructure base teardown safely
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ScrollLogsToTopRequested = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Re-seeds the default search window to end at the current moment.
        /// Called when the Logs tab is activated so a long-running Manager
        /// does not keep searching the window it started with.
        /// </summary>
        public void ResetDateWindowToNow()
        {
            FromDate = DateTime.Now.AddDays(-_appConfig.LogsWindowDays);
            ToDate = DateTime.Now;
        }

        #endregion
    }
}
