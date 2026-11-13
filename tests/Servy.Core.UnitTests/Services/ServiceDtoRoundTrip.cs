using System.Reflection;
using System.Xml.Serialization;
using Servy.Core.DTOs;
using Servy.Testing;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// The round-trip contract the JSON and XML serializer suites share: which <see cref="ServiceDto"/> properties
    /// a round trip is not expected to preserve, and the reflective comparison that checks every other one.
    /// </summary>
    internal static class ServiceDtoRoundTrip
    {
        /// <summary>
        /// The identity, runtime and credential properties a serializer round trip is not expected to preserve:
        /// <c>Id</c> and <c>Pid</c> are assigned outside the payload, <c>Password</c> is dropped by
        /// <c>[XmlIgnore]</c>/<c>[JsonIgnore]</c>, <c>UserAccount</c> is written only for a passwordless account
        /// and is otherwise reset on import together with <c>RunAsLocalSystem</c>, and the remaining five are runtime state (<c>PreviousStopTimeout</c>, <c>ActiveStdoutPath</c>,
        /// <c>ActiveStderrPath</c>, <c>RestartAttempts</c>, <c>RestartAttemptsUpdatedAtTicks</c>). <c>DecryptionFailed</c> is the outcome of one read,
        /// also dropped by <c>[XmlIgnore]</c>/<c>[JsonIgnore]</c>: an export must not carry a verdict about the key
        /// that read it (#7348), and neither must <c>LegacyEncryptionBlocked</c>, which says which kind of failure it was (#7816). Stated once, so adding a property to
        /// <see cref="ServiceDto"/> means deciding here whether it round trips, rather than in four test bodies.
        /// </summary>
        public static readonly IReadOnlyList<string> NonRoundTrippedProperties = new[]
        {
            "Id",
            "Pid",
            "UserAccount",
            "Password",
            "RunAsLocalSystem",
            "PreviousStopTimeout",
            "ActiveStdoutPath",
            "ActiveStderrPath",
            "RestartAttempts",
            "RestartAttemptsUpdatedAtTicks",
            "DecryptionFailed",
            "LegacyEncryptionBlocked"
        };

        /// <summary>
        /// Asserts that every mapped <see cref="ServiceDto"/> property that is non-null on <paramref name="expected"/>
        /// compares equal on <paramref name="actual"/>, and that enough of them were compared for the assertion to
        /// mean anything.
        /// </summary>
        /// <param name="expected">The instance carrying the fixture values.</param>
        /// <param name="actual">The instance the values must have survived into.</param>
        /// <param name="minComparedRatio">
        /// The share of the mapped properties that must have been compared. A ratio rather than a fixed count, so
        /// the sparsity floor follows <see cref="ServiceDto"/> as it grows instead of rotting at one number.
        /// </param>
        public static void AssertPropertiesSurvived(ServiceDto expected, ServiceDto actual, double minComparedRatio = 0.9)
        {
            AssertExclusionListIsCurrent();

            var properties = TestReflection.GetMappedProperties<ServiceDto>(NonRoundTrippedProperties).ToList();

            var compared = 0;
            foreach (var prop in properties)
            {
                // A null fixture value is skipped to avoid breaking on values hydrated
                // to system defaults during deserialization.
                var expectedValue = prop.GetValue(expected);
                if (expectedValue == null)
                {
                    continue;
                }

                Assert.Equal(expectedValue, prop.GetValue(actual));
                compared++;
            }

            var minCompared = (int)Math.Ceiling(properties.Count * minComparedRatio);

            Assert.True(compared >= minCompared,
                $"Only {compared} of {properties.Count} properties were compared, at least {minCompared} expected; the fixture has gone sparse.");
        }

        /// <summary>
        /// Asserts that <see cref="NonRoundTrippedProperties"/> and <see cref="ServiceDto"/> agree in both
        /// directions. Every entry must still name a property <see cref="ServiceDto"/> actually has: the exclusion
        /// is matched by string in <c>TestReflection.GetMappedProperties</c>, so a renamed or removed property
        /// turns its entry into a silent no-op, and the property rejoins the "must survive the round trip" set
        /// with no diagnostic of any kind. Conversely, every property the serializers drop through
        /// <c>[XmlIgnore]</c> must be listed, so a new runtime-state property cannot be added without deciding here
        /// that it does not round trip.
        /// </summary>
        private static void AssertExclusionListIsCurrent()
        {
            // Arrange
            var dtoProperties = typeof(ServiceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var liveNames = dtoProperties.Select(p => p.Name).ToList();

            // Act
            var stale = NonRoundTrippedProperties.Where(name => !liveNames.Contains(name)).ToList();
            var unlisted = dtoProperties
                .Where(p => p.GetCustomAttribute<XmlIgnoreAttribute>() != null
                            && !NonRoundTrippedProperties.Contains(p.Name))
                .Select(p => p.Name)
                .ToList();

            // Assert
            Assert.True(stale.Count == 0,
                $"NonRoundTrippedProperties names {stale.Count} entr{(stale.Count == 1 ? "y" : "ies")} ServiceDto no longer has "
                + $"(renamed or removed?): {string.Join(", ", stale)}. The exclusion is matched by name, so a stale "
                + "entry stops excluding anything instead of failing.");
            Assert.True(unlisted.Count == 0,
                $"ServiceDto has [XmlIgnore] properties missing from NonRoundTrippedProperties: {string.Join(", ", unlisted)}. "
                + "Decide here whether they round trip.");
        }
    }
}
