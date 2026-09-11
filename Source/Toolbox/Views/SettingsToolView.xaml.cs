using System.Windows.Controls;

using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

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

        // The language/color ComboBoxes bind SelectedItem OneWay and forward user picks here; see
        // ToolWindowSelection for why a TwoWay binding is unreliable in these hosted tool windows.
        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SettingsToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out LanguageOption language))
                viewModel.UserSelectLanguage(language);
        }

        // The color ComboBox lives in an ItemsControl template, so its DataContext is a per-row
        // ColorItemViewModel rather than the hosted settings view model.
        private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.DataContext is ColorItemViewModel colorItem && ToolWindowSelection.TryGetAddedItem(e, out string colorName))
                colorItem.UserSelectColorName(colorName);
        }
    }
}
