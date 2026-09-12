using System.Runtime.CompilerServices;
using System.Windows.Threading;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.ViewModels
{
    [TestClass]
    public class ToolboxMenuViewModelTests
    {
        [TestMethod]
        public void WhenFolderSelectionAwaitsConfirmationThenExistingRoutesRemainAvailable()
        {
            using ToolboxMenuViewModel viewModel = CreateViewModel(out FolderModel targetFolder, out RouteModel route, out _);

            viewModel.UserSelectFolder(targetFolder);

            Assert.AreSame(route, viewModel.Routes[0]);
        }

        [TestMethod]
        public void WhenFolderSelectionAwaitsConfirmationThenExistingRouteSelectionRemains()
        {
            using ToolboxMenuViewModel viewModel = CreateViewModel(out FolderModel targetFolder, out RouteModel route, out _);

            viewModel.UserSelectFolder(targetFolder);

            Assert.AreSame(route, viewModel.SelectedRoute);
        }

        [TestMethod]
        public void WhenFolderSelectionIsAcceptedThenPreviousRoutesAreClearedBeforeDiscoveryCompletes()
        {
            using ToolboxMenuViewModel viewModel = CreateViewModel(out FolderModel targetFolder, out _, out IToolboxMenu toolboxMenu);
            viewModel.UserSelectFolder(targetFolder);

            toolboxMenu.PopulateRoutes([]);

            Assert.AreEqual(0, viewModel.Routes.Count);
        }

        [TestMethod]
        public void WhenFolderSelectionIsAcceptedThenPreviousRouteSelectionIsClearedBeforeDiscoveryCompletes()
        {
            using ToolboxMenuViewModel viewModel = CreateViewModel(out FolderModel targetFolder, out _, out IToolboxMenu toolboxMenu);
            viewModel.UserSelectFolder(targetFolder);

            toolboxMenu.PopulateRoutes([]);

            Assert.IsNull(viewModel.SelectedRoute);
        }

        private static ToolboxMenuViewModel CreateViewModel(out FolderModel targetFolder, out RouteModel route, out IToolboxMenu toolboxMenu)
        {
            GameWindow gameWindow = (GameWindow)RuntimeHelpers.GetUninitializedObject(typeof(GameWindow));
            HostedToolboxMenu menu = new HostedToolboxMenu(gameWindow);
            toolboxMenu = menu;
            FolderModel currentFolder = new FolderModel("Current Folder", "C:\\Current", null);
            targetFolder = new FolderModel("Target Folder", "C:\\Target", null);
            route = new RouteModel(WorldLocation.None)
            {
                Id = "current-route",
                Name = "Current Route",
            };
            route.Initialize(currentFolder);
            toolboxMenu.PopulateContentFolders([currentFolder, targetFolder]);
            toolboxMenu.PopulateRoutes([route]);
            toolboxMenu.PreSelectRoute(route.Name);

            return new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher);
        }
    }
}
