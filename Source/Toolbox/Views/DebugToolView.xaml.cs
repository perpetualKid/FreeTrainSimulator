using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Debug Information dockable tool window. Its DataContext is supplied by the shell
    /// as a <see cref="ViewModels.DebugToolWindowViewModel"/>, so all bindings are relative to that view model.
    /// </summary>
    internal partial class DebugToolView : UserControl
    {
        public DebugToolView()
        {
            InitializeComponent();
        }
    }
}
