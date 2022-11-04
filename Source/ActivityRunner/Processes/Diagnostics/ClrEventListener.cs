using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Tracing;

using Orts.Common.DebugInfo;

namespace Orts.ActivityRunner.Processes.Diagnostics
{
    internal class ClrEventListener : EventListener, INameValueInformationProvider
    {
        private readonly DebugInfoBase debugInfo = new DebugInfoBase();

        private readonly string[] counters = {
            "CPU Usage",
            "Working Set",
            "Gen 0 Size",
            "Gen 1 Size",
            "Gen 2 Size",
            "Gen 0 GC Count",
            "Gen 1 GC Count",
            "Gen 2 GC Count",
            "ThreadPool Thread Count",
            "Monitor Lock Contention Count",
            "Allocation Rate",
            "GC Fragmentation,",
            "Exception Count",
            "Number of Assemblies Loaded",
        };

        public NameValueCollection DebugInfo => debugInfo.DebugInfo;

        public Dictionary<string, FormatOption> FormattingOptions => debugInfo.FormattingOptions;

        public ClrEventListener()
        {
            Array.Sort(counters);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals("System.Runtime", StringComparison.OrdinalIgnoreCase))
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string>()
                {
                    ["EventCounterIntervalSec"] = SystemProcess.UpdateInterval.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                        if (eventPayload.TryGetValue("DisplayName", out object displayName) && displayName is string displayNameString && Array.BinarySearch(counters, displayName) > -1 &&
                            (eventPayload.TryGetValue("Mean", out object value) || eventPayload.TryGetValue("Increment", out value)))
                        {
                            debugInfo[displayNameString] = $"{value} {(eventPayload.TryGetValue("DisplayUnits", out object unit) ? unit : null)}";
                        }
                    }
                }
            }
        }
    }
}
