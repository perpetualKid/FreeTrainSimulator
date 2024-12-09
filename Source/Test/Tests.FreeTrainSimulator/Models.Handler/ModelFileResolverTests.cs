using System.Collections.Frozen;
using System.Collections.Generic;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Handler
{
    [TestClass]
    public class ModelFileResolverTests
    {        
        [TestMethod]
        public void ProfileExtensionTest()
        {
            string profileModelExtension = ModelFileResolver<ProfileModel>.FileExtension;
            Assert.AreEqual(".profile", profileModelExtension);
        }

        [TestMethod]
        public void ProfileSubFolderTest()
        {
            string profileModelExtension = ModelFileResolver<ProfileModel>.SubFolder;
            Assert.AreEqual(string.Empty, profileModelExtension);
        }

        [TestMethod]
        public void ProfileFilePathTest()
        {
            string targetFileName = ModelFileResolver<ProfileModel>.FilePath<ProfileModel>("Default", null);
            Assert.IsTrue(targetFileName.EndsWith("Content\\Default.profile", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void PathExtensionTest()
        {
            string profileModelExtension = ModelFileResolver<PathModelCore>.FileExtension;
            Assert.AreEqual(".path", profileModelExtension);
        }

        [TestMethod]
        public void PathSubFolderTest()
        {
            string profileModelExtension = ModelFileResolver<PathModelCore>.SubFolder;
            Assert.AreEqual("TrainPaths", profileModelExtension);
        }

        [TestMethod]
        public void RouteFilePathTest()
        {
            ProfileModel profile = new ProfileModel("Default", FrozenSet<FolderModel>.Empty);
            FolderModel folder = new FolderModel("RouteTestFolder", "C:\\", profile);
            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = "RouteFilePathTest",
            };
            routeModel.Initialize(folder);
            string targetFileName = ModelFileResolver<RouteModelCore>.FilePath(routeModel);
            Assert.IsTrue(targetFileName.EndsWith($"Content\\{profile.Name}\\{folder.Name}\\{routeModel.Id}.route", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void PathFilePathTest()
        {
            ProfileModel profile = new ProfileModel("Default", FrozenSet<FolderModel>.Empty);
            FolderModel folder = new FolderModel("RouteTestFolder", "C:\\", profile);
            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = "RouteFilePathTest",
            };
            routeModel.Initialize(folder);
            PathModelCore pathModel = new PathModelCore()
            {
                Id = "PathFilePathTest",
            };
            pathModel.Initialize(routeModel);
            string targetFileName = ModelFileResolver<PathModelCore>.FilePath(pathModel);
            Assert.IsTrue(targetFileName.EndsWith($"Content\\{profile.Name}\\{folder.Name}\\{routeModel.Id}\\TrainPaths\\{pathModel.Id}.path", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void PathFileFromNamePathTest()
        {
            ProfileModel profile = new ProfileModel("Default", FrozenSet<FolderModel>.Empty);
            FolderModel folder = new FolderModel("RouteTestFolder", "C:\\", profile);
            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = "RouteFilePathTest",
            };
            routeModel.Initialize(folder);
            string pathId = "PathFilePathTest";
            string targetFileName = ModelFileResolver<PathModelCore>.FilePath<RouteModelCore>(pathId, routeModel);
            Assert.IsTrue(targetFileName.EndsWith($"Content\\{profile.Name}\\{folder.Name}\\{routeModel.Id}\\TrainPaths\\{pathId}.path", System.StringComparison.OrdinalIgnoreCase));
        }

    }
}
