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
                Assert.HasCount(1, tr.Strings);
                Assert.HasCount(2, tr.Strings[0]);
            }
            using (TestFile file = new TestFile(","))
            {
                TimetableReader tr = new TimetableReader(file.FileName);
                Assert.HasCount(1, tr.Strings);
                Assert.HasCount(2, tr.Strings[0]);
            }
            using (TestFile file = new TestFile("\t"))
            {
                TimetableReader tr = new TimetableReader(file.FileName);
                Assert.HasCount(1, tr.Strings);
                Assert.HasCount(2, tr.Strings[0]);
            }
            using (TestFile file = new TestFile(":"))
            {
                Assert.ThrowsExactly<InvalidDataException>(() =>
                {
                    TimetableReader tr = new TimetableReader(file.FileName);
                });
            }
        }

        private static readonly string[] expectedbcd = new string[] { "", "b", "c", "d" };
        private static readonly string[] expectedArray123 = new[] { "1", "2", "3" };
        private static readonly string[] expectedArrayABCDE = new[] { "A", "B", "C", "D", "E" };

        [TestMethod]
        public void ParseStructureTest()
        {
            using (TestFile file = new TestFile(";b;c;d\n1;2;3\nA;B;C;D;E"))
            {
                TimetableReader tr = new TimetableReader(file.FileName);
                Assert.HasCount(3, tr.Strings);
                Assert.HasCount(4, tr.Strings[0]);
                Assert.HasCount(3, tr.Strings[1]);
                Assert.HasCount(5, tr.Strings[2]);
                Assert.AreSequenceEqual(expectedbcd, tr.Strings[0]);
                Assert.AreSequenceEqual(expectedArray123, tr.Strings[1]);
                Assert.AreSequenceEqual(expectedArrayABCDE, tr.Strings[2]);
            }
        }
    }
}
