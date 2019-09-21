using System;
using System.Collections.Generic;
using System.Diagnostics;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    /// <summary>
    /// Parses Traffic Definitions in Traffic File
    /// </summary>
    public class Traffic
    {
        public string Name { get; private set; }
        public int Serial { get; private set; }
        public List<Services> Services { get; } = new List<Services>();

        public Traffic(STFReader stf)
        {
            stf.MustMatch("(");
            Name = stf.ReadString();
            stf.MustMatch("serial");
            Serial = stf.ReadIntBlock(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("service_definition", ()=>{ Services.Add(new Services(stf)); }),
            });
        }
    }

    /// <summary>
    /// Parses Traffic Definition Items in Traffic Definitions in Traffic File
    /// </summary>
    public class Services: List<TrafficDetail>
    {
        public string ServiceName { get; private set; }
        public int Time { get; private set; }

        public Services(int serviceTime)
        {
            Time = serviceTime;
        }

        public Services(STFReader stf)
        {
            int arrivalTime = 0;
            int departTime = 0;
            int skipCount = 0;
            float distanceDownPath = 0f;
            int platformStartID = 0;

            stf.MustMatch("(");
            ServiceName = stf.ReadString();
            Time = stf.ReadInt(null);   // Cannot use stt.ReadFloat(STFReader.Units.Time, null) as number will be followed by "arrivaltime"
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("arrivaltime", ()=>{ arrivalTime = (int)stf.ReadFloatBlock(STFReader.Units.Time, null); }),
                new STFReader.TokenProcessor("departtime", ()=>{ departTime = (int)stf.ReadFloatBlock(STFReader.Units.Time, null); }),
                new STFReader.TokenProcessor("skipcount", ()=>{ skipCount = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("distancedownpath", ()=>{ distanceDownPath = stf.ReadFloatBlock(STFReader.Units.Distance, null); }),
                new STFReader.TokenProcessor("platformstartid", ()=>{ platformStartID = stf.ReadIntBlock(null);
                    Add(new TrafficDetail(arrivalTime, departTime, skipCount, distanceDownPath, platformStartID, this));
                }),
            });
        }

        // This is used to convert the player data taken from the .act file into a traffic service definition for autopilot mode
        public Services(string serviceName, Player_Traffic_Definition playerTraffic)
        {
            ServiceName = serviceName;
            Time = playerTraffic.Time;

            AddRange(playerTraffic);
        }

    }

    public class TrafficDetail
    {
        public int ArrivalTime { get; private set; }
        public int DepartTime { get; private set; }
        public int SkipCount { get; private set; }
        public float DistanceDownPath { get; private set; }
        public int PlatformStartID { get; private set; }

        public TrafficDetail(int arrivalTime, int departTime, int skipCount, float distanceDownPath, int platformStartID)
        {
            ArrivalTime = arrivalTime;
            DepartTime = departTime;
            SkipCount = skipCount;
            DistanceDownPath = distanceDownPath;
            PlatformStartID = platformStartID;
        }

        public TrafficDetail(int arrivalTime, int departTime, int skipCount, float distanceDownPath, int platformStartID, Services parent)
            : this(arrivalTime, departTime, skipCount, distanceDownPath, platformStartID)

        {
            if (arrivalTime < 0)
            {
                ArrivalTime = departTime < 0 ? parent.Time : Math.Min(departTime, parent.Time);
                Trace.TraceInformation($"Train Service {parent.ServiceName} : Corrected negative arrival time within .trf or .act file");
            }
            if (departTime < 0)
            {
                DepartTime = Math.Max(ArrivalTime, parent.Time);
                Trace.TraceInformation($"Train Service {parent.ServiceName} : Corrected negative depart time within .trf or .act file");
            }
        }
    }
}
