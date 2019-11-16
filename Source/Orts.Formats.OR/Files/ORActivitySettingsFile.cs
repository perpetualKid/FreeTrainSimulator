using Orts.Formats.Msts.Parsers;
using Orts.Formats.OR.Models;

namespace Orts.Formats.OR.Files
{
    public class ORActivitySettingsFile
    {
        public ORActivity Activity { get; private set; }

        public ORActivitySettingsFile(string fileName)
        {
            using (STFReader stf = new STFReader(fileName, false))
            {
                bool tokenPresent = false;
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tr_activity", ()=>{ tokenPresent = true;  ParseORActivitySettings(stf); }),
                    });
                if (!tokenPresent)
                    STFException.TraceWarning(stf, "Missing Tr_Activity statement");
            }
        }

        private void ParseORActivitySettings(STFReader stf)
        {
            stf.MustMatchBlockStart();
            var tokenPresent = false;
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tr_activity_file", ()=>{ tokenPresent = true; ParseORActivityFileSettings(stf); }),
            });
            if (!tokenPresent)
                STFException.TraceWarning(stf, "Missing Tr_Activity_File statement");
        }

        private void ParseORActivityFileSettings(STFReader stf)
        {
            stf.MustMatchBlockStart();
            var tokenPresent = false;
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tr_activity_file", ()=>{ tokenPresent = true; Activity = new ORActivity(stf); }),
            });
            if (!tokenPresent)
                STFException.TraceWarning(stf, "Missing Tr_Activity_File statement");
        }
    }
}
