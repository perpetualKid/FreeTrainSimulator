using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Orts
{
    [TestClass]
    public sealed class AssertWarningsTests
    {
        private readonly TestContext testContext;

        public AssertWarningsTests(TestContext testContext)
        {
            this.testContext = testContext;
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GlobalCleanup_RestoresHostListenersAfterWarningAssertions(bool assertionFails)
        {
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            using (TextWriterTraceListener hostListener = new TextWriterTraceListener(output))
            {
                Trace.Listeners.Add(hostListener);
                try
                {
                    TraceListener[] listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
                    try
                    {
                        AssertWarnings.Expected();
                        AssertWarnings.NotExpected();
                        if (assertionFails)
                        {
                            Assert.ThrowsExactly<AssertFailedException>(() => AssertWarnings.Matching(
                                "expected warning", () => Trace.TraceWarning("different warning")));
                        }
                        else
                        {
                            AssertWarnings.Matching("expected warning", () => Trace.TraceWarning("expected warning"));
                        }

                        Assert.HasCount(1, Trace.Listeners);
                        Assert.IsInstanceOfType<AssertWarnings>(Trace.Listeners[0]);
                    }
                    finally
                    {
                        TraceListenerCleanup.RestoreTraceListeners(testContext);
                    }

                    CollectionAssert.AreEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                    Trace.TraceInformation("trace after warning cleanup");
                    StringAssert.Contains(output.ToString(), "trace after warning cleanup");

                    TraceListenerCleanup.RestoreTraceListeners(testContext);
                    CollectionAssert.AreEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                }
                finally
                {
                    Trace.Listeners.Remove(hostListener);
                }
            }
        }

        [TestMethod]
        public void GlobalCleanup_LeavesListenersUnchangedWithoutWarningMonitoring()
        {
            TraceListener[] listeners = Trace.Listeners.Cast<TraceListener>().ToArray();

            TraceListenerCleanup.RestoreTraceListeners(testContext);

            CollectionAssert.AreEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
        }
    }
}