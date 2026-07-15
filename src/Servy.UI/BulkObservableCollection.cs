using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Servy.UI
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> extension that supports bulk operations without 
    /// triggering a <see cref="INotifyCollectionChanged.CollectionChanged"/> event for every individual item.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <remarks>
    /// <para>
    /// Thread Safety: To ensure UI consistency in WPF, bulk operations should ideally be performed 
    /// on the UI thread.
    /// </para>
    /// <para>
    /// Performance: This class optimizes range removals and additions by manipulating the internal 
    /// Items collection directly, bypassing per-item virtual method overhead, followed by a single 
    /// <see cref="NotifyCollectionChangedAction.Reset"/> notification.
    /// </para>
    /// </remarks>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BulkObservableCollection{T}"/> class.
        /// </summary>
        public BulkObservableCollection() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="BulkObservableCollection{T}"/> class 
        /// that contains elements copied from the specified collection.
        /// </summary>
        /// <param name="collection">The collection from which the elements are copied.</param>
        public BulkObservableCollection(IEnumerable<T> collection) : base(collection) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="BulkObservableCollection{T}"/> class 
        /// that contains elements copied from the specified list.
        /// </summary>
        /// <param name="list">The list from which the elements are copied.</param>
        public BulkObservableCollection(List<T> list) : base(list) { }

        /// <summary>
        /// Adds a collection of items to the end of the <see cref="BulkObservableCollection{T}"/>.
        /// </summary>
        /// <param name="items">The collection of items to add. If null, no action is taken.</param>
        /// <remarks>
        /// This method mutates the underlying list directly and raises a 
        /// single <see cref="NotifyCollectionChangedAction.Reset"/> event upon completion.
        /// </remarks>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) return;

            bool wasModified = false;

            try
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                    wasModified = true;
                }
            }
            finally
            {
                // Only trigger the reset notification if we actually added items
                if (wasModified)
                {
                    RaiseResetNotifications();
                }
            }
        }

        /// <summary>
        /// Trims the collection to the specified maximum size by removing items from the beginning.
        /// Uses an optimized range removal to avoid O(n²) performance degradation.
        /// </summary>
        /// <param name="maxItems">The maximum number of items allowed in the collection.</param>
        /// <remarks>
        /// On .NET (net10.0-windows), the internal <see cref="ObservableCollection{T}.Items"/> collection
        /// is guaranteed to be a <see cref="List{T}"/>. This method uses <see cref="List{T}.RemoveRange"/> for O(n)
        /// performance instead of O(n²) for repeated <see cref="Collection{T}.RemoveAt"/> calls.
        /// </remarks>
        public void TrimToSize(int maxItems)
        {
            if (maxItems < 0) throw new ArgumentOutOfRangeException(nameof(maxItems), "maxItems must be non-negative.");

            int removeCount = Items.Count - maxItems;
            if (removeCount <= 0) return;

            bool wasModified = false;

            try
            {
                // Items is guaranteed to be List<T> in ObservableCollection's implementation
                if (Items is List<T> list)
                {
                    list.RemoveRange(0, removeCount);
                    wasModified = true;
                }
            }
            finally
            {
                // Only trigger the reset notification if we actually removed items
                if (wasModified)
                {
                    RaiseResetNotifications();
                }
            }
        }

        /// <summary>
        /// Explicitly notifies active data-binding targets and UI listeners that the entire 
        /// collection has been reset, including its item count and indexer state.
        /// </summary>
        /// <remarks>
        /// Required because <see cref="AddRange"/> and <see cref="TrimToSize"/> mutate the protected
        /// <c>Items</c> list directly, which does not raise change notifications. This raises
        /// <c>PropertyChanged</c> for <c>Count</c> and <c>Item[]</c> followed by a single
        /// <see cref="NotifyCollectionChangedAction.Reset"/> so bound WPF controls refresh.
        /// </remarks>
        private void RaiseResetNotifications()
        {
            // Explicitly notify bindings that Count and the indexer have changed,
            // as manual manipulation of the internal 'Items' collection bypasses these.

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}