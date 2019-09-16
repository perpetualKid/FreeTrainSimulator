using System.Collections.Generic;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Entities
{
    public class TrackItemSound
    {
        public List<WorldSoundSource> SoundSources { get; private set; } = new List<WorldSoundSource>();
        public List<WorldSoundRegion> SoundRegions { get; private set; } = new List<WorldSoundRegion>();

        public TrackItemSound(STFReader stf, TrItem[] trItems)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("soundsource", ()=>{ SoundSources.Add(new WorldSoundSource(stf)); }),
                new STFReader.TokenProcessor("soundregion", ()=>{ SoundRegions.Add(new WorldSoundRegion(stf, trItems)); }),
            });
        }
    }

    public class WorldSoundSource
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public string FileName { get; private set; }

        public WorldSoundSource(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("filename", ()=>{ FileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("position", ()=>{
                    stf.MustMatch("(");
                    X = stf.ReadFloat(STFReader.Units.None, null);
                    Y = stf.ReadFloat(STFReader.Units.None, null);
                    Z = stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    public class WorldSoundRegion
    {
        public int TrackType { get; private set; } = -1;
        public float RotY { get; private set; }
        public List<int> TrackNodes { get; private set; }

        public WorldSoundRegion(STFReader stf, TrItem[] trItems)
        {
            TrackNodes = new List<int>();
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("soundregiontracktype", ()=>{ TrackType = stf.ReadIntBlock(-1); }),
                new STFReader.TokenProcessor("soundregionroty", ()=>{ RotY = stf.ReadFloatBlock(STFReader.Units.None, float.MaxValue); }),
                new STFReader.TokenProcessor("tritemid", ()=>{
                    stf.MustMatch("(");
                    stf.ReadInt(0);//dummy read
                    var trItemId = stf.ReadInt(-1);
                    if (trItemId != -1) {
                        if (trItemId >= trItems.Length) {
                            STFException.TraceWarning(stf, string.Format("Ignored invalid TrItemId {0}", trItemId));
                        } else {
                            TrackNodes.Add(trItemId);
                        }
                    }
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }
}
