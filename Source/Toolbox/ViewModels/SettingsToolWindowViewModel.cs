using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted settings dockable tool window. Unlike the read-only snapshot tool
    /// windows this is interactive/two-way. The bound properties use optimistic local backing fields so the
    /// checkbox reflects the user's choice immediately; the actual write to the hosted
    /// <see cref="SettingsToolWindow"/> bridge is marshaled to the game thread and applied asynchronously.
    /// Reading the bridge synchronously right after a write would return the stale (not-yet-applied) value and
    /// make WPF revert the checkbox, which is why a local field is used instead of reading the bridge live.
    /// </summary>
    internal sealed class SettingsToolWindowViewModel : ObservableObject, IDisposable
    {
        // Visibility groups and labels mirror the pre-migration WinForms menu structure.
        private static readonly (string Label, MapContentType Setting)[] trackVisibilityItems =
        {
            ("Track Segments", MapContentType.Tracks),
            ("End Nodes", MapContentType.EndNodes),
            ("Junction Nodes", MapContentType.JunctionNodes),
            ("Crossover Nodes", MapContentType.Crossovers),
            ("Level Crossings", MapContentType.LevelCrossings),
        };

        private static readonly (string Label, MapContentType Setting)[] roadVisibilityItems =
        {
            ("Road Segments", MapContentType.Roads),
            ("Road End Nodes", MapContentType.RoadEndNodes),
            ("Level Crossings", MapContentType.RoadCrossings),
            ("Car Spawners", MapContentType.CarSpawners),
        };

        private static readonly (string Label, MapContentType Setting)[] interactiveVisibilityItems =
        {
            ("Main Signals", MapContentType.Signals),
            ("Other Signals", MapContentType.OtherSignals),
            ("Platforms", MapContentType.Platforms),
            ("Platform Names", MapContentType.PlatformNames),
            ("Station Names", MapContentType.StationNames),
            ("Sidings", MapContentType.Sidings),
            ("Siding Names", MapContentType.SidingNames),
            ("Speed Limits", MapContentType.SpeedPosts),
            ("Mileposts", MapContentType.MilePosts),
            ("Hazards", MapContentType.Hazards),
            ("Pickup Points (Fuel, Cargo)", MapContentType.Pickups),
            ("Sound Regions", MapContentType.SoundRegions),
        };

        private static readonly (string Label, MapContentType Setting)[] otherVisibilityItems =
        {
            ("Tile Grid", MapContentType.Grid),
            ("Show Path", MapContentType.Paths),
        };

        // Color settings and labels mirror the subset that had color pickers in the pre-migration menu.
        private static readonly (string Label, ColorSetting Setting)[] colorItems =
        {
            ("Background Color", ColorSetting.Background),
            ("Track Color", ColorSetting.RailTrack),
            ("Track End Node Color", ColorSetting.RailTrackEnd),
            ("Track Junction Node Color", ColorSetting.RailTrackJunction),
            ("Track Crossing Color", ColorSetting.RailTrackCrossing),
            ("Track Level Crossing Color", ColorSetting.RailLevelCrossing),
            ("Road Color", ColorSetting.RoadTrack),
            ("Road End Node Color", ColorSetting.RoadTrackEnd),
            ("Path Color", ColorSetting.PathTrack),
            ("Station Color", ColorSetting.StationItem),
            ("Platform Color", ColorSetting.PlatformItem),
            ("Siding Color", ColorSetting.SidingItem),
            ("SpeedPost Color", ColorSetting.SpeedPostItem),
            ("Mile Post Color", ColorSetting.MilePostItem),
        };

        private readonly SettingsToolWindow toolWindow;
        private bool enableLogging;
        private bool restoreLastView;
        private bool fontOutline;
        private bool realTrackWidth;
        private bool disposed;

        public SettingsToolWindowViewModel(SettingsToolWindow toolWindow)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);
            this.toolWindow = toolWindow;

            enableLogging = toolWindow.EnableLogging;
            restoreLastView = toolWindow.RestoreLastView;
            fontOutline = toolWindow.FontOutline;
            realTrackWidth = toolWindow.RealTrackWidth;

            TrackVisibilityItems = CreateVisibilityItems(trackVisibilityItems);
            RoadVisibilityItems = CreateVisibilityItems(roadVisibilityItems);
            InteractiveVisibilityItems = CreateVisibilityItems(interactiveVisibilityItems);
            OtherVisibilityItems = CreateVisibilityItems(otherVisibilityItems);
            ColorItems = CreateColorItems();
        }

        public string Title => toolWindow.Title;

        public ReadOnlyCollection<VisibilityItemViewModel> TrackVisibilityItems { get; }

        public ReadOnlyCollection<VisibilityItemViewModel> RoadVisibilityItems { get; }

        public ReadOnlyCollection<VisibilityItemViewModel> InteractiveVisibilityItems { get; }

        public ReadOnlyCollection<VisibilityItemViewModel> OtherVisibilityItems { get; }

        public ReadOnlyCollection<ColorItemViewModel> ColorItems { get; }

        public bool EnableLogging
        {
            get => enableLogging;
            set
            {
                if (SetProperty(ref enableLogging, value))
                    toolWindow.SetEnableLogging(value);
            }
        }

        public bool RestoreLastView
        {
            get => restoreLastView;
            set
            {
                if (SetProperty(ref restoreLastView, value))
                    toolWindow.SetRestoreLastView(value);
            }
        }

        public bool FontOutline
        {
            get => fontOutline;
            set
            {
                if (SetProperty(ref fontOutline, value))
                    toolWindow.SetFontOutline(value);
            }
        }

        public bool RealTrackWidth
        {
            get => realTrackWidth;
            set
            {
                if (SetProperty(ref realTrackWidth, value))
                    toolWindow.SetRealTrackWidth(value);
            }
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, nameof(SettingsToolWindowViewModel));

            // Re-sync the local fields from the bridge in case settings changed elsewhere while the pane was
            // hidden, so the checkboxes reflect the live state when shown. SetProperty only raises a change
            // notification when the value actually differs.
            SetProperty(ref enableLogging, toolWindow.EnableLogging, nameof(EnableLogging));
            SetProperty(ref restoreLastView, toolWindow.RestoreLastView, nameof(RestoreLastView));
            SetProperty(ref fontOutline, toolWindow.FontOutline, nameof(FontOutline));
            SetProperty(ref realTrackWidth, toolWindow.RealTrackWidth, nameof(RealTrackWidth));

            RefreshVisibilityItems(TrackVisibilityItems);
            RefreshVisibilityItems(RoadVisibilityItems);
            RefreshVisibilityItems(InteractiveVisibilityItems);
            RefreshVisibilityItems(OtherVisibilityItems);
            foreach (ColorItemViewModel item in ColorItems)
                item.Refresh(toolWindow.GetColorPreference(item.Setting));
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            disposed = true;
        }

        private ReadOnlyCollection<VisibilityItemViewModel> CreateVisibilityItems((string Label, MapContentType Setting)[] source)
        {
            List<VisibilityItemViewModel> items = new List<VisibilityItemViewModel>(source.Length);
            foreach ((string label, MapContentType setting) in source)
                items.Add(new VisibilityItemViewModel(label, setting, toolWindow.GetItemVisibility(setting), toolWindow.SetItemVisibility));
            return items.AsReadOnly();
        }

        private ReadOnlyCollection<ColorItemViewModel> CreateColorItems()
        {
            List<ColorItemViewModel> items = new List<ColorItemViewModel>(colorItems.Length);
            foreach ((string label, ColorSetting setting) in colorItems)
                items.Add(new ColorItemViewModel(label, setting, toolWindow.GetColorPreference(setting), toolWindow.AvailableColorNames, toolWindow.SetColorPreference));
            return items.AsReadOnly();
        }

        private void RefreshVisibilityItems(ReadOnlyCollection<VisibilityItemViewModel> items)
        {
            foreach (VisibilityItemViewModel item in items)
                item.Refresh(toolWindow.GetItemVisibility(item.Setting));
        }
    }
}