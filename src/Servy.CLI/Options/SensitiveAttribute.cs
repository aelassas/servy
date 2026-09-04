namespace Servy.CLI.Options
{
    /// <summary>
    /// Marks a command-line option whose value is sensitive (a password, environment
    /// variables, or arbitrary process parameters).
    /// </summary>
    /// <remarks>
    /// The attribute has no runtime effect. No production code reads it; it is a
    /// compile-time marker that anchors a contract enforced by two tests:
    /// <list type="bullet">
    /// <item><description><c>SensitiveOptionsTests.SensitiveProperties_MustHaveSensitiveAttribute</c>
    /// (unit) fails when an option property whose name looks sensitive is not marked.</description></item>
    /// <item><description><c>SensitiveOptionsTests.SensitiveOptions_MustBeListedInServyPsm1</c>
    /// (integration) fails when a marked option's long name is missing from the
    /// <c>$sensitiveFields</c> array in <c>Servy.psm1</c>.</description></item>
    /// </list>
    /// The masking itself is done by <c>Format-SecureLogMessage</c> in <c>Servy.psm1</c>,
    /// driven by that array, and independently by <c>ServiceHelper.MaskRawArguments</c>,
    /// which matches on keywords rather than on this attribute.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class SensitiveAttribute : Attribute
    {
    }
}
