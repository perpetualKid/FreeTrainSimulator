using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common.IO;
using Tests.Orts.Shared;

namespace Tests.Orts.Common.IO
{
    [TestClass]
    public class FileSystemCacheTests
    {
        [TestMethod]
        public void InitializeTest()
        {
            using (TestFile file = new TestFile(string.Empty))
            {
                string directory = Path.GetDirectoryName(file.FileName);
                FileSystemCache.Initialize(new DirectoryInfo(directory));
            }
        }

        [TestMethod]
        public void InitializeRootNotFoundTest()
        {
            using (TestFile file = new TestFile(string.Empty))
            {
                string directory = file.FileName;
                Assert.ThrowsException<FileNotFoundException>(() => FileSystemCache.Initialize(new DirectoryInfo(directory)));
            }
        }

        [TestMethod]
        public void FileFoundTest()
        {
            using (TestFile file = new TestFile(string.Empty))
            {
                string directory = Path.GetDirectoryName(file.FileName);
                FileSystemCache.Initialize(new DirectoryInfo(directory));

                Assert.IsTrue(FileSystemCache.FileExists(file.FileName));
            }
        }

        [TestMethod]
        public void DirectoryNotFoundTest()
        {
            using (TestFile file = new TestFile(string.Empty))
            {
                string directory = Path.GetDirectoryName(file.FileName);
                FileSystemCache.Initialize(new DirectoryInfo(directory));

                Assert.IsFalse(FileSystemCache.DirectoryExists(directory));
            }
        }

    }
}
