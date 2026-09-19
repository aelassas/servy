using System.Collections.Specialized;
using System.ComponentModel;

namespace Servy.UI.UnitTests
{
    public class BulkObservableCollectionTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_FromIEnumerable_SeedsCollection()
        {
            // Arrange
            IEnumerable<int> source = new[] { 1, 2, 3 };

            // Act
            var collection = new BulkObservableCollection<int>(source);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, collection);
        }

        [Fact]
        public void Constructor_FromList_SeedsCollection()
        {
            // Arrange: the static type is List<int>, so overload resolution selects the
            // List<T> constructor rather than the IEnumerable<T> one above.
            var source = new List<int> { 4, 5, 6 };

            // Act
            var collection = new BulkObservableCollection<int>(source);

            // Assert
            Assert.Equal(new[] { 4, 5, 6 }, collection);
        }

        #endregion

        #region AddRange Tests

        [Fact]
        public void AddRange_NullItems_ReturnsImmediately()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            bool eventRaised = false;
            collection.CollectionChanged += (s, e) => eventRaised = true;

            // Act
            collection.AddRange(null!);

            // Assert
            Assert.Empty(collection);
            Assert.False(eventRaised);
        }

        [Fact]
        public void AddRange_ValidItems_RaisesSingleResetInsteadOfPerItemEvents()
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

        [Fact]
        public void AddRange_EmptyItems_RaisesNoNotifications()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            bool eventRaised = false;
            collection.CollectionChanged += (s, e) => eventRaised = true;

            // Act
            collection.AddRange(Enumerable.Empty<int>());

            // Assert
            Assert.Empty(collection);
            Assert.False(eventRaised);
        }

        [Fact]
        public void AddRange_SequenceThrowsMidEnumeration_PublishesItemsAddedSoFar()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            int collectionChangedCount = 0;
            NotifyCollectionChangedAction? lastAction = null;

            collection.CollectionChanged += (s, e) =>
            {
                collectionChangedCount++;
                lastAction = e.Action;
            };

            IEnumerable<int> Faulty()
            {
                yield return 1;
                yield return 2;
                throw new InvalidOperationException("source failed");
            }

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => collection.AddRange(Faulty()));

            Assert.Equal(2, collection.Count);
            Assert.Equal(1, collectionChangedCount);
            Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
        }

        [Fact]
        public void AddRange_SequenceThrowsAtStart_RaisesNoNotifications()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            bool eventRaised = false;
            collection.CollectionChanged += (s, e) => eventRaised = true;

            IEnumerable<int> FaultyAtStart()
            {
                if (collection != null)
                {
                    throw new InvalidOperationException("source failed immediately");
                }

                yield break;
            }

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => collection.AddRange(FaultyAtStart()));

            Assert.Empty(collection);
            Assert.False(eventRaised);
        }

        #endregion

        #region Per-Item Event Tests

        [Fact]
        public void StandardAdd_RaisesIndividualAddEvent()
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
        public void TrimToSize_CountAboveMax_RemovesOldestItemsAndRaisesSingleReset()
        {
            // Arrange
            var collection = new BulkObservableCollection<int>();
            for (int i = 0; i < 10; i++) collection.Add(i);

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

            // Act
            collection.TrimToSize(3); // Remove 7 items

            // Assert
            Assert.Equal(3, collection.Count);
            Assert.Equal(7, collection[0]); // Verification: 0-6 removed, 7 is the new first item
            Assert.Equal(1, collectionChangedCount); // Only one event raised
            Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
            Assert.Contains("Count", changedProperties);
            Assert.Contains("Item[]", changedProperties);
        }

        [Fact]
        public void TrimToSize_NegativeMaxItems_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var collection = new BulkObservableCollection<int> { 1 };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.TrimToSize(-1));
        }

        [Fact]
        public void TrimToSize_ZeroMaxItems_RemovesAllItems()
        {
            // Arrange
            var collection = new BulkObservableCollection<int> { 1, 2, 3 };

            // Act
            collection.TrimToSize(0);

            // Assert
            Assert.Empty(collection);
        }

        #endregion

        #region Reentrancy Tests

        [Fact]
        public void AddRange_ReentrantAddRangeDuringResetNotification_ThrowsInvalidOperationException()
        {
            // Arrange: ObservableCollection<T>.CheckReentrancy() only throws while a change
            // notification is being raised AND the event has more than one subscriber, so a
            // second handler is required. The reentrant handler mutates only on its first
            // invocation, so that the collection settles instead of recursing without bound
            // if the guard is ever removed.
            var collection = new BulkObservableCollection<int>();
            var reentered = false;
            collection.CollectionChanged += (s, e) => { };
            collection.CollectionChanged += (s, e) =>
            {
                if (reentered) return;
                reentered = true;
                collection.AddRange(new[] { 99 });
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => collection.AddRange(new[] { 1, 2 }));
        }

        [Fact]
        public void TrimToSize_ReentrantTrimToSizeDuringResetNotification_ThrowsInvalidOperationException()
        {
            // Arrange: see the AddRange sibling above for why two handlers and the one-shot
            // flag are both needed.
            var collection = new BulkObservableCollection<int> { 1, 2, 3, 4, 5 };
            var reentered = false;
            collection.CollectionChanged += (s, e) => { };
            collection.CollectionChanged += (s, e) =>
            {
                if (reentered) return;
                reentered = true;
                collection.TrimToSize(0);
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => collection.TrimToSize(2));
        }

        #endregion
    }
}
