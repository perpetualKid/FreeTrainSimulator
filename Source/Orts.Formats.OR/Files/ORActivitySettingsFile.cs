using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.OR.Files
{
    public class ORActivitySettingsFile
    {
        public void InsertORSpecificData(string fileName)
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
            stf.MustMatch("(");
            var tokenPresent = false;
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tr_activity_file", ()=>{ tokenPresent = true; ParseORActivityFileSettings(stf); }),
            });
            if (!tokenPresent)
                STFException.TraceWarning(stf, "Missing Tr_Activity_File statement");
        }

        private void ParseORActivityFileSettings(STFReader stf)
        {
            stf.MustMatch("(");
            var tokenPresent = false;
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tr_activity_file", ()=>{ tokenPresent = true; OverrideActivityUserSettings(stf); }),
            });
            if (!tokenPresent)
                STFException.TraceWarning(stf, "Missing Tr_Activity_File statement");
        }

        private void OverrideActivityUserSettings(STFReader reader)
        {

        }
    }
}
