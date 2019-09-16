using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Entities
{
    public class SDShape
    {
        public SDShape()
        {
            ESD_Bounding_Box = new ESD_Bounding_Box();
        }

        public SDShape(STFReader stf)
        {
            stf.ReadString(); // Ignore the filename string. TODO: Check if it agrees with the SD file name? Is this important?
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("esd_detail_level", ()=>{ ESD_Detail_Level = stf.ReadIntBlock(null); }),
                    new STFReader.TokenProcessor("esd_alternative_texture", ()=>{ ESD_Alternative_Texture = stf.ReadIntBlock(null); }),
                    new STFReader.TokenProcessor("esd_no_visual_obstruction", ()=>{ ESD_No_Visual_Obstruction = stf.ReadBoolBlock(true); }),
                    new STFReader.TokenProcessor("esd_snapable", ()=>{ ESD_Snapable = stf.ReadBoolBlock(true); }),
                    new STFReader.TokenProcessor("esd_subobj", ()=>{ ESD_SubObj = true; stf.SkipBlock(); }),
                    new STFReader.TokenProcessor("esd_bounding_box", ()=>{
                        ESD_Bounding_Box = new ESD_Bounding_Box(stf);
                        if (ESD_Bounding_Box.Min == null || ESD_Bounding_Box.Max == null)  // ie quietly handle ESD_Bounding_Box()
                            ESD_Bounding_Box = null;
                    }),
                    new STFReader.TokenProcessor("esd_ortssoundfilename", ()=>{ ESD_SoundFileName = stf.ReadStringBlock(null); }),
                    new STFReader.TokenProcessor("esd_ortsbellanimationfps", ()=>{ ESD_BellAnimationFPS = stf.ReadFloatBlock(STFReader.Units.Frequency, null); }),
                });
            // TODO - some objects have no bounding box - ie JP2BillboardTree1.sd
            //if (ESD_Bounding_Box == null) throw new STFException(stf, "Missing ESD_Bound_Box statement");
            if (ESD_Bounding_Box == null) STFException.TraceInformation(stf, "Missing ESD_Bound_Box statement");
        }

        public int ESD_Detail_Level { get; private set; }
        public int ESD_Alternative_Texture { get; private set; }
        public ESD_Bounding_Box ESD_Bounding_Box { get; private set; }
        public bool ESD_No_Visual_Obstruction { get; private set; }
        public bool ESD_Snapable { get; private set; }
        public bool ESD_SubObj { get; private set; }
        public string ESD_SoundFileName { get; private set; } = string.Empty;
    public float ESD_BellAnimationFPS { get; private set; } = 8;
    }

    public class ESD_Bounding_Box
    {
        public TWorldPosition Min { get; private set; }
        public TWorldPosition Max { get; private set; }

        public ESD_Bounding_Box() // default used for files with no SD file
        {
            Min = new TWorldPosition(0, 0, 0);
            Max = new TWorldPosition(0, 0, 0);
        }

        public ESD_Bounding_Box(STFReader stf)
        {
            stf.MustMatch("(");
            string item = stf.ReadString();
            if (item == ")") return;    // quietly return on ESD_Bounding_Box()
            stf.StepBackOneItem();
            float X = stf.ReadFloat(STFReader.Units.None, null);
            float Y = stf.ReadFloat(STFReader.Units.None, null);
            float Z = stf.ReadFloat(STFReader.Units.None, null);
            Min = new TWorldPosition(X, Y, Z);
            X = stf.ReadFloat(STFReader.Units.None, null);
            Y = stf.ReadFloat(STFReader.Units.None, null);
            Z = stf.ReadFloat(STFReader.Units.None, null);
            Max = new TWorldPosition(X, Y, Z);
            // JP2indirt.sd has extra parameters
            stf.SkipRestOfBlock();
        }

    }
}
