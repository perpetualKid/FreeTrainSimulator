using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Behaviors
{
    /// <summary>
    /// Attached behavior that makes one <see cref="GridViewColumn"/> in a <see cref="ListView"/> fill the
    /// remaining horizontal space, so tool-window lists adapt to the panel width instead of using fixed column
    /// widths. Set <see cref="StretchListViewProperty"/> to <see langword="true"/> on the <see cref="ListView"/>
    /// and mark exactly one column with <see cref="StretchColumnProperty"/> set to <see langword="true"/>.
    /// </summary>
    internal static class GridViewColumnBehavior
    {
        // Marker set on the single column that should absorb the remaining width.
        public static readonly DependencyProperty StretchColumnProperty =
            DependencyProperty.RegisterAttached(
                "StretchColumn",
                typeof(bool),
                typeof(GridViewColumnBehavior),
                new PropertyMetadata(false));

        public static bool GetStretchColumn(GridViewColumn column) => (bool)column.GetValue(StretchColumnProperty);

        public static void SetStretchColumn(GridViewColumn column, bool value) => column.SetValue(StretchColumnProperty, value);

        // Activator set on the ListView; hooks layout events to keep the stretch column sized to the view.
        public static readonly DependencyProperty StretchListViewProperty =
            DependencyProperty.RegisterAttached(
                "StretchListView",
                typeof(bool),
                typeof(GridViewColumnBehavior),
                new PropertyMetadata(false, OnStretchListViewChanged));

        public static bool GetStretchListView(ListView listView) => (bool)listView.GetValue(StretchListViewProperty);

        public static void SetStretchListView(ListView listView, bool value) => listView.SetValue(StretchListViewProperty, value);

        private static void OnStretchListViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ListView listView)
                return;

            if ((bool)e.NewValue)
            {
                listView.Loaded += ListView_Loaded;
                listView.SizeChanged += ListView_SizeChanged;
            }
            else
            {
                listView.Loaded -= ListView_Loaded;
                listView.SizeChanged -= ListView_SizeChanged;
            }
        }

        private static void ListView_Loaded(object sender, RoutedEventArgs e) => AdjustStretchColumn((ListView)sender);

        private static void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                AdjustStretchColumn((ListView)sender);
        }

        private static void AdjustStretchColumn(ListView listView)
        {
            if (listView.View is not GridView gridView)
                return;

            GridViewColumn stretchColumn = gridView.Columns.FirstOrDefault(column => GetStretchColumn(column));
            if (stretchColumn is null)
                return;

            // Reserve space for a possible vertical scrollbar so the stretch column never forces a horizontal one.
            double available = listView.ActualWidth - SystemParameters.VerticalScrollBarWidth - 2;
            double fixedWidth = gridView.Columns.Where(column => column != stretchColumn).Sum(column => column.ActualWidth);
            double newWidth = available - fixedWidth;

            if (newWidth > 0)
                stretchColumn.Width = newWidth;
        }
    }
}
