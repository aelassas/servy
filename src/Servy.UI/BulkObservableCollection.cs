using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

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
    /// on the UI thread. The <c>_suppressNotification</c> field is marked volatile to ensure 
    /// visibility across threads if background updates are performed.
    /// </para>
    /// <para>
    /// Performance: This class optimizes range removals and additions by suppressing UI 
    /// updates until the entire operation is complete, followed by a single <see cref="NotifyCollectionChangedAction.Reset"/> notification.
    /// </para>
    /// </remarks>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Flag to indicate whether collection changed notifications should be suppressed.
        /// Marked volatile to ensure cross-thread visibility of the suppression state.
        /// </summary>
        private volatile bool _suppressNotification = false;

        /// <summary>
        /// Adds a collection of items to the end of the <see cref="BulkObservableCollection{T}"/>.
        /// </summary>
        /// <param name="items">The collection of items to add. If null, no action is taken.</param>
        /// <remarks>
        /// This method suppresses notifications for each individual addition and raises a 
        /// single <see cref="NotifyCollectionChangedAction.Reset"/> event upon completion.
        /// </remarks>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) return;

            _suppressNotification = true;

            try
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Raises the <see cref="ObservableCollection{T}.CollectionChanged"/> event.
        /// </summary>
        /// <param name="e">Arguments of the event being raised.</param>
        /// <remarks>
        /// If <c>_suppressNotification</c> is <see langword="true"/>, the event is swallowed 
        /// to prevent UI overhead during bulk operations.
        /// </remarks>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }

        /// <summary>
        /// Trims the collection to the specified maximum size by removing items from the beginning.
        /// Uses an optimized range removal to avoid O(n²) performance degradation.
        /// </summary>
        /// <param name="maxItems">The maximum number of items allowed in the collection.</param>
        /// <remarks>
        /// In .NET Framework 4.8, the internal <see cref="ObservableCollection{T}.Items"/> collection is a 
        /// <see cref="List{T}"/>. This method uses <see cref="List{T}.RemoveRange"/> for O(n) performance 
        /// instead of O(n²) for multiple <see cref="Collection{T}.RemoveAt"/> calls.
        /// </remarks>
        public void TrimToSize(int maxItems)
        {
            int removeCount = Items.Count - maxItems;
            if (removeCount <= 0) return;

            _suppressNotification = true;

            try
            {
                // In .NET Framework 4.8, the internal Items collection is a List<T>.
                // Using RemoveRange is O(n) instead of O(n^2) for multiple RemoveAt(0) calls.
                if (Items is List<T> list)
                {
                    list.RemoveRange(0, removeCount);
                }
                else
                {
                    // Fallback for non-List implementations, though ObservableCollection uses List by default
                    for (int i = 0; i < removeCount; i++)
                    {
                        Items.RemoveAt(0);
                    }
                }
            }
            finally
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}