using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Two-row column chrome. The owning view model supplies filter text, row visibility, and toggling;
    /// this control never accesses list items or decides which rows match.
    /// </summary>
    internal partial class ColumnFilterHeader : UserControl
    {
        /// <summary>The permanent upper-row caption.</summary>
        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
            nameof(Caption), typeof(string), typeof(ColumnFilterHeader), new PropertyMetadata(string.Empty));

        /// <summary>The owner's filter text, updated as the user types or clears it.</summary>
        public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(
            nameof(FilterText), typeof(string), typeof(ColumnFilterHeader),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>List-wide lower-row visibility, including space reserved by nonsearchable columns.</summary>
        public static readonly DependencyProperty IsFilterRowVisibleProperty = DependencyProperty.Register(
            nameof(IsFilterRowVisible), typeof(bool), typeof(ColumnFilterHeader), new PropertyMetadata(false));

        /// <summary>Whether this column presents filter controls rather than reserved lower-row space.</summary>
        public static readonly DependencyProperty IsSearchableProperty = DependencyProperty.Register(
            nameof(IsSearchable), typeof(bool), typeof(ColumnFilterHeader), new PropertyMetadata(false));

        /// <summary>The owner's command deciding whether the list-wide filter row can toggle.</summary>
        public static readonly DependencyProperty ToggleFilterRowCommandProperty = DependencyProperty.Register(
            nameof(ToggleFilterRowCommand), typeof(ICommand), typeof(ColumnFilterHeader), new PropertyMetadata(null));

        /// <summary>Gets or sets the permanent column caption.</summary>
        public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

        /// <summary>Gets or sets filter text without applying any filtering predicate.</summary>
        public string FilterText { get => (string)GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }

        /// <summary>Gets or sets the owner's list-wide lower-row visibility.</summary>
        public bool IsFilterRowVisible { get => (bool)GetValue(IsFilterRowVisibleProperty); set => SetValue(IsFilterRowVisibleProperty, value); }

        /// <summary>Gets or sets whether the column offers a text filter.</summary>
        public bool IsSearchable { get => (bool)GetValue(IsSearchableProperty); set => SetValue(IsSearchableProperty, value); }

        /// <summary>Gets or sets the command that toggles the owner's filter row.</summary>
        public ICommand ToggleFilterRowCommand { get => (ICommand)GetValue(ToggleFilterRowCommandProperty); set => SetValue(ToggleFilterRowCommandProperty, value); }

        /// <summary>Creates column chrome with view-local resources.</summary>
        public ColumnFilterHeader()
        {
            InitializeComponent();
        }

        private void FilterToggle_Click(object sender, RoutedEventArgs e)
        {
            // Button executes its command after Click, so focus after the visibility binding settles.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
            {
                if (IsFilterRowVisible && IsSearchable)
                    FilterEditor.Focus();
            }));
            e.Handled = true;
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterEditor.Clear();
            FilterEditor.Focus();
            e.Handled = true;
        }
    }
}
