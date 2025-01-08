using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Profiles", ".current")]
    internal sealed partial record AllProfileSettingsModel : ProfileSettingsModelBase
    {
        public override ProfileSettingsModelBase Parent => null;

        public string Profile { get; set; }

        public UpdateMode UpdateMode { get; set; }

        public override void Initialize(ModelBase parent)
        {
            if (parent != null)
                Trace.TraceWarning($"Parent initialization for {nameof(AllProfileSettingsModel)} is not supported");
            base.Initialize(parent);
        }
    }
}
