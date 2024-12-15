using System;
using System.Collections.Frozen;
using System.Diagnostics;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Profiles", ".profile")]
    public sealed partial record ProfileModel : ProfileSettingsModelBase
    {
        public override ProfileSettingsModelBase Parent => null; // Profile is root and does not implement a parent
        
        [MemoryPackIgnore]
        public static ProfileModel None { get; } = default(ProfileModel);

        public ProfileModel(string name) : base(name, null)
        {
        }

        public override void Initialize(ModelBase parent)
        {
            if (parent != null)
                Trace.TraceWarning($"Parent initialization for {nameof(ProfileModel)} is not supported");
            base.Initialize(parent);
        }

        public bool Equals(ProfileModel other)
        {
            return other != null && other.Name == Name && other.Version == Version;
        }

        [DebuggerStepThrough]
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Version);
        }
    }
}
