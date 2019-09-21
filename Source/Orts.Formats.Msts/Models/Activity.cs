using System.Collections.Generic;
using Orts.Common;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    /// <summary>
    /// Parses ActivityObject objects and saves them in ActivityObjectList.
    /// </summary>
    public class ActivityObjects : List<ActivityObject>
    {
        public ActivityObjects(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("activityobject", ()=>{ Add(new ActivityObject(stf)); }),
            });
        }
    }

    public class ActivityObject
    {
        private WorldLocation location;
        public TrainSet TrainSet { get; private set; }
        public int Direction { get; private set; }  // 0 means forwards and anything != 0 means reverse
        public int ID { get; private set; }
        public ref WorldLocation Location => ref location;

        public ActivityObject(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("objecttype", ()=>{ stf.MustMatch("("); stf.MustMatch("WagonsList"); stf.MustMatch(")"); }),
                new STFReader.TokenProcessor("train_config", ()=>{ TrainSet = new TrainSet(stf); }),
                new STFReader.TokenProcessor("direction", ()=>{ Direction = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("id", ()=>{ ID = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("tile", ()=>{
                    stf.MustMatch("(");
                    location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                        stf.ReadFloat(STFReader.Units.None, null), 0f, stf.ReadFloat(STFReader.Units.None, null));
                    stf.MustMatch(")");
                }),
            });
        }
    }

    public class MaxVelocity
    {
        public float A { get; private set; }
        public float B { get; private set; } = 0.001f;

        public MaxVelocity(STFReader stf)
        {
            stf.MustMatch("(");
            A = stf.ReadFloat(STFReader.Units.Speed, null);
            B = stf.ReadFloat(STFReader.Units.Speed, null);
            stf.MustMatch(")");
        }
    }

    public class PlatformPassengersWaiting : List<PlatformData>
    {  // For use, see file EUROPE1\ACTIVITIES\aftstorm.act

        public PlatformPassengersWaiting(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("platformdata", ()=>{ Add(new PlatformData(stf)); }),
            });
        }
    }

    public class PlatformData
    { // e.g. "PlatformData ( 41 20 )" 
        public int ID { get; private set; }
        public int PassengerCount { get; private set; }

        public PlatformData(int id, int passengerCount)
        {
            ID = id;
            PassengerCount = passengerCount;
        }

        public PlatformData(STFReader stf)
        {
            stf.MustMatch("(");
            ID = stf.ReadInt(null);
            PassengerCount = stf.ReadInt(null);
            stf.MustMatch(")");
        }
    }

    public class FailedSignals : List<int>
    { // e.g. ActivityFailedSignals ( ActivityFailedSignal ( 50 ) )

        public FailedSignals(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("activityfailedsignal", ()=>{ Add(stf.ReadIntBlock(null)); }),
            });
        }
    }

    public class RestrictedSpeedZones : List<RestrictedSpeedZone>
    {  // For use, see file EUROPE1\ACTIVITIES\aftstorm.act

        public RestrictedSpeedZones(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("activityrestrictedspeedzone", ()=>{ Add(new RestrictedSpeedZone(stf)); }),
            });
        }
    }

    public class RestrictedSpeedZone
    {
        private WorldLocation startPosition;
        private WorldLocation endPosition;

        public ref WorldLocation StartPosition => ref startPosition;
        public ref WorldLocation EndPosition => ref endPosition;

        public RestrictedSpeedZone(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("startposition", ()=>{
                    stf.MustMatch("(");
                    startPosition = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                        stf.ReadFloat(STFReader.Units.None, null), 0f, stf.ReadFloat(STFReader.Units.None, null));
                    stf.MustMatch(")");
                }),
                new STFReader.TokenProcessor("endposition", ()=>{
                    stf.MustMatch("(");
                    endPosition = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                        stf.ReadFloat(STFReader.Units.None, null), 0f, stf.ReadFloat(STFReader.Units.None, null));
                    stf.MustMatch(")");
                })
            });
        }
    }

}
