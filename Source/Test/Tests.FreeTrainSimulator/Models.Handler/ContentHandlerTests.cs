using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader;
using FreeTrainSimulator.Models.Loader.Handler;
using FreeTrainSimulator.Models.Loader.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Loader
{
    [TestClass]
    public class ContentHandlerTests
    {
        [TestMethod]
        public void ResolveContentFolderFileTest()
        {
            ProfileModel profile = new ProfileModel("something");
            FolderModel folder = new FolderModel("TestModel", ".", profile);

            string contentFolderFile = ModelFileResolver<FolderModel>.FilePath("test123", profile);
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\test123.folder", StringComparison.OrdinalIgnoreCase));

            contentFolderFile = ModelFileResolver<FolderModel>.FilePath(folder);
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\TestModel.folder", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task GetContentFolderTest()
        {
            ProfileModel defaultModel = await ProfileModel.Null.Get(CancellationToken.None);
            if (null != defaultModel)
            {
                FolderModel folderModel = await FolderModelHandler.Get("Demo", defaultModel, CancellationToken.None).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task ConvertContentFolderTest()
        {
            ProfileModel defaultModel = await ProfileModel.Null.Get(CancellationToken.None);
            if (null != defaultModel)
            {
                FolderModel folderModel = await FolderModelHandler.Get("Demo Model 1", defaultModel, CancellationToken.None).ConfigureAwait(false);

                folderModel = await FolderModelHandler.Convert(folderModel, CancellationToken.None).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task ConvertContentProfileTest()
        {
            ProfileModel defaultModel = await ProfileModelHandler.Convert(null, Enumerable.Empty<(string, string)>(), CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(VersionInfo.Version, defaultModel.Version);

            ProfileModel otherModel = await ProfileModelHandler.Convert("otherProfile", new List<(string, string)>() { ("First", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)) }, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(VersionInfo.Version, otherModel.Version);
            Assert.AreEqual(1, otherModel.ContentFolders.Count);
        }

        [TestMethod]
        public async Task ConvertContentPathTest()
        {
            ProfileModel defaultModel = await ProfileModelHandler.Convert(null, Enumerable.Empty<(string, string)>(), CancellationToken.None).ConfigureAwait(false);
            FolderModel folderModel = null != defaultModel ? await defaultModel.FolderModel("Demo Model 1", CancellationToken.None).ConfigureAwait(false) : null;
            RouteModel routeModel = null != folderModel ? await folderModel.RouteModel("Monogame", CancellationToken.None).ConfigureAwait(false) : null;
            await routeModel.Convert(CancellationToken.None).ConfigureAwait(false);
            //            PathModel pathModel = null != routeModel ? await routeModel.PathModel("", CancellationToken.None).ConfigureAwait(false) : null;
        }
    }
}
