// COPYRIGHT 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Orts
{
    public delegate void RunCode();

    /// <summary>
    /// This class can be used to test for Trace.TraceWarning() calls.
    /// Instead of having the warnings go to the output window, they are captured by this class.
    /// This means that if a warning is not expected, a fail will result.
    /// And if you want to test that a warning is given, you can test for that also.
    /// NotExpected and Expected set whether trace events are allowed; Matching checks a specific message.
    /// The assembly's global test cleanup restores the original listeners after each test, even on failure.
    /// </summary>
    internal sealed class AssertWarnings : TraceListener
    {
        private static readonly AssertWarnings listener = new AssertWarnings();
        private static TraceListener[] originalListeners;

        private static void Initialize()
        {
            if (originalListeners == null)
                originalListeners = Trace.Listeners.Cast<TraceListener>().ToArray();

            // Prevent warnings from going to MsTest.
            Trace.Listeners.Clear();
            // We now intercept the trace warnings with our own listener.
            Trace.Listeners.Add(listener);
        }

        /// <summary>
        /// Restores the caller-owned listeners without disposing them and resets warning expectations.
        /// </summary>
        public static void RestoreListeners()
        {
            if (originalListeners != null)
            {
                Trace.Listeners.Clear();
                Trace.Listeners.AddRange(originalListeners);
                originalListeners = null;
            }
            listener.Set(false);
        }

        /// <summary>
        /// Declare that no warnings are expected to be generated during the following test.
        /// </summary>
        public static void NotExpected()
        {
            Initialize();
            listener.Set(false);
        }

        /// <summary>
        /// Declare that warnings are expected to be generated during the following test.
        /// </summary>
        public static void Expected()
        {
            Initialize();
            listener.Set(true);
        }

        /// <summary>
        /// Declare that a specific warning is expected to be generated during the specified code.
        /// </summary>
        /// <param name="pattern">Pattern to match the warning against; if there is no match, the test fails.</param>
        /// <param name="code">Code which is expected to generate a matching warning.</param>
        public static void Matching(string pattern, RunCode code)
        {
            Initialize();
            listener.InternalMatching(pattern, code);
        }

        private bool warningExpected;
        private bool warningOccurred;
        private string lastWarning;

        private AssertWarnings()
        {
        }

        private void Set(bool expected)
        {
            warningExpected = expected;
            warningOccurred = false;
            lastWarning = null;
        }

        private void InternalMatching(string pattern, RunCode callback)
        {
            Set(true);
            lastWarning = null;
            callback.Invoke();
            Assert.IsTrue(warningOccurred, "Expected a warning, but did not get it");
            if (warningOccurred && pattern != null)
            {
                Assert.IsTrue(Regex.IsMatch(lastWarning, pattern), lastWarning + " does not match pattern " + pattern);
            }
            Set(false);
        }

        public override void Write(string message)
        {
            //Not sure what this is needed for exactly, calling a fail until we know something better
            Assert.Fail("Unexpected TraceListener.Write(string) call");
        }

        public override void WriteLine(string message)
        {
            //Not sure what this is needed for exactly, calling a fail until we know something better
            Assert.Fail("Unexpected TraceListener.WriteLine(string) call");
        }

        public override void WriteLine(object o)
        {
            //Not sure what this is needed for exactly, calling a fail until we know something better
            Assert.Fail("Unexpected TraceListener.WriteLine(object) call");
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            lastWarning = "";
            Assert.IsTrue(warningExpected, "Unexpected warning");
            warningOccurred = true;
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            lastWarning = message;
            Assert.IsTrue(warningExpected, "Unexpected warning: " + lastWarning);
            warningOccurred = true;
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            lastWarning = string.Format(CultureInfo.CurrentCulture, format, args);
            Assert.IsTrue(warningExpected, $"Unexpected warning: {lastWarning}");
            warningOccurred = true;
        }
    }
}
