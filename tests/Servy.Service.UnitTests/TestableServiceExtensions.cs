namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Exposes protected OnStart for testing
    /// </summary>
    public static class TestableServiceExtensions
    {
        public static void TestOnStart(this TestableService service, string[] args)
        {
            typeof(TestableService)
                .GetMethod("OnStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(service, [ args ]);
        }
    }
}
