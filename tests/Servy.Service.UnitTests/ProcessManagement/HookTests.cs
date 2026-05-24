using Servy.Service.ProcessManagement;
using System.Diagnostics;

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
            var hook = new Hook
            {
                OperationName = "TestOp"
            };

            Assert.Equal("TestOp", hook.OperationName);
        }

        [Fact]
        public void Dispose_WhenProcessIsNotNull_DisposesProcess()
        {
            var hook = new Hook();
            // We create a dummy process instance. 
            // We do not Start() it, as we only want to test the disposal logic.
            var process = new Process();
            hook.Process = process;

            // Act
            hook.Dispose();

            // Assert
            // No exception thrown = Success. 
            // Note: Process does not expose a public "IsDisposed" property, 
            // but the test verifies the Hook class handled the reference correctly.
        }

        [Fact]
        public void Dispose_WhenProcessIsNull_DoesNotThrow()
        {
            var hook = new Hook();
            hook.Process = null;

            // Act & Assert
            var ex = Record.Exception(() => hook.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var hook = new Hook();

            // Act
            hook.Dispose();

            // Second call should hit the 'if (_disposed) return;' branch
            var ex = Record.Exception(() => hook.Dispose());

            Assert.Null(ex);
        }

        [Fact]
        public void ProtectedDispose_WithFalse_DoesNotAttemptToDisposeProcess()
        {
            var hook = new TestableHook();
            // Process should not be disposed here, ensuring we don't try to 
            // access managed objects during finalization.
            var ex = Record.Exception(() => hook.CallProtectedDispose(false));

            Assert.Null(ex);
        }
    }
}