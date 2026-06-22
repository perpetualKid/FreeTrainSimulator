namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// View model for the WPF train-path save dialog. Collects the five path-metadata fields (Path Name,
    /// Path ID, Path Start, Path End, Player Path) that replace the legacy MonoGame save popup. Save is only
    /// enabled once both an id and a name are entered.
    /// </summary>
    internal sealed class TrainPathSaveDialogViewModel : ObservableObject
    {
        private string pathName = string.Empty;
        private string pathId = string.Empty;
        private string pathStart = string.Empty;
        private string pathEnd = string.Empty;
        private bool playerPath = true;

        public string PathName
        {
            get => pathName;
            set
            {
                if (SetProperty(ref pathName, value))
                    OnPropertyChanged(nameof(CanSave));
            }
        }

        public string PathId
        {
            get => pathId;
            set
            {
                if (SetProperty(ref pathId, value))
                    OnPropertyChanged(nameof(CanSave));
            }
        }

        public string PathStart
        {
            get => pathStart;
            set => SetProperty(ref pathStart, value);
        }

        public string PathEnd
        {
            get => pathEnd;
            set => SetProperty(ref pathEnd, value);
        }

        public bool PlayerPath
        {
            get => playerPath;
            set => SetProperty(ref playerPath, value);
        }

        /// <summary>True once both id and name are non-blank, matching the legacy save behaviour.</summary>
        public bool CanSave => !string.IsNullOrWhiteSpace(PathId) && !string.IsNullOrWhiteSpace(PathName);
    }
}
