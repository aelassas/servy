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
    /// <b>Encryption, and what the default hides.</b> Nine columns are encrypted at rest (the registry is
    /// <c>ServiceRepository.SensitiveFields</c>): the four parameter fields, the two pre/post-stop
    /// parameter fields, both environment-variable fields, and the password. By default every one of them
    /// renders as <see cref="Strings.Msg_Show_Masked"/>, and the record is read with
    /// <c>decrypt: false</c>, so the plaintext is not merely withheld from the console - it is never
    /// produced in this process at all. A masked row still tells the reader the field is set, because an
    /// unset column is blank in either mode.
    /// </para>
    /// <para>
    /// <b><c>--decrypt</c> reveals eight of the nine.</b> With the flag the read passes
    /// <c>decrypt: true</c> and the parameter and environment-variable fields print in clear text - the
    /// same data <c>export</c> has always written for the same elevated caller
    /// (<c>ExportXmlAsync</c> and <c>ExportJsonAsync</c> both read decrypted).
    /// <b><see cref="ServiceDto.Password"/> is the exception and stays masked even then</b>, the same way
    /// its <c>[XmlIgnore]</c> and <c>[JsonIgnore]</c> keep it out of an export file. Rendering the
    /// password value is the one change to this class that would breach that contract.
    /// </para>
    /// <para>
    /// The flag is refused without a service name rather than ignored: the list mode renders none of the
    /// nine columns, so it always reads with <c>decrypt: false</c> and there is nothing there to reveal.
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

                // --decrypt reveals columns only the single-service view renders, so asking for it while
                // listing is refused rather than ignored: silently accepting it would suggest the list had
                // been unmasked when it never carried a masked field in the first place.
                if (!hasName && opts.Decrypt)
                    return CommandResult.Fail(Strings.Msg_Show_DecryptRequiresName);

                // The stored records describe privileged launch configuration, so the verb is elevated.
                if (!BypassElevationCheck)
                {
                    SecurityHelper.EnsureAdministrator();
                }

                if (hasName)
                {
                    // The read is only decrypted when the caller asked to see the values, so by default
                    // the plaintext is never produced in this process. See the class remarks.
                    var dto = await _serviceRepository.GetByNameAsync(opts.ServiceName, decrypt: opts.Decrypt, cancellationToken: cancellationToken);

                    if (dto == null)
                        return CommandResult.Fail(Core.Resources.Strings.Msg_ServiceNotFound);

                    var detail = BuildServiceDetail(dto, opts.Decrypt, cancellationToken);
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
        /// <param name="decrypted">
        /// <c>true</c> when the DTO was read decrypted and the caller asked to see the encrypted-at-rest
        /// fields in clear text; <c>false</c> to mask them. The password is masked either way.
        /// </param>
        /// <param name="cancellationToken">A token used when resolving the live service status.</param>
        /// <returns>The rendered report.</returns>
        private string BuildServiceDetail(ServiceDto dto, bool decrypted, CancellationToken cancellationToken)
        {
            var sections = new List<Section>();

            // Identity first, then the live status, then everything else - the order the feature
            // request asks for, so the two lines a reader is most likely to want are the top two.
            var core = new Section(null);
            core.Always(Strings.Msg_Show_Label_Name, dto.Name);
            core.Always(Strings.Msg_Show_Label_Status, ResolveStatusToken(dto.Name, cancellationToken));
            core.Always(Strings.Msg_Show_Label_Pid, dto.Pid?.ToString(CultureInfo.InvariantCulture));
            core.Always(Strings.Msg_Show_Label_DisplayName, dto.DisplayName);
            core.Always(Strings.Msg_Show_Label_Description, dto.Description);
            core.Always(Strings.Msg_Show_Label_StartupType, FormatEnum(typeof(ServiceStartType), dto.StartupType));
            core.Always(Strings.Msg_Show_Label_Priority, FormatEnum(typeof(ProcessPriority), dto.Priority));
            core.IfSet(Strings.Msg_Show_Label_CpuAffinity, dto.CpuAffinity);
            core.Always(Strings.Msg_Show_Label_Executable, dto.ExecutablePath);
            core.Always(Strings.Msg_Show_Label_StartupDir, dto.StartupDirectory);
            core.Always(Strings.Msg_Show_Label_Parameters, Secret(dto.Parameters, decrypted));
            sections.Add(core);

            // Account: the stored password gets a row only to show that one is set; its value is never
            // rendered, not even under --decrypt - see the class remarks.
            var account = new Section(Strings.Msg_Show_Group_Account);
            account.IfSet(Strings.Msg_Show_Label_RunAsLocalSystem, FormatBoolean(dto.RunAsLocalSystem));
            account.IfSet(Strings.Msg_Show_Label_UserAccount, dto.UserAccount);
            // Always masked, including under --decrypt: the value is never rendered, only its presence.
            account.IfSet(Strings.Msg_Show_Label_Password, Secret(dto.Password, decrypted: false));
            sections.Add(account);

            if (!string.IsNullOrWhiteSpace(dto.StdoutPath) || !string.IsNullOrWhiteSpace(dto.StderrPath))
            {
                var logs = new Section(Strings.Msg_Show_Group_Logs);
                logs.Always(Strings.Msg_Show_Label_Stdout, dto.StdoutPath);
                logs.Always(Strings.Msg_Show_Label_Stderr, dto.StderrPath);
                logs.Always(Strings.Msg_Show_Label_ActiveStdout, dto.ActiveStdoutPath);
                logs.Always(Strings.Msg_Show_Label_ActiveStderr, dto.ActiveStderrPath);
                logs.Always(Strings.Msg_Show_Label_SizeRotation, FormatBoolean(dto.EnableSizeRotation));
                logs.Always(Strings.Msg_Show_Label_RotationSize, FormatMegabytes(dto.RotationSize));
                logs.Always(Strings.Msg_Show_Label_DateRotation, FormatBoolean(dto.EnableDateRotation));
                logs.Always(Strings.Msg_Show_Label_RotationPeriod, FormatEnum(typeof(DateRotationType), dto.DateRotationType));
                logs.Always(Strings.Msg_Show_Label_MaxFiles, dto.MaxRotations?.ToString(CultureInfo.InvariantCulture));
                logs.Always(Strings.Msg_Show_Label_LocalTimeRotation, FormatBoolean(dto.UseLocalTimeForRotation));
                sections.Add(logs);
            }

            var timeouts = new Section(Strings.Msg_Show_Group_Timeouts);
            timeouts.IfSet(Strings.Msg_Show_Label_Start, FormatSeconds(dto.StartTimeout));
            timeouts.IfSet(Strings.Msg_Show_Label_Stop, FormatSeconds(dto.StopTimeout));
            sections.Add(timeouts);

            if (dto.EnableHealthMonitoring == true)
            {
                var recovery = new Section(Strings.Msg_Show_Group_Recovery);
                recovery.Always(Strings.Msg_Show_Label_HealthCheck, FormatBoolean(dto.EnableHealthMonitoring));
                recovery.Always(Strings.Msg_Show_Label_Heartbeat, FormatSeconds(dto.HeartbeatInterval));
                recovery.Always(Strings.Msg_Show_Label_MaxFailedChecks, dto.MaxFailedChecks?.ToString(CultureInfo.InvariantCulture));
                recovery.Always(Strings.Msg_Show_Label_Recovery, FormatEnum(typeof(RecoveryAction), dto.RecoveryAction));
                recovery.Always(Strings.Msg_Show_Label_OnCleanExit, FormatBoolean(dto.RecoveryOnCleanExit));
                recovery.Always(Strings.Msg_Show_Label_MaxAttempts, dto.MaxRestartAttempts?.ToString(CultureInfo.InvariantCulture));
                recovery.Always(Strings.Msg_Show_Label_HeartbeatUrl, dto.HeartbeatUrl);
                recovery.Always(Strings.Msg_Show_Label_UrlTimeout, FormatSeconds(dto.HeartbeatUrlTimeoutSeconds));
                recovery.Always(Strings.Msg_Show_Label_UrlFlags, FormatBoolean(dto.EnableHeartbeatUrlFlags));
                sections.Add(recovery);
            }

            if (!string.IsNullOrWhiteSpace(dto.FailureProgramPath))
            {
                var failure = new Section(Strings.Msg_Show_Group_FailureProgram);
                failure.Always(Strings.Msg_Show_Label_Executable, dto.FailureProgramPath);
                failure.Always(Strings.Msg_Show_Label_StartupDir, dto.FailureProgramStartupDirectory);
                failure.Always(Strings.Msg_Show_Label_Parameters, Secret(dto.FailureProgramParameters, decrypted));
                sections.Add(failure);
            }

            var environment = new Section(Strings.Msg_Show_Group_Environment);
            environment.IfSet(Strings.Msg_Show_Label_EnvironmentVariables, Secret(dto.EnvironmentVariables, decrypted));
            environment.IfSet(Strings.Msg_Show_Label_AllowOverriddenRuntimeVars, FormatBoolean(dto.AllowOverriddenRuntimeVars));
            environment.IfSet(Strings.Msg_Show_Label_Dependencies, dto.ServiceDependencies);
            sections.Add(environment);

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchExecutablePath))
            {
                var preLaunch = new Section(Strings.Msg_Show_Group_PreLaunch);
                preLaunch.Always(Strings.Msg_Show_Label_Executable, dto.PreLaunchExecutablePath);
                preLaunch.Always(Strings.Msg_Show_Label_StartupDir, dto.PreLaunchStartupDirectory);
                preLaunch.Always(Strings.Msg_Show_Label_Parameters, Secret(dto.PreLaunchParameters, decrypted));
                preLaunch.Always(Strings.Msg_Show_Label_EnvironmentVariables, Secret(dto.PreLaunchEnvironmentVariables, decrypted));
                preLaunch.Always(Strings.Msg_Show_Label_Stdout, dto.PreLaunchStdoutPath);
                preLaunch.Always(Strings.Msg_Show_Label_Stderr, dto.PreLaunchStderrPath);
                preLaunch.Always(Strings.Msg_Show_Label_Timeout, FormatSeconds(dto.PreLaunchTimeoutSeconds));
                preLaunch.Always(Strings.Msg_Show_Label_RetryAttempts, dto.PreLaunchRetryAttempts?.ToString(CultureInfo.InvariantCulture));
                preLaunch.Always(Strings.Msg_Show_Label_IgnoreFailure, FormatBoolean(dto.PreLaunchIgnoreFailure));
                sections.Add(preLaunch);
            }

            if (!string.IsNullOrWhiteSpace(dto.PostLaunchExecutablePath))
            {
                var postLaunch = new Section(Strings.Msg_Show_Group_PostLaunch);
                postLaunch.Always(Strings.Msg_Show_Label_Executable, dto.PostLaunchExecutablePath);
                postLaunch.Always(Strings.Msg_Show_Label_StartupDir, dto.PostLaunchStartupDirectory);
                postLaunch.Always(Strings.Msg_Show_Label_Parameters, Secret(dto.PostLaunchParameters, decrypted));
                sections.Add(postLaunch);
            }

            if (!string.IsNullOrWhiteSpace(dto.PreStopExecutablePath))
            {
                var preStop = new Section(Strings.Msg_Show_Group_PreStop);
                preStop.Always(Strings.Msg_Show_Label_Executable, dto.PreStopExecutablePath);
                preStop.Always(Strings.Msg_Show_Label_StartupDir, dto.PreStopStartupDirectory);
                preStop.Always(Strings.Msg_Show_Label_Parameters, Secret(dto.PreStopParameters, decrypted));
                preStop.Always(Strings.Msg_Show_Label_Timeout, FormatSeconds(dto.PreStopTimeoutSeconds));
                preStop.Always(Strings.Msg_Show_Label_LogAsError, FormatBoolean(dto.PreStopLogAsError));
                sections.Add(preStop);
            }

            if (!string.IsNullOrWhiteSpace(dto.PostStopExecutablePath))
            {
                var postStop = new Section(Strings.Msg_Show_Group_PostStop);
                postStop.Always(Strings.Msg_Show_Label_Executable, dto.PostStopExecutablePath);
                postStop.Always(Strings.Msg_Show_Label_StartupDir, dto.PostStopStartupDirectory);
                postStop.Always(Strings.Msg_Show_Label_Parameters, Secret(dto.PostStopParameters, decrypted));
                sections.Add(postStop);
            }

            var other = new Section(Strings.Msg_Show_Group_Other);
            other.IfSet(Strings.Msg_Show_Label_ConsoleUI, FormatBoolean(dto.EnableConsoleUI));
            other.IfSet(Strings.Msg_Show_Label_DebugLogs, FormatBoolean(dto.EnableDebugLogs));
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
                    Display(service.Pid?.ToString(CultureInfo.InvariantCulture), notSet)
                });
            }

            var widths = new int[headers.Length];
            for (var col = 0; col < headers.Length; col++)
            {
                widths[col] = rows.Select(r => r[col].Length).Concat(new[] { headers[col].Length }).Max();
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

            return status?.ToString()
                ?? (_serviceManager.IsServiceInstalled(serviceName, cancellationToken)
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

        /// <summary>Renders an optional boolean as Yes or No.</summary>
        /// <param name="value">The stored boolean, or <c>null</c> when unset.</param>
        /// <returns>The rendered boolean, or <c>null</c> when unset.</returns>
        /// <remarks>
        /// Every boolean in the report goes through this one formatter, deliberately. An earlier version
        /// split them into "feature flags" rendered as Enabled/Disabled and everything else as Yes/No,
        /// which put two vocabularies for the same data type in one screen. Yes/No is the pair that reads
        /// correctly on every label here: several of them - Local System, On Clean Exit, Ignore Failure,
        /// Log As Error - are predicates rather than features, and "Ignore Failure: Enabled" is wrong in a
        /// way "Ignore Failure: Yes" is not.
        /// </remarks>
        private static string FormatBoolean(bool? value) =>
            value.HasValue ? (value.Value ? Strings.Msg_Show_Yes : Strings.Msg_Show_No) : null;

        /// <summary>Renders an optional second count with its unit.</summary>
        /// <param name="value">The stored seconds, or <c>null</c> when unset.</param>
        /// <returns>The rendered duration, or <c>null</c> when unset.</returns>
        private static string FormatSeconds(int? value) =>
            value.HasValue ? string.Format(Strings.Msg_Show_Seconds, value.Value.ToString(CultureInfo.InvariantCulture)) : null;

        /// <summary>Renders an optional megabyte count with its unit.</summary>
        /// <param name="value">The stored megabytes, or <c>null</c> when unset.</param>
        /// <returns>The rendered size, or <c>null</c> when unset.</returns>
        private static string FormatMegabytes(int? value) =>
            value.HasValue ? string.Format(Strings.Msg_Show_Megabytes, value.Value.ToString(CultureInfo.InvariantCulture)) : null;

        /// <summary>
        /// Renders a column that is encrypted at rest: the value when the caller asked for it, the mask
        /// when not, and <c>null</c> when the column is unset so no row is emitted at all.
        /// </summary>
        /// <param name="value">The stored value - plaintext when the read was decrypted, ciphertext otherwise.</param>
        /// <param name="decrypted">Whether the caller asked to see the value in clear text.</param>
        /// <returns>The value, the mask, or <c>null</c> when unset.</returns>
        /// <remarks>
        /// Masking keys on whether the column holds anything, never on the content, so the mask cannot
        /// leak the length or shape of a secret - and an unset column stays blank rather than becoming a
        /// row of stars that would imply a value exists.
        /// </remarks>
        private static string Secret(string value, bool decrypted)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            return decrypted ? value : Strings.Msg_Show_Masked;
        }

        /// <summary>Substitutes the not-set placeholder for a blank value.</summary>
        /// <param name="value">The value to display.</param>
        /// <param name="notSet">The placeholder to use when the value is blank.</param>
        /// <returns>The value, or the placeholder.</returns>
        private static string Display(string value, string notSet) =>
            string.IsNullOrWhiteSpace(value) ? notSet : value;

        #endregion

        #region Layout model

        /// <summary>
        /// One rendered line: the already-indented label and the value to show beside it.
        /// </summary>
        private sealed class DetailRow
        {
            /// <summary>Gets the indented label, which also fixes the width of the label column.</summary>
            public string Prefix { get; }

            /// <summary>Gets the rendered value.</summary>
            public string Value { get; }

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
        /// <see cref="Render"/>, so a category whose columns are all empty contributes no empty
        /// heading. Account, Timeouts, Other and Environment are always built and rely on exactly
        /// that, because every row they hold is added by <c>IfSet</c>. The first three still render
        /// for a service Servy created - each flag and timeout is stored with an <c>AppConfig</c>
        /// default at install time - while Environment is dropped unless environment variables or
        /// dependencies are set. Logs, Recovery, Failure Program, Pre-Launch, Post-Launch, Pre-Stop
        /// and Post-Stop are built only when their trigger is present - a stdout or stderr path,
        /// health monitoring, or the category's own executable path - and then add every row with
        /// <c>Always</c>, so a blank column shows the not-set placeholder instead of vanishing.
        /// </summary>
        private sealed class Section
        {
            private const string Indent = "  ";

            /// <summary>Gets the group heading, or <c>null</c> for the leading ungrouped block.</summary>
            public string Title { get; }

            /// <summary>Gets the rows collected for this section.</summary>
            public List<DetailRow> Rows { get; } = new List<DetailRow>();

            /// <summary>
            /// Initializes a new instance of the <see cref="Section"/> class.
            /// </summary>
            /// <param name="title">The group heading, or <c>null</c> for the leading ungrouped block.</param>
            public Section(string title)
            {
                Title = title;
            }

            /// <summary>
            /// Adds a row that is always rendered, showing the not-set placeholder when the value is blank.
            /// </summary>
            /// <param name="label">The row label.</param>
            /// <param name="value">The value to render, or <c>null</c>/blank to render the not-set placeholder.</param>
            public void Always(string label, string value) =>
                Rows.Add(new DetailRow(Prefix(label), Display(value, Strings.Msg_Show_ValueNotSet)));

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
            private string Prefix(string label) => Title == null ? label : Indent + label;
        }

        #endregion
    }
}
