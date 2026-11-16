using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Covers <see cref="SystemEnvironmentHelper"/> and <see cref="SystemEnvironmentRefresher"/>: a System variable
    /// added, changed or removed after the process started is applied to the process environment without a restart
    /// (#7393), with the application rules (only while the process still holds the old System value) and the service
    /// rules (the registry is the source, a User variable wins, <c>Path</c> joins both). The registry is replaced by
    /// dictionaries and every variable has a unique name, so no test depends on the machine or on another test.
    /// </summary>
    public class SystemEnvironmentHelperTests : IDisposable
    {
        /// <summary>The System variables the fake registry returns; <see langword="null"/> makes it unreadable.</summary>
        private Dictionary<string, string> _system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The user variables the fake registry returns.</summary>
        private readonly Dictionary<string, string> _user = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The process variables this test created, removed again by <see cref="Dispose"/>.</summary>
        private readonly List<string> _names = new List<string>();

        /// <summary>The number of System registry reads.</summary>
        private int _reads;

        /// <summary>The names the refresher asked the setter to change, in call order.</summary>
        private readonly List<string> _setNames = new List<string>();

        /// <summary>Whether setting a process variable throws.</summary>
        private bool _throwOnSet;

        /// <summary>
        /// Removes every process variable the test created.
        /// </summary>
        public void Dispose()
        {
            foreach (var name in _names)
                Environment.SetEnvironmentVariable(name, null);
        }

        /// <summary>
        /// Returns a variable name no process or registry defines, and schedules its removal.
        /// </summary>
        /// <returns>A unique variable name.</returns>
        private string UniqueName()
        {
            var name = "SERVY_TEST_" + Guid.NewGuid().ToString("N");
            _names.Add(name);
            return name;
        }

        /// <summary>
        /// Creates a refresher over the fake registry that writes the real process environment.
        /// </summary>
        /// <param name="pathName">The variable treated as <c>Path</c>.</param>
        /// <returns>The refresher.</returns>
        private SystemEnvironmentRefresher CreateRefresher(string pathName = "Path")
            => new SystemEnvironmentRefresher(
                () => { _reads++; return _system; },
                () => _user,
                (name, value) =>
                {
                    _setNames.Add(name);
                    if (_throwOnSet) throw new InvalidOperationException("set refused");
                    Environment.SetEnvironmentVariable(name, value);
                },
                pathName);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(@"C:\Apps\tool.exe")]
        public void ExpandSystemEnvironmentVariables_NoPlaceholder_ReturnsInputWithoutReadingTheRegistry(string input)
        {
            // Arrange
            var refresher = CreateRefresher();

            // Act
            var result = SystemEnvironmentHelper.ExpandSystemEnvironmentVariables(input, refresher);

            // Assert
            Assert.Equal(input, result);
            Assert.Equal(0, _reads);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_Placeholder_RefreshesThenExpands()
        {
            // Arrange: a System variable added after the process started
            var name = UniqueName();
            _system[name] = @"C:\Python312\python.exe";
            var refresher = CreateRefresher();

            // Act
            var result = SystemEnvironmentHelper.ExpandSystemEnvironmentVariables($"\"%{name.ToLowerInvariant()}%\" -m app", refresher);

            // Assert
            Assert.Equal("\"C:\\Python312\\python.exe\" -m app", result);
            Assert.Equal(1, _reads);
            Assert.Equal(@"C:\Python312\python.exe", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_RegistryUnreadable_ChangesNothing()
        {
            // Arrange
            var name = UniqueName();
            Environment.SetEnvironmentVariable(name, "process");
            var refresher = CreateRefresher();
            refresher.UseRegistryAsSource();
            _system = null;

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("process", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_NoBaseline_AddsMissingVariablesOnly()
        {
            // Arrange: no baseline was captured, so a differing value cannot be told from an override
            var added = UniqueName();
            var existing = UniqueName();
            Environment.SetEnvironmentVariable(existing, "process");
            _system[added] = "new";
            _system[existing] = "registry";
            var refresher = CreateRefresher();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("new", Environment.GetEnvironmentVariable(added));
            Assert.Equal("process", Environment.GetEnvironmentVariable(existing));
        }

        [Fact]
        public void Refresh_Application_ChangedValueStillHeldByTheProcess_IsUpdated()
        {
            // Arrange: the process got the System value at start, then it changed in Windows
            var name = UniqueName();
            _system[name] = @"C:\Java\jdk-17";
            Environment.SetEnvironmentVariable(name, @"C:\Java\jdk-17");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = @"C:\Java\jdk-21";

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Java\jdk-21", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_ChangedValueOverriddenByTheProcess_IsKept()
        {
            // Arrange: the process holds its own value (a User variable or the parent's), not the System one
            var name = UniqueName();
            _system[name] = @"C:\Java\jdk-17";
            Environment.SetEnvironmentVariable(name, @"D:\MyJava");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = @"C:\Java\jdk-21";

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"D:\MyJava", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_ChangedListPrefix_KeepsTheUserPart()
        {
            // Arrange: like Path, the process value is the System value followed by the user's
            var name = UniqueName();
            _system[name] = @"C:\Sys1";
            Environment.SetEnvironmentVariable(name, @"C:\Sys1;C:\User1");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = @"C:\Sys1;C:\Sys2";

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Sys1;C:\Sys2;C:\User1", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_ChangedExpandableValueHeldExpandedByTheProcess_IsUpdated()
        {
            // Arrange: a REG_EXPAND_SZ value is read raw from the registry, the process holds it expanded
            var root = UniqueName();
            var name = UniqueName();
            Environment.SetEnvironmentVariable(root, @"C:\Root");
            _system[name] = $@"%{root}%\jdk-17";
            Environment.SetEnvironmentVariable(name, @"C:\Root\jdk-17");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = $@"%{root}%\jdk-21";

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Root\jdk-21", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_ProcessValueThatOnlyBeginsWithTheOldSystemValue_IsKeptAsAnOverride()
        {
            // Arrange: the process value starts with the baseline value, but not at a ';' boundary
            var name = UniqueName();
            _system[name] = @"C:\Java\jdk-2";
            Environment.SetEnvironmentVariable(name, @"C:\Java\jdk-21");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = @"C:\Java\jdk-3";

            // Act
            refresher.Refresh();

            // Assert
            Assert.DoesNotContain(name, _setNames);
            Assert.Equal(@"C:\Java\jdk-21", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_RemovedVariable_IsRemovedOnlyWhenStillTheSystemValue()
        {
            // Arrange
            var removed = UniqueName();
            var overridden = UniqueName();
            _system[removed] = "system";
            _system[overridden] = "system";
            Environment.SetEnvironmentVariable(removed, "system");
            Environment.SetEnvironmentVariable(overridden, "own");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system.Remove(removed);
            _system.Remove(overridden);

            // Act
            refresher.Refresh();

            // Assert
            Assert.Null(Environment.GetEnvironmentVariable(removed));
            Assert.Equal("own", Environment.GetEnvironmentVariable(overridden));
        }

        [Fact]
        public void Refresh_Application_RemovedVariableWithAUserPart_IsKept()
        {
            // Arrange: like Path, the process value is the System value followed by the user's
            var name = UniqueName();
            _system[name] = @"C:\Sys1";
            Environment.SetEnvironmentVariable(name, @"C:\Sys1;C:\User1");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system.Remove(name);

            // Act
            refresher.Refresh();

            // Assert
            Assert.DoesNotContain(name, _setNames);
            Assert.Equal(@"C:\Sys1;C:\User1", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_VariableEmptiedInTheRegistry_IsRemovedLikeADeletedOne()
        {
            // Arrange: the value is still listed in the registry, but empty
            var name = UniqueName();
            _system[name] = "system";
            Environment.SetEnvironmentVariable(name, "system");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = string.Empty;

            // Act
            refresher.Refresh();

            // Assert
            Assert.Contains(name, _setNames);
            Assert.Null(Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_RemovedPerAccountVariable_IsKept()
        {
            // Arrange: HOMESHARE is a per-account name; the process holds exactly the baseline value
            var original = Environment.GetEnvironmentVariable("HOMESHARE");
            try
            {
                Environment.SetEnvironmentVariable("HOMESHARE", @"\\server\share");
                _system["HOMESHARE"] = @"\\server\share";
                var refresher = CreateRefresher();
                refresher.CaptureBaseline();
                _system.Remove("HOMESHARE");

                // Act
                refresher.Refresh();

                // Assert
                Assert.DoesNotContain("HOMESHARE", _setNames);
                Assert.Equal(@"\\server\share", Environment.GetEnvironmentVariable("HOMESHARE"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("HOMESHARE", original);
            }
        }

        [Fact]
        public void Refresh_Application_UpdatesTheBaseline_SoAKeptOverrideIsNeverRevisited()
        {
            // Arrange: a first refresh applies a change and moves the baseline to v2
            var name = UniqueName();
            _system[name] = "v1";
            Environment.SetEnvironmentVariable(name, "v1");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = "v2";
            refresher.Refresh();
            Environment.SetEnvironmentVariable(name, "v1"); // the user deliberately goes back to the old value

            // Act
            refresher.Refresh();

            // Assert: the registry did not change since the baseline, so v1 is a kept override and stays
            Assert.Equal("v1", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_ExpandableValue_IsExpanded()
        {
            // Arrange: a REG_EXPAND_SZ value read raw
            var name = UniqueName();
            _system[name] = @"%SystemRoot%\System32";
            var refresher = CreateRefresher();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(Environment.GetEnvironmentVariable("SystemRoot") + @"\System32", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_NewVariableReferringToAnotherNewVariable_IsResolvedInALaterPass()
        {
            // Arrange: JAVA_BIN is enumerated first and refers to JAVA_HOME, both added after start
            var home = UniqueName();
            var bin = UniqueName();
            _system[bin] = $@"%{home}%\bin";
            _system[home] = @"C:\Java\jdk-21";
            var refresher = CreateRefresher();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Java\jdk-21\bin", Environment.GetEnvironmentVariable(bin));
        }

        [Fact]
        public void Refresh_EmptyValueAndPerAccountVariable_AreNeverApplied()
        {
            // Arrange: USERNAME=SYSTEM sits in the System key; a service running as a user must keep its own
            var empty = UniqueName();
            _system[empty] = string.Empty;
            _system["USERNAME"] = "SYSTEM";
            var before = Environment.GetEnvironmentVariable("USERNAME");
            var refresher = CreateRefresher();
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.DoesNotContain(empty, _setNames);
            Assert.Null(Environment.GetEnvironmentVariable(empty));
            Assert.Equal(before, Environment.GetEnvironmentVariable("USERNAME"));
        }

        [Fact]
        public void Refresh_Service_RegistryWinsOverTheInheritedValue()
        {
            // Arrange: the Service Control Manager's boot-time value is stale
            var name = UniqueName();
            Environment.SetEnvironmentVariable(name, "boot");
            _system[name] = "now";
            var refresher = CreateRefresher();
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("now", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Service_UserVariableOfTheSameName_Wins()
        {
            // Arrange
            var name = UniqueName();
            Environment.SetEnvironmentVariable(name, "user");
            _system[name] = "system";
            _user[name] = "user";
            var refresher = CreateRefresher();
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("user", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Service_PathIsTheSystemValueFollowedByTheUserValue()
        {
            // Arrange: a unique name stands in for Path, so the test never touches the real one
            var path = UniqueName();
            Environment.SetEnvironmentVariable(path, @"C:\Old;C:\User");
            _system[path] = @"C:\Sys1;C:\Sys2;";
            _user[path] = @"C:\User";
            var refresher = CreateRefresher(path);
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Sys1;C:\Sys2;C:\User", Environment.GetEnvironmentVariable(path));
        }

        [Fact]
        public void Refresh_Service_PathWithAnExpandableUserValue_AppendsTheUserValueExpanded()
        {
            // Arrange: the user's Path refers to another variable, as %USERPROFILE%\... does
            var home = UniqueName();
            var path = UniqueName();
            Environment.SetEnvironmentVariable(home, @"C:\Users\svc");
            Environment.SetEnvironmentVariable(path, @"C:\Old");
            _system[path] = @"C:\Sys1";
            _user[path] = $@"%{home}%\bin";
            var refresher = CreateRefresher(path);
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Sys1;C:\Users\svc\bin", Environment.GetEnvironmentVariable(path));
        }

        [Fact]
        public void Refresh_ProcessSetThrows_IsIgnored()
        {
            // Arrange
            var name = UniqueName();
            _system[name] = "value";
            _throwOnSet = true;
            var refresher = CreateRefresher();

            // Act
            var ex = Record.Exception(() => refresher.Refresh());

            // Assert
            Assert.Null(ex);
            Assert.Null(Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_VariableAddedAfterBaselineButHeldByTheProcess_IsKept()
        {
            // Arrange: the name is not in the baseline, and the process already has its own value
            var name = UniqueName();
            Environment.SetEnvironmentVariable(name, "own");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = "registry";

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("own", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_EmptyValueAndPerAccountVariable_AreNeverApplied()
        {
            // Arrange: HOMESHARE is a per-account name; it is absent from the process, then restored afterwards
            var empty = UniqueName();
            var original = Environment.GetEnvironmentVariable("HOMESHARE");
            try
            {
                Environment.SetEnvironmentVariable("HOMESHARE", null);
                _system[empty] = string.Empty;
                _system["HOMESHARE"] = @"\\server\share";
                var refresher = CreateRefresher();
                refresher.CaptureBaseline();

                // Act
                refresher.Refresh();

                // Assert
                Assert.DoesNotContain(empty, _setNames);
                Assert.DoesNotContain("HOMESHARE", _setNames);
                Assert.Null(Environment.GetEnvironmentVariable(empty));
                Assert.Null(Environment.GetEnvironmentVariable("HOMESHARE"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("HOMESHARE", original);
            }
        }

        [Fact]
        public void Refresh_Service_UserRegistryUnreadable_StillAppliesTheSystemValue()
        {
            // Arrange
            var name = UniqueName();
            Environment.SetEnvironmentVariable(name, "boot");
            _system[name] = "now";
            var refresher = new SystemEnvironmentRefresher(
                () => _system,
                () => null,
                (n, v) => Environment.SetEnvironmentVariable(n, v));
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("now", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Service_PathNameInAnotherCaseThanTheRegistryName_StillJoinsBothValues()
        {
            // Arrange: the registry names the variable in lower case, the refresher was given it in upper case
            var path = UniqueName();
            Environment.SetEnvironmentVariable(path, @"C:\Old");
            _system[path.ToLowerInvariant()] = @"C:\Sys1";
            _user[path] = @"C:\User";
            var refresher = CreateRefresher(path.ToUpperInvariant());
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Sys1;C:\User", Environment.GetEnvironmentVariable(path));
        }

        [Fact]
        public void Refresh_Service_EmptyUserVariableOfTheSameName_DoesNotHideTheSystemValue()
        {
            // Arrange
            var name = UniqueName();
            Environment.SetEnvironmentVariable(name, "boot");
            _system[name] = "now";
            _user[name] = string.Empty;
            var refresher = CreateRefresher();
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("now", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void Refresh_Application_ChangedListPrefixHeldInAnotherCase_KeepsTheUserPart()
        {
            // Arrange: the process holds the System part in lower case, followed by the user's
            var name = UniqueName();
            _system[name] = @"C:\Sys1";
            Environment.SetEnvironmentVariable(name, @"c:\sys1;C:\User1");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = @"C:\Sys1;C:\Sys2";

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(@"C:\Sys1;C:\Sys2;C:\User1", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void CaptureBaseline_CalledTwice_KeepsTheFirstBaseline()
        {
            // Arrange: the System value changes between the two calls
            var name = UniqueName();
            _system[name] = "v1";
            Environment.SetEnvironmentVariable(name, "v1");
            var refresher = CreateRefresher();
            refresher.CaptureBaseline();
            _system[name] = "v2";
            refresher.CaptureBaseline();

            // Act
            refresher.Refresh();

            // Assert: the change is still measured against v1, so it is applied
            Assert.Equal("v2", Environment.GetEnvironmentVariable(name));
        }

        [Fact]
        public void CaptureBaseline_RegistryUnreadable_DoesNotThrow_AndTheNextRefreshOnlyAddsMissingVariables()
        {
            // Arrange
            var held = UniqueName();
            var added = UniqueName();
            Environment.SetEnvironmentVariable(held, "own");
            var refresher = CreateRefresher();
            var readable = _system;
            _system = null;

            // Act
            var ex = Record.Exception(() => refresher.CaptureBaseline());
            _system = readable;
            _system[held] = "registry";
            _system[added] = "new";
            refresher.Refresh();

            // Assert
            Assert.Null(ex);
            Assert.Equal("own", Environment.GetEnvironmentVariable(held));
            Assert.Equal("new", Environment.GetEnvironmentVariable(added));
        }

        [Fact]
        public void Refresh_Service_SetsAChangedVariableOnce_AndNeverSetsOneThatIsAlreadyCurrent()
        {
            // Arrange
            var stale = UniqueName();
            var current = UniqueName();
            Environment.SetEnvironmentVariable(stale, "boot");
            Environment.SetEnvironmentVariable(current, "now");
            _system[stale] = "now";
            _system[current] = "now";
            var refresher = CreateRefresher();
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal(1, _setNames.Count(n => n == stale));
            Assert.DoesNotContain(current, _setNames);
        }
    }
}
