using System.Windows;

using AvalonDock.Layout;

using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Views
{
    [STATestClass]
    public class ToolWindowTests
    {
        [TestMethod]
        [DataRow(0d, 0d, 520d, 560d)]
        [DataRow(620d, 0d, 620d, 560d)]
        [DataRow(0d, 680d, 520d, 680d)]
        [DataRow(620d, 680d, 620d, 680d)]
        [DataRow(100d, 100d, 100d, 100d)]
        public void WhenAutoHideSizeIsInitializedThenOnlyMissingDimensionsUseViewDefaults(
            double width, double height, double expectedWidth, double expectedHeight)
        {
            FrameworkElement view = new();
            ToolWindow.SetDefaultFloatingSize(view, new Size(520, 560));
            LayoutAnchorable anchorable = new()
            {
                Content = view,
                AutoHideWidth = width,
                AutoHideHeight = height,
            };

            ToolWindow.ApplyDefaultAutoHideSize(anchorable);

            Assert.AreEqual(new Size(expectedWidth, expectedHeight), new Size(anchorable.AutoHideWidth, anchorable.AutoHideHeight));
        }

        [TestMethod]
        public void WhenRestoredAutoHideSizeIsMissingThenSavedFloatingSizeIsPreserved()
        {
            FrameworkElement view = new();
            ToolWindow.SetDefaultFloatingSize(view, new Size(520, 560));
            LayoutAnchorable anchorable = new()
            {
                Content = view,
                FloatingWidth = 730,
                FloatingHeight = 810,
            };

            ToolWindow.ApplyDefaultAutoHideSize(anchorable);

            Assert.AreEqual(new Size(730, 810), new Size(anchorable.FloatingWidth, anchorable.FloatingHeight));
        }

        [TestMethod]
        public void WhenViewHasNoDefaultSizeThenAutoHideDimensionsRemainUnset()
        {
            LayoutAnchorable anchorable = new() { Content = new FrameworkElement() };

            ToolWindow.ApplyDefaultAutoHideSize(anchorable);

            Assert.AreEqual(new Size(0, 0), new Size(anchorable.AutoHideWidth, anchorable.AutoHideHeight));
        }

        [TestMethod]
        public void WhenContentIsNotAViewThenAutoHideDimensionsRemainUnset()
        {
            LayoutAnchorable anchorable = new() { Content = "Content" };

            ToolWindow.ApplyDefaultAutoHideSize(anchorable);

            Assert.AreEqual(new Size(0, 0), new Size(anchorable.AutoHideWidth, anchorable.AutoHideHeight));
        }
    }
}
