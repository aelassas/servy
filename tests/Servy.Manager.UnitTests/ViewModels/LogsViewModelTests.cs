using Moq;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using Servy.Manager.Models;
using Servy.Manager.ViewModels;
using Servy.UI.Services;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class LogsViewModelTests
    {
        private readonly Mock<IEventLogService> _eventLogServiceMock;
        private readonly Mock<ICursorService> _cursorServiceMock; // New Dependency

        public LogsViewModelTests()
        {
            _eventLogServiceMock = new Mock<IEventLogService>();
            _cursorServiceMock = new Mock<ICursorService>();
        }

        private LogsViewModel CreateViewModel()
        {
            return new LogsViewModel(
                _eventLogServiceMock.Object,
                _cursorServiceMock.Object); // Injecting the mock cursor service
        }

        [Fact]
        public void Constructor_ShouldInitializeDefaults()
        {
            var vm = CreateViewModel();

            Assert.NotNull(vm.LogsView);
            Assert.NotNull(vm.SearchCommand);
            Assert.NotNull(vm.RowClickCommand);
            Assert.False(vm.IsBusy);
            Assert.Contains("Search", vm.SearchButtonText); // Matches Strings.Button_Search
            Assert.NotNull(vm.FromDate);
            Assert.NotNull(vm.ToDate);
            Assert.Equal(EventLogLevel.All, vm.SelectedLevel);
        }

        [Fact]
        public void PropertyChanged_IsRaised()
        {
            var vm = CreateViewModel();
            string? propertyName = null;
            vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

            vm.IsBusy = true;

            Assert.Equal(nameof(LogsViewModel.IsBusy), propertyName);
        }

        [Fact]
        public void FromDate_ShouldUpdate_ToDateMinDate()
        {
            var vm = CreateViewModel();
            var newDate = DateTime.Today.AddDays(-5);

            vm.FromDate = newDate;

            Assert.Equal(newDate, vm.ToDateMinDate);
        }

        [Fact]
        public void ToDate_ShouldUpdate_FromDateMaxDate()
        {
            var vm = CreateViewModel();
            var newDate = DateTime.Today;

            vm.ToDate = newDate;

            Assert.Equal(newDate, vm.FromDateMaxDate);
        }

        [Fact]
        public void RowClickCommand_ShouldSetSelectedLog()
        {
            var vm = CreateViewModel();
            var log = new LogEntryModel { Message = "test" };

            vm.RowClickCommand.Execute(log);

            Assert.Equal("test", vm.SelectedLog?.Message);
            Assert.Equal("test", vm.SelectedLogMessage);
        }

        [Fact]
        public void SearchCommand_ShouldPopulateLogs_AndRaiseScrollEvent()
        {
            Helper.RunOnSTA(async () =>
            {
                // Arrange
                var entries = new List<ServyEventLogEntry>
                {
                    new ServyEventLogEntry { EventId = 1, Time = DateTime.Now, Level = EventLogLevel.Information, Message = "test message" }
                };

                _eventLogServiceMock
                    .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entries);

                var vm = CreateViewModel();
                var scrollRaised = false;
                vm.ScrollLogsToTopRequested += () => scrollRaised = true;

                // Act
                await vm.SearchCommand.ExecuteAsync(null);

                // Assert
                Assert.Single(vm.LogsView.SourceCollection.Cast<LogEntryModel>());
                Assert.True(scrollRaised);
                Assert.Contains("Search", vm.SearchButtonText);
                Assert.False(vm.IsBusy);

                // Verify the cursor service was utilized
                _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public void Cleanup_ShouldCancelAndDisposeToken()
        {
            var vm = CreateViewModel();

            vm.Cleanup();

            // After cleanup, a second call should not throw
            var exception = Record.Exception(() => vm.Cleanup());
            Assert.Null(exception);
        }
    }
}