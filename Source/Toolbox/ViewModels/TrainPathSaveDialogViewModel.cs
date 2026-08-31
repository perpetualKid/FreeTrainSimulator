using GetText;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// View model for the WPF train-path save dialog. Collects the five path-metadata fields (Path Name,
    /// Path ID, Path Start, Path End, Player Path) that replace the legacy MonoGame save popup. Save is only
    /// enabled once both an id and a name are entered.
    /// </summary>
    internal sealed class TrainPathSaveDialogViewModel : ObservableObject
    {
        private string pathName;
        private string pathId;
        private string pathStart;
        private string pathEnd;
        private bool playerPath;

        public TrainPathSaveDialogViewModel(string sourcePathId, string pathName, string pathId, string pathStart, string pathEnd, bool playerPath)
        {
            SourcePathId = sourcePathId;
            this.pathName = pathName ?? string.Empty;
            this.pathId = pathId ?? string.Empty;
            this.pathStart = pathStart ?? string.Empty;
            this.pathEnd = pathEnd ?? string.Empty;
            this.playerPath = playerPath;
        }

        public string SourcePathId { get; }

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
                {
                    OnPropertyChanged(nameof(CanSave));
                    OnPropertyChanged(nameof(IsSaveAs));
                    OnPropertyChanged(nameof(SaveActionText));
                    OnPropertyChanged(nameof(IdentityMessage));
                }
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

        public bool IsSaveAs => !string.Equals(SourcePathId, PathId?.Trim(), System.StringComparison.OrdinalIgnoreCase);

        public string SaveActionText => CatalogManager.Catalog.GetString(IsSaveAs ? "Save As" : "Save");

        public string IdentityMessage => IsSaveAs
            ? CatalogManager.Catalog.GetString("Save As creates or replaces the entered Path ID. The original path is preserved.")
            : CatalogManager.Catalog.GetString("Save updates the active path.");
    }
}
