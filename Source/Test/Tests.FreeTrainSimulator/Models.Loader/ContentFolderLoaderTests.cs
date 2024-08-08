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
        public async Task LoadContentFolder()
        {
            ContentProfileModel profile = new ContentProfileModel("something");
            ContentFolderModel folder = new ContentFolderModel("TestModel",".", profile);

            string contentFolderFile = ModelFileResolver<ContentFolderModel>.FilePath("test123", profile);

            contentFolderFile = ModelFileResolver<ContentFolderModel>.FilePath(folder);

            //ContentProfileModel result = await ContentProfileLoader.Load(CancellationToken.None).ConfigureAwait(false);

            //ContentProfileModel otherresult = await ContentProfileLoader.Load(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
