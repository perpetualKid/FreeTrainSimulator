using System;
using System.Windows;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.ViewModels;

namespace FreeTrainSimulator.Toolbox.Dialogs
{
    /// <summary>
    /// WPF modal dialog that collects train-path metadata, replacing the legacy MonoGame
    /// <c>TrainPathSaveWindow</c> popup in hosted mode. On Save it exposes the entered values as a
    /// <see cref="PathModelHeader"/> through <see cref="PathDetails"/> and returns a true dialog result.
    /// </summary>
    public partial class TrainPathSaveDialog : Window
    {
        private readonly TrainPathSaveDialogViewModel viewModel;

        internal TrainPathSaveDialog(PathModelHeader initialPathDetails, string sourcePathId)
        {
            ArgumentNullException.ThrowIfNull(initialPathDetails);

            InitializeComponent();
            viewModel = new TrainPathSaveDialogViewModel(sourcePathId, initialPathDetails.Name, initialPathDetails.Id,
                initialPathDetails.Start, initialPathDetails.End, initialPathDetails.PlayerPath);
            DataContext = viewModel;
        }

        /// <summary>The collected path metadata, set when the dialog is confirmed with Save.</summary>
        internal PathModelHeader PathDetails { get; private set; }

        internal string SourcePathId => viewModel.SourcePathId;

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!viewModel.CanSave)
                return;

            PathDetails = new PathModelHeader
            {
                Id = viewModel.PathId.Trim(),
                Name = viewModel.PathName.Trim(),
                Start = viewModel.PathStart.Trim(),
                End = viewModel.PathEnd.Trim(),
                PlayerPath = viewModel.PlayerPath,
            };

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
