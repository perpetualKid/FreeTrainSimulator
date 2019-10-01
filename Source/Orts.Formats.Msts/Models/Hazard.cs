using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class Hazard
    {
        public Hazard(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("filename", ()=>{ FileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("workers", ()=>{ Workers = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("distance", ()=>{ Distance = stf.ReadFloatBlock(STFReader.Units.None, 10); }),
                new STFReader.TokenProcessor("speed", ()=>{ Speed = stf.ReadFloatBlock(STFReader.Units.None, 3); }),
                new STFReader.TokenProcessor("idle_key", ()=>{ stf.ReadVector2Block(STFReader.Units.None, ref idleKey); }),
                new STFReader.TokenProcessor("idle_key2", ()=>{ stf.ReadVector2Block(STFReader.Units.None, ref idleKey2); }),
                new STFReader.TokenProcessor("surprise_key_left", ()=>{ stf.ReadVector2Block(STFReader.Units.None, ref surpriseKeyLeft); }),
                new STFReader.TokenProcessor("surprise_key_right", ()=>{ stf.ReadVector2Block(STFReader.Units.None, ref surpriseKeyRight); }),
                new STFReader.TokenProcessor("success_scarper_key", ()=>{ stf.ReadVector2Block(STFReader.Units.None, ref successScarperKey); }),
           });
            //TODO This should be changed to STFException.TraceError() with defaults values created
            if (FileName == null) throw new STFException(stf, "Missing FileName");
        }

        private Vector2 idleKey, idleKey2, surpriseKeyLeft, surpriseKeyRight, successScarperKey;

        public string FileName { get; private set; } // ie OdakyuSE - used for MKR,RDB,REF,RIT,TDB,TIT
        public string Workers { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public ref Vector2 IdleKey => ref idleKey;
        public ref Vector2 IdleKey2 => ref idleKey2;
        public ref Vector2 SurpriseKeyLeft => ref surpriseKeyLeft;
        public ref Vector2 SurpriseKeyRight => ref surpriseKeyRight;
        public ref Vector2 SuccessScarperKey => ref successScarperKey;

    }
}
