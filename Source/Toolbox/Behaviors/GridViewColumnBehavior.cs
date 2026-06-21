using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FreeTrainSimulator.Toolbox.Behaviors
{
    /// <summary>
    /// Attached behavior that sizes one <see cref="GridViewColumn"/> in a <see cref="ListView"/> to the
    /// remaining horizontal space. Any other columns should use <c>Width="Auto"</c> so they size to their
    /// content; the marked column then fills whatever space is left. When that column's content fits, it fills
    /// the viewport exactly (no horizontal scrollbar); when it is wider, the column extends to its content width
    /// up to a reasonable cap and a horizontal scrollbar appears so the text can be read instead of clipped. Set
    /// <see cref="StretchListViewProperty"/> to <see langword="true"/> on the <see cref="ListView"/> and mark
    /// exactly one column with <see cref="StretchColumnProperty"/> set to <see langword="true"/>.
    /// </summary>
    internal static class GridViewColumnBehavior
    {
        // Upper bound on how far the stretch column may extend beyond the available width, as a multiple of that
        // width, so a single very long entry cannot create a runaway horizontal scroll range.
        private const double maxOverflowFactor = 2.5;

        // Guards against re-entrant layout passes: setting the column width can toggle the horizontal scrollbar,
        // which changes the list's content area and would otherwise re-trigger adjustment in a loop.
        private static readonly DependencyProperty AdjustingProperty =
            DependencyProperty.RegisterAttached(
                "Adjusting",
                typeof(bool),
                typeof(GridViewColumnBehavior),
                new PropertyMetadata(false));

        // Stores the items-collection handler so it can be detached when the behavior is disabled.
        private static readonly DependencyProperty ItemsHandlerProperty =
            DependencyProperty.RegisterAttached(
                "ItemsHandler",
                typeof(NotifyCollectionChangedEventHandler),
                typeof(GridViewColumnBehavior),
                new PropertyMetadata(null));
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

                // The lists are populated from a polling snapshot after load, and auto-sized columns only settle
                // once their content exists, so re-run adjustment whenever the items collection changes.
                if (listView.Items is INotifyCollectionChanged incc)
                {
                    NotifyCollectionChangedEventHandler handler = (_, _) => AdjustStretchColumn(listView);
                    listView.SetValue(ItemsHandlerProperty, handler);
                    incc.CollectionChanged += handler;
                }
            }
            else
            {
                listView.Loaded -= ListView_Loaded;
                listView.SizeChanged -= ListView_SizeChanged;

                if (listView.GetValue(ItemsHandlerProperty) is NotifyCollectionChangedEventHandler handler
                    && listView.Items is INotifyCollectionChanged incc)
                {
                    incc.CollectionChanged -= handler;
                    listView.ClearValue(ItemsHandlerProperty);
                }
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

            if ((bool)listView.GetValue(AdjustingProperty))
                return;

            GridViewColumn stretchColumn = gridView.Columns.FirstOrDefault(column => GetStretchColumn(column));
            if (stretchColumn is null)
                return;

            // Auto-size the stretch column to its content first; the actual fill/extend decision is made on the
            // next layout pass once the other (auto) columns and this one have measured their content.
            listView.SetValue(AdjustingProperty, true);
            RefreshAutoColumns(gridView, stretchColumn);
            stretchColumn.ClearValue(GridViewColumn.WidthProperty);
            listView.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    // Use the inner scroll viewport width: it is the true content area (already excludes the
                    // vertical scrollbar and borders), so filling to it never overshoots into a phantom
                    // horizontal scrollbar the way subtracting estimated chrome from ActualWidth did.
                    double viewport = GetViewportWidth(listView);
                    if (viewport <= 0)
                        return;

                    double contentWidth = stretchColumn.ActualWidth;
                    double fixedWidth = gridView.Columns.Where(column => column != stretchColumn).Sum(column => column.ActualWidth);
                    double fillWidth = viewport - fixedWidth;
                    if (fillWidth <= 0)
                        return;

                    // Fit within the viewport when the content fits (no scrollbar); otherwise extend to the
                    // content width, capped, so a horizontal scrollbar reveals the text instead of clipping it.
                    double maxWidth = fillWidth * maxOverflowFactor;
                    stretchColumn.Width = contentWidth <= fillWidth ? fillWidth : Math.Min(contentWidth, maxWidth);
                }
                finally
                {
                    listView.SetValue(AdjustingProperty, false);
                }
            }));
        }

        // Re-applies content auto-sizing to every non-stretch column. A GridViewColumn with Width="Auto" only
        // fits its content at first measure; since these lists are populated later from a polling snapshot, the
        // auto columns must be nudged to re-measure whenever the content changes. Setting an explicit width and
        // then NaN forces GridView to recompute the column's desired (content) width.
        private static void RefreshAutoColumns(GridView gridView, GridViewColumn stretchColumn)
        {
            foreach (GridViewColumn column in gridView.Columns)
            {
                if (column == stretchColumn)
                    continue;
                column.Width = column.ActualWidth;
                column.Width = double.NaN;
            }
        }

        // Returns the width of the ListView's inner scroll viewport (content area excluding the vertical
        // scrollbar and borders), or the control width as a fallback before the template has been applied.
        private static double GetViewportWidth(ListView listView)
        {
            ScrollViewer scrollViewer = FindScrollViewer(listView);
            if (scrollViewer != null && scrollViewer.ViewportWidth > 0)
                return scrollViewer.ViewportWidth;
            return listView.ActualWidth;
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer scrollViewer)
                return scrollViewer;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                ScrollViewer result = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
