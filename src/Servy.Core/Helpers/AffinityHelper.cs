using Servy.Core.Resources;
using System.Globalization;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides utility methods to parse and validate CPU affinity specifications.
    /// Supports core index ranges (e.g., "0-3,8"), comma-separated cores (e.g., "0,2,4"),
    /// or hexadecimal bitmask strings (e.g., "0xFF00").
    /// </summary>
    public static class AffinityHelper
    {
        /// <summary>
        /// Parses a string representation of CPU affinity into an <see cref="IntPtr"/> bitmask.
        /// </summary>
        /// <param name="affinityInput">
        /// The string representing CPU affinity (e.g., "0-3,8", "0,2,4", or "0xFF00").
        /// Null or whitespace returns <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <returns>An <see cref="IntPtr"/> bitmask representing the CPU affinity mask.</returns>
        /// <exception cref="ArgumentException">Thrown when a token or hex format is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when core indices or hexadecimal masks exceed allowed processor bounds (0 to Math.Min(Environment.ProcessorCount, 64) - 1).
        /// </exception>
        public static IntPtr ParseAffinity(string? affinityInput)
        {
            if (string.IsNullOrWhiteSpace(affinityInput))
                return IntPtr.Zero;

            long mask = 0;
            string cleaned = affinityInput.Trim();

            int maxAllowedCores = Math.Min(Environment.ProcessorCount, 64); // Windows IntPtr limit per group is 64
            long allowedMask = maxAllowedCores == 64 ? -1L : (1L << maxAllowedCores) - 1;

            // 1. Hexadecimal format (e.g., "0xFF00" or "0XFF00")
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(cleaned.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out mask))
                {
                    if (mask == 0 || (mask & ~allowedMask) != 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(affinityInput),
                            string.Format(Strings.Msg_CoreIndexRangeOutOfBounds, cleaned, maxAllowedCores - 1));
                    }

                    return new IntPtr(mask);
                }

                throw new ArgumentException(
                    string.Format(Strings.Msg_InvalidHexAffinityFormat, affinityInput),
                    nameof(affinityInput));
            }

            // 2. Comma-separated list with ranges (e.g., "0-3,8,10-12")
            string[] parts = cleaned.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string token = part.Trim();
                if (token.Contains("-"))
                {
                    var range = token.Split('-');
                    if (range.Length == 2 &&
                        int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int start) &&
                        int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int end))
                    {
                        if (end >= maxAllowedCores || start > end)
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(affinityInput),
                                string.Format(Strings.Msg_CoreIndexRangeOutOfBounds, token, maxAllowedCores - 1));
                        }

                        for (int core = start; core <= end; core++)
                        {
                            mask |= (1L << core);
                        }
                        continue;
                    }
                }
                else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int core))
                {
                    if (core >= maxAllowedCores)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(affinityInput),
                            string.Format(Strings.Msg_CoreIndexOutOfBounds, core, maxAllowedCores - 1));
                    }

                    mask |= (1L << core);
                    continue;
                }

                throw new ArgumentException(
                    string.Format(Strings.Msg_InvalidCoreSpecification, token),
                    nameof(affinityInput));
            }

            return new IntPtr(mask);
        }

        /// <summary>
        /// Validates whether a CPU affinity string is correctly formatted and within processor bounds.
        /// </summary>
        /// <param name="affinityInput">The string representation of CPU affinity to validate.</param>
        /// <param name="errorMessage">
        /// When this method returns <c>false</c>, contains the error description detailing why validation failed; otherwise, <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if the affinity input is null/empty or valid; otherwise, <c>false</c>.</returns>
        public static bool ValidateAffinity(string? affinityInput, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(affinityInput))
                return true;

            try
            {
                ParseAffinity(affinityInput);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
