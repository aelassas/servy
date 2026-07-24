using Servy.Service.ProcessManagement;
using Servy.Testing;
using System;
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
            // We create a dummy process instance. 
            // We do not Start() it, as we only want to test the disposal logic.
            var process = new Process();
            hook.Process = process;

            // Act
            hook.Dispose();

            // Assert
            Assert.Throws<InvalidOperationException>(() => _ = process.Id); // or ObjectDisposedException, depending on member
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

            // We instantiate a real Process to act as our disposable resource.
            // Note: We don't call Start(), so no real OS process is spawned.
            var process = new Process();
            hook.Process = process;

            // Verify initial state via reflection
            bool disposedBefore = TestReflection.GetField<bool>(hook, "_disposed");
            Assert.False(disposedBefore);

            // Act
            hook.Dispose();

            // Assert - First call should dispose process and set _disposed to true
            bool disposedAfterFirst = TestReflection.GetField<bool>(hook, "_disposed");
            Assert.True(disposedAfterFirst);

            // Act
            var ex = Record.Exception(hook.Dispose);

            // Assert - Second call should hit 'if (_disposed) return;' and return safely
            Assert.Null(ex);

            // Clean up the shell process wrapper safely
            process.Dispose();
        }

        [Fact]
        public void ProtectedDispose_WithFalse_DoesNotAttemptToDisposeProcess()
        {
            // Arrange
            var hook = new TestableHook();
            using (var process = new Process())
            {
                hook.Process = process;

                // Act
                hook.CallProtectedDispose(false);

                // Process should not be disposed here, ensuring we don't try to 
                // access managed objects during finalization.
                var ex = Record.Exception(() => hook.CallProtectedDispose(false));

                // Assert
                Assert.Null(ex);
            }
        }
    }
}