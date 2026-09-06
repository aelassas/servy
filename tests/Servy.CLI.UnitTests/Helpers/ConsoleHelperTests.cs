using Servy.CLI.Helpers;
using Servy.Testing;

namespace Servy.CLI.UnitTests.Helpers
{
    [Collection("SequentialConsoleTests")]
    public class ConsoleHelperTests
    {
        private const string RedirectedOverrideFieldName = "_isOutputRedirectedOverride";

        [Fact]
        public async Task RunWithLoadingAnimation_NullAction()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => ConsoleHelper.RunWithLoadingAnimation(null!));
        }

        /// <summary>
        /// Covers the branch where Console.IsOutputRedirected is true.
        /// The loading animation task should exit early, and the safe line clearing block
        /// should skip the window calculations, avoiding any structural output.
        /// </summary>
        [Fact]
        public async Task RunWithLoadingAnimation_WhenOutputIsRedirected_ExecutesActionWithoutAnimation()
        {
            // Arrange
            var actionExecuted = false;
            // Keep the action silent to avoid polluting the StringWriter
            Func<Task> dummyAction = () =>
            {
                actionExecuted = true;
                return Task.CompletedTask;
            };

            // Force the redirection state to true via centralized reflection utilities
            TestReflection.SetFieldStatic(typeof(ConsoleHelper), RedirectedOverrideFieldName, true);

            try
            {
                using (var sw = new StringWriter())
                {
                    var originalOut = Console.Out;
                    Console.SetOut(sw);

                    try
                    {
                        // Act
                        await ConsoleHelper.RunWithLoadingAnimation(dummyAction, "Testing Redirected...");
                    }
                    finally
                    {
                        Console.SetOut(originalOut);
                    }

                    // Assert
                    Assert.True(actionExecuted);

                    // The redirected path writes nothing at all: the spinner task returns before its
                    // first frame (ConsoleHelper.cs:41) and the clearing block is skipped
                    // (ConsoleHelper.cs:74), while the action above is silent by construction.
                    // SequentialConsoleTests is declared with DisableParallelization = true, so no
                    // other console-mutating test runs alongside this one and can bleed into the
                    // writer. Assert the whole capture is empty rather than only that the animation
                    // text is absent: any other output on this path is the regression to catch.
                    Assert.Empty(sw.ToString());
                }
            }
            finally
            {
                // Reset the test seam state to prevent cross-test environment contamination
                TestReflection.SetFieldStatic(typeof(ConsoleHelper), RedirectedOverrideFieldName, null);
            }
        }

        /// <summary>
        /// Covers the branch where the provided action throws an exception.
        /// The finally block must still execute, cancel the background task safely, and propagate the error.
        /// </summary>
        [Fact]
        public async Task RunWithLoadingAnimation_WhenActionThrows_PropagatesException()
        {
            // Arrange
            Func<Task> faultingAction = () => throw new InvalidOperationException("Simulated action failure");

            // Redirect out to ensure environment isolation during testing
            using (var sw = new StringWriter())
            {
                var originalOut = Console.Out;
                Console.SetOut(sw);

                try
                {
                    // Act & Assert
                    var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    {
                        await ConsoleHelper.RunWithLoadingAnimation(faultingAction, "Testing Error...");
                    });

                    Assert.Equal("Simulated action failure", exception.Message);
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        /// <summary>
        /// Covers the non-redirected branch where the animation runs.
        /// Because we cannot reliably toggle Console.IsOutputRedirected back to false if the test runner
        /// environment is already redirected (e.g., CI/CD builds or Test Explorer instances), this test uses
        /// a custom TextWriter wrapper that simulates an IOException on property access to force coverage
        /// of the deepest nested catch block.
        /// </summary>
        [Fact]
        public async Task RunWithLoadingAnimation_WhenLineClearingHitsIOException_ExecutesFallbackNewline()
        {
            // Arrange
            Func<Task> dummyAction = () => Task.Delay(50); // Small delay to let the loop spin if it can

            // Force output redirection to false to ensure the clear-line code path runs
            TestReflection.SetFieldStatic(typeof(ConsoleHelper), RedirectedOverrideFieldName, false);

            try
            {
                // Instantiating a TextWriter that forces an IOException upon attempting to check Console settings
                // or write operations, simulating terminal detachment during cleanup execution.
                using (var faultingWriter = new FaultingStringWriter())
                {
                    var originalOut = Console.Out;
                    Console.SetOut(faultingWriter);

                    try
                    {
                        // Act
                        await ConsoleHelper.RunWithLoadingAnimation(dummyAction, "Testing Fallback...");
                    }
                    finally
                    {
                        Console.SetOut(originalOut);
                    }

                    // Assert
                    // If Console.IsOutputRedirected is false in the runtime context but WindowWidth access drops an IOException,
                    // the catch block handles it by calling Console.WriteLine(), appending the Environment.NewLine sequence.
                    Assert.True(faultingWriter.IsFallbackWriteLineCalled);
                }
            }
            finally
            {
                TestReflection.SetFieldStatic(typeof(ConsoleHelper), RedirectedOverrideFieldName, null);
            }
        }

        /// <summary>
        /// Dedicated TextWriter mock wrapper subclass targeting the final exception mitigation branch.
        /// </summary>
        private class FaultingStringWriter : StringWriter
        {
            public bool IsFallbackWriteLineCalled { get; private set; }

            public override void Write(string? value)
            {
                // Simulate an unexpected programmatic TTY drop on the clearing write only.
                // That write is the one whose IOException reaches the catch at ConsoleHelper.cs:84
                // and produces the fallback newline; the spinner frame at ConsoleHelper.cs:45 also
                // begins with "\r" and contains spaces, but its exception is swallowed at :68 and
                // proves nothing. The clearing write is the only one of the two that also ends
                // with "\r", so match on that and leave the spinner alone.
                if (value != null && value.StartsWith("\r") && value.EndsWith("\r") && value.Contains(" "))
                {
                    throw new IOException("The handle is invalid or screen buffer configuration lost.");
                }
                base.Write(value);
            }

            public override void WriteLine()
            {
                IsFallbackWriteLineCalled = true;
                base.WriteLine();
            }
        }
    }
}
