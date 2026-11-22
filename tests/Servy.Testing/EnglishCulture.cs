using System.Globalization;

namespace Servy.Testing
{
    /// <summary>
    /// Provides the English culture for tests that assert English text or compare formatted numbers, so they pass on a
    /// Windows installed in any language. Each call switches the culture for one delegate and restores it afterwards, so
    /// two tests switching at once cannot leave the culture of the other one pointing at the wrong resx.
    /// </summary>
    public static class EnglishCulture
    {
        /// <summary>The culture applied: the invariant culture, whose resource lookup ends at English.</summary>
        public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Runs <paramref name="action"/> under <see cref="Culture"/> and restores the previous cultures afterwards.
        /// </summary>
        public static T Run<T>(Func<T> action) => WithCulture(action);

        /// <summary>
        /// Runs <paramref name="action"/> under <see cref="Culture"/> and restores the previous cultures afterwards.
        /// </summary>
        public static void Run(Action action) => WithCulture(() => { action(); return true; });

        /// <summary>
        /// Runs <paramref name="action"/> under <see cref="Culture"/> and restores the previous cultures afterwards.
        /// </summary>
        public static async Task<T> RunAsync<T>(Func<Task<T>> action)
        {
            var previous = Capture();
            try
            {
                Apply();
                return await action();
            }
            finally
            {
                Restore(previous);
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> under <see cref="Culture"/> and restores the previous cultures afterwards.
        /// </summary>
        public static async Task RunAsync(Func<Task> action) => await RunAsync(async () => { await action(); return true; });

        /// <summary>
        /// Switches the current thread and the defaults new threads inherit to <see cref="Culture"/>.
        /// </summary>
        public static void Apply()
        {
            CultureInfo.CurrentUICulture = Culture;
            CultureInfo.CurrentCulture = Culture;
            CultureInfo.DefaultThreadCurrentUICulture = Culture;
            CultureInfo.DefaultThreadCurrentCulture = Culture;
        }

        private static T WithCulture<T>(Func<T> action)
        {
            var previous = Capture();
            try
            {
                Apply();
                return action();
            }
            finally
            {
                Restore(previous);
            }
        }

        private static (CultureInfo Ui, CultureInfo Culture, CultureInfo? DefaultUi, CultureInfo? DefaultCulture) Capture()
            => (CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture,
                CultureInfo.DefaultThreadCurrentUICulture, CultureInfo.DefaultThreadCurrentCulture);

        private static void Restore((CultureInfo Ui, CultureInfo Culture, CultureInfo? DefaultUi, CultureInfo? DefaultCulture) previous)
        {
            CultureInfo.CurrentUICulture = previous.Ui;
            CultureInfo.CurrentCulture = previous.Culture;
            CultureInfo.DefaultThreadCurrentUICulture = previous.DefaultUi;
            CultureInfo.DefaultThreadCurrentCulture = previous.DefaultCulture;
        }
    }

}

namespace Servy.Testing
{
    /// <summary>
    /// A serialized collection whose classes compare English text or culture-formatted numbers: they call
    /// <see cref="EnglishCulture"/> around each action and never switch the culture in a constructor, so xUnit's
    /// re-enumeration of a partially filtered collection cannot leak a switch into a sibling class.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class CultureTestCollection
    {
        /// <summary>The collection name to pass to <see cref="CollectionAttribute"/>.</summary>
        public const string Name = "CultureTests";
    }
}

namespace Servy.Testing
{
    /// <summary>
    /// Switches the culture to English for the lifetime of a test class instance. The class must belong to
    /// <see cref="CultureTestCollection"/>, so no other culture-sensitive class runs at the same time.
    /// </summary>
    public sealed class EnglishCultureScope : IDisposable
    {
        private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo? _previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        private readonly CultureInfo? _previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;

        /// <summary>Switches the current thread and the defaults new threads inherit to <see cref="EnglishCulture.Culture"/>.</summary>
        public EnglishCultureScope() => EnglishCulture.Apply();

        /// <summary>Restores the cultures in effect when the scope was created.</summary>
        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _previousUiCulture;
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _previousDefaultUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = _previousDefaultCulture;
        }
    }
}
