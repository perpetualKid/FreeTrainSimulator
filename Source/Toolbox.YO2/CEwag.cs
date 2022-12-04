using System.IO;

using Orts.Formats.Msts.Parsers;

namespace Toolbox.YO2
{
    /// <summary>
    /// Work with wagon files
    /// </summary>
    public class CEwag
    {
        public string Name { get; private set; }

        public CEwag(string fileName)
        {
            Name = Path.GetFileNameWithoutExtension(fileName);
            using (var stf = new STFReader(fileName, false))
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("wagon", ()=>{
                        stf.ReadString();
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                        });
                    }),
                });
        }
    }
}
