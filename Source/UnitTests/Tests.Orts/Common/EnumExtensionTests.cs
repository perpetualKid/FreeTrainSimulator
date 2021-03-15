using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            SimpleIndexedEnum simpleIndexedEnum = SimpleIndexedEnum.One;

            SimpleIndexedEnum result = simpleIndexedEnum.Next();
            Assert.AreEqual(SimpleIndexedEnum.Two, result);
        }

        [TestMethod]
        public void SimpleIndexEnumDoubleNextTest()
        {
            SimpleIndexedEnum simpleIndexedEnum = SimpleIndexedEnum.One;

            SimpleIndexedEnum result = simpleIndexedEnum.Next().Next();
            Assert.AreEqual(SimpleIndexedEnum.Three, result);
        }

        [TestMethod]
        public void SimpleIndexEnumCarryOverForwardTest()
        {
            SimpleIndexedEnum simpleIndexedEnum = SimpleIndexedEnum.Four;

            SimpleIndexedEnum result = simpleIndexedEnum.Next();
            Assert.AreEqual(SimpleIndexedEnum.Null, result);
        }

        [TestMethod]
        public void SimpleIndexEnumPreviousTest()
        {
            SimpleIndexedEnum simpleIndexedEnum = SimpleIndexedEnum.Three;

            SimpleIndexedEnum result = simpleIndexedEnum.Previous();
            Assert.AreEqual(SimpleIndexedEnum.Two, result);
        }

        [TestMethod]
        public void SimpleIndexEnumDoublePreviousTest()
        {
            SimpleIndexedEnum simpleIndexedEnum = SimpleIndexedEnum.Three;

            SimpleIndexedEnum result = simpleIndexedEnum.Previous().Previous();
            Assert.AreEqual(SimpleIndexedEnum.One, result);
        }

        [TestMethod]
        public void SimpleIndexEnumCarryOverBackwardTest()
        {
            SimpleIndexedEnum simpleIndexedEnum = SimpleIndexedEnum.Null;

            SimpleIndexedEnum result = simpleIndexedEnum.Previous();
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
            SimpleIndexedEnumPositiveOffset simpleIndexedEnum = SimpleIndexedEnumPositiveOffset.One;

            SimpleIndexedEnumPositiveOffset result = simpleIndexedEnum.Next();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetDoubleNextTest()
        {
            SimpleIndexedEnumPositiveOffset simpleIndexedEnum = SimpleIndexedEnumPositiveOffset.One;

            SimpleIndexedEnumPositiveOffset result = simpleIndexedEnum.Next().Next();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Three, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetCarryOverForwardTest()
        {
            SimpleIndexedEnumPositiveOffset simpleIndexedEnum = SimpleIndexedEnumPositiveOffset.Four;

            SimpleIndexedEnumPositiveOffset result = simpleIndexedEnum.Next();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Null, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetPreviousTest()
        {
            SimpleIndexedEnumPositiveOffset simpleIndexedEnum = SimpleIndexedEnumPositiveOffset.Three;

            SimpleIndexedEnumPositiveOffset result = simpleIndexedEnum.Previous();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetDoublePreviousTest()
        {
            SimpleIndexedEnumPositiveOffset simpleIndexedEnum = SimpleIndexedEnumPositiveOffset.Three;

            SimpleIndexedEnumPositiveOffset result = simpleIndexedEnum.Previous().Previous();
            Assert.AreEqual(SimpleIndexedEnumPositiveOffset.One, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumPositiveOffsetCarryOverBackwardTest()
        {
            SimpleIndexedEnumPositiveOffset simpleIndexedEnum = SimpleIndexedEnumPositiveOffset.Null;

            SimpleIndexedEnumPositiveOffset result = simpleIndexedEnum.Previous();
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
            SimpleIndexedEnumNegativeOffset simpleIndexedEnum = SimpleIndexedEnumNegativeOffset.One;

            SimpleIndexedEnumNegativeOffset result = simpleIndexedEnum.Next();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetDoubleNextTest()
        {
            SimpleIndexedEnumNegativeOffset simpleIndexedEnum = SimpleIndexedEnumNegativeOffset.One;

            SimpleIndexedEnumNegativeOffset result = simpleIndexedEnum.Next().Next();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Three, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetCarryOverForwardTest()
        {
            SimpleIndexedEnumNegativeOffset simpleIndexedEnum = SimpleIndexedEnumNegativeOffset.Four;

            SimpleIndexedEnumNegativeOffset result = simpleIndexedEnum.Next();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Null, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetPreviousTest()
        {
            SimpleIndexedEnumNegativeOffset simpleIndexedEnum = SimpleIndexedEnumNegativeOffset.Four;

            SimpleIndexedEnumNegativeOffset result = simpleIndexedEnum.Previous();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Three, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetDoublePreviousTest()
        {
            SimpleIndexedEnumNegativeOffset simpleIndexedEnum = SimpleIndexedEnumNegativeOffset.Four;

            SimpleIndexedEnumNegativeOffset result = simpleIndexedEnum.Previous().Previous();
            Assert.AreEqual(SimpleIndexedEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SimpleIndexedEnumNegativeOffsetCarryOverBackwardTest()
        {
            SimpleIndexedEnumNegativeOffset simpleIndexedEnum = SimpleIndexedEnumNegativeOffset.Null;

            SimpleIndexedEnumNegativeOffset result = simpleIndexedEnum.Previous();
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
            SparseEnumNegativeOffset simpleIndexedEnum = SparseEnumNegativeOffset.One;

            SparseEnumNegativeOffset result = simpleIndexedEnum.Next();
            Assert.AreEqual(SparseEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetDoubleNextTest()
        {
            SparseEnumNegativeOffset simpleIndexedEnum = SparseEnumNegativeOffset.One;

            SparseEnumNegativeOffset result = simpleIndexedEnum.Next().Next();
            Assert.AreEqual(SparseEnumNegativeOffset.Three, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetCarryOverForwardTest()
        {
            SparseEnumNegativeOffset simpleIndexedEnum = SparseEnumNegativeOffset.Four;

            SparseEnumNegativeOffset result = simpleIndexedEnum.Next();
            Assert.AreEqual(SparseEnumNegativeOffset.Null, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetPreviousTest()
        {
            SparseEnumNegativeOffset simpleIndexedEnum = SparseEnumNegativeOffset.Three;

            SparseEnumNegativeOffset result = simpleIndexedEnum.Previous();
            Assert.AreEqual(SparseEnumNegativeOffset.Two, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetDoublePreviousTest()
        {
            SparseEnumNegativeOffset simpleIndexedEnum = SparseEnumNegativeOffset.Three;

            SparseEnumNegativeOffset result = simpleIndexedEnum.Previous().Previous();
            Assert.AreEqual(SparseEnumNegativeOffset.One, result);
        }

        [TestMethod]
        public void SparseEnumNegativeOffsetCarryOverBackwardTest()
        {
            SparseEnumNegativeOffset simpleIndexedEnum = SparseEnumNegativeOffset.Null;

            SparseEnumNegativeOffset result = simpleIndexedEnum.Previous();
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
