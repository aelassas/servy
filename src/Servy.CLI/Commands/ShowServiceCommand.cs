using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to print a human-readable view of the Servy service configuration held in the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Complements <c>export</c>, which writes the same records as XML or JSON: this verb renders them
    /// for a person reading a console rather than for a machine reading a file.
    /// </para>
    /// <para>
    /// The command requires elevation because the records it reads live under <c>%ProgramData%\Servy</c>
    /// and describe how privileged processes are launched.
    /// </para>
    /// <para>
    /// <b>Encryption.</b> Nine columns are encrypted at rest (the registry is
    /// <c>ServiceRepository.SensitiveFields</c>): the four parameter fields, the two pre/post-stop
    /// parameter fields, both environment-variable fields, and the password. Eight of those nine are
    /// rendered here, so the single-service read passes <c>decrypt: true</c> - under
    /// <c>decrypt: false</c> the console showed the stored ciphertext instead of the configuration.
    /// This matches <c>export</c>, which has always written those fields decrypted for the same
    /// elevated caller: <c>ExportXmlAsync</c> and <c>ExportJsonAsync</c> both read with
    /// <c>decrypt: true</c>.
    /// </para>
    /// <para>
    /// <b>The password is still never printed.</b> That call decrypts it in memory, exactly as an export
    /// does, but it is not among the rendered fields - the same way <see cref="ServiceDto.Password"/>
    /// carries <c>[XmlIgnore]</c> and <c>[JsonIgnore]</c> so an export file never contains it. Adding a
    /// password row is the one change to this class that would breach that contract.
    /// </para>
    /// <para>
    /// The list mode deliberately keeps <c>decrypt: false</c>: none of its six columns is an encrypted
    /// one, so decrypting every record would cost nine field decryptions per service for output that
    /// cannot show any of them.
    /// </para>
    /// <para>
    /// Only labels are localized. Values that form a machine-readable vocabulary - the status token, the
    /// startup-type, priority, rotation-period and recovery-action enum member names - are rendered
    /// invariantly, for the same reason <see cref="ServiceStatusCommand"/> keeps its status token
    /// invariant: callers' scripts parse them.
    /// </para>
    /// </remarks>
    public class ShowServiceCommand : BaseCommand
    {
        private const int MinLabelColumnWidth = 16;
        private const int ColumnGap = 2;

        private readonly IServiceRepository _serviceRepository;
        private readonly IServiceManager _serviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShowServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceRepository">Repository used to read the stored service configuration.</param>
        /// <param name="serviceManager">Service manager used to resolve the live status of each service.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceRepository"/> or <paramref name="serviceManager"/> is <c>null</c>.
        /// </exception>
        public ShowServiceCommand(IServiceRepository serviceRepository, IServiceManager serviceManager)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        }

        /// <summary>
        /// Executes the show command in single-service mode or list mode, depending on whether a name was given.
        /// </summary>
        /// <param name="opts">Show service options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> whose message is the rendered report.</returns>
        public async Task<CommandResult> ExecuteAsync(ShowServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var hasName = !string.IsNullOrWhiteSpace(opts.ServiceName);

            var action = hasName
                ? string.Format(Strings.Msg_ShowServiceAction, opts.ServiceName)
                : Strings.Msg_ShowServicesAction;
            var suggestion = Strings.Msg_ShowServiceSuggestion;

            return await ExecuteWithHandlingAsync("show", action, suggestion, async () =>
            {
                // --search narrows a list; once a single service is named there is nothing left to narrow,
                // so the combination is refused rather than silently dropping one of the two options.
                if (hasName && !string.IsNullOrWhiteSpace(opts.SearchKeyword))
                    return CommandResult.Fail(Strings.Msg_Show_NameAndSearchNotAllowed);

                // The stored records describe privileged launch configuration, so the verb is elevated.
                if (!BypassElevationCheck)
                {
                    SecurityHelper.EnsureAdministrator();
                }

                if (hasName)
                {
                    // decrypt: true - eight of the nine encrypted columns are rendered below, and the
                    // password, which is the ninth, is not. See the class remarks.
                    var dto = await _serviceRepository.GetByNameAsync(opts.ServiceName, decrypt: true, cancellationToken: cancellationToken);

                    if (dto == null)
                        return CommandResult.Fail(Core.Resources.Strings.Msg_ServiceNotFound);

                    var detail = BuildServiceDetail(dto, cancellationToken);
                    Logger.Info(string.Format(Strings.Msg_ShowServiceLogged, opts.ServiceName));
                    return CommandResult.Ok(detail);
                }

                // SearchAsync with an empty keyword is the repository's "everything" query, which is what
                // the list mode wants when --search is absent; it is also the call the Manager's service
                // list uses, so both surfaces narrow on the same fields.
                // decrypt: false - none of the six listed columns is an encrypted one, so decrypting
                // every record would buy nothing. See the class remarks.
                var found = await _serviceRepository.SearchAsync(opts.SearchKeyword ?? string.Empty, decrypt: false, cancellationToken: cancellationToken);

                var services = (found ?? Enumerable.Empty<ServiceDto>())
                    .Where(s => s != null)
                    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (services.Count == 0)
                    return CommandResult.Ok(Strings.Msg_Show_NoServices);

                var table = BuildServiceTable(services, cancellationToken);
                Logger.Info(string.Format(Strings.Msg_ShowServicesLogged, services.Count));
                return CommandResult.Ok(table);
            });
        }

        #region Rendering - single service

        /// <summary>
        /// Renders the full configuration of one service as an aligned, category-grouped report.
        /// </summary>
        /// <param name="dto">The stored service configuration.</param>
        /// <param name="cancellationToken">A token used when resolving the live service status.</param>
        /// <returns>The rendered report.</returns>
        private string BuildServiceDetail(ServiceDto dto, CancellationToken cancellationToken)
        {
            var sections = new List<Section>();

            // Identity first, then the live status, then everything else - the order the feature
            // request asks for, so the two lines a reader is most likely to want are the top two.
            var core = new Section(null);
            core.Always(Strings.Msg_Show_Label_Name, dto.Name);
            core.Always(Strings.Msg_Show_Label_Status, ResolveStatusToken(dto.Name, cancellationToken));
            core.Always(Strings.Msg_Show_Label_Pid, dto.Pid.HasValue ? dto.Pid.Value.ToString(CultureInfo.InvariantCulture) : null);
            core.Always(Strings.Msg_Show_Label_DisplayName, dto.DisplayName);
            core.Always(Strings.Msg_Show_Label_Description, dto.Description);
            core.Always(Strings.Msg_Show_Label_StartupType, FormatEnum(typeof(ServiceStartType), dto.StartupType));
            core.Always(Strings.Msg_Show_Label_Priority, FormatEnum(typeof(ProcessPriority), dto.Priority));
            core.IfSet(Strings.Msg_Show_Label_CpuAffinity, dto.CpuAffinity);
            core.Always(Strings.Msg_Show_Label_Executable, dto.ExecutablePath);
            core.Always(Strings.Msg_Show_Label_StartupDir, dto.StartupDirectory);
            core.Always(Strings.Msg_Show_Label_Parameters, dto.Parameters);
            sections.Add(core);

            // Account: the stored password is deliberately absent - see the class remarks.
            var account = new Section(Strings.Msg_Show_Group_Account);
            account.IfSet(Strings.Msg_Show_Label_RunAsLocalSystem, FormatYesNo(dto.RunAsLocalSystem));
            account.IfSet(Strings.Msg_Show_Label_UserAccount, dto.UserAccount);
            sections.Add(account);

            var logs = new Section(Strings.Msg_Show_Group_Logs);
            logs.IfSet(Strings.Msg_Show_Label_Stdout, dto.StdoutPath);
            logs.IfSet(Strings.Msg_Show_Label_Stderr, dto.StderrPath);
            logs.IfSet(Strings.Msg_Show_Label_ActiveStdout, dto.ActiveStdoutPath);
            logs.IfSet(Strings.Msg_Show_Label_ActiveStderr, dto.ActiveStderrPath);
            logs.IfSet(Strings.Msg_Show_Label_SizeRotation, FormatToggle(dto.EnableSizeRotation));
            logs.IfSet(Strings.Msg_Show_Label_RotationSize, FormatMegabytes(dto.RotationSize));
            logs.IfSet(Strings.Msg_Show_Label_DateRotation, FormatToggle(dto.EnableDateRotation));
            logs.IfSet(Strings.Msg_Show_Label_RotationPeriod, FormatEnum(typeof(DateRotationType), dto.DateRotationType));
            logs.IfSet(Strings.Msg_Show_Label_MaxFiles, dto.MaxRotations.HasValue ? dto.MaxRotations.Value.ToString(CultureInfo.InvariantCulture) : null);
            logs.IfSet(Strings.Msg_Show_Label_LocalTimeRotation, FormatYesNo(dto.UseLocalTimeForRotation));
            sections.Add(logs);

            var timeouts = new Section(Strings.Msg_Show_Group_Timeouts);
            timeouts.IfSet(Strings.Msg_Show_Label_Start, FormatSeconds(dto.StartTimeout));
            timeouts.IfSet(Strings.Msg_Show_Label_Stop, FormatSeconds(dto.StopTimeout));
            sections.Add(timeouts);

            var recovery = new Section(Strings.Msg_Show_Group_Recovery);
            recovery.IfSet(Strings.Msg_Show_Label_HealthCheck, FormatToggle(dto.EnableHealthMonitoring));
            recovery.IfSet(Strings.Msg_Show_Label_Heartbeat, FormatSeconds(dto.HeartbeatInterval));
            recovery.IfSet(Strings.Msg_Show_Label_MaxFailedChecks, dto.MaxFailedChecks.HasValue ? dto.MaxFailedChecks.Value.ToString(CultureInfo.InvariantCulture) : null);
            recovery.IfSet(Strings.Msg_Show_Label_Recovery, FormatEnum(typeof(RecoveryAction), dto.RecoveryAction));
            recovery.IfSet(Strings.Msg_Show_Label_OnCleanExit, FormatYesNo(dto.RecoveryOnCleanExit));
            recovery.IfSet(Strings.Msg_Show_Label_MaxAttempts, dto.MaxRestartAttempts.HasValue ? dto.MaxRestartAttempts.Value.ToString(CultureInfo.InvariantCulture) : null);
            recovery.IfSet(Strings.Msg_Show_Label_HeartbeatUrl, dto.HeartbeatUrl);
            recovery.IfSet(Strings.Msg_Show_Label_UrlTimeout, FormatSeconds(dto.HeartbeatUrlTimeoutSeconds));
            recovery.IfSet(Strings.Msg_Show_Label_UrlFlags, FormatToggle(dto.EnableHeartbeatUrlFlags));
            sections.Add(recovery);

            var failure = new Section(Strings.Msg_Show_Group_FailureProgram);
            failure.IfSet(Strings.Msg_Show_Label_Executable, dto.FailureProgramPath);
            failure.IfSet(Strings.Msg_Show_Label_StartupDir, dto.FailureProgramStartupDirectory);
            failure.IfSet(Strings.Msg_Show_Label_Parameters, dto.FailureProgramParameters);
            sections.Add(failure);

            var environment = new Section(Strings.Msg_Show_Group_Environment);
            environment.IfSet(Strings.Msg_Show_Label_EnvironmentVariables, dto.EnvironmentVariables);
            environment.IfSet(Strings.Msg_Show_Label_Dependencies, dto.ServiceDependencies);
            sections.Add(environment);

            var preLaunch = new Section(Strings.Msg_Show_Group_PreLaunch);
            preLaunch.IfSet(Strings.Msg_Show_Label_Executable, dto.PreLaunchExecutablePath);
            preLaunch.IfSet(Strings.Msg_Show_Label_StartupDir, dto.PreLaunchStartupDirectory);
            preLaunch.IfSet(Strings.Msg_Show_Label_Parameters, dto.PreLaunchParameters);
            preLaunch.IfSet(Strings.Msg_Show_Label_EnvironmentVariables, dto.PreLaunchEnvironmentVariables);
            preLaunch.IfSet(Strings.Msg_Show_Label_Stdout, dto.PreLaunchStdoutPath);
            preLaunch.IfSet(Strings.Msg_Show_Label_Stderr, dto.PreLaunchStderrPath);
            preLaunch.IfSet(Strings.Msg_Show_Label_Timeout, FormatSeconds(dto.PreLaunchTimeoutSeconds));
            preLaunch.IfSet(Strings.Msg_Show_Label_RetryAttempts, dto.PreLaunchRetryAttempts.HasValue ? dto.PreLaunchRetryAttempts.Value.ToString(CultureInfo.InvariantCulture) : null);
            preLaunch.IfSet(Strings.Msg_Show_Label_IgnoreFailure, FormatYesNo(dto.PreLaunchIgnoreFailure));
            sections.Add(preLaunch);

            var postLaunch = new Section(Strings.Msg_Show_Group_PostLaunch);
            postLaunch.IfSet(Strings.Msg_Show_Label_Executable, dto.PostLaunchExecutablePath);
            postLaunch.IfSet(Strings.Msg_Show_Label_StartupDir, dto.PostLaunchStartupDirectory);
            postLaunch.IfSet(Strings.Msg_Show_Label_Parameters, dto.PostLaunchParameters);
            sections.Add(postLaunch);

            var preStop = new Section(Strings.Msg_Show_Group_PreStop);
            preStop.IfSet(Strings.Msg_Show_Label_Executable, dto.PreStopExecutablePath);
            preStop.IfSet(Strings.Msg_Show_Label_StartupDir, dto.PreStopStartupDirectory);
            preStop.IfSet(Strings.Msg_Show_Label_Parameters, dto.PreStopParameters);
            preStop.IfSet(Strings.Msg_Show_Label_Timeout, FormatSeconds(dto.PreStopTimeoutSeconds));
            preStop.IfSet(Strings.Msg_Show_Label_LogAsError, FormatYesNo(dto.PreStopLogAsError));
            sections.Add(preStop);

            var postStop = new Section(Strings.Msg_Show_Group_PostStop);
            postStop.IfSet(Strings.Msg_Show_Label_Executable, dto.PostStopExecutablePath);
            postStop.IfSet(Strings.Msg_Show_Label_StartupDir, dto.PostStopStartupDirectory);
            postStop.IfSet(Strings.Msg_Show_Label_Parameters, dto.PostStopParameters);
            sections.Add(postStop);

            var other = new Section(Strings.Msg_Show_Group_Other);
            other.IfSet(Strings.Msg_Show_Label_ConsoleUI, FormatToggle(dto.EnableConsoleUI));
            other.IfSet(Strings.Msg_Show_Label_DebugLogs, FormatToggle(dto.EnableDebugLogs));
            sections.Add(other);

            return Render(sections);
        }

        /// <summary>
        /// Lays the populated sections out with one shared label column, so every value starts at the
        /// same offset whether its row sits at the top level or under a group heading.
        /// </summary>
        /// <param name="sections">The candidate sections; empty ones are dropped.</param>
        /// <returns>The rendered report.</returns>
        private static string Render(List<Section> sections)
        {
            var populated = sections.Where(s => s.Rows.Count > 0).ToList();

            var width = Math.Max(
                MinLabelColumnWidth,
                populated.SelectMany(s => s.Rows).Select(r => r.Prefix.Length).DefaultIfEmpty(0).Max() + 1);

            var sb = new StringBuilder();

            foreach (var section in populated)
            {
                if (section.Title != null)
                {
                    sb.AppendLine();
                    sb.AppendLine(section.Title);
                }

                foreach (var row in section.Rows)
                {
                    sb.Append(row.Prefix.PadRight(width)).Append(": ").AppendLine(row.Value);
                }
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        #endregion

        #region Rendering - service list

        /// <summary>
        /// Renders one line per service with the columns the feature request asks for.
        /// </summary>
        /// <param name="services">The services to list, already ordered.</param>
        /// <param name="cancellationToken">A token used when resolving each live service status.</param>
        /// <returns>The rendered table.</returns>
        private string BuildServiceTable(List<ServiceDto> services, CancellationToken cancellationToken)
        {
            var notSet = Strings.Msg_Show_ValueNotSet;

            var headers = new[]
            {
                Strings.Msg_Show_Column_Name,
                Strings.Msg_Show_Column_DisplayName,
                Strings.Msg_Show_Column_Description,
                Strings.Msg_Show_Column_StartupType,
                Strings.Msg_Show_Column_Status,
                Strings.Msg_Show_Column_Pid
            };

            var rows = new List<string[]>();

            foreach (var service in services)
            {
                cancellationToken.ThrowIfCancellationRequested();

                rows.Add(new[]
                {
                    Display(service.Name, notSet),
                    Display(service.DisplayName, notSet),
                    Display(service.Description, notSet),
                    Display(FormatEnum(typeof(ServiceStartType), service.StartupType), notSet),
                    ResolveStatusToken(service.Name, cancellationToken),
                    Display(service.Pid.HasValue ? service.Pid.Value.ToString(CultureInfo.InvariantCulture) : null, notSet)
                });
            }

            var widths = new int[headers.Length];
            for (var col = 0; col < headers.Length; col++)
            {
                var index = col;
                widths[col] = rows.Select(r => r[index].Length).Concat(new[] { headers[index].Length }).Max();
            }

            var sb = new StringBuilder();
            AppendTableRow(sb, headers, widths);
            AppendTableRow(sb, widths.Select(w => new string('-', w)).ToArray(), widths);

            foreach (var row in rows)
            {
                AppendTableRow(sb, row, widths);
            }

            sb.AppendLine();
            sb.Append(string.Format(Strings.Msg_Show_ServiceCount, services.Count));

            return sb.ToString();
        }

        /// <summary>
        /// Appends one table line, padding every cell but the last so no trailing spaces are emitted.
        /// </summary>
        /// <param name="sb">The builder receiving the line.</param>
        /// <param name="cells">The cell values for this line.</param>
        /// <param name="widths">The computed column widths.</param>
        private static void AppendTableRow(StringBuilder sb, string[] cells, int[] widths)
        {
            for (var col = 0; col < cells.Length; col++)
            {
                if (col == cells.Length - 1)
                {
                    sb.Append(cells[col]);
                }
                else
                {
                    sb.Append(cells[col].PadRight(widths[col] + ColumnGap));
                }
            }

            sb.AppendLine();
        }

        #endregion

        #region Formatting helpers

        /// <summary>
        /// Resolves the invariant status token for a service, using the same fallback ladder as the
        /// <c>status</c> verb: the SCM value, then Unknown for an installed service whose status cannot
        /// be read, then NotInstalled.
        /// </summary>
        /// <param name="serviceName">The service name to query.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The status token.</returns>
        private string ResolveStatusToken(string serviceName, CancellationToken cancellationToken)
        {
            var status = _serviceManager.GetServiceStatus(serviceName, cancellationToken: cancellationToken);

            return status.HasValue
                ? status.Value.ToString()
                : (_serviceManager.IsServiceInstalled(serviceName, cancellationToken)
                        ? nameof(ServiceStatus.Unknown)
                        : nameof(ServiceStatus.NotInstalled));
        }

        /// <summary>
        /// Renders a stored enum ordinal as its member name, falling back to the raw number when the
        /// stored value is not a defined member.
        /// </summary>
        /// <param name="enumType">The enum type the column represents.</param>
        /// <param name="value">The stored ordinal, or <c>null</c> when the column is unset.</param>
        /// <returns>The member name, the raw number, or <c>null</c> when unset.</returns>
        private static string FormatEnum(Type enumType, int? value)
        {
            if (!value.HasValue) return null;

            var raw = value.Value.ToString(CultureInfo.InvariantCulture);

            try
            {
                // ServiceStartType is backed by uint, so the stored int has to be converted to the
                // enum's own underlying type before Enum.IsDefined can recognise it.
                var converted = Convert.ChangeType(value.Value, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture);
                return Enum.IsDefined(enumType, converted) ? Enum.GetName(enumType, converted) ?? raw : raw;
            }
            catch (OverflowException)
            {
                return raw;
            }
        }

        /// <summary>Renders an optional feature flag as Enabled or Disabled.</summary>
        /// <param name="value">The stored flag, or <c>null</c> when unset.</param>
        /// <returns>The rendered flag, or <c>null</c> when unset.</returns>
        private static string FormatToggle(bool? value)
        {
            return value.HasValue ? (value.Value ? Strings.Msg_Show_Enabled : Strings.Msg_Show_Disabled) : null;
        }

        /// <summary>Renders an optional boolean as Yes or No.</summary>
        /// <param name="value">The stored boolean, or <c>null</c> when unset.</param>
        /// <returns>The rendered boolean, or <c>null</c> when unset.</returns>
        private static string FormatYesNo(bool? value)
        {
            return value.HasValue ? (value.Value ? Strings.Msg_Show_Yes : Strings.Msg_Show_No) : null;
        }

        /// <summary>Renders an optional second count with its unit.</summary>
        /// <param name="value">The stored seconds, or <c>null</c> when unset.</param>
        /// <returns>The rendered duration, or <c>null</c> when unset.</returns>
        private static string FormatSeconds(int? value)
        {
            return value.HasValue ? string.Format(Strings.Msg_Show_Seconds, value.Value.ToString(CultureInfo.InvariantCulture)) : null;
        }

        /// <summary>Renders an optional megabyte count with its unit.</summary>
        /// <param name="value">The stored megabytes, or <c>null</c> when unset.</param>
        /// <returns>The rendered size, or <c>null</c> when unset.</returns>
        private static string FormatMegabytes(int? value)
        {
            return value.HasValue ? string.Format(Strings.Msg_Show_Megabytes, value.Value.ToString(CultureInfo.InvariantCulture)) : null;
        }

        /// <summary>Substitutes the not-set placeholder for a blank value.</summary>
        /// <param name="value">The value to display.</param>
        /// <param name="notSet">The placeholder to use when the value is blank.</param>
        /// <returns>The value, or the placeholder.</returns>
        private static string Display(string value, string notSet)
        {
            return string.IsNullOrWhiteSpace(value) ? notSet : value;
        }

        #endregion

        #region Layout model

        /// <summary>
        /// One rendered line: the already-indented label and the value to show beside it.
        /// </summary>
        private sealed class DetailRow
        {
            /// <summary>Gets the indented label, which also fixes the width of the label column.</summary>
            public string Prefix { get; private set; }

            /// <summary>Gets the rendered value.</summary>
            public string Value { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="DetailRow"/> class.
            /// </summary>
            /// <param name="prefix">The indented label.</param>
            /// <param name="value">The rendered value.</param>
            public DetailRow(string prefix, string value)
            {
                Prefix = prefix;
                Value = value;
            }
        }

        /// <summary>
        /// A category of the single-service report. A section with no rows is dropped by
        /// <see cref="Render"/>, so an unused feature contributes no empty heading.
        /// </summary>
        private sealed class Section
        {
            private const string Indent = "  ";

            /// <summary>Gets the group heading, or <c>null</c> for the leading ungrouped block.</summary>
            public string Title { get; private set; }

            /// <summary>Gets the rows collected for this section.</summary>
            public List<DetailRow> Rows { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="Section"/> class.
            /// </summary>
            /// <param name="title">The group heading, or <c>null</c> for the leading ungrouped block.</param>
            public Section(string title)
            {
                Title = title;
                Rows = new List<DetailRow>();
            }

            /// <summary>
            /// Adds a row that is always rendered, showing the not-set placeholder when the value is blank.
            /// </summary>
            /// <param name="label">The row label.</param>
            /// <param name="value">The value to render.</param>
            public void Always(string label, string value)
            {
                Rows.Add(new DetailRow(Prefix(label), Display(value, Strings.Msg_Show_ValueNotSet)));
            }

            /// <summary>
            /// Adds a row only when the value is present, so an unconfigured field takes no line.
            /// </summary>
            /// <param name="label">The row label.</param>
            /// <param name="value">The value to render, or <c>null</c>/blank to skip the row.</param>
            public void IfSet(string label, string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                Rows.Add(new DetailRow(Prefix(label), value));
            }

            /// <summary>Indents a label when it belongs to a group.</summary>
            /// <param name="label">The row label.</param>
            /// <returns>The indented label.</returns>
            private string Prefix(string label)
            {
                return Title == null ? label : Indent + label;
            }
        }

        #endregion
    }
}
