using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: CLSCompliant(false)]
[assembly: DoNotParallelize]

namespace Tests.Orts
{
    [TestClass]
    public sealed class TraceListenerCleanup
    {
        [GlobalTestCleanup]
        public static void RestoreTraceListeners(TestContext _)
        {
            AssertWarnings.RestoreListeners();
        }
    }
}
