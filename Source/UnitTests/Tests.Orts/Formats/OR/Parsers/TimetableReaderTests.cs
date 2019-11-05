using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Formats.OR.Parsers;

using Tests.Orts.Shared;

namespace Tests.Orts.Formats.OR.Parsers
{
    [TestClass]
    public class TimetableReaderTests
    {
        [TestMethod]
        public void DetectSeparatorTest()
        {
            using (var file = new TestFile(";"))
            {
                var tr = new TimetableReader(file.FileName);
                Assert.AreEqual(1, tr.Strings.Count);
                Assert.AreEqual(2, tr.Strings[0].Length);
            }
            using (var file = new TestFile(","))
            {
                var tr = new TimetableReader(file.FileName);
                Assert.AreEqual(1, tr.Strings.Count);
                Assert.AreEqual(2, tr.Strings[0].Length);
            }
            using (var file = new TestFile("\n"))
            {
                Assert.ThrowsException<InvalidDataException>(() => {
                    var tr = new TimetableReader(file.FileName);
                });
            }
        }

        [TestMethod]
        public void ParseStructureTest()
        {
            using (var file = new TestFile(";b;c;d\n1;2;3\nA;B;C;D;E"))
            {
                var tr = new TimetableReader(file.FileName);
                Assert.AreEqual(3, tr.Strings.Count);
                Assert.AreEqual(4, tr.Strings[0].Length);
                Assert.AreEqual(3, tr.Strings[1].Length);
                Assert.AreEqual(5, tr.Strings[2].Length);
                CollectionAssert.AreEqual(new[] { "", "b", "c", "d" }, tr.Strings[0]);
                CollectionAssert.AreEqual(new[] { "1", "2", "3" }, tr.Strings[1]);
                CollectionAssert.AreEqual(new[] { "A", "B", "C", "D", "E" }, tr.Strings[2]);
            }
        }
    }
}
