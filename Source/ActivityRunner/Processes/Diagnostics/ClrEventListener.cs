using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Processes.Diagnostics
{
    internal sealed class ClrEventListener : EventListener, INameValueInformationProvider
    {
        private class ClrDebugInfo : DetailInfoBase
        {
            internal int[] GcCollections = new int[3];
            internal long[] GcSize = new long[3];

            public ClrDebugInfo() : base()
            {
                this["CLR .NET Metrics"] = null;
                this[".0"] = null;
                this["CPU Usage"] = null;
                this["Memory Size (Gen0/Gen1/Gen2)"] = null;
                this["GC Count (Gen0/Gen1/Gen2)"] = null;
                this["GC Count total (Gen0/Gen1/Gen2)"] = null;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    this["Memory Size (Gen0/Gen1/Gen2)"] = $"{FormatBytes(GcSize[0])} / {FormatBytes(GcSize[1])} / {FormatBytes(GcSize[2])}";
                    this["GC Count (Gen0/Gen1/Gen2)"] = $"{GcCollections[0]} / {GcCollections[1]} / {GcCollections[2]}";
                    this["GC Count total (Gen0/Gen1/Gen2)"] = $"{GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}";
                    base.Update(gameTime);
                }
            }
        }

        private readonly string[] counters = {
            "cpu-usage",
            "working-set",
            "gen-0-size",
            "gen-1-size",
            "gen-2-size",
            "gen-0-gc-count",
            "gen-1-gc-count",
            "gen-2-gc-count",
            "threadpool-thread-count",
            "monitor-lock-contention-count",
            "alloc-rate",
            "gc-fragmentation",
            "gc-heap-size",
            "loh-size",
            "exception-count",
            "assembly-count",
        };

        private readonly ClrDebugInfo debugInfo = new ClrDebugInfo();

        public InformationDictionary DetailInfo => debugInfo.DetailInfo;

        public Dictionary<string, FormatOption> FormattingOptions => debugInfo.FormattingOptions;

        public ClrEventListener()
        {
            Array.Sort(counters);
        }

        public void Update(GameTime gameTime)
        {
            debugInfo.Update(gameTime);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals("System.Runtime", StringComparison.OrdinalIgnoreCase))
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string>()
                {
                    ["EventCounterIntervalSec"] = "1", // SystemProcess.UpdateInterval.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName.Equals("EventCounters", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < eventData.Payload.Count; ++i)
                {
                    if (eventData.Payload[i] is IDictionary<string, object> eventPayload)
                    {
                        if (eventPayload.TryGetValue("Name", out object name) && name is string nameString && Array.BinarySearch(counters, nameString) > -1 &&
                        (eventPayload.TryGetValue("Mean", out object value) || eventPayload.TryGetValue("Increment", out value)))
                        {
                            switch (nameString)
                            {
                                case "gen-0-size":
                                    debugInfo.GcSize[0] = (long)(double)value;
                                    break;
                                case "gen-1-size":
                                    debugInfo.GcSize[1] = (long)(double)value;
                                    break;
                                case "gen-2-size":
                                    debugInfo.GcSize[2] = (long)(double)value;
                                    break;
                                case "gen-0-gc-count":
                                    debugInfo.GcCollections[0] = (int)(double)value;
                                    break;
                                case "gen-1-gc-count":
                                    debugInfo.GcCollections[1] = (int)(double)value;
                                    break;
                                case "gen-2-gc-count":
                                    debugInfo.GcCollections[2] = (int)(double)value;
                                    break;
                                default:
                                    if (eventPayload.TryGetValue("DisplayName", out object displayName) && displayName is string displayNameString && eventPayload.TryGetValue("DisplayUnits", out object unit) && unit is string unitString)
                                    {
                                        debugInfo[displayNameString] = unitString == "B" ? $"{FormatBytes((long)(double)value)}" : $"{value:N0} {unit}";
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            return bytes switch
            {
                > 8388608 => $"{bytes >> 20} MB",
                > 8192 => $"{bytes >> 10} kB",
                _ => $"{bytes} B",
            };
        }


    }
}
