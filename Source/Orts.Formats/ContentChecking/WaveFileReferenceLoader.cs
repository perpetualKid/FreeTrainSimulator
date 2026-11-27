using System.Diagnostics;

namespace Orts.Formats.ContentChecking
{
    internal sealed class WaveFileReferenceLoader : Loader
    {
        public override void TryLoading(string file)
        {
            if (WaveFileChecker == null)
            {
                FilesLoaded = 0;
                FilesSkipped = 1;
                Trace.TraceWarning("WAV file was not checked because no runtime WAV checker was supplied: {0}", file);
                return;
            }

            WaveFileChecker(file);
        }
    }
}