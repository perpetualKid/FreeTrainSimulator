using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

using FreeTrainSimulator.Toolbox.ViewModels;

using GetText;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>Main-route inspection with integral station selection, owned by the dockable shell.</summary>
    internal partial class PathChartToolView : UserControl
    {
        private bool synchronizingViewportScrollBar;

        public PathChartToolView()
        {
            InitializeComponent();
            Chart.ViewportChanged += ChartViewportChanged;
            SynchronizeViewportScrollBar();
            Loaded += ViewLoaded;
        }

        /// <summary>Refreshes native drawing labels; the shell's Localizer owns the static XAML strings.</summary>
        public void RefreshLocalization()
        {
            Catalog catalog = CatalogManager.Catalog;
            AutomationProperties.SetName(PathChartToolbar, catalog.GetString("Path Chart"));
            AutomationProperties.SetName(ShowOnMapButton, catalog.GetString("Show on Map"));
            AutomationProperties.SetName(ResetChartViewButton, catalog.GetString("Reset View"));
            AutomationProperties.SetName(StationList, CatalogManager.Catalog.GetString("Stations along the main route"));
            AutomationProperties.SetName(ViewportScrollBar, CatalogManager.Catalog.GetString("Visible main-route distance range"));
            ViewportScrollBar.ToolTip = CatalogManager.Catalog.GetString("Scroll to pan the zoomed path chart");
            ChartHint.Text = CatalogManager.Catalog.GetString("Mouse wheel: zoom; drag or Page Up/Down: pan; arrow keys: select distance; Home/End: route ends; +/-: zoom")
                + Environment.NewLine + CatalogManager.Catalog.GetString("Stations encountered along the main route are not scheduled stops. Speedposts are source annotations, not an operating speed profile.");
            AutomationProperties.SetHelpText(Chart, ChartHint.Text);
            Chart.RefreshLocalization();
        }

        private void ChartViewportChanged(object sender, EventArgs e)
        {
            SynchronizeViewportScrollBar();
        }

        private void SynchronizeViewportScrollBar()
        {
            synchronizingViewportScrollBar = true;
            try
            {
                ViewportScrollBar.Maximum = Chart.ViewportMaximum;
                ViewportScrollBar.ViewportSize = Chart.ViewportLength;
                ViewportScrollBar.SmallChange = Chart.ViewportLength / 20;
                ViewportScrollBar.LargeChange = Chart.ViewportLength / 2;
                ViewportScrollBar.Value = Chart.ViewportStart;
                ViewportScrollBar.IsEnabled = Chart.ViewportMaximum > 0;
            }
            finally
            {
                synchronizingViewportScrollBar = false;
            }
        }

        private void ViewportScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            e.Handled = true;
            if (!synchronizingViewportScrollBar && ViewportScrollBar.IsEnabled)
                Chart.PanViewportTo(e.NewValue);
        }

        private void ViewportScrollBar_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (ViewportScrollBar.IsEnabled)
                Chart.PanViewportTo(Chart.ViewportStart - (e.Delta / 120.0 * ViewportScrollBar.SmallChange));
        }

        private void ViewLoaded(object sender, RoutedEventArgs e)
        {
            RefreshLocalization();
        }

        private void StationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
                StationList.ScrollIntoView(e.AddedItems[0]);
        }

        private void StationList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not PathChartToolWindowViewModel viewModel
                || e.OriginalSource is not DependencyObject source
                || ItemsControl.ContainerFromElement(StationList, source) is not ListViewItem { IsSelected: true } row
                || row.DataContext is not PathChartStationItemViewModel { IsGroupingAnchor: false } station)
                return;
            for (DependencyObject current = source; current != null && !ReferenceEquals(current, row);)
            {
                if (current is Expander)
                    return;
                current = current switch
                {
                    ContentElement content => ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent,
                    System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D => System.Windows.Media.VisualTreeHelper.GetParent(current),
                    _ => LogicalTreeHelper.GetParent(current),
                };
            }
            viewModel.CursorDistance = station.Distance;
        }

        private void StationList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Left || e.Key == Key.Right) && sender is ListView list
                && list.DataContext is PathChartToolWindowViewModel viewModel)
            {
                PathChartStationItemViewModel station = e.OriginalSource is DependencyObject element
                    && ItemsControl.ContainerFromElement(list, element) is ListViewItem container
                    && container.DataContext is PathChartStationItemViewModel focused ? focused : viewModel.SelectedStation;
                viewModel.TrySetStationGroupExpanded(station, e.Key == Key.Right);
                e.Handled = true;
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            Chart.ResetViewport();
            e.Handled = true;
        }
    }
}