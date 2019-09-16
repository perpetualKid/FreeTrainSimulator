// COPYRIGHT 2010, 2012 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts
{
    public class WorldSoundFile
    {
        public TrackItemSound TrackItemSound { get; private set; }

        public WorldSoundFile(string fileName, TrItem[] trItems)
        {
            if (File.Exists(fileName))
            {
                Trace.Write("$");
                using (STFReader stf = new STFReader(fileName, false))
                {
                    stf.ParseFile(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tr_worldsoundfile", ()=>{ TrackItemSound = new TrackItemSound(stf, trItems); }),
                    });
                    if (TrackItemSound == null)
                        STFException.TraceWarning(stf, "Missing TR_WorldSoundFile statement");
                }
            }
        }
    }

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
