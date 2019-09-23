using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.OR.Files
{
    public class ORActivityFile
    {
        public void InsertORSpecificData(string filenamewithpath)
        {
            using (STFReader stf = new STFReader(filenamewithpath, false))
            {
                //var tr_activityTokenPresent = false;
                //stf.ParseFile(() => false && (Tr_Activity.Tr_Activity_Header != null), new STFReader.TokenProcessor[] {
                //    new STFReader.TokenProcessor("tr_activity", ()=>{ tr_activityTokenPresent = true;  Tr_Activity.InsertORSpecificData (stf); }),
                //    });
                //if (!tr_activityTokenPresent)
                //    STFException.TraceWarning(stf, "Missing Tr_Activity statement");
            }
        }


    }
}
