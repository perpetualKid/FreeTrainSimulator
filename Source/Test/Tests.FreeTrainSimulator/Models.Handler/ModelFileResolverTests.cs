using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Handler
{
    [TestClass]
    public class ModelFileResolverTests
    {

        [TestMethod]
        public async Task ContentModelTest()
        {
            ContentModel model = await ContentModel.None.Get(CancellationToken.None).ConfigureAwait(false);
            Assert.IsNotNull(model);
        }

        [TestMethod]
        public void ContentModelFolderPathTest()
        {
            ContentModel model = new ContentModel();
            string genericTargetFileName = ModelFileResolver<ContentModel>.FolderPath(model);
            Assert.IsTrue(genericTargetFileName.EndsWith("\\Content", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void CurrentProfileFilePathTest()
        {
            AllProfileSettingsModel current = new AllProfileSettingsModel
            {
                Profile = "Something"
            };
            string targetFileName = ModelFileResolver<AllProfileSettingsModel>.FilePath(current);
            Assert.IsTrue(targetFileName.EndsWith($"{ModelFileResolver<AllProfileSettingsModel>.SubFolder}\\{ModelFileResolver<AllProfileSettingsModel>.FileExtension}", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ProfileExtensionTest()
        {
            string profileModelExtension = ModelFileResolver<ProfileModel>.FileExtension;
            Assert.AreEqual(".profile", profileModelExtension);
        }

        [TestMethod]
        public void ProfileSubFolderTest()
        {
            string profileFolder = ModelFileResolver<ProfileModel>.SubFolder;
            Assert.AreEqual("Profiles", profileFolder);
        }

        [TestMethod]
        public void ProfileFilePathTest()
        {
            string targetFileName = ModelFileResolver<ProfileModel>.FilePath<ProfileModel>("Default", null);
            Assert.IsTrue(targetFileName.EndsWith("Profiles\\Default.profile", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task ProfileSelectionsExtensionTest()
        {
            ProfileModel profile = new ProfileModel("TestDefault");
            ProfileSelectionsModel profileSelections = new ProfileSelectionsModel() { Id = "TestDefault" };
            profileSelections.Initialize(profile);
            Assert.AreEqual(profileSelections, await ProfileSettingModelHandler<ProfileSelectionsModel>.FromFile(profileSelections, CancellationToken.None).ConfigureAwait(false));
            string targetFileName = ModelFileResolver<ProfileSelectionsModel>.FilePath(profileSelections);
            Assert.IsTrue(targetFileName.EndsWith("Profiles\\TestDefault\\TestDefault.selections", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void MultipleExtensionTest()
        {
            string profileModelExtension = ModelFileResolver<ProfileModel>.FileExtension;
            string pathModelExtension = ModelFileResolver<PathModelHeader>.FileExtension;
            Assert.AreEqual(".profile", profileModelExtension);
            Assert.AreEqual(".path", pathModelExtension);
        }

        [TestMethod]
        public void PathExtensionTest()
        {
            string pathModelExtension = ModelFileResolver<PathModelHeader>.FileExtension;
            Assert.AreEqual(".path", pathModelExtension);
        }

        [TestMethod]
        public void PathSubFolderTest()
        {
            string profileModelExtension = ModelFileResolver<PathModelHeader>.SubFolder;
            Assert.AreEqual("TrainPaths", profileModelExtension);
        }

        [TestMethod]
        public void RouteFilePathTest()
        {
            ContentModel content = new ContentModel();
            FolderModel folder = new FolderModel("RouteTestFolder", "C:\\", content);
            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = "RouteFilePathTest",
            };
            routeModel.Initialize(folder);
            string targetFileName = ModelFileResolver<RouteModelHeader>.FilePath(routeModel);
            Assert.IsTrue(targetFileName.EndsWith($"Content\\{folder.Name}\\{routeModel.Id}.route", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void PathFilePathTest()
        {
            ContentModel content = new ContentModel(ImmutableArray<FolderModel>.Empty);
            FolderModel folder = new FolderModel("RouteTestFolder", "C:\\", content);
            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = "RouteFilePathTest",
            };
            routeModel.Initialize(folder);
            PathModelHeader pathModel = new PathModelHeader()
            {
                Id = "PathFilePathTest",
            };
            pathModel.Initialize(routeModel);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(pathModel);
            Assert.IsTrue(targetFileName.EndsWith(Path.Combine("Content", content.Name, folder.Name, routeModel.Id, "TrainPaths", pathModel.Id + ModelFileResolver<PathModelHeader>.FileExtension), System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void PathFileFromNamePathTest()
        {
            ContentModel content = new ContentModel(ImmutableArray<FolderModel>.Empty);
            FolderModel folder = new FolderModel("RouteTestFolder", "C:\\", content);
            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = "RouteFilePathTest",
            };
            routeModel.Initialize(folder);
            string pathId = "PathFilePathTest";
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath<RouteModelHeader>(pathId, routeModel);
            Assert.IsTrue(targetFileName.EndsWith(Path.Combine("Content", content.Name, folder.Name, routeModel.Id, "TrainPaths", pathId + ModelFileResolver<PathModelHeader>.FileExtension), System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void PathFolderPathTest()
        {
            ContentModel content = new ContentModel(ImmutableArray<FolderModel>.Empty);
            FolderModel folder = new FolderModel("TestFolder", "C:\\", content);
            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = "TestRoute",
            };
            routeModel.Initialize(folder);
            string targetRouteFolderName = ModelFileResolver<RouteModelHeader>.FolderPath(routeModel);
            string targetPathFolderName = ModelFileResolver<PathModelHeader>.FolderPath(routeModel);
            Assert.IsTrue(Path.GetRelativePath(targetPathFolderName, targetRouteFolderName) == "..");
            Assert.IsTrue(targetPathFolderName.EndsWith(Path.Combine("Content", content.Name, folder.Name, routeModel.Id, "TrainPaths"), System.StringComparison.OrdinalIgnoreCase));
        }

    }
}
