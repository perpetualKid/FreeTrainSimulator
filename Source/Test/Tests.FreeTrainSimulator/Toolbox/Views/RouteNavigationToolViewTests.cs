using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Views
{
    [STATestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class RouteNavigationToolViewTests
    {
        [TestMethod]
        [DataRow("Stations")]
        [DataRow("Platforms")]
        [DataRow("Sidings")]
        public void WhenNavigationRowIsFilteredThenItsProductionStyleCollapsesAndRestoresIt(string tabName)
        {
            RouteNavigationToolView view = new RouteNavigationToolView();
            Grid root = (Grid)view.Content;
            TabControl tabs = root.Children.OfType<TabControl>().Single();
            TabItem tab = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, tabName));
            ListView list = ((DockPanel)tab.Content).Children.OfType<ListView>().Single();
            RouteNavigationItemViewModel row = new RouteNavigationItemViewModel(1, "Station", "Group");
            ListViewItem container = new ListViewItem { DataContext = row, Style = list.ItemContainerStyle };
            container.Measure(new Size(400, 40));
            container.Arrange(new Rect(0, 0, 400, 40));
            Assert.AreEqual(Visibility.Visible, container.Visibility);

            row.Visible = false;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            Assert.AreEqual(Visibility.Collapsed, container.Visibility);

            row.Visible = true;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            Assert.AreEqual(Visibility.Visible, container.Visibility);
        }
    }
}