using System.Windows.Media;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.ToolWindows;

using GetText;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>Bindable row for the available-paths list. Observable so it can be updated in place.</summary>
    internal sealed class TrainPathListItemViewModel : ObservableObject
    {
        private string id;
        private string name;
        private PathValidationState validationState;
        private bool hasUnsavedChanges;
        private bool visible = true;
        private TrainPathComparisonRow comparison;
        private Brush comparisonBrush = Brushes.Transparent;

        public TrainPathListItemViewModel(string id, string name, PathValidationState validationState)
            : this(id, name, validationState, false)
        {
        }

        public TrainPathListItemViewModel(string id, string name, PathValidationState validationState, bool hasUnsavedChanges)
        {
            this.id = id;
            this.name = name;
            this.validationState = validationState;
            this.hasUnsavedChanges = hasUnsavedChanges;
        }

        public string Id
        {
            get => id;
            private set => SetProperty(ref id, value);
        }

        public string Name
        {
            get => name;
            private set => SetProperty(ref name, value);
        }

        /// <summary>Persisted validation state of the path against the current track.</summary>
        public PathValidationState ValidationState
        {
            get => validationState;
            private set => SetProperty(ref validationState, value);
        }

        /// <summary>Whether the path holds edits that have not been persisted yet.</summary>
        public bool HasUnsavedChanges
        {
            get => hasUnsavedChanges;
            private set => SetProperty(ref hasUnsavedChanges, value);
        }

        public bool IsVisible
        {
            get => visible;
            set => SetProperty(ref visible, value);
        }

        /// <summary>Saved-only witness sent back to the owner, never a supplied full editor model.</summary>
        internal TrainPathComparisonRow Comparison => comparison;
        /// <summary>Never-persisted paths have no saved comparison row.</summary>
        public bool CanCompare => comparison != null;
        /// <summary>Authoritative comparison selection, separate from the list/editor selection.</summary>
        public bool IsCompared => comparison?.IsSelected == true;
        /// <summary>Frozen legend brush matching the map's route-local color.</summary>
        public Brush ComparisonBrush => comparisonBrush;
        /// <summary>Localized saved-baseline scope, progress, or actionable refusal.</summary>
        public string ComparisonStatus => comparison?.Error ?? (comparison == null
            ? CatalogManager.Catalog.GetString("Save this new path before comparing it.")
            : comparison.IsLoading ? CatalogManager.Catalog.GetString("Loading saved path comparison...")
            : CatalogManager.Catalog.GetString("Saved baseline only (read-only). Up to six comparisons; independent of Show Path and editor changes."));

        /// <summary>Applies immutable owner state without issuing a command or changing editor selection.</summary>
        internal void UpdateComparison(TrainPathComparisonRow row)
        {
            if (ReferenceEquals(comparison, row))
                return;
            comparison = row;
            if (row?.IsSelected == true)
            {
                SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(row.Color.A, row.Color.R, row.Color.G, row.Color.B));
                brush.Freeze();
                comparisonBrush = brush;
            }
            else
                comparisonBrush = Brushes.Transparent;
            OnPropertyChanged(nameof(CanCompare));
            OnPropertyChanged(nameof(IsCompared));
            OnPropertyChanged(nameof(ComparisonBrush));
            OnPropertyChanged(nameof(ComparisonStatus));
        }

        public void Update(string id, string name, PathValidationState validationState, bool hasUnsavedChanges)
        {
            Id = id;
            Name = name;
            ValidationState = validationState;
            HasUnsavedChanges = hasUnsavedChanges;
        }
    }
}
