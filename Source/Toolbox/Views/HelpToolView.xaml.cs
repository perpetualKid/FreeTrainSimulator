using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Help dockable tool window. Its DataContext is supplied by the shell as a
    /// <see cref="ViewModels.HelpToolWindowViewModel"/>, so all bindings are relative to that view model.
    /// </summary>
    internal partial class HelpToolView : UserControl
    {
        public HelpToolView()
        {
            InitializeComponent();
        }
    }
}
