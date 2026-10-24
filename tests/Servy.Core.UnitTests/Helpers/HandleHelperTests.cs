using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Covers <see cref="HandleHelper"/>'s handling of the Restart Manager results through a fake API: buffer growth,
    /// errors, session cleanup, de-duplication and PID-reuse filtering.
    /// </summary>
    public class HandleHelperTests
    {
        private const uint Session = 42;

        /// <summary>
        /// Plays back scripted <c>RmGetList</c> results and records every call.
        /// </summary>
        private sealed class FakeRestartManager : IRestartManagerApi
        {
            public int StartResult { get; set; }
            public int RegisterResult { get; set; }
            public int EndResult { get; set; }

            /// <summary>Each GetList call takes the next result; the last one repeats.</summary>
            public List<(int Rc, NativeMethods.RM_PROCESS_INFO[] Entries)> Lists { get; } = new List<(int, NativeMethods.RM_PROCESS_INFO[])>();

            public List<string> Registered { get; } = new List<string>();
            public List<int?> BufferLengths { get; } = new List<int?>();
            public int EndCalls { get; private set; }
            public uint? EndedSession { get; private set; }

            public int StartSession(out uint sessionHandle)
            {
                sessionHandle = Session;
                return StartResult;
            }

            public int RegisterFile(uint sessionHandle, string filePath)
            {
                Registered.Add(filePath);
                return RegisterResult;
            }

            public int GetList(uint sessionHandle, out uint needed, ref uint count, NativeMethods.RM_PROCESS_INFO[] processes)
            {
                BufferLengths.Add(processes?.Length);
                var (rc, entries) = Lists[Math.Min(BufferLengths.Count - 1, Lists.Count - 1)];
                needed = (uint)entries.Length;

                if (rc == 0)
                {
                    // Like the real API: success only when the entries fit.
                    if (entries.Length > (processes?.Length ?? 0) && entries.Length > 0)
                        return Errors.ERROR_MORE_DATA;
                    for (int i = 0; i < entries.Length; i++)
                        processes[i] = entries[i];
                    count = (uint)entries.Length;
                }

                return rc;
            }

            public int EndSession(uint sessionHandle)
            {
                EndCalls++;
                EndedSession = sessionHandle;
                return EndResult;
            }
        }

        private static int CurrentPid
        {
            get { using (var self = Process.GetCurrentProcess()) return self.Id; }
        }

        private static NativeMethods.FILETIME ToFileTime(DateTime time)
        {
            long ft = time.ToUniversalTime().ToFileTimeUtc();
            return new NativeMethods.FILETIME { dwLowDateTime = (uint)(ft & 0xFFFFFFFF), dwHighDateTime = (uint)(ft >> 32) };
        }

        private static NativeMethods.RM_PROCESS_INFO Entry(int pid, NativeMethods.FILETIME startTime) =>
            new NativeMethods.RM_PROCESS_INFO
            {
                Process = new NativeMethods.RM_UNIQUE_PROCESS { dwProcessId = pid, ProcessStartTime = startTime },
                strAppName = "app",
                strServiceShortName = string.Empty,
            };

        private static NativeMethods.RM_PROCESS_INFO Self()
        {
            using (var self = Process.GetCurrentProcess())
                return Entry(self.Id, ToFileTime(self.StartTime));
        }

        /// <summary>
        /// Finds a process identifier no running process has, for an entry whose process has exited.
        /// </summary>
        private static int UnusedPid()
        {
            var used = new HashSet<int>(Process.GetProcesses().Select(p => { using (p) return p.Id; }));
            int pid = 0x7FFFFFF0;
            while (used.Contains(pid)) pid -= 4;
            return pid;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void GetProcessesUsingFile_BlankPath_ThrowsArgumentExceptionWithoutStartingASession(string path)
        {
            // Arrange
            var api = new FakeRestartManager();

            // Act
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile(path, api));

            // Assert
            Assert.Empty(api.Registered);
            Assert.Equal(0, api.EndCalls);
        }

        [Fact]
        public void GetProcessesUsingFile_NullApi_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => HandleHelper.GetProcessesUsingFile(@"C:\x.txt", null));
        }

        [Fact]
        public void GetProcessesUsingFile_RelativePath_RegistersTheFullPath()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((0, Array.Empty<NativeMethods.RM_PROCESS_INFO>()));

            // Act
            HandleHelper.GetProcessesUsingFile("locked.dll", api);

            // Assert
            Assert.Equal(new[] { Path.GetFullPath("locked.dll") }, api.Registered);
        }

        [Fact]
        public void GetProcessesUsingFile_StartSessionFails_ThrowsWin32ExceptionWithItsCodeAndEndsNothing()
        {
            // Arrange
            var api = new FakeRestartManager { StartResult = 5 };

            // Act
            var ex = Assert.Throws<Win32Exception>(() => HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api));

            // Assert
            Assert.Equal(5, ex.NativeErrorCode);
            Assert.Empty(api.Registered);
            Assert.Equal(0, api.EndCalls);
        }

        [Fact]
        public void GetProcessesUsingFile_RegisterFails_ThrowsWin32ExceptionAndEndsTheSession()
        {
            // Arrange
            var api = new FakeRestartManager { RegisterResult = 87 };

            // Act
            var ex = Assert.Throws<Win32Exception>(() => HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api));

            // Assert
            Assert.Equal(87, ex.NativeErrorCode);
            Assert.Empty(api.BufferLengths);
            Assert.Equal(1, api.EndCalls);
            Assert.Equal(Session, api.EndedSession);
        }

        [Fact]
        public void GetProcessesUsingFile_GetListFails_ThrowsWin32ExceptionAndEndsTheSession()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((Errors.ERROR_ACCESS_DENIED, Array.Empty<NativeMethods.RM_PROCESS_INFO>()));

            // Act
            var ex = Assert.Throws<Win32Exception>(() => HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api));

            // Assert
            Assert.Equal(Errors.ERROR_ACCESS_DENIED, ex.NativeErrorCode);
            Assert.Equal(1, api.EndCalls);
        }

        [Fact]
        public void GetProcessesUsingFile_NothingHoldsTheFile_ReturnsEmptyAfterOneSizeQuery()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((0, Array.Empty<NativeMethods.RM_PROCESS_INFO>()));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            Assert.Empty(results);
            Assert.Equal(new int?[] { null }, api.BufferLengths);
            Assert.Equal(1, api.EndCalls);
        }

        [Fact]
        public void GetProcessesUsingFile_ListDoesNotFit_GrowsTheBufferToTheReportedSizeAndReturnsTheHolder()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((0, new[] { Self() }));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            // First call sizes the list (no buffer), the second fills a buffer of exactly the reported size.
            Assert.Equal(new int?[] { null, 1 }, api.BufferLengths);
            var holder = Assert.Single(results);
            Assert.Equal(CurrentPid, holder.ProcessId);
            using (var self = Process.GetCurrentProcess())
                Assert.Equal(self.ProcessName, holder.ProcessName);
        }

        [Fact]
        public void GetProcessesUsingFile_ListKeepsGrowing_ThrowsInvalidOperationAfterTheConfiguredAttemptsAndEndsTheSession()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((Errors.ERROR_MORE_DATA, new[] { Self() }));

            // Act
            Assert.Throws<InvalidOperationException>(() => HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api));

            // Assert
            Assert.Equal(AppConfig.RestartManagerGetListMaxAttempts, api.BufferLengths.Count);
            Assert.Equal(1, api.EndCalls);
        }

        [Fact]
        public void GetProcessesUsingFile_SameProcessListedTwice_ReturnsItOnce()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((0, new[] { Self(), Self() }));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            Assert.Single(results);
        }

        [Fact]
        public void GetProcessesUsingFile_NonPositivePid_IsSkipped()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((0, new[] { Entry(0, default), Entry(-4, default), Self() }));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            Assert.Equal(new[] { CurrentPid }, results.Select(r => r.ProcessId));
        }

        [Fact]
        public void GetProcessesUsingFile_ProcessExitedSinceTheList_IsSkipped()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((0, new[] { Entry(UnusedPid(), default), Self() }));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            Assert.Equal(new[] { CurrentPid }, results.Select(r => r.ProcessId));
        }

        [Fact]
        public void GetProcessesUsingFile_PidReusedByALaterProcess_IsSkipped()
        {
            // Arrange
            // The entry claims this PID belonged to a process created an hour before this one: the holder exited and
            // the PID was reused, so killing the current owner would kill the wrong process.
            DateTime started;
            using (var self = Process.GetCurrentProcess())
                started = self.StartTime;
            var api = new FakeRestartManager();
            api.Lists.Add((0, new[] { Entry(CurrentPid, ToFileTime(started.AddHours(-1))) }));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetProcessesUsingFile_NoStartTimeReported_KeepsTheProcess()
        {
            // Arrange
            var api = new FakeRestartManager();
            api.Lists.Add((0, new[] { Entry(CurrentPid, default) }));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            Assert.Equal(new[] { CurrentPid }, results.Select(r => r.ProcessId));
        }

        [Fact]
        public void GetProcessesUsingFile_EndSessionFails_StillReturnsTheResult()
        {
            // Arrange
            var api = new FakeRestartManager { EndResult = 6 };
            api.Lists.Add((0, new[] { Self() }));

            // Act
            var results = HandleHelper.GetProcessesUsingFile(@"C:\x.txt", api);

            // Assert
            Assert.Single(results);
            Assert.Equal(1, api.EndCalls);
        }
    }
}
