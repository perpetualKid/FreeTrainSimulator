using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Bindable item for a single map content-type visibility toggle in the settings tool window. Uses an
    /// optimistic local backing field so the checkbox reflects the user's choice immediately; the actual write
    /// is forwarded through <see cref="apply"/>, which marshals onto the game thread.
    /// </summary>
    internal sealed class VisibilityItemViewModel : ObservableObject
    {
        private readonly Action<MapContentType, bool> apply;
        private bool isVisible;

        public VisibilityItemViewModel(string label, MapContentType setting, bool isVisible, Action<MapContentType, bool> apply)
        {
            ArgumentException.ThrowIfNullOrEmpty(label);
            ArgumentNullException.ThrowIfNull(apply);

            Label = label;
            Setting = setting;
            this.isVisible = isVisible;
            this.apply = apply;
        }

        public string Label { get; }

        public MapContentType Setting { get; }

        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (SetProperty(ref isVisible, value))
                    apply(Setting, value);
            }
        }

        /// <summary>Re-syncs the local field from the live value without re-applying it to the game side.</summary>
        internal void Refresh(bool value)
            => SetProperty(ref isVisible, value, nameof(IsVisible));
    }

    /// <summary>
    /// Bindable item for a single map color setting in the settings tool window. Uses an optimistic local
    /// backing field; the actual write is forwarded through <see cref="apply"/>, which marshals onto the game
    /// thread.
    /// </summary>
    internal sealed class ColorItemViewModel : ObservableObject
    {
        private readonly Action<ColorSetting, string> apply;
        private string selectedColorName;

        public ColorItemViewModel(string label, ColorSetting setting, string selectedColorName,
            IReadOnlyList<string> availableColorNames, Action<ColorSetting, string> apply)
        {
            ArgumentException.ThrowIfNullOrEmpty(label);
            ArgumentNullException.ThrowIfNull(availableColorNames);
            ArgumentNullException.ThrowIfNull(apply);

            Label = label;
            Setting = setting;
            this.selectedColorName = selectedColorName;
            AvailableColorNames = availableColorNames;
            this.apply = apply;
        }

        public string Label { get; }

        public ColorSetting Setting { get; }

        public IReadOnlyList<string> AvailableColorNames { get; }

        public string SelectedColorName
        {
            get => selectedColorName;
            set
            {
                if (SetProperty(ref selectedColorName, value))
                    apply(Setting, value);
            }
        }

        /// <summary>Re-syncs the local field from the live value without re-applying it to the game side.</summary>
        internal void Refresh(string value)
            => SetProperty(ref selectedColorName, value, nameof(SelectedColorName));
    }
}
