using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Settings dockable tool window (General, Colors, and Item Visibility tabs). Its
    /// DataContext is supplied by the shell as a <see cref="ViewModels.SettingsToolWindowViewModel"/>, so all
    /// bindings are relative to that view model. The Reset button binds to the view model's
    /// <c>ResetCommand</c>, which the shell assigns so the reset works whether the pane is docked or floating.
    /// </summary>
    internal partial class SettingsToolView : UserControl
    {
        public SettingsToolView()
        {
            InitializeComponent();
        }
    }
}
