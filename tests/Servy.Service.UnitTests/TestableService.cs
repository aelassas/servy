using Servy.Core;
using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// A testable version of the Service class that allows injecting dependencies.
    /// </summary>
    public class TestableService : Service
    {
        private readonly IServiceHelper _serviceHelper;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestableService"/> class.
        /// </summary>
        /// <param name="serviceHelper">The service helper instance to use.</param>
        /// <param name="logger">The logger (optional).</param>
        public TestableService(IServiceHelper serviceHelper, ILogger logger=null)
        {
            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _logger = logger;
        }

        /// <summary>
        /// Override OnStart to use injected IServiceHelper.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the service.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                var options = _serviceHelper.InitializeStartup(_logger);
                if (options == null)
                {
                    // Can't call base.Stop() directly because it's not virtual,
                    // you can raise an event or set a flag for test assertions if needed
                    // Or expose a public method to simulate stopping behavior in tests.
                    return;
                }

                _serviceHelper.EnsureValidWorkingDirectory(options, _logger);

                // You may want to call base.OnStart(args) here, or
                // replicate logic from your original OnStart method if needed,
                // possibly exposing protected methods for testability.

                // For example, you might expose some protected methods in the base Service
                // and call them here to reuse code in the testable class.
            }
            catch (Exception ex)
            {
                _logger?.Error($"Exception in OnStart: {ex.Message}");
                // Stop logic or raise an event for testing
            }
        }
    }
}
