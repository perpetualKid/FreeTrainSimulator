using System;
using System.Collections.Generic;
using System.Diagnostics;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    /// <summary>
    /// Parses Traffic Definitions in Traffic File
    /// </summary>
    public class TrafficDefinition
    {
        public string Name { get; private set; }
        public int Serial { get; private set; }
        public List<ServiceDefinition> Services { get; } = new List<ServiceDefinition>();

        public TrafficDefinition(STFReader stf)
        {
            stf.MustMatch("(");
            Name = stf.ReadString();
            stf.MustMatch("serial");
            Serial = stf.ReadIntBlock(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("service_definition", ()=>{ Services.Add(new ServiceDefinition(stf)); }),
            });
        }
    }

    /// <summary>
    /// Parses Traffic Definition Items in Traffic Definitions in Traffic File
    /// </summary>
    public class ServiceDefinition
    {
        public string ServiceName { get; private set; }
        public int Time { get; private set; }
        public List<TrafficDetail> TrafficDetails { get; } = new List<TrafficDetail>();

        public ServiceDefinition(int trafficTime)
        {
            Time = trafficTime;
        }

        public ServiceDefinition(STFReader stf)
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
                    TrafficDetails.Add(new TrafficDetail(arrivalTime, departTime, skipCount, distanceDownPath, platformStartID, this));
                }),
            });
        }

        // This is used to convert the player data taken from the .act file into a traffic service definition for autopilot mode
        public ServiceDefinition(string service_Definition, Player_Traffic_Definition player_Traffic_Definition)
        {
            int arrivalTime = 0;
            int departTime = 0;
            int skipCount = 0;
            float distanceDownPath = 0f;
            int platformStartID = 0;

            ServiceName = service_Definition;
            Time = player_Traffic_Definition.Time;

            foreach (Player_Traffic_Item player_Traffic_Item in player_Traffic_Definition.Player_Traffic_List)
            {
                arrivalTime = (int)player_Traffic_Item.ArrivalTime.TimeOfDay.TotalSeconds;
                departTime = (int)player_Traffic_Item.DepartTime.TimeOfDay.TotalSeconds;
                distanceDownPath = player_Traffic_Item.DistanceDownPath;
                platformStartID = player_Traffic_Item.PlatformStartID;
                TrafficDetails.Add(new TrafficDetail(arrivalTime, departTime, skipCount, distanceDownPath, platformStartID, this));
            }
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

        public TrafficDetail(int arrivalTime, int departTime, int skipCount, float distanceDownPath, int platformStartID, ServiceDefinition parent)
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
