using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Toolbox
{
    // Integration test exercising the real persist + reload cycle that PathEditor.ValidateRoutePaths performs:
    // resolve each path, persist the ValidationState via the content handler, then re-read the summary headers.
    // Reproduces the toolbox "Validate All" flow end-to-end (minus UI) against the isolated content root.
    [TestClass]
    public class PathValidationPersistenceTests
    {
        [TestMethod]
        public async Task WhenValidateRoutePathsRunsThenReloadedHeadersCarryPersistedValidationState()
        {
            RouteModelHeader route = await SeedRouteWithPathsAsync().ConfigureAwait(false);

            _ = await PathEditor.ValidateRoutePaths(route, null, true, CancellationToken.None).ConfigureAwait(false);

            ImmutableArray<PathModelHeader> reloaded = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            PathModelHeader emptyPath = reloaded.Single(p => p.Id == "empty-path");
            PathModelHeader linearPath = reloaded.Single(p => p.Id == "linear-path");

            Assert.AreEqual(PathValidationState.Invalid, emptyPath.ValidationState);
            Assert.AreEqual(PathValidationState.Valid, linearPath.ValidationState);
        }

        private static async Task<RouteModelHeader> SeedRouteWithPathsAsync()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel folderModel = new FolderModel($"Folder-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-content-{uniqueSuffix}"), contentModel);
            contentModel = contentModel with { ContentFolders = ImmutableArray.Create(folderModel) };
            contentModel.Initialize(null);

            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = $"Route-{uniqueSuffix}",
                Name = "Validation Test Route",
                RouteKey = "ValidationTestKey",
            };
            routeModel.Initialize(folderModel);

            PathModel emptyPath = new PathModel() { Id = "empty-path", Name = "Empty Path" };
            PathModel linearPath = new PathModel()
            {
                Id = "linear-path",
                Name = "Linear Path",
                PathNodes = ImmutableArray.Create(
                    new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero)) { NodeType = PathNodeType.Start, NextMainNode = 1 },
                    new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero)) { NodeType = PathNodeType.End, NextMainNode = -1 }),
            };

            _ = await routeModel.Save(emptyPath).ConfigureAwait(false);
            _ = await routeModel.Save(linearPath).ConfigureAwait(false);
            return routeModel;
        }
    }
}
