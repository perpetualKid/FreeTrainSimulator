using System.Collections.ObjectModel;

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
        public SignalEnvironmentSaveState SignalEnvironmentSaveState { get; set; }
        public Collection<MovingTableSaveState> MovingTables { get; set; }
        public int ActiveMovingTable { get; set; }
        public ActivitySaveState Activity {  get; set; }
    }
}
