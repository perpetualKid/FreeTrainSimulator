﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class Activity
    {
        public int Serial { get; private set; } = -1;

        public PlayerServices PlayerServices { get; private set; }
        public int NextServiceUiD { get; private set; } = 1;
        public int NextActivityObjectUiD { get; private set; } = 32786;
        public ActivityObjects ActivityObjects { get; private set; }
        public FailedSignals FailedSignals { get; private set; }
        public ActivityEvents Events { get; private set; }
        public Traffic Traffic { get; private set; }
        public PlatformPassengersWaiting PlatformWaitingPassengers { get; private set; }
        public RestrictedSpeedZones ActivityRestrictedSpeedZones { get; private set; }
        public bool AIBlowsHornAtLevelCrossings { get; private set; }
        public LevelCrossingHornPattern AILevelCrossingHornPattern { get; private set; } = LevelCrossingHornPattern.Single;

#pragma warning disable CA1034 // Nested types should not be visible
        public class ActivityHeader     //this redirection has no functional advantage, only grouping to improve clarity in development
#pragma warning restore CA1034 // Nested types should not be visible
        {
            public string RouteID { get; internal protected set; }
            public string Name { get; internal protected set; }                 // AE Display Name
            public string Description { get; internal protected set; }
            public string Briefing { get; internal protected set; }
            public bool CompleteActivity { get; internal protected set; }
            public int Type { get; internal protected set; }
            public ActivityMode Mode { get; internal protected set; } = ActivityMode.Player;
            public TimeSpan StartTime { get; internal protected set; } = new TimeSpan(10, 0, 0);
            public SeasonType Season { get; internal protected set; } = SeasonType.Summer;
            public WeatherType Weather { get; internal protected set; } = WeatherType.Clear;
            public string PathID { get; internal protected set; }
            public float StartingSpeed { get; internal protected set; }
            public TimeSpan Duration { get; internal protected set; } = new TimeSpan(1, 0, 0);
            public Difficulty Difficulty { get; internal protected set; } = Difficulty.Easy;
            public int Animals { get; internal protected set; } = 100;       // percent
            public int Workers { get; internal protected set; }             // percent
            public int FuelWater { get; internal protected set; } = 100;     // percent
            public int FuelCoal { get; internal protected set; } = 100;      // percent
            public int FuelDiesel { get; internal protected set; } = 100;   // percent
            public string LoadStationsPopulationFile { get; internal protected set; }
        }

        public ActivityHeader Header { get; } = new ActivityHeader();

        internal Activity(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tr_activity_file", ()=>{ ParseActivityDetails(stf); }),
                new STFReader.TokenProcessor("serial", ()=>{ Serial = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("tr_activity_header", ()=>{ ParseActivityHeader(stf); }),
            });
        }

        private void ParseActivityHeader(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("routeid", ()=>{ Header.RouteID = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("name", ()=>{ Header.Name = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("description", ()=>{ Header.Description = stf.ReadStringBlock(string.Empty); }),
                new STFReader.TokenProcessor("briefing", ()=>{ Header.Briefing = stf.ReadStringBlock(string.Empty); }),
                new STFReader.TokenProcessor("completeactivity", ()=>{ Header.CompleteActivity = (stf.ReadIntBlock(1) == 1); }),
                new STFReader.TokenProcessor("type", ()=>{ Header.Type = stf.ReadIntBlock(0); }),
                new STFReader.TokenProcessor("mode", ()=>{ Header.Mode = (ActivityMode)stf.ReadIntBlock((int)Header.Mode); }),
                new STFReader.TokenProcessor("starttime", ()=> {
                    stf.MustMatchBlockStart();
                    Header.StartTime = new TimeSpan(stf.ReadInt(null), stf.ReadInt(null), stf.ReadInt(null));
                    stf.MustMatchBlockEnd();
                }),
                new STFReader.TokenProcessor("season", ()=>{ Header.Season = (SeasonType)stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("weather", ()=>{ Header.Weather = (WeatherType)stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("pathid", ()=>{ Header.PathID = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("startingspeed", ()=>{ Header.StartingSpeed = stf.ReadFloatBlock(STFReader.Units.Speed, Header.StartingSpeed); }),
                new STFReader.TokenProcessor("duration", ()=>{
                    stf.MustMatchBlockStart();
                    Header.Duration = new TimeSpan(stf.ReadInt(null), stf.ReadInt(null), 0);
                    stf.MustMatchBlockEnd();
                }),
                new STFReader.TokenProcessor("difficulty", ()=>{ Header.Difficulty = (Difficulty)stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("animals", ()=>{ Header.Animals = stf.ReadIntBlock(Header.Animals); }),
                new STFReader.TokenProcessor("workers", ()=>{ Header.Workers = stf.ReadIntBlock(Header.Workers); }),
                new STFReader.TokenProcessor("fuelwater", ()=>{ Header.FuelWater = stf.ReadIntBlock(Header.FuelWater); }),
                new STFReader.TokenProcessor("fuelcoal", ()=>{ Header.FuelCoal = stf.ReadIntBlock(Header.FuelCoal); }),
                new STFReader.TokenProcessor("fueldiesel", ()=>{ Header.FuelDiesel = stf.ReadIntBlock(Header.FuelDiesel); }),
                new STFReader.TokenProcessor("ortsloadstationspopulation", ()=>{ Header.LoadStationsPopulationFile = stf.ReadStringBlock(null); }),
            });
        }

        private void ParseActivityDetails(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("player_service_definition",()=>{ PlayerServices = new PlayerServices(stf); }),
                new STFReader.TokenProcessor("nextserviceuid",()=>{ NextServiceUiD = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("nextactivityobjectuid",()=>{ NextActivityObjectUiD = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("events",()=>{ Events = new ActivityEvents(stf); }),
                new STFReader.TokenProcessor("traffic_definition",()=>{ Traffic = new Traffic(stf); }),
                new STFReader.TokenProcessor("activityobjects",()=>{ ActivityObjects = new ActivityObjects(stf); }),
                new STFReader.TokenProcessor("platformnumpassengerswaiting",()=>{ PlatformWaitingPassengers = new PlatformPassengersWaiting(stf); }),  // 35 files. To test, use EUROPE1\ACTIVITIES\aftstorm.act
                new STFReader.TokenProcessor("activityfailedsignals",()=>{ FailedSignals = new FailedSignals(stf); }),
                new STFReader.TokenProcessor("activityrestrictedspeedzones",()=>{ ActivityRestrictedSpeedZones = new RestrictedSpeedZones(stf); }),   // 27 files. To test, use EUROPE1\ACTIVITIES\lclsrvce.act
                new STFReader.TokenProcessor("ortsaihornatcrossings", () => { AIBlowsHornAtLevelCrossings = stf.ReadIntBlock(Convert.ToInt32(AIBlowsHornAtLevelCrossings)) > 0; }),
                new STFReader.TokenProcessor("ortsaicrossinghornpattern", () =>{if (EnumExtension.GetValue(stf.ReadStringBlock(""), out LevelCrossingHornPattern value)) AILevelCrossingHornPattern = value; })
            });
        }

        // Used for explore in activity mode
        public Activity(int startTime, string name)
        {
            Serial = -1;
            Header = new ActivityHeader();
            PlayerServices = new PlayerServices(startTime, name);
        }
    }

    /// <summary>
    /// Parses ActivityObject objects and saves them in ActivityObjectList.
    /// </summary>
    public class ActivityObjects : List<ActivityObject>
    {
        internal ActivityObjects(STFReader stf)
        {
            stf.MustMatchBlockStart();
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
        public ref readonly WorldLocation Location => ref location;

        internal ActivityObject(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("objecttype", ()=>{ stf.MustMatchBlockStart(); stf.MustMatch("WagonsList"); stf.MustMatchBlockEnd(); }),
                new STFReader.TokenProcessor("train_config", ()=>{ TrainSet = new TrainSet(stf); }),
                new STFReader.TokenProcessor("direction", ()=>{ Direction = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("id", ()=>{ ID = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("tile", ()=>{
                    stf.MustMatchBlockStart();
                    location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                        stf.ReadFloat(STFReader.Units.None, null), 0f, stf.ReadFloat(STFReader.Units.None, null));
                    stf.MustMatchBlockEnd();
                }),
            });
        }
    }

    public class MaxVelocity
    {
        public float A { get; private set; }
        public float B { get; private set; } = 0.001f;

        internal MaxVelocity(STFReader stf)
        {
            stf.MustMatchBlockStart();
            A = stf.ReadFloat(STFReader.Units.Speed, null);
            B = stf.ReadFloat(STFReader.Units.Speed, null);
            stf.MustMatchBlockEnd();
        }
    }

    public class PlatformPassengersWaiting : List<PlatformData>
    {  // For use, see file EUROPE1\ACTIVITIES\aftstorm.act

        internal PlatformPassengersWaiting(STFReader stf)
        {
            stf.MustMatchBlockStart();
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

        internal PlatformData(STFReader stf)
        {
            stf.MustMatchBlockStart();
            ID = stf.ReadInt(null);
            PassengerCount = stf.ReadInt(null);
            stf.MustMatchBlockEnd();
        }
    }

    public class FailedSignals : List<int>
    { // e.g. ActivityFailedSignals ( ActivityFailedSignal ( 50 ) )

        internal FailedSignals(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("activityfailedsignal", ()=>{ Add(stf.ReadIntBlock(null)); }),
            });
        }
    }

    public class RestrictedSpeedZones : List<RestrictedSpeedZone>
    {  // For use, see file EUROPE1\ACTIVITIES\aftstorm.act

        internal RestrictedSpeedZones(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("activityrestrictedspeedzone", ()=>{ Add(new RestrictedSpeedZone(stf)); }),
            });
        }
    }

    public class RestrictedSpeedZone
    {
        private WorldLocation startPosition;
        private WorldLocation endPosition;

        public ref readonly WorldLocation StartPosition => ref startPosition;
        public ref readonly WorldLocation EndPosition => ref endPosition;

        internal RestrictedSpeedZone(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("startposition", ()=>{
                    stf.MustMatchBlockStart();
                    startPosition = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                        stf.ReadFloat(STFReader.Units.None, null), 0f, stf.ReadFloat(STFReader.Units.None, null));
                    stf.MustMatchBlockEnd();
                }),
                new STFReader.TokenProcessor("endposition", ()=>{
                    stf.MustMatchBlockStart();
                    endPosition = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                        stf.ReadFloat(STFReader.Units.None, null), 0f, stf.ReadFloat(STFReader.Units.None, null));
                    stf.MustMatchBlockEnd();
                })
            });
        }
    }

    public class RestartWaitingTrain
    {
        public string WaitingTrainToRestart { get; private set; }
        public int WaitingTrainStartingTime { get; private set; } = -1;
        public int DelayToRestart { get; private set; }
        public int MatchingWPDelay { get; private set; }

        internal RestartWaitingTrain(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("ortswaitingtraintorestart", ()=>{
                    stf.MustMatchBlockStart();
                    WaitingTrainToRestart = stf.ReadString();
                    WaitingTrainStartingTime = stf.ReadInt(-1);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("ortsdelaytorestart", ()=>{ DelayToRestart = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("ortsmatchingwpdelay", ()=>{ MatchingWPDelay = stf.ReadIntBlock(null); }),
            });
        }
    }

    public readonly struct LoadData : IEquatable<LoadData>
    {
        public string Name { get; }
        public string Folder { get; }
        public LoadPosition LoadPosition { get; }

        public LoadData(string name, string folder, LoadPosition loadPosition)
        {
            Name = name;
            Folder = folder;
            LoadPosition = loadPosition;
        }

        public LoadData(STFReader stf)
        {
            ArgumentNullException.ThrowIfNull(stf);
            Name = stf.ReadString();
            Folder = stf.ReadString();
            string positionString = stf.ReadString();
            if (!EnumExtension.GetValue(positionString, out LoadPosition loadPosition))
                Trace.TraceWarning($"Can not parse '{positionString}' string into LoadPosition");
            LoadPosition = loadPosition;
        }

        public override bool Equals(object obj)
        {
            return obj is LoadData loadData && Equals(loadData);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Folder, LoadPosition);
        }

        public static bool operator ==(LoadData left, LoadData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LoadData left, LoadData right)
        {
            return !(left == right);
        }

        public bool Equals(LoadData other)
        {
            return other.LoadPosition == LoadPosition
                && StringComparer.OrdinalIgnoreCase.Equals(other.Name, Name)
                && StringComparer.OrdinalIgnoreCase.Equals(other.Folder, Folder);
        }
    }
}
