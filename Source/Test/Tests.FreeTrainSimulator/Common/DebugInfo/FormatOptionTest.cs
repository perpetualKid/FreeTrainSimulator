using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.DebugInfo
{
    [TestClass]
    public class FormatOptionTest
    {
        [TestMethod]
        public void BothNullEqualsOpTest()
        {
            FormatOption left = null;
            FormatOption right = null;

#pragma warning disable CA1508 // Avoid dead conditional code
            Assert.IsTrue(left == right);
#pragma warning restore CA1508 // Avoid dead conditional code
        }

        [TestMethod]
        public void BothNullNotEqualsOpTest()
        {
            FormatOption left = null;
            FormatOption right = null;

#pragma warning disable CA1508 // Avoid dead conditional code
            Assert.IsFalse(left != right);
#pragma warning restore CA1508 // Avoid dead conditional code
        }

        [TestMethod]
        public void BothNullEqualsBaseTest()
        {
            FormatOption left = null;
            FormatOption right = null;

#pragma warning disable CA1508 // Avoid dead conditional code
            Assert.IsTrue(Equals(left, right));
#pragma warning restore CA1508 // Avoid dead conditional code
        }

        [TestMethod]
        public void CompareToNullEqualsOpTest()
        {
            FormatOption left = FormatOption.BoldRed;
            FormatOption right = null;

            Assert.IsTrue(left != right);
        }

        [TestMethod]
        public void CompareToNullEqualsTest()
        {
            FormatOption left = FormatOption.BoldRed;
            FormatOption right = null;

            Assert.IsFalse(left.Equals(right));
        }

        [TestMethod]
        public void CompareNotEqualsTest()
        {
            FormatOption left = FormatOption.BoldRed;
            FormatOption right = FormatOption.BoldBlue;

            Assert.IsFalse(left.Equals(right));
        }

        [TestMethod]
        public void CompareNotEqualsOpTest()
        {
            FormatOption left = FormatOption.BoldRed;
            FormatOption right = FormatOption.BoldBlue;

            Assert.IsTrue(left != right);
        }

        [TestMethod]
        public void CompareEqualsOpTrueTest()
        {
            FormatOption left = FormatOption.BoldRed;
            FormatOption right = FormatOption.BoldRed;

            Assert.IsTrue(left == right);
        }

        [TestMethod]
        public void CompareEqualsOpFalseTest()
        {
            FormatOption left = FormatOption.BoldRed;
            FormatOption right = FormatOption.BoldBlue;

            Assert.IsFalse(left == right);
        }

    }
}
