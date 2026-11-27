using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Orts
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
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
        public void GlobalCleanupRestoresHostListenersAfterWarningAssertions(bool assertionFails)
        {
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
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
                                Assert.ThrowsExactly<AssertFailedException>(() => AssertWarnings.Matching("expected warning", () => Trace.TraceWarning("different warning")));
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

                        Assert.AreSequenceEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                        Trace.TraceInformation("trace after warning cleanup");
                        Assert.Contains("trace after warning cleanup", output.ToString());

                        TraceListenerCleanup.RestoreTraceListeners(testContext);
                        Assert.AreSequenceEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                    }
                    finally
                    {
                        Trace.Listeners.Remove(hostListener);
                    }
                }
            }
        }

        [TestMethod]
        public void GlobalCleanupLeavesListenersUnchangedWithoutWarningMonitoring()
        {
            TraceListener[] listeners = Trace.Listeners.Cast<TraceListener>().ToArray();

            TraceListenerCleanup.RestoreTraceListeners(testContext);

            Assert.AreSequenceEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
        }
    }
}