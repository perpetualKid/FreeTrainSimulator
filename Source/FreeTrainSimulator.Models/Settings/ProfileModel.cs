using System.Diagnostics;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    /// <summary>
    /// Represents a named user profile that serves as the root container for all profile-specific
    /// settings (user, keyboard, RailDriver, dispatcher, selections). Stored as <c>.profile</c>
    /// files under the <c>Profiles</c> folder in user data.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Profiles", ".profile")]
    public sealed partial record ProfileModel : ProfileSettingsModelBase
    {
        /// <summary>Reserved profile name used for automated testing.</summary>
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
