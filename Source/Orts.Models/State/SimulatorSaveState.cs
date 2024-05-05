using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class SimulatorSaveState : SaveStateBase
    {
        public double ClockTime { get; set; }
        public SeasonType Season { get; set; }
        public WeatherType Weather { get; set; }
        public string WeatherFile { get; set; }
        public bool TimetableMode { get; set; }

    }
}
