using Servy.Core.Services;

namespace Servy.Core.UnitTests.Services
{
    public class ServiceDependencyNodeTests
    {
        [Fact]
        public void Constructor_ShouldInitializePropertiesCorrectly()
        {
            // Arrange & Act
            var node = new ServiceDependencyNode("wuauserv", "Windows Update", true, true);

            // Assert
            Assert.Equal("wuauserv", node.ServiceName);
            Assert.Equal("Windows Update", node.DisplayName);
            Assert.True(node.IsRunning);
            Assert.True(node.IsCyclic);
            Assert.False(node.IsExpanded); // Verifies the authentic constructor default initialization value
            Assert.NotNull(node.Dependencies);
            Assert.Empty(node.Dependencies);
        }

        [Fact]
        public void IsExpanded_Property_CanBeMutatedSuccessfully()
        {
            // Arrange
            var node = new ServiceDependencyNode("wuauserv", "Windows Update", true, false);

            // Act
            node.IsExpanded = true;

            // Assert
            Assert.True(node.IsExpanded);
        }

        [Fact]
        public void IsExpanded_Set_RaisesPropertyChanged_WhenValueChanges()
        {
            // Arrange
            var node = new ServiceDependencyNode("wuauserv", "Windows Update", true, false);
            string? raisedPropertyName = null;
            node.PropertyChanged += (s, e) => raisedPropertyName = e.PropertyName;
            const string expectedPropName = nameof(ServiceDependencyNode.IsExpanded);

            // Act
            node.IsExpanded = true;

            // Assert
            Assert.Equal(expectedPropName, raisedPropertyName);
        }

        [Fact]
        public void DisplayName_Set_RaisesPropertyChanged_WhenValueChanges()
        {
            // Arrange
            var node = new ServiceDependencyNode("svc", "Display", false);
            string? raisedPropertyName = null;
            node.PropertyChanged += (s, e) => raisedPropertyName = e.PropertyName;
            const string expectedPropName = nameof(ServiceDependencyNode.DisplayName);

            // Act
            node.DisplayName = "New Display Name";

            // Assert
            Assert.Equal(expectedPropName, raisedPropertyName);
        }

        [Fact]
        public void IsRunning_Set_RaisesPropertyChanged_WhenValueChanges()
        {
            // Arrange
            var node = new ServiceDependencyNode("svc", "Display", false);
            string? raisedPropertyName = null;
            node.PropertyChanged += (s, e) => raisedPropertyName = e.PropertyName;
            const string expectedPropName = nameof(ServiceDependencyNode.IsRunning);

            // Act
            node.IsRunning = true;

            // Assert
            Assert.Equal(expectedPropName, raisedPropertyName);
        }

        [Fact]
        public void SetProperty_ShouldNotRaisePropertyChanged_WhenValueIsSame()
        {
            // Arrange
            var node = new ServiceDependencyNode("svc", "Display", true);
            bool wasRaised = false;
            node.PropertyChanged += (s, e) => wasRaised = true;

            // Act
            node.DisplayName = "Display"; // Same as initial
            node.IsRunning = true;        // Same as initial

            // Assert
            Assert.False(wasRaised);
        }

        [Fact]
        public void Dependencies_ShouldAllowAddingItems()
        {
            // Arrange
            var parent = new ServiceDependencyNode("parent", "Parent", true);
            var child = new ServiceDependencyNode("child", "Child", false);

            // Act
            parent.Dependencies.Add(child);

            // Assert
            Assert.Single(parent.Dependencies);
            Assert.Contains(child, parent.Dependencies);
        }
    }
}
