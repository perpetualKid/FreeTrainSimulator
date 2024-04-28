using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Common;

namespace Tests.Orts.Common
{

    [TestClass]
    public class EnumExtensionTests
    {
        #region SimpleIndexedEnum
        private enum SimpleIndexedEnum
        { 
            Null,
            One,
            Two,
            Three,
            Four
        }

        [TestMethod]
        public void SimpleIndexEnumNextTest()
        {
            SimpleIndexedEnum expected = SimpleIndexedEnum.One;

            SimpleIndexedEnum result = expected.Next();
            Assert.AreEqual(SimpleIndexedEnum.Two, result);
        }

        [TestMethod]
        public void SimpleIndexEnumDoubleNextTest()
        {
            SimpleIndexedEnum expected = SimpleIndexedEnum.One;

            SimpleIndexedEnum result = expected.Next().Next();
            Assert.AreEqual(SimpleIndexedEnum.Three, result);
        }

        [TestMethod]
        public void SimpleIndexEnumCarryOverForwardTest()
        {
            SimpleIndexedEnum expected = SimpleIndexedEnum.Four;

            SimpleIndexedEnum result = expected.Next();
            Assert.AreEqual(SimpleIndexedEnum.Null, result);
        }

        [TestMethod]
        public void SimpleIndexEnumPreviousTest()
        {
            SimpleIndexedEnum expected = SimpleIndexedEnum.Three;

            SimpleIndexedEnum result = expected.Previous();
            Assert.AreEqual(SimpleIndexedEnum.Two, result);
        }

        [TestMethod]
        public void SimpleIndexEnumDoublePreviousTest()
        {
            SimpleIndexedEnum expected = SimpleIndexedEnum.Three;

            SimpleIndexedEnum result = expected.Previous().Previous();
            Assert.AreEqual(SimpleIndexedEnum.One, result);
        }

        [TestMethod]
        public void SimpleIndexEnumCarryOverBackwardTest()
        {
            SimpleIndexedEnum expected = SimpleIndexedEnum.Null;

            SimpleIndexedEnum result = expected.Previous();
            Assert.AreEqual(SimpleIndexedEnum.Four, result);
        }

        [TestMethod]
        public void SimpleIndexEnumMinTest()
        {
            SimpleIndexedEnum result = EnumExtension.Min<SimpleIndexedEnum>();
            Assert.AreEqual(SimpleIndexedEnum.Null, result);
        }

        [TestMethod]
        public void SimpleIndexEnumMaxTest()
        {
            SimpleIndexedEnum result = EnumExtension.Max<SimpleIndexedEnum>();
            Assert.AreEqual(SimpleIndexedEnum.Four, result);
        }
        #endregion

        #region SimpleIndexedEnumPositiveOffset
        private enum SimpleIndexedEnumPositiveOffset
        {
            Null = 2,
            One,
            Two,
            Three,
            Four
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetNextTest()
        {
            SimpleIndexedEnumPositiveOffset expected = SimpleIndexedEnumPositiveOffset.One;

            SimpleIndexedEnumPositiveOffset result = expected.Next();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetDoubleNextTest()
        {
            SimpleIndexedEnumPositiveOffset expected = SimpleIndexedEnumPositiveOffset.One;

            SimpleIndexedEnumPositiveOffset result = expected.Next().Next();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Three, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetCarryOverForwardTest()
        {
            SimpleIndexedEnumPositiveOffset expected = SimpleIndexedEnumPositiveOffset.Four;

            SimpleIndexedEnumPositiveOffset result = expected.Next();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Null, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetPreviousTest()
        {
            SimpleIndexedEnumPositiveOffset expected = SimpleIndexedEnumPositiveOffset.Three;

            SimpleIndexedEnumPositiveOffset result = expected.Previous();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetDoublePreviousTest()
        {
            SimpleIndexedEnumPositiveOffset expected = SimpleIndexedEnumPositiveOffset.Three;

            SimpleIndexedEnumPositiveOffset result = expected.Previous().Previous();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.One, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetCarryOverBackwardTest()
        {
            SimpleIndexedEnumPositiveOffset expected = SimpleIndexedEnumPositiveOffset.Null;

            SimpleIndexedEnumPositiveOffset result = expected.Previous();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Four, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetMinTest()
        {
            SimpleIndexedEnumPositiveOffset result = EnumExtension.Min<SimpleIndexedEnumPositiveOffset>();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Null, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetMaxTest()
        {
            SimpleIndexedEnumPositiveOffset result = EnumExtension.Max<SimpleIndexedEnumPositiveOffset>();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Four, result);
        }
        #endregion

        #region SimpleIndexedEnumNegativeOffset
        private enum SimpleIndexedEnumNegativeOffset
        {
            Null = -2,
            One,
            Two,
            Three,
            Four
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetNextTest()
        {
            SimpleIndexedEnumNegativeOffset expected = SimpleIndexedEnumNegativeOffset.One;

            SimpleIndexedEnumNegativeOffset result = expected.Next();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetDoubleNextTest()
        {
            SimpleIndexedEnumNegativeOffset expected = SimpleIndexedEnumNegativeOffset.One;

            SimpleIndexedEnumNegativeOffset result = expected.Next().Next();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Three, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetCarryOverForwardTest()
        {
            SimpleIndexedEnumNegativeOffset expected = SimpleIndexedEnumNegativeOffset.Four;

            SimpleIndexedEnumNegativeOffset result = expected.Next();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Null, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetPreviousTest()
        {
            SimpleIndexedEnumNegativeOffset expected = SimpleIndexedEnumNegativeOffset.Four;

            SimpleIndexedEnumNegativeOffset result = expected.Previous();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Three, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetDoublePreviousTest()
        {
            SimpleIndexedEnumNegativeOffset expected = SimpleIndexedEnumNegativeOffset.Four;

            SimpleIndexedEnumNegativeOffset result = expected.Previous().Previous();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetCarryOverBackwardTest()
        {
            SimpleIndexedEnumNegativeOffset expected = SimpleIndexedEnumNegativeOffset.Null;

            SimpleIndexedEnumNegativeOffset result = expected.Previous();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Four, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetMinTest()
        {
            SimpleIndexedEnumNegativeOffset result = EnumExtension.Min<SimpleIndexedEnumNegativeOffset>();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Null, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetMaxTest()
        {
            SimpleIndexedEnumNegativeOffset result = EnumExtension.Max<SimpleIndexedEnumNegativeOffset>();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Four, result);
        }

        #endregion

        #region SparseEnumNegativeOffset
        private enum SparseEnumNegativeOffset
        {
            Null = -20,
            One = -10,
            Two = -5,
            Three = 10,
            Four = 20
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetNextTest()
        {
            SparseEnumNegativeOffset expected = SparseEnumNegativeOffset.One;

            SparseEnumNegativeOffset result = expected.Next();
            Assert.AreEqual(SparseEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetDoubleNextTest()
        {
            SparseEnumNegativeOffset expected = SparseEnumNegativeOffset.One;

            SparseEnumNegativeOffset result = expected.Next().Next();
            Assert.AreEqual(SparseEnumNegativeOffset.Three, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetCarryOverForwardTest()
        {
            SparseEnumNegativeOffset expected = SparseEnumNegativeOffset.Four;

            SparseEnumNegativeOffset result = expected.Next();
            Assert.AreEqual(SparseEnumNegativeOffset.Null, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetPreviousTest()
        {
            SparseEnumNegativeOffset expected = SparseEnumNegativeOffset.Three;

            SparseEnumNegativeOffset result = expected.Previous();
            Assert.AreEqual(SparseEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetDoublePreviousTest()
        {
            SparseEnumNegativeOffset expected = SparseEnumNegativeOffset.Three;

            SparseEnumNegativeOffset result = expected.Previous().Previous();
            Assert.AreEqual(SparseEnumNegativeOffset.One, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetCarryOverBackwardTest()
        {
            SparseEnumNegativeOffset expected = SparseEnumNegativeOffset.Null;

            SparseEnumNegativeOffset result = expected.Previous();
            Assert.AreEqual(SparseEnumNegativeOffset.Four, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetMinTest()
        {
            SparseEnumNegativeOffset result = EnumExtension.Min<SparseEnumNegativeOffset>();
            Assert.AreEqual(SparseEnumNegativeOffset.Null, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetMaxTest()
        {
            SparseEnumNegativeOffset result = EnumExtension.Max<SparseEnumNegativeOffset>();
            Assert.AreEqual(SparseEnumNegativeOffset.Four, result);
        }

        #endregion
    }
}
