namespace Servy.Testing
{
    /// <summary>
    /// Process identifiers reserved for tests that need a PID no running process can own.
    /// </summary>
    public static class TestProcessIds
    {
        /// <summary>
        /// A process identifier no process can ever hold.
        /// </summary>
        /// <remarks>
        /// Windows process and thread identifiers are handle-table indices and are therefore always
        /// multiples of four. <c>999999</c> is <c>3 mod 4</c>, so it is invalid no matter how many
        /// processes are running or how far the handle table has grown. Any replacement value must
        /// keep that property: <c>int.MaxValue</c> also works, while a round number such as
        /// <c>1000000</c> is <c>0 mod 4</c> and can legitimately be allocated.
        /// </remarks>
        public const int NeverValid = 999999;
    }
}
