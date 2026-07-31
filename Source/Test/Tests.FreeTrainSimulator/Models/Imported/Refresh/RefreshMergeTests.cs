using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.ImportHandler;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Imported.Refresh
{
    [TestClass]
    public class RefreshMergeTests
    {
        [TestMethod]
        public async Task ConvertContentRefreshesModelNativeRouteWhenRouteVersionIsOutdated()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel folderModel = new FolderModel($"Folder-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-content-{uniqueSuffix}"), contentModel);

            contentModel = contentModel with
            {
                ContentFolders = ImmutableArray.Create(folderModel),
            };
            contentModel.Initialize(null);

            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = $"Route-Outdated-{uniqueSuffix}",
                Name = "Outdated Model Native Route",
                Version = "0.0.0",
                RouteKey = "OutdatedRouteKey",
                Settings = new Dictionary<string, string> { { "SampleSetting", "SampleValue" } }.ToImmutableDictionary(),
            };
            routeModel.Initialize(folderModel);

            await SaveRouteModelAsync(routeModel, CancellationToken.None).ConfigureAwait(false);

            _ = await ContentModelConverter.ConvertContent(contentModel, true, CancellationToken.None).ConfigureAwait(false);

            RouteModel refreshedRoute = await RouteModelHandler.GetExtended(routeModel.Id, folderModel, CancellationToken.None).ConfigureAwait(false);
            Assert.IsNotNull(refreshedRoute);
            Assert.IsLessThanOrEqualTo(0, VersionInfo.Compare(refreshedRoute.Version));
            Assert.IsFalse(RouteModelImportHandler.IsSourceBackedRoute(refreshedRoute));
            Assert.AreEqual("OutdatedRouteKey", refreshedRoute.RouteKey);
            Assert.IsTrue(refreshedRoute.Settings.ContainsKey("SampleSetting"));
        }

        [TestMethod]
        public async Task RefreshPersistedRouteModelPreservesExtendedDataWhenMigratingOutdatedRoute()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel folderModel = new FolderModel($"Folder-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-content-{uniqueSuffix}"), contentModel);
            folderModel.Initialize(contentModel);

            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = $"Route-Extended-{uniqueSuffix}",
                Name = "Extended Model Native Route",
                Version = "0.0.0",
                RouteKey = "ExtendedRouteKey",
                Settings = new Dictionary<string, string> { { "SampleSetting", "SampleValue" } }.ToImmutableDictionary(),
            };
            routeModel.Initialize(folderModel);

            await SaveRouteModelAsync(routeModel, CancellationToken.None).ConfigureAwait(false);

            // GetCore returns a header-only instance, mirroring the input the refresh pipeline feeds the migration.
            RouteModelHeader persistedHeader = await RouteModelHandler.GetCore(routeModel.Id, folderModel, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(persistedHeader is RouteModel);

            _ = await RouteModelImportHandler.RefreshPersistedRouteModel(persistedHeader, CancellationToken.None).ConfigureAwait(false);

            RouteModel reloaded = await ReadPersistedRouteModelAsync(routeModel, CancellationToken.None).ConfigureAwait(false);
            Assert.IsNotNull(reloaded);
            Assert.AreEqual("ExtendedRouteKey", reloaded.RouteKey);
            Assert.IsTrue(reloaded.Settings.ContainsKey("SampleSetting"));
            Assert.IsLessThanOrEqualTo(0, VersionInfo.Compare(reloaded.Version));
        }

        [TestMethod]
        public async Task ExpandRouteModelsPreservesPersistedModelNativeRouteWhenLegacySourceIsUnavailable()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel folderModel = new FolderModel($"Folder-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-content-{uniqueSuffix}"), contentModel);
            folderModel.Initialize(contentModel);

            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = $"Route-{uniqueSuffix}",
                Name = "Model Native Route",
            };
            routeModel.Initialize(folderModel);

            await SaveRouteModelAsync(routeModel, CancellationToken.None).ConfigureAwait(false);

            ImmutableArray<RouteModelHeader> routes = await RouteModelImportHandler.ExpandRouteModels(folderModel, CancellationToken.None).ConfigureAwait(false);

            RouteModelHeader preservedRoute = routes.Single(route => string.Equals(route.Id, routeModel.Id, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(routeModel.Name, preservedRoute.Name);
            Assert.IsFalse(RouteModelImportHandler.IsSourceBackedRoute(preservedRoute));
        }

        [TestMethod]
        public async Task ExpandRouteModelsPreservesPersistedSourceBackedRouteWhenSourceIsUnavailable()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel folderModel = new FolderModel($"Folder-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-content-{uniqueSuffix}"), contentModel);
            folderModel.Initialize(contentModel);

            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = $"Route-SourceBacked-{uniqueSuffix}",
                Name = "Persisted Source Backed Route",
                Tags = new Dictionary<string, string>
                {
                    { RouteModelImportHandler.SourceNameKey, $"MissingRoute-{uniqueSuffix}" },
                }.ToImmutableDictionary(),
            };
            routeModel.Initialize(folderModel);

            await SaveRouteModelAsync(routeModel, CancellationToken.None).ConfigureAwait(false);

            ImmutableArray<RouteModelHeader> routes = await RouteModelImportHandler.ExpandRouteModels(folderModel, CancellationToken.None).ConfigureAwait(false);

            RouteModelHeader preservedRoute = routes.Single(route => string.Equals(route.Id, routeModel.Id, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(RouteModelImportHandler.IsSourceBackedRoute(preservedRoute));
            Assert.IsFalse(RouteModelImportHandler.HasResolvableSourceRoute(preservedRoute));
        }

        [TestMethod]
        public async Task ExpandFolderModelsPreservesConfiguredFolderWhenNotPresentInLegacyDiscovery()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel configuredFolder = new FolderModel($"Configured-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-configured-{uniqueSuffix}"), contentModel);

            contentModel = contentModel with
            {
                ContentFolders = ImmutableArray.Create(configuredFolder),
            };
            contentModel.Initialize(null);

            ImmutableArray<FolderModel> refreshedFolders = await FolderModelImportHandler.ExpandFolderModels(contentModel, CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(refreshedFolders.Any(folder => string.Equals(folder.Id, configuredFolder.Id, StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void MergeFoldersForRefreshPreservesConfiguredAndAddsMissingLegacyFolders()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            string sharedPath = Path.Combine(Path.GetTempPath(), $"fts-shared-{uniqueSuffix}");

            ContentModel contentModel = new ContentModel();
            FolderModel configuredSharedFolder = new FolderModel($"ConfiguredShared-{uniqueSuffix}", sharedPath, contentModel);
            FolderModel configuredOnlyFolder = new FolderModel($"ConfiguredOnly-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-configured-only-{uniqueSuffix}"), contentModel);

            contentModel = contentModel with
            {
                ContentFolders = ImmutableArray.Create(configuredSharedFolder, configuredOnlyFolder),
            };
            contentModel.Initialize(null);

            FolderModel legacySharedFolder = new FolderModel($"LegacyShared-{uniqueSuffix}", sharedPath, contentModel);
            FolderModel legacyOnlyFolder = new FolderModel($"LegacyOnly-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-legacy-only-{uniqueSuffix}"), contentModel);

            ImmutableArray<FolderModel> mergedFolders = FolderModelImportHandler.MergeFoldersForRefresh(contentModel, ImmutableArray.Create(legacySharedFolder, legacyOnlyFolder));

            Assert.HasCount(3, mergedFolders);
            Assert.IsTrue(mergedFolders.Any(folder => string.Equals(folder.Id, configuredSharedFolder.Id, StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(mergedFolders.Any(folder => string.Equals(folder.Id, configuredOnlyFolder.Id, StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(mergedFolders.Any(folder => string.Equals(folder.Id, legacyOnlyFolder.Id, StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(mergedFolders.Any(folder => string.Equals(folder.Id, legacySharedFolder.Id, StringComparison.OrdinalIgnoreCase)));
        }

        private static async Task SaveRouteModelAsync(RouteModel routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string targetFileName = ModelFileResolver<RouteModelHeader>.FilePath(routeModel) + ContentHandlerBase<RouteModelHeader>.SaveStateExtension;
            string targetDirectory = Path.GetDirectoryName(targetFileName) ?? throw new InvalidOperationException($"Unable to determine target directory for {targetFileName}.");
            _ = Directory.CreateDirectory(targetDirectory);

            using (FileStream saveFile = new FileStream(targetFileName, FileMode.Create, FileAccess.Write))
            {
                await MemoryPackSerializer.SerializeAsync(saveFile, routeModel, null, cancellationToken).ConfigureAwait(false);
                await saveFile.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<RouteModel> ReadPersistedRouteModelAsync(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string targetFileName = ModelFileResolver<RouteModelHeader>.FilePath(routeModel) + ContentHandlerBase<RouteModelHeader>.SaveStateExtension;

            using (FileStream readFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
            {
                return await MemoryPackSerializer.DeserializeAsync<RouteModel>(readFile, null, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
