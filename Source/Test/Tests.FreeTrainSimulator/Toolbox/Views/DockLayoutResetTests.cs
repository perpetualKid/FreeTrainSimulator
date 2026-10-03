using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Serializer.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Views
{
    [STATestClass]
    public class DockLayoutResetTests
    {
        [TestMethod]
        public void WhenOpenAutoHideFlyoutIsRetiredBeforeLayoutReplacementThenContentIsReusedWithoutStaleFlyout()
        {
            FrameworkElement toolView = new();
            FrameworkElement mapView = new();
            LayoutAnchorable tool = new() { ContentId = "Settings", Title = "Settings", Content = toolView };
            LayoutDocument map = new() { ContentId = "Map", Title = "Map", Content = mapView };
            LayoutAnchorablePane toolPane = new();
            toolPane.Children.Add(tool);
            LayoutDocumentPane mapPane = new();
            mapPane.Children.Add(map);
            LayoutPanel panel = new();
            panel.Children.Add(toolPane);
            panel.Children.Add(mapPane);
            DockingManager manager = new() { Layout = new LayoutRoot { RootPanel = panel } };
            Window window = new() { Content = manager, Width = 800, Height = 600, ShowInTaskbar = false };

            try
            {
                window.Show();
                window.UpdateLayout();
                using MemoryStream defaultLayout = new();
                new JsonLayoutSerializer(manager).Serialize(defaultLayout);
                tool.ToggleAutoHide();
                window.UpdateLayout();
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
                tool.IsActive = true;
                window.UpdateLayout();
                Assert.AreSame(tool, manager.AutoHideWindow.Model);

                tool.HideAnchorable(false);
                defaultLayout.Position = 0;
                new JsonLayoutSerializer(manager).Deserialize(defaultLayout);

                LayoutAnchorable restoredTool = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single();
                LayoutDocument restoredMap = manager.Layout.Descendents().OfType<LayoutDocument>().Single();
                Assert.AreEqual((true, true, true),
                    (manager.AutoHideWindow.Model == null, ReferenceEquals(toolView, restoredTool.Content), ReferenceEquals(mapView, restoredMap.Content)));
            }
            finally
            {
                window.Close();
            }
        }
    }
}
