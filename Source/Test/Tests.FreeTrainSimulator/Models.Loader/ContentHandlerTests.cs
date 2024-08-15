using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\test123.contentfolder", StringComparison.OrdinalIgnoreCase));

            contentFolderFile = ModelFileResolver<FolderModel>.FilePath(folder);
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\TestModel.contentfolder", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task GetContentFolderTest()
        {
            ProfileModel defaultModel = ContentProfileHandler.DefaultProfile;
            FolderModel folderModel = await ContentFolderHandler.Get("Demo", defaultModel, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ConvertContentFolderTest()
        {
            ProfileModel defaultModel = null;

            defaultModel = await defaultModel.Get(CancellationToken.None).ConfigureAwait(false);
            if (null != defaultModel)
            {
                FolderModel folderModel = await ContentFolderHandler.Get("Demo Model 1", defaultModel, CancellationToken.None).ConfigureAwait(false);

                folderModel = await ContentFolderHandler.Convert(folderModel, CancellationToken.None).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task ConvertContentProfileTest()
        {
            ProfileModel defaultModel = await ContentProfileHandler.Convert(null, Enumerable.Empty<(string, string)>(), CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(VersionInfo.Version, defaultModel.Version);

            ProfileModel otherModel = await ContentProfileHandler.Convert("otherProfile", new List<(string, string)>() { ("First", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)) }, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(VersionInfo.Version, otherModel.Version);
            Assert.AreEqual(1, otherModel.ContentFolders.Count);
        }

    }
}
