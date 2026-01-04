using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Servy.UI.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels, implementing <see cref="INotifyPropertyChanged"/> 
    /// to support two-way data binding in WPF.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Checks if a property already matches a desired value. Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="field">Reference to a field with both get and set accessors.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners. 
        /// This value is optional and can be provided automatically when invoked from compilers that support <see cref="CallerMemberNameAttribute"/>.</param>
        /// <returns>True if the value was changed, false if the existing value coincided with the desired value.</returns>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.
        /// This value is optional and can be provided automatically when invoked from compilers that support <see cref="CallerMemberNameAttribute"/>.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}