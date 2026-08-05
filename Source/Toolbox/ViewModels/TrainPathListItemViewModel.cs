using FreeTrainSimulator.Models.Content;

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

        public void Update(string id, string name, PathValidationState validationState, bool hasUnsavedChanges)
        {
            Id = id;
            Name = name;
            ValidationState = validationState;
            HasUnsavedChanges = hasUnsavedChanges;
        }
    }
}
