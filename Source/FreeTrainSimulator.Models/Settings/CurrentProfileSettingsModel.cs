using System.Diagnostics;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Profiles", ".current")]
    internal sealed partial record CurrentProfileSettingsModel : ProfileSettingsModelBase
    {
        public override ProfileSettingsModelBase Parent => null;

        public string Profile { get; set; }

        public override void Initialize(ModelBase parent)
        {
            if (parent != null)
                Trace.TraceWarning($"Parent initialization for {nameof(CurrentProfileSettingsModel)} is not supported");
            base.Initialize(parent);
        }
    }
}
