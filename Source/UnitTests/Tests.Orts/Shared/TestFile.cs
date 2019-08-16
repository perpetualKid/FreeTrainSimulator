using System;
using System.IO;

namespace Tests.Orts.Shared
{
    public class TestFile : IDisposable
    {
        private bool disposed;

        public string FileName { get; private set; }

        public TestFile(string contents)
        {
            FileName = Path.GetTempFileName();
            using (var writer = new StreamWriter(FileName))
            {
                writer.Write(contents);
            }
        }

        public void Cleanup()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void IDisposable.Dispose()
        {
            Cleanup();
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
