using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Path Editor dockable tool window (Path Nodes, Path Data, and Paths tabs).
    /// Its DataContext is supplied by the shell as a <see cref="ViewModels.TrainPathToolWindowViewModel"/>, so
    /// all bindings are relative to that view model.
    /// </summary>
    internal partial class TrainPathToolView : UserControl
    {
        public TrainPathToolView()
        {
            InitializeComponent();
        }
    }
}
