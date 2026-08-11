using Servy.Service.ProcessManagement;
using Servy.Testing;
using System.Diagnostics;
using Xunit;

namespace Servy.Service.UnitTests.ProcessManagement
{
    public class HookTests
    {
        // Helper subclass to access the protected Dispose(bool) method
        private class TestableHook : Hook
        {
            public void CallProtectedDispose(bool disposing) => base.Dispose(disposing);
        }

        /// <summary>
        /// Subclass of <see cref="Process"/> to track whether Dispose(bool) was called.
        /// </summary>
        private sealed class DisposeTrackingProcess : Process
        {
            public bool Disposed { get; private set; }
            public int DisposeCallCount { get; private set; }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Disposed = true;
                    DisposeCallCount++;
                }
                base.Dispose(disposing);
            }
        }

        [Fact]
        public void Properties_SetGet_WorkCorrectly()
        {
            // Arrange
            using (var process = new Process())
            {
                // Act
                var hook = new Hook
                {
                    OperationName = "TestOp",
                    Process = process
                };

                // Assert
                Assert.Equal("TestOp", hook.OperationName);
                Assert.Equal(process, hook.Process);
            }
        }

        [Fact]
        public void Dispose_WhenProcessIsNotNull_DisposesProcess()
        {
            // Arrange
            var hook = new Hook();
            var process = new DisposeTrackingProcess();
            hook.Process = process;

            // Act
            hook.Dispose();

            // Assert
            Assert.True(process.Disposed);
            Assert.Equal(1, process.DisposeCallCount);
        }

        [Fact]
        public void Dispose_WhenProcessIsNull_DoesNotThrow()
        {
            // Arrange
            var hook = new Hook();
            hook.Process = null;

            // Act
            var ex = Record.Exception(() => hook.Dispose());

            // Assert
            Assert.Null(ex);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            // Arrange
            var hook = new Hook();

            // We instantiate a DisposeTrackingProcess to track disposal calls without spawning an OS process.
            var process = new DisposeTrackingProcess();
            hook.Process = process;

            // Verify initial state via reflection
            bool disposedBefore = TestReflection.GetField<bool>(hook, "_disposed");
            Assert.False(disposedBefore);

            // Act
            hook.Dispose();

            // Assert - First call should dispose process and set _disposed to true
            Assert.True(process.Disposed);
            Assert.Equal(1, process.DisposeCallCount);
            bool disposedAfterFirst = TestReflection.GetField<bool>(hook, "_disposed");
            Assert.True(disposedAfterFirst);

            // Act
            var ex = Record.Exception(hook.Dispose);

            // Assert - Second call should hit 'if (_disposed) return;' and return safely without re-disposing
            Assert.Null(ex);
            Assert.Equal(1, process.DisposeCallCount);
        }

        [Fact]
        public void ProtectedDispose_WithFalse_DoesNotAttemptToDisposeProcess()
        {
            // Arrange
            var hook = new TestableHook();
            using (var process = new DisposeTrackingProcess())
            {
                hook.Process = process;

                // Act - finalizer path: managed members must not be touched
                hook.CallProtectedDispose(false);

                // Assert - the Process is still usable, i.e. it was NOT disposed
                Assert.False(process.Disposed);
                var ex = Record.Exception(() => _ = process.StartInfo);
                Assert.Null(ex);
                Assert.Same(process, hook.Process);
            }
        }
    }
}
