using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Servy.UI
{
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) return;

            _suppressNotification = true;

            foreach (var item in items)
                Items.Add(item); // Note: base.Items is not necessary if _suppressNotification is handled

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        /// <summary>
        /// Trims the collection to the specified maximum size.
        /// Uses an optimized range removal to avoid O(n^2) performance degradation.
        /// </summary>
        public void TrimToSize(int maxItems)
        {
            int removeCount = Items.Count - maxItems;
            if (removeCount <= 0) return;

            _suppressNotification = true;

            try
            {
                // Cast to List<T> to access RemoveRange, which performs a single memory shift (O(n))
                // rather than shifting the entire list for every single removed item.
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