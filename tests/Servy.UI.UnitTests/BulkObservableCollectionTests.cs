using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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

        #endregion
    }
}