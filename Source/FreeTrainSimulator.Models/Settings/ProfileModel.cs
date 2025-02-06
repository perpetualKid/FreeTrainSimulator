using System.Diagnostics;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Profiles", ".profile")]
    public sealed partial record ProfileModel : ProfileSettingsModelBase
    {
        public const string TestingProfile = "$testing";

        public override ProfileSettingsModelBase Parent => null; // Profile is root and does not implement a parent
        
        public ProfileModel(string name) : base(name, null)
        {
        }

        public override void Initialize(ModelBase parent)
        {
            if (parent != null)
                Trace.TraceWarning($"Parent initialization for {nameof(ProfileModel)} is not supported");
            base.Initialize(parent);
        }
    }
}
