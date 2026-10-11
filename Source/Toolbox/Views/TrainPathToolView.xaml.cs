using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ViewModels;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Path Editor dockable tool window (Paths and Editor tabs).
    /// Its DataContext is supplied by the shell as a <see cref="ViewModels.TrainPathToolWindowViewModel"/>, so
    /// all bindings are relative to that view model.
    /// </summary>
    internal partial class TrainPathToolView : UserControl
    {
        public TrainPathToolView()
        {
            InitializeComponent();
            AddHandler(Button.ClickEvent, new RoutedEventHandler(EditorAction_PreviewClick), true);
            PreviewMouseDown += EditorAction_PreviewMouseDown;
            PreviewKeyDown += EditorAction_PreviewKeyDown;
            AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Editor_GotKeyboardFocus), true);
            AddHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Editor_LostKeyboardFocus), true);
        }

        private int? waitEditorNodeIndex;
        private string waitEditorPathId;
        private TextBox metadataEditor;
        private string metadataEditorPathId;
        private UIElement focusedEditorElement;

        private void Editor_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ReleaseFocusedEditorElement();
            if (e.NewFocus is UIElement element)
            {
                focusedEditorElement = element;
                element.IsVisibleChanged += FocusedEditorElement_AvailabilityChanged;
                element.IsEnabledChanged += FocusedEditorElement_AvailabilityChanged;
            }
        }

        private void Editor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus == focusedEditorElement)
                ReleaseFocusedEditorElement();
        }

        private void ReleaseFocusedEditorElement()
        {
            if (focusedEditorElement == null)
                return;

            focusedEditorElement.IsVisibleChanged -= FocusedEditorElement_AvailabilityChanged;
            focusedEditorElement.IsEnabledChanged -= FocusedEditorElement_AvailabilityChanged;
            focusedEditorElement = null;
        }

        private void FocusedEditorElement_AvailabilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not UIElement element || element.IsVisible && element.IsEnabled)
                return;

            ReleaseFocusedEditorElement();
            if (PathEditorTabs.IsVisible && PathEditorTabs.IsEnabled)
                PathEditorTabs.Focus();
            else
                Keyboard.ClearFocus();
        }

        private void EditorAction_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            if (!IsWithinEditor(WaitTimeEditor, source))
                CommitWaitEditor();

            if (metadataEditor != null && !IsWithinEditor(metadataEditor, source))
                CommitMetadataEditor();
        }

        private static bool IsWithinEditor(DependencyObject editor, DependencyObject source)
        {
            for (DependencyObject current = source; current != null;)
            {
                if (current == editor)
                    return true;

                // Routed mouse events can originate from content elements such as Run.
                current = current switch
                {
                    ContentElement content => ContentOperations.GetParent(content)
                        ?? (content as FrameworkContentElement)?.Parent,
                    System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                        => System.Windows.Media.VisualTreeHelper.GetParent(current),
                    _ => LogicalTreeHelper.GetParent(current),
                };
            }

            return false;
        }

        private void EditorAction_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && WaitTimeEditor.IsKeyboardFocusWithin)
            {
                CommitWaitEditor();

                if (DataContext is TrainPathToolWindowViewModel viewModel)
                {
                    waitEditorNodeIndex = viewModel.SelectedNode?.Index;
                    waitEditorPathId = viewModel.SelectedPath?.Id;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Space && Keyboard.FocusedElement is not TextBox)
            {
                CommitWaitEditor();
                CommitMetadataEditor();
            }
        }

        private void EditorAction_PreviewClick(object sender, RoutedEventArgs e)
        {
            CommitWaitEditor();
            CommitMetadataEditor();
        }

        private void WaitTimeEditor_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel)
            {
                waitEditorNodeIndex = viewModel.SelectedNode?.Index;
                waitEditorPathId = viewModel.SelectedPath?.Id;
            }
        }

        private void WaitTimeEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitWaitEditor();
        }

        private void CommitWaitEditor()
        {
            if (waitEditorNodeIndex.HasValue && DataContext is TrainPathToolWindowViewModel viewModel && waitEditorPathId == viewModel.SelectedPath?.Id && waitEditorNodeIndex == viewModel.SelectedNode?.Index
                && viewModel.CanEditSelectedNodeWaitTime)
                WaitTimeEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            waitEditorNodeIndex = null;
            waitEditorPathId = null;
        }

        private void NodeActions_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menu || e.Source != menu || DataContext is not TrainPathToolWindowViewModel viewModel)
                return;

            CommitWaitEditor();
            PopulateNodeActions(menu, viewModel);

            if (menu.Items.Count == 0)
                menu.Items.Add(new Separator());
        }

        private static void PopulateNodeActions(ItemsControl menu, TrainPathToolWindowViewModel viewModel)
        {
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
        }

        // The path/node/diagnostic/route-candidate lists bind SelectedItem OneWay and forward user picks here;
        // see ToolWindowSelection for why a TwoWay binding is unreliable in these hosted tool windows.
        private void PathList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out TrainPathListItemViewModel item))
            {
                CommitWaitEditor();
                CommitMetadataEditor();
                viewModel.UserSelectPath(item);
            }
        }

        private void PathList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListView listView || DataContext is not TrainPathToolWindowViewModel viewModel)
                return;

            DependencyObject source = e.OriginalSource as DependencyObject;
            ListViewItem row = ItemsControl.ContainerFromElement(listView, source) as ListViewItem;

            if (row?.IsSelected != true)
                return;

            e.Handled = true;
            CommitWaitEditor();
            CommitMetadataEditor();
            listView.SelectedItem = null;
            viewModel.UserSelectPath(null);
        }

        private void PathList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click must not select/load a different path or trigger unsaved-edit confirmation.
            e.Handled = true;
        }

        private void PathList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not ListView list || DataContext is not TrainPathToolWindowViewModel viewModel)
                return;

            list.ContextMenu.Items.Clear();
            ListViewItem row = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListViewItem;

            if (row == null && e.CursorLeft < 0)
                row = list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) as ListViewItem;

            if (row?.DataContext is not TrainPathListItemViewModel path || !viewModel.IsActivePathActionTarget(path))
            {
                e.Handled = true;
                return;
            }

            CommitWaitEditor();
            CommitMetadataEditor();
            AddPathAction(list.ContextMenu, ReResolvePathMenuItem, viewModel, path);

            if (viewModel.CanCancelNewPath)
                AddPathAction(list.ContextMenu, CancelNewPathMenuItem, viewModel, path);

            list.ContextMenu.PlacementTarget = row;
        }

        private static void AddPathAction(ContextMenu menu, MenuItem source, TrainPathToolWindowViewModel viewModel, TrainPathListItemViewModel path)
        {
            ICommand command = source.Command;
            menu.Items.Add(new MenuItem
            {
                Header = source.Header,
                Command = new RelayCommand(_ =>
                {
                    if (viewModel.IsActivePathActionTarget(path) && command.CanExecute(null))
                        command.Execute(null);

                }, _ => viewModel.IsActivePathActionTarget(path) && command.CanExecute(null)),
            });
        }

        private void NodeList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListView listView || DataContext is not TrainPathToolWindowViewModel viewModel)
                return;

            ListViewItem row = ItemsControl.ContainerFromElement(listView, e.OriginalSource as DependencyObject) as ListViewItem;

            if (row?.IsSelected != true)
                return;

            e.Handled = true;
            CommitWaitEditor();
            listView.SelectedItem = null;
            viewModel.UserSelectNode(null);
        }

        private void NodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out TrainPathNodeItemViewModel item))
            {
                CommitWaitEditor();
                viewModel.UserSelectNode(item);
            }
        }

        private void DiagnosticList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out TrainPathDiagnosticItemViewModel item))
                viewModel.UserSelectDiagnostic(item);
        }

        private void RouteCandidateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out TrainPathRouteCandidateItemViewModel item))
                viewModel.UserSelectRouteCandidate(item);
        }

        private void CandidateSpan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out TrainPathRouteCandidateItemViewModel item))
                viewModel.UserSelectCandidateSpan(item);
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

            CommitWaitEditor();
            listView.SelectedItem = node;
            viewModel.UserSelectNode(node);
            ContextMenu menu = listView.ContextMenu;
            PopulateNodeActions(menu, viewModel);
            if (menu.Items.Count == 0)
            {
                e.Handled = true;
                return;
            }

            menu.PlacementTarget = row;
        }

        private void MetadataEditor_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is TrainPathToolWindowViewModel viewModel)
            {
                metadataEditor = textBox;
                metadataEditorPathId = viewModel.SelectedPath?.Id;
            }
        }

        private void CommitMetadataEditor()
        {
            TextBox editor = metadataEditor;
            string pathId = metadataEditorPathId;
            metadataEditor = null;
            metadataEditorPathId = null;
            if (editor != null && DataContext is TrainPathToolWindowViewModel viewModel && pathId == viewModel.SelectedPath?.Id)
            {
                editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                viewModel.CommitMetadata();
                if (editor.IsKeyboardFocusWithin)
                {
                    metadataEditor = editor;
                    metadataEditorPathId = pathId;
                }
            }
        }

        private void MetadataEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitMetadataEditor();
        }

        private void MetadataEditor_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TrainPathToolWindowViewModel viewModel)
                viewModel.CommitMetadata();
        }

        private void PathEditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == PathEditorTabs)
            {
                CommitWaitEditor();
                CommitMetadataEditor();
            }
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
