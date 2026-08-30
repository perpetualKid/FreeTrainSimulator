using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.PathEditing;

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

        private void PathNodes_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not ListView listView || DataContext is not TrainPathToolWindowViewModel viewModel)
                return;

            DependencyObject source = e.OriginalSource as DependencyObject;
            ListViewItem row = ItemsControl.ContainerFromElement(listView, source) as ListViewItem
                ?? listView.ItemContainerGenerator.ContainerFromItem(listView.SelectedItem) as ListViewItem;
            if (row?.DataContext is not TrainPathNodeItemViewModel node)
            {
                e.Handled = true;
                return;
            }

            listView.SelectedItem = node;
            ContextMenu menu = listView.ContextMenu;
            menu.Items.Clear();
            foreach (MapContextMenuItem action in viewModel.GetSelectedNodeActions())
            {
                if (action.IsSeparator)
                {
                    menu.Items.Add(new Separator());
                    continue;
                }

                MenuItem item = new() { Header = MainWindow.GetMapContextMenuCaption(action) };
                MapContextMenuItem capturedAction = action;
                item.Click += (_, _) => viewModel.ExecuteNodeAction(capturedAction);
                menu.Items.Add(item);
            }
            if (menu.Items.Count == 0)
            {
                e.Handled = true;
                return;
            }

            menu.PlacementTarget = row;
        }

        private void MetadataEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel)
                viewModel.CommitMetadata();
        }

        private void MetadataEditor_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel)
                viewModel.CommitMetadata();
        }

        private void PathEditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || !IsVisible || e.Source != PathEditorTabs || PathEditorTabs.SelectedIndex < 0
                || DataContext is not TrainPathToolWindowViewModel viewModel
                || Mouse.LeftButton != MouseButtonState.Pressed && !PathEditorTabs.IsKeyboardFocusWithin)
            {
                return;
            }

            viewModel.SelectedTabIndex = PathEditorTabs.SelectedIndex;
        }

        private void WaitTimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
                e.Handled = !IsValidWaitTimeEdit(textBox, e.Text);
        }

        private void WaitTimeTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox || !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (!IsValidWaitTimeEdit(textBox, pastedText))
                e.CancelCommand();
        }

        // Accept only an empty value or a non-negative Int32. Building the proposed full text also handles
        // selection replacement and prevents a sequence of individually valid digits from overflowing Int32.
        private static bool IsValidWaitTimeEdit(TextBox textBox, string insertedText)
        {
            if (textBox == null || insertedText == null || insertedText.Any(character => !char.IsDigit(character)))
                return false;

            string currentText = textBox.Text ?? string.Empty;
            string proposedText = currentText.Remove(textBox.SelectionStart, textBox.SelectionLength)
                .Insert(textBox.SelectionStart, insertedText);

            return proposedText.Length == 0 || int.TryParse(proposedText, out _);
        }
    }
}
