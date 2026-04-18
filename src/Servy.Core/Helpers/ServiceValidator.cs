using Servy.Core.Config;
using Servy.Core.DTOs;
using System.Linq;

namespace Servy.Core.Helpers
{
    public static class ServiceValidator
    {
        /// <summary>
        /// Performs deep validation on a ServiceDto to ensure Windows SCM compatibility 
        /// and system stability.
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateDto(ServiceDto dto)
        {
            // 1. Mandatory Fields & Lengths
            if (string.IsNullOrWhiteSpace(dto.Name)) return (false, "Service name is required.");
            if (string.IsNullOrWhiteSpace(dto.ExecutablePath)) return (false, "Executable path is required.");
            if (dto.Name.Length > AppConfig.MaxServiceNameLength)
                return (false, $"Service name exceeds {AppConfig.MaxServiceNameLength} characters.");

            if (dto.DisplayName?.Length > AppConfig.MaxDisplayNameLength)
                return (false, "Display name is too long.");

            if (dto.Description?.Length > AppConfig.MaxDescriptionLength)
                return (false, "Description exceeds safety limits.");

            // 2. Argument Safety (Win32 Limit)
            var args = new[] { dto.Parameters, dto.PreLaunchParameters, dto.PostLaunchParameters,
                               dto.PreStopParameters, dto.PostStopParameters, dto.FailureProgramParameters };
            if (args.Any(a => a?.Length > AppConfig.MaxArgumentLength))
                return (false, "One or more argument strings exceed the Windows command-line limit.");

            // 3. Numeric Guardrails (Prevent Overflows & Invalid State)
            if (dto.StartTimeout.HasValue && dto.StartTimeout < AppConfig.MinStartTimeout)
                return (false, $"Start Timeout must be at least {AppConfig.MinStartTimeout} second(s).");

            if (dto.StartTimeout.HasValue && dto.StartTimeout > AppConfig.MaxStartTimeout)
                return (false, $"Start Timeout exceeds maximum ({AppConfig.MaxStartTimeout}).");

            // Protect against uint overflow in ChangeServiceConfig2 (SCM)
            if (dto.StopTimeout.HasValue && dto.StopTimeout < AppConfig.MinStopTimeout)
                return (false, $"Stop Timeout must be at least {AppConfig.MinStopTimeout} second(s).");

            if (dto.StopTimeout.HasValue && dto.StopTimeout > AppConfig.MaxStopTimeout)
                return (false, $"Stop Timeout exceeds maximum ({AppConfig.MaxStopTimeout}).");

            if (dto.EnableSizeRotation.HasValue && dto.EnableSizeRotation.Value && (dto.RotationSize < AppConfig.MinRotationSize))
                return (false, "Rotation size is too small.");

            if (dto.EnableHealthMonitoring.HasValue && dto.EnableHealthMonitoring.Value)
            {
                if (dto.HeartbeatInterval.HasValue && dto.HeartbeatInterval < AppConfig.MinHeartbeatInterval)
                    return (false, "Heartbeat interval is too low.");
                if (dto.MaxRestartAttempts.HasValue && dto.MaxRestartAttempts < 0)
                    return (false, "Max restart attempts cannot be negative.");
            }

            return (true, null);
        }
    }
}