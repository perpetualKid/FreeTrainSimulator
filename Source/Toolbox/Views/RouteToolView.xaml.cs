using System.Windows.Controls;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.ViewModels;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Routes dockable tool window. Its DataContext is supplied
    /// by the shell as a <see cref="ViewModels.ToolboxMenuViewModel"/>, so all bindings are relative to that
    /// view model.
    /// </summary>
    internal partial class RouteToolView : UserControl
    {
        public RouteToolView()
        {
            InitializeComponent();
        }

        // The folder/route ComboBoxes bind SelectedItem OneWay and forward user picks explicitly here. A TwoWay
        // binding is unreliable in this tool window because AvalonDock re-parents the hosted content, which
        // cycles the DataContext and silently drops the ComboBox's target-to-source write-back after the first
        // change. SelectionChanged fires reliably regardless, so it is the source of truth for user intent. The
        // resulting selection is applied back to the ComboBox by the view model once the hosted bridge confirms
        // the change (OneWay binding + PropertyChanged), mirroring the WinForms menu flow.
        private void FolderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ToolboxMenuViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is FolderModel folder)
                viewModel.UserSelectFolder(folder);
        }

        private void RouteCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ToolboxMenuViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is RouteModelHeader route)
                viewModel.UserSelectRoute(route);
        }
    }
}
