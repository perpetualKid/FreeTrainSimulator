using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader;
using FreeTrainSimulator.Models.Loader.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Loader
{
    [TestClass]
    public class ContentHandlerTests
    {
        [TestMethod]
        public void ResolveContentFolderFile()
        {
            ContentProfileModel profile = new ContentProfileModel("something");
            ContentFolderModel folder = new ContentFolderModel("TestModel", ".", profile);

            string contentFolderFile = ModelFileResolver<ContentFolderModel>.FilePath("test123", profile);
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\test123.contentfolder", StringComparison.OrdinalIgnoreCase));

            contentFolderFile = ModelFileResolver<ContentFolderModel>.FilePath(folder);
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\TestModel.contentfolder", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task GetContentFolder()
        {
            ContentProfileModel defaultModel = ContentProfileHandler.DefaultProfile;
            ContentFolderModel folderModel = await ContentFolderHandler.Get("Demo", defaultModel, CancellationToken.None).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ConvertContentProfile()
        {
            ContentProfileModel defaultModel = await ContentProfileHandler.Convert(null, Enumerable.Empty<(string, string)>(), CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(VersionInfo.Version, defaultModel.Version);

            ContentProfileModel otherModel = await ContentProfileHandler.Convert("otherProfile", new List<(string, string)>() { ("First", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))}, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(VersionInfo.Version, otherModel.Version);
            Assert.AreEqual(1, otherModel.ContentFolders.Count);
        }

    }
}
