using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Servy.Core.DTOs;
using Servy.Testing;
using Xunit;

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
        /// <c>Id</c> and <c>Pid</c> are assigned outside the payload, <c>UserAccount</c> and <c>Password</c> are
        /// dropped by <c>[XmlIgnore]</c>/<c>[JsonIgnore]</c>, <c>RunAsLocalSystem</c> is reset to the configured
        /// default, and the remaining three are runtime state. Stated once, so adding a property to
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
            "ActiveStderrPath"
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
        /// Asserts that every entry of <see cref="NonRoundTrippedProperties"/> still names a property
        /// <see cref="ServiceDto"/> actually has. The exclusion is matched by string in
        /// <c>TestReflection.GetMappedProperties</c>, so a renamed or removed property turns its entry into a
        /// silent no-op: the property rejoins the "must survive the round trip" set with no diagnostic of any
        /// kind, leaving either a confusing failure attributed to the wrong cause or a green assertion that
        /// checks a field it was deliberately meant to skip.
        /// </summary>
        private static void AssertExclusionListIsCurrent()
        {
            var liveNames = typeof(ServiceDto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToList();

            var stale = NonRoundTrippedProperties.Where(name => !liveNames.Contains(name)).ToList();

            Assert.True(stale.Count == 0,
                $"NonRoundTrippedProperties names {stale.Count} entr{(stale.Count == 1 ? "y" : "ies")} ServiceDto no longer has "
                + $"(renamed or removed?): {string.Join(", ", stale)}. The exclusion is matched by name, so a stale "
                + "entry stops excluding anything instead of failing.");
        }
    }
}
