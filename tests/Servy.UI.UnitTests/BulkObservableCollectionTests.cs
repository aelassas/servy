using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace Servy.UI.UnitTests
{
    public class BulkObservableCollectionTests
    {
        #region AddRange Tests

        [Fact]
        public void AddRange_NullItems_ReturnsImmediately()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            bool eventRaised = false;
            collection.CollectionChanged += (s, e) => eventRaised = true;

            // Act
            collection.AddRange(null);

            // Assert
            Assert.Empty(collection);
            Assert.False(eventRaised);
        }

        [Fact]
        public void AddRange_ValidItems_SuppressesIndividualEventsAndRaisesReset()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            int collectionChangedCount = 0;
            NotifyCollectionChangedAction? lastAction = null;
            var changedProperties = new List<string>();

            collection.CollectionChanged += (s, e) =>
            {
                collectionChangedCount++;
                lastAction = e.Action;
            };
            ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null) changedProperties.Add(e.PropertyName);
            };

            var itemsToAdd = new[] { 1, 2, 3 };

            // Act
            collection.AddRange(itemsToAdd);

            // Assert
            Assert.Equal(3, collection.Count);
            Assert.Equal(1, collectionChangedCount); // Only one event raised
            Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
            Assert.Contains("Count", changedProperties);
            Assert.Contains("Item[]", changedProperties);
        }

        #endregion

        #region OnCollectionChanged Suppression Tests

        [Fact]
        public void StandardAdd_NoSuppression_RaisesIndividualEvent()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            NotifyCollectionChangedAction? lastAction = null;
            collection.CollectionChanged += (s, e) => lastAction = e.Action;

            // Act
            collection.Add(1);

            // Assert
            Assert.Equal(NotifyCollectionChangedAction.Add, lastAction);
        }

        #endregion

        #region TrimToSize Tests

        [Theory]
        [InlineData(5, 5)] // Count == maxItems
        [InlineData(5, 10)] // Count < maxItems
        public void TrimToSize_NoRemovalNeeded_ReturnsImmediately(int initialCount, int maxItems)
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            for (int i = 0; i < initialCount; i++) collection.Add(i);

            bool eventRaised = false;
            collection.CollectionChanged += (s, e) => eventRaised = true;

            // Act
            collection.TrimToSize(maxItems);

            // Assert
            Assert.Equal(initialCount, collection.Count);
            Assert.False(eventRaised);
        }

        [Fact]
        public void TrimToSize_ListImplementation_UsesRemoveRange()
        {
            // Arrange
            // Default constructor uses List<T> internally
            var collection = new BulkObservableCollection<int>();
            for (int i = 0; i < 10; i++) collection.Add(i);

            int collectionChangedCount = 0;
            collection.CollectionChanged += (s, e) => collectionChangedCount++;

            // Act
            collection.TrimToSize(3); // Remove 7 items

            // Assert
            Assert.Equal(3, collection.Count);
            Assert.Equal(7, collection[0]); // Verification: 0-6 removed, 7 is the new first item
            Assert.Equal(1, collectionChangedCount); // Reset event
        }

        [Fact]
        public void TrimToSize_NonListImplementation_UsesManualLoop()
        {
            // Arrange
            // Passing an array forces the internal IList to be an array-wrapper, not List<T>
            var initialData = new int[] { 1, 2, 3, 4, 5 };

            // To force the 'Items is not List<T>' branch, we must ensure the protected 
            // Items property is not a List. However, ObservableCollection<T> wraps 
            // the provided list in a Collection<T>. To hit the fallback, we use 
            // an implementation of IList that is NOT a List<T>.
            var customCollection = new NonListObservableCollection<int>(initialData);

            // Act
            customCollection.TrimToSize(2);

            // Assert
            Assert.Equal(2, customCollection.Count);
            Assert.Equal(4, customCollection[0]);
        }

        #endregion

        /// <summary>
        /// A test-specific subclass to expose protected 'Items' and test the fallback branch.
        /// </summary>
        private class NonListObservableCollection<T> : BulkObservableCollection<T>
        {
            public NonListObservableCollection(IEnumerable<T> items) : base(new ListWrapper<T>(items.ToList())) { }

            // Minimal wrapper that implements IList but is NOT List<T>
            private class ListWrapper<TItem> : IList<TItem>
            {
                private readonly List<TItem> _inner;
                public ListWrapper(List<TItem> inner) => _inner = inner;
                public TItem this[int index] { get => _inner[index]; set => _inner[index] = value; }
                public int Count => _inner.Count;
                public bool IsReadOnly => false;
                public void Add(TItem item) => _inner.Add(item);
                public void Clear() => _inner.Clear();
                public bool Contains(TItem item) => _inner.Contains(item);
                public void CopyTo(TItem[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
                public IEnumerator<TItem> GetEnumerator() => _inner.GetEnumerator();
                public int IndexOf(TItem item) => _inner.IndexOf(item);
                public void Insert(int index, TItem item) => _inner.Insert(index, item);
                public bool Remove(TItem item) => _inner.Remove(item);
                public void RemoveAt(int index) => _inner.RemoveAt(index);
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            }
        }
    }
}