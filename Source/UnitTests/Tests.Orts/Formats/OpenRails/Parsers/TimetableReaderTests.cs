using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Formats.OpenRails.Parsers;

namespace Tests.Orts.Formats.OpenRails.Parsers
{
    [TestClass]
    public class TimetableReaderTests
    {
        [TestMethod]
        public void DetectSeparatorTest()
        {
            using (TestFile file = new TestFile(";"))
            {
                TimetableReader tr = new TimetableReader(file.FileName);
                Assert.AreEqual(1, tr.Strings.Count);
                Assert.AreEqual(2, tr.Strings[0].Length);
            }
            using (TestFile file = new TestFile(","))
            {
                TimetableReader tr = new TimetableReader(file.FileName);
                Assert.AreEqual(1, tr.Strings.Count);
                Assert.AreEqual(2, tr.Strings[0].Length);
            }
            using (TestFile file = new TestFile("\t"))
            {
                TimetableReader tr = new TimetableReader(file.FileName);
                Assert.AreEqual(1, tr.Strings.Count);
                Assert.AreEqual(2, tr.Strings[0].Length);
            }
            using (TestFile file = new TestFile(":"))
            {
                Assert.ThrowsException<InvalidDataException>(() =>
                {
                    TimetableReader tr = new TimetableReader(file.FileName);
                });
            }
        }

        [TestMethod]
        public void ParseStructureTest()
        {
            using (TestFile file = new TestFile(";b;c;d\n1;2;3\nA;B;C;D;E"))
            {
                TimetableReader tr = new TimetableReader(file.FileName);
                Assert.AreEqual(3, tr.Strings.Count);
                Assert.AreEqual(4, tr.Strings[0].Length);
                Assert.AreEqual(3, tr.Strings[1].Length);
                Assert.AreEqual(5, tr.Strings[2].Length);
#pragma warning disable CA1861 // Avoid constant arrays as arguments
                CollectionAssert.AreEqual(new[] { "", "b", "c", "d" }, tr.Strings[0]);
                CollectionAssert.AreEqual(new[] { "1", "2", "3" }, tr.Strings[1]);
                CollectionAssert.AreEqual(new[] { "A", "B", "C", "D", "E" }, tr.Strings[2]);
#pragma warning restore CA1861 // Avoid constant arrays as arguments
            }
        }
    }
}
