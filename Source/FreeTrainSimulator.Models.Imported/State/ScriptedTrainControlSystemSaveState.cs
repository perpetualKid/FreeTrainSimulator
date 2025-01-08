using System.Buffers;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class ScriptedTrainControlSystemSaveState : SaveStateBase
    {
        public string ScriptName { get; set; }
        public ReadOnlySequence<byte> ScriptState { get; set; }
    }
}
