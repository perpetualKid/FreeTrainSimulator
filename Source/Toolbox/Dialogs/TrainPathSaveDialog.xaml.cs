using System.Windows;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.Wpf.ViewModels;

namespace FreeTrainSimulator.Toolbox.Wpf.Dialogs
{
    /// <summary>
    /// WPF modal dialog that collects train-path metadata, replacing the legacy MonoGame
    /// <c>TrainPathSaveWindow</c> popup in hosted mode. On Save it exposes the entered values as a
    /// <see cref="PathModelHeader"/> through <see cref="PathDetails"/> and returns a true dialog result.
    /// </summary>
    public partial class TrainPathSaveDialog : Window
    {
        private readonly TrainPathSaveDialogViewModel viewModel = new TrainPathSaveDialogViewModel();

        internal TrainPathSaveDialog()
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>The collected path metadata, set when the dialog is confirmed with Save.</summary>
        internal PathModelHeader PathDetails { get; private set; }

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
