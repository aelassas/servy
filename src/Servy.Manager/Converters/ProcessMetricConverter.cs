using Microsoft.Extensions.DependencyInjection;
using Servy.Core.Helpers;
using Servy.UI.Constants;
using Servy.UI.Design;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Serves as a base class for process-related value converters to encapsulate shared scaffolding,
    /// such as dependency injection resolution, XAML design-time safe fallbacks, and default one-way binding behaviors.
    /// </summary>
    /// <typeparam name="TValue">The underlying value type expected by the converter (e.g., double, long).</typeparam>
    public abstract class ProcessMetricConverter<TValue> : IValueConverter where TValue : struct
    {
        /// <summary>
        /// The process formatting helper dependency.
        /// </summary>
        protected readonly IProcessHelper ProcessHelper;

        /// <summary>
        /// Metric fallback indicator string when data is not available.
        /// </summary>
        protected static readonly string UnknownMetricUsage = UiConstants.NotAvailable;

        /// <summary>
        /// Initializes a new instance of the ProcessMetricConverter base class.
        /// Provides design-time support to prevent XAML designer crashes when DI is not initialized.
        /// </summary>
        protected ProcessMetricConverter()
        {
            // Check for design mode before accessing App.Services
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                ProcessHelper = new DesignTimeProcessHelper();
                return;
            }

            ProcessHelper = App.Services?.GetService<IProcessHelper>() ?? new DesignTimeProcessHelper();
        }

        /// <summary>
        /// Formats the typed value into its specialized metric string depiction.
        /// </summary>
        /// <param name="value">The strongly typed unboxed value extracted from the binding engine.</param>
        /// <returns>A human-readable formatted string representing the system metric.</returns>
        protected abstract string Format(TValue value);

        /// <summary>
        /// Evaluates a numeric process metric value into a formatted string for display in the Manager UI.
        /// </summary>
        /// <param name="value">The process usage value, typically a unboxed value type or its nullable equivalent.</param>
        /// <param name="targetType">The type of the binding target property; typically <see cref="string"/>.</param>
        /// <param name="parameter">Optional converter parameter; not used in this implementation.</param>
        /// <param name="culture">The culture to use in the converter; uses the system's current UI culture.</param>
        /// <returns>
        /// A string representing the formatted system metric produced by specialized subclasses, 
        /// or the <see cref="UnknownMetricUsage"/> placeholder if the value is null or an incompatible type.
        /// </returns>
        /// <remarks>
        /// This implementation utilizes C# pattern matching to provide a "fail-silent" guard.
        /// If the bound property is a null nullable structure, the pattern match fails, 
        /// safely returning the unknown placeholder instead of throwing a <see cref="NullReferenceException"/>.
        /// </remarks>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Pattern matching handles the null check and unboxing in one go.
            // If the bound property is nullable and is null, the pattern match fails
            // and we fall through to the UnknownMetricUsage placeholder.
            if (value is TValue typedUsage)
            {
                return Format(typedUsage);
            }

            return UnknownMetricUsage;
        }

        /// <summary>
        /// Not implemented (one-way binding only).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}