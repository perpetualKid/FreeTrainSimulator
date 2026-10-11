using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Settings;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Settings
{
    [TestClass]
    public class ProfileToolboxSettingsModelTests
    {
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void WhenTabPreferenceRoundTripsThenSelectedTabIsPreserved(int selectedTabIndex)
        {
            ProfileToolboxSettingsModel settings = new() { PathEditorSelectedTabIndex = selectedTabIndex };

            ProfileToolboxSettingsModel restored = MemoryPackSerializer.Deserialize<ProfileToolboxSettingsModel>(MemoryPackSerializer.Serialize(settings));

            Assert.AreEqual(selectedTabIndex, restored.PathEditorSelectedTabIndex);
        }

        [TestMethod]
        public void WhenNewSettingsRoundTripThenPathsTabIsDefault()
        {
            ProfileToolboxSettingsModel restored = MemoryPackSerializer.Deserialize<ProfileToolboxSettingsModel>(
                MemoryPackSerializer.Serialize(new ProfileToolboxSettingsModel()));

            Assert.AreEqual(0, restored.PathEditorSelectedTabIndex);
        }

        [TestMethod]
        public void WhenPreTabSettingsAreDeserializedThenPathsTabIsDefault()
        {
            byte[] payload = MemoryPackSerializer.Serialize(new PreTabProfileToolboxSettingsModel());

            ProfileToolboxSettingsModel restored = MemoryPackSerializer.Deserialize<ProfileToolboxSettingsModel>(payload);

            Assert.AreEqual(0, restored.PathEditorSelectedTabIndex);
        }

        [TestMethod]
        public void WhenPreTabSettingsAreDeserializedThenPreviousTailIsPreserved()
        {
            byte[] payload = MemoryPackSerializer.Serialize(new PreTabProfileToolboxSettingsModel
            {
                WindowPlacements = new WindowPlacementSettings(10, 20, 800, 600, true),
            });

            ProfileToolboxSettingsModel restored = MemoryPackSerializer.Deserialize<ProfileToolboxSettingsModel>(payload);

            Assert.AreEqual(new WindowPlacementSettings(10, 20, 800, 600, true), restored.WindowPlacements);
        }

        [TestMethod]
        [DataRow(-1)]
        [DataRow(2)]
        [DataRow(int.MaxValue)]
        public void WhenSerializedTabIsInvalidThenPathsTabIsRestored(int invalidTabIndex)
        {
            byte[] payload = MemoryPackSerializer.Serialize(new ProfileToolboxSettingsModel { PathEditorSelectedTabIndex = 1 });
            // The append-only schema ends with the four-byte integer preference.
            BitConverter.GetBytes(invalidTabIndex).CopyTo(payload, payload.Length - sizeof(int));

            ProfileToolboxSettingsModel restored = MemoryPackSerializer.Deserialize<ProfileToolboxSettingsModel>(payload);

            Assert.AreEqual(0, restored.PathEditorSelectedTabIndex);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenProfileSettingsAreSavedThenTabPreferenceLoadsIndependentlyOfRestoreLastView(bool restoreLastView)
        {
            ProfileModel profile = new("tab-preference-" + Guid.NewGuid().ToString("N"));
            ProfileToolboxSettingsModel settings = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None);
            settings.RestoreLastView = restoreLastView;
            settings.PathEditorSelectedTabIndex = 1;
            await profile.UpdateSettingsModel(settings, CancellationToken.None);

            ProfileToolboxSettingsModel restored = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None);

            Assert.AreEqual(1, restored.PathEditorSelectedTabIndex);
        }
    }

    // Frozen pre-tab schema: base-first members and the existing non-version-tolerant tool-window store
    // must remain readable when the profile gains an appended preference.
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PreTabProfileToolboxSettingsModel : ProfileSettingsModelBase
    {
        public EnumArray<(int X, int Y), WindowSetting> WindowSettings { get; set; } = new ProfileToolboxSettingsModel().WindowSettings;
        public int WindowScreen { get; set; }
        public PointD ContentPosition { get; set; }
        public double ContentScale { get; set; }
        public EnumArray<string, ColorSetting> ColorSettings { get; set; } = new ProfileToolboxSettingsModel().ColorSettings;
        public EnumArray<bool, MapContentType> ViewSettings { get; set; } = new ProfileToolboxSettingsModel().ViewSettings;
        public ToolWindowSettingsStore ToolWindowSettings { get; set; } = new();
        public bool RestoreLastView { get; set; } = true;
        public bool FontOutline { get; set; } = true;
        public string Folder { get; set; }
        public string RouteId { get; set; }
        public string PathId { get; set; }
        public string DockLayoutJson { get; set; }
        public bool LimitTrackWidth { get; set; } = true;
        public WindowPlacementSettings WindowPlacements { get; set; }
    }
}
