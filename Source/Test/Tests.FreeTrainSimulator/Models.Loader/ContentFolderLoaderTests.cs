using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader;
using FreeTrainSimulator.Models.Loader.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Loader
{
    [TestClass]
    public class ContentFolderLoaderTests
    {
        [TestMethod]
        public void ResolveContentFolderFile()
        {
            ContentProfileModel profile = new ContentProfileModel("something");
            ContentFolderModel folder = new ContentFolderModel("TestModel",".", profile);

            string contentFolderFile = ModelFileResolver<ContentFolderModel>.FilePath("test123", profile);
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\test123.contentfolder", StringComparison.OrdinalIgnoreCase));

            contentFolderFile = ModelFileResolver<ContentFolderModel>.FilePath(folder);
            Assert.IsTrue(contentFolderFile.EndsWith("Content\\something\\test123.contentfolder", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task GetContentFolder()
        {
            ContentProfileModel defaultModel = ContentProfileModel.Default;
            ContentFolderModel folderModel = await ContentFolderHandler.Get("Demo", defaultModel, CancellationToken.None).ConfigureAwait(false);
        }

    }
}
