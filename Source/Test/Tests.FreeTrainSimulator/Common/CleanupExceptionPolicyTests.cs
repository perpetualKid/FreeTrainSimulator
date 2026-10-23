using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

using FreeTrainSimulator.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class CleanupExceptionPolicyTests
    {
        [TestMethod]
        [DataRow(typeof(InvalidOperationException), true)]
        [DataRow(typeof(ObjectDisposedException), true)]
        [DataRow(typeof(ArgumentException), true)]
        [DataRow(typeof(IOException), true)]
        [DataRow(typeof(COMException), true)]
        [DataRow(typeof(ExternalException), true)]
        [DataRow(typeof(OperationCanceledException), true)]
        [DataRow(typeof(OutOfMemoryException), false)]
        [DataRow(typeof(StackOverflowException), false)]
        [DataRow(typeof(AccessViolationException), false)]
        [DataRow(typeof(SEHException), false)]
        [DataRow(typeof(FatalException), false)]
        public void CleanupClassifiesManagedAndNoncontinuableFailures(Type exceptionType, bool expected)
        {
            Exception error = (Exception)Activator.CreateInstance(exceptionType, "Release failed.");

            Assert.AreEqual(expected, CleanupExceptionPolicy.CanContinue(error));
        }

        [TestMethod]
        public void ManagedAggregateAllowsIndependentCleanup()
        {
            AggregateException error = new AggregateException(new IOException(), new InvalidOperationException());

            Assert.IsTrue(CleanupExceptionPolicy.CanContinue(error));
        }

        [TestMethod]
        public void WrappedFatalAggregateStopsIndependentCleanup()
        {
            TargetInvocationException error = new TargetInvocationException(
                new AggregateException(new IOException(), new FatalException()));

            Assert.IsFalse(CleanupExceptionPolicy.CanContinue(error));
        }

        [TestMethod]
        [DataRow(typeof(InvalidOperationException), true)]
        [DataRow(typeof(FatalException), false)]
        [Timeout(10000, CooperativeCancellation = true)]
        public void DeepAggregateChainsPreserveCleanupClassification(Type exceptionType, bool expected)
        {
            Exception failure = (Exception)Activator.CreateInstance(exceptionType, "Nested release failure.");
            // Revisiting the first child would require billions of calls for this small chain.
            for (int depth = 0; depth < 32; depth++)
                failure = new AggregateException(failure);

            Assert.AreEqual(expected, CleanupExceptionPolicy.CanContinue(failure));
        }

        [TestMethod]
        public void MissingFailureDoesNotMatchCleanupFilter()
        {
            Assert.IsFalse(CleanupExceptionPolicy.CanContinue(null));
        }
    }
}
