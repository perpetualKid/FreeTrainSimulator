using System.Buffers;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class ScriptedTrainControlSystemSaveState : SaveStateBase
    {
        public string ScriptName { get; set; }
        public ReadOnlySequence<byte> ScriptState { get; set; }
    }
}
