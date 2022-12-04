
using System.IO;

using Orts.Formats.Msts.Parsers;

namespace Toolbox.YO2
{
    /// <summary>
    /// Work with engine files
    /// </summary>
    public class CEeng
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string CabViewFile { get; private set; }

        public CEeng(string fileName)
        {
            Name = Path.GetFileNameWithoutExtension(fileName);
            using (STFReader stf = new STFReader(fileName, false))
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("engine", ()=>{
                        stf.ReadString();
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                            new STFReader.TokenProcessor("description", ()=>{ Description = stf.ReadStringBlock(null); }),
 //                           new STFReader.TokenProcessor("cabview", ()=>{ CabViewFile = stf.ReadStringBlock(null); }),
                        });
                    }),
                });
        }
    }
}
