using Moq;
using Servy.Core.Native;
using Servy.Core.Services;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Arranges a mocked <see cref="IWindowsServiceApi"/> for the <c>ServiceManager.InstallServiceAsync</c> paths
    /// ("service created" and "already exists, configuration updated") and the <c>UninstallServiceAsync</c> path
    /// of a stopped service, so the test classes that exercise them share one copy of the arrangement instead
    /// of one copy per class.
    /// </summary>
    /// <remarks>
    /// Shared next to <see cref="FakeServiceHandles"/>: a change to what <c>InstallServiceAsync</c> calls on the
    /// Service Control Manager, in either direction, is made here once, and every consumer sees it.
    /// </remarks>
    internal static class ServiceManagerInstallArrange
    {
        /// <summary>
        /// Arranges the Service Control Manager wiring every install path needs: <c>OpenSCManager</c> returns a
        /// valid handle and the three <c>ChangeServiceConfig2</c> overloads succeed.
        /// </summary>
        /// <param name="api">The mocked Windows service API to arrange.</param>
        /// <param name="handles">The factory that owns the fake native handles handed out.</param>
        /// <exception cref="InvalidOperationException">Thrown when a fake handle cannot be constructed.</exception>
        internal static void ScmAvailable(Mock<IWindowsServiceApi> api, FakeServiceHandles handles)
        {
            api.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(handles.Scm(123));
            api.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny)).Returns(true);
            api.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);
            api.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>())).Returns(true);
        }

        /// <summary>
        /// Arranges CreateService to create the service, so the "new service" success path runs.
        /// </summary>
        /// <param name="api">The mocked Windows service API to arrange.</param>
        /// <param name="handles">The factory that owns the fake native handles handed out.</param>
        /// <returns>The handle of the created service.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a fake handle cannot be constructed.</exception>
        internal static SafeServiceHandle ServiceCreated(Mock<IWindowsServiceApi> api, FakeServiceHandles handles)
        {
            var serviceHandle = handles.Service(456);
            api.Setup(x => x.CreateService(
                    It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                    It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(serviceHandle);
            return serviceHandle;
        }

        /// <summary>
        /// Arranges CreateService to fail while the service is listed as installed, so the
        /// "already exists, configuration updated" success path runs.
        /// </summary>
        /// <param name="api">The mocked Windows service API to arrange.</param>
        /// <param name="handles">The factory that owns the fake native handles handed out.</param>
        /// <param name="serviceName">The name the service is listed under and opened by.</param>
        /// <exception cref="InvalidOperationException">Thrown when a fake handle cannot be constructed.</exception>
        internal static void ServiceAlreadyExists(Mock<IWindowsServiceApi> api, FakeServiceHandles handles, string serviceName)
        {
            api.Setup(x => x.CreateService(
                    It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                    It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(handles.Service(0));
            api.Setup(x => x.GetServices())
                .Returns(new List<WindowsServiceInfo> { new WindowsServiceInfo { ServiceName = serviceName } });
            api.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), serviceName, It.IsAny<uint>()))
                .Returns(() => handles.Service(789));
            api.Setup(x => x.ChangeServiceConfig(
                    It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);
        }

        /// <summary>
        /// Arranges an installed, already stopped service so an uninstall reaches the delete.
        /// </summary>
        /// <param name="api">The mocked Windows service API to arrange.</param>
        /// <param name="win32Errors">The mocked Win32 error provider; reports the service as not active.</param>
        /// <param name="handles">The factory that owns the fake native handles handed out.</param>
        /// <param name="serviceName">The name the service is opened by.</param>
        /// <param name="deleteSucceeds">Whether <c>DeleteService</c> reports success.</param>
        /// <returns>The handle of the opened service.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a fake handle cannot be constructed.</exception>
        internal static SafeServiceHandle ServiceInstalledAndStopped(
            Mock<IWindowsServiceApi> api, Mock<IWin32ErrorProvider> win32Errors, FakeServiceHandles handles,
            string serviceName, bool deleteSucceeds = true)
        {
            var serviceHandle = handles.Service(456);
            api.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), serviceName, It.IsAny<uint>())).Returns(serviceHandle);
            api.Setup(x => x.ControlService(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_STATUS>.IsAny)).Returns(false);
            win32Errors.Setup(x => x.GetLastWin32Error()).Returns(Errors.ERROR_SERVICE_NOT_ACTIVE);
            api.Setup(x => x.ChangeServiceConfig(
                    serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), null, null, IntPtr.Zero, null, null, null, null))
                .Returns(true);
            api.Setup(x => x.DeleteService(serviceHandle)).Returns(deleteSucceeds);
            return serviceHandle;
        }
    }
}
