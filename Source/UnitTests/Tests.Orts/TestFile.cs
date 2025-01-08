using System;
using System.IO;

namespace Tests.Orts
{
    public class TestFile : IDisposable
    {
        private bool disposed;

        public string FileName { get; private set; }

        public TestFile(string contents)
        {
            FileName = Path.GetTempFileName();
            using (StreamWriter writer = new StreamWriter(FileName))
            {
                writer.Write(contents);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TestFile()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                File.Delete(FileName);
            }
            disposed = true;
        }
    }
}
