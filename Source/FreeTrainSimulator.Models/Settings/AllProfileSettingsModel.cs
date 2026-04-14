using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    /// <summary>
    /// Internal settings record that persists the currently active profile name and
    /// application-wide update mode. Stored as a single <c>.current</c> file under the
    /// <c>Profiles</c> folder, shared across all profiles.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Profiles", ".current")]
    internal sealed partial record AllProfileSettingsModel : ProfileSettingsModelBase
    {
        public override ProfileSettingsModelBase Parent => null;

        /// <summary>Name of the currently active user profile.</summary>
        public string Profile { get; set; }

        /// <summary>Application update channel (Release, Testing, etc.).</summary>
        public UpdateMode UpdateMode { get; set; }

        public override void Initialize(ModelBase parent)
        {
            if (parent != null)
                Trace.TraceWarning($"Parent initialization for {nameof(AllProfileSettingsModel)} is not supported");
            base.Initialize(parent);
        }
    }
}
