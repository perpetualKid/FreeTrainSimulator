using System;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class PathWaitOperationCodecTests
    {
        [TestMethod]
        [DataRow(1, WaitOperationType.BasicWait)]
        [DataRow(29999, WaitOperationType.BasicWait)]
        [DataRow(30000, WaitOperationType.WaitUntil)]
        [DataRow(32359, WaitOperationType.WaitUntil)]
        [DataRow(40102, WaitOperationType.Uncouple)]
        [DataRow(50102, WaitOperationType.Uncouple)]
        [DataRow(60001, WaitOperationType.JoinSplit)]
        [DataRow(60002, WaitOperationType.PassRedSignal)]
        [DataRow(60011, WaitOperationType.Horn)]
        [DataRow(60021, WaitOperationType.Horn)]
        public void WhenSupportedRawCodeIsDecodedThenTypedOperationAndCodeAreRetained(int rawCode, WaitOperationType type)
        {
            PathNodeWaitInfo operation = PathWaitOperationCodec.FromMstsWaitTime(rawCode);

            Assert.AreEqual(type, operation.WaitOperation);
            Assert.AreEqual(rawCode, PathWaitOperationCodec.GetMstsWaitCode(operation));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(39999)]
        [DataRow(60000)]
        [DataRow(60010)]
        [DataRow(60022)]
        public void WhenUnsupportedRawCodeIsDecodedThenItRemainsAnUnknownPreservableValue(int rawCode)
        {
            PathNodeWaitInfo operation = PathWaitOperationCodec.FromMstsWaitTime(rawCode);

            Assert.AreEqual(WaitOperationType.Unknown, operation.WaitOperation);
            Assert.AreEqual(rawCode, operation.UnknownCode);
            Assert.AreEqual(rawCode, PathWaitOperationCodec.GetMstsWaitCode(operation));
        }

        [TestMethod]
        public void WhenAdvancedOperationIsCreatedThenParametersEncodeTheCurrentRuntimeContract()
        {
            Assert.IsTrue(PathWaitOperationCodec.TryCreateWaitUntil(23, 59, out PathNodeWaitInfo until));
            Assert.AreEqual((23, 59, 32359), (until.TimeOfDay.Hours, until.TimeOfDay.Minutes,
                PathWaitOperationCodec.GetMstsWaitCode(until)));
            Assert.IsTrue(PathWaitOperationCodec.TryCreateUncouple(4, 12, true, out PathNodeWaitInfo uncouple));
            Assert.AreEqual((4, 12, true, 50412),
                (uncouple.CarCount, (int)uncouple.Duration.TotalSeconds, uncouple.KeepRear,
                    PathWaitOperationCodec.GetMstsWaitCode(uncouple)));
            Assert.AreEqual(60001, PathWaitOperationCodec.GetMstsWaitCode(PathWaitOperationCodec.JoinSplit()));
            Assert.AreEqual(60002, PathWaitOperationCodec.GetMstsWaitCode(PathWaitOperationCodec.PassRedSignal()));
            Assert.IsTrue(PathWaitOperationCodec.TryCreateHorn(11, out PathNodeWaitInfo horn));
            Assert.AreEqual((11, 60021), ((int)horn.Duration.TotalSeconds, PathWaitOperationCodec.GetMstsWaitCode(horn)));
        }

        [TestMethod]
        public void WhenExplicitTypedFieldsAreInvalidThenProjectionIsRefused()
        {
            PathNodeWaitInfo invalidBasic = new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.BasicWait,
                Duration = TimeSpan.Zero,
            };
            PathNodeWaitInfo overflowingTime = new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.WaitUntil,
                TimeOfDay = TimeSpan.FromHours(25),
            };
            PathNodeWaitInfo partialMinute = new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.WaitUntil,
                TimeOfDay = TimeSpan.FromTicks(1),
            };

            Assert.AreEqual((0, 0, 0), (PathWaitOperationCodec.GetMstsWaitCode(invalidBasic),
                PathWaitOperationCodec.GetMstsWaitCode(overflowingTime), PathWaitOperationCodec.GetMstsWaitCode(partialMinute)));
        }

        [TestMethod]
        public void WhenOperationParametersAreOutsideTheSupportedEncodingThenCreationFails()
        {
            Assert.IsFalse(PathWaitOperationCodec.TryCreateBasicWait(0, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateBasicWait(30000, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateWaitUntil(24, 0, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateWaitUntil(12, 60, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateUncouple(0, 1, false, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateUncouple(100, 1, false, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateUncouple(1, 100, false, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateHorn(0, out _));
            Assert.IsFalse(PathWaitOperationCodec.TryCreateHorn(12, out _));
        }
    }
}
