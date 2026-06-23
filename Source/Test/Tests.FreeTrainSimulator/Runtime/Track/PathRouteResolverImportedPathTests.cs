using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Regression tests for resolving imported MSTS path models.
    /// </summary>
    [TestClass]
    public class PathRouteResolverImportedPathTests
    {
        /// <summary>
        /// Verifies that an imported MSTS passing path preserves authored links used by the resolver.
        /// </summary>
        [TestMethod]
        public async Task ResolveWhenImportedPathHasPassingBranchRecognizesMainRejoin()
        {
            string contentRoot = CreateContentRoot();
            string routeName = "Route" + Guid.NewGuid().ToString("N");
            string pathId = "ImportedPassing" + Guid.NewGuid().ToString("N");
            CreatePathFile(contentRoot, routeName, pathId,
                """
                        TrPathNode ( 00000000 1 2 0 )
                        TrPathNode ( 00000000 3 4294967295 1 )
                        TrPathNode ( 00000004 4294967295 3 2 )
                        TrPathNode ( 00000000 4294967295 4294967295 3 )
                """);

            FolderModel folderModel = new FolderModel("Folder" + Guid.NewGuid().ToString("N"), contentRoot, null);
            RouteModel routeModel = new RouteModel(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                Id = routeName,
                Name = routeName,
                Tags = ImmutableDictionary.Create<string, string>().Add(RouteModelImportHandler.SourceNameKey, routeName),
            };
            routeModel.Initialize(folderModel);

            ImmutableArray<PathModelHeader> importedPaths = await PathModelImportHandler.ExpandPathModels(routeModel, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel importedPath = importedPaths.OfType<PathModel>().Single(path => string.Equals(path.Id, pathId, StringComparison.OrdinalIgnoreCase));

            PathRouteResolution result = PathRouteResolver.Resolve(importedPath, null, TestContext.CancellationToken);

            Assert.HasCount(4, importedPath.PathNodes);
            Assert.AreEqual(1, importedPath.PathNodes[0].NextMainNode);
            Assert.AreEqual(2, importedPath.PathNodes[0].NextSidingNode);
            Assert.AreEqual(3, importedPath.PathNodes[2].NextSidingNode);
            Assert.HasCount(1, result.PassingRoutes);
            Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain));
        }

        /// <summary>
        /// Verifies that an imported MSTS passing path that does not rejoin the main path is reported.
        /// </summary>
        [TestMethod]
        public async Task ResolveWhenImportedPathPassingBranchDoesNotRejoinMainReportsDiagnostic()
        {
            string contentRoot = CreateContentRoot();
            string routeName = "Route" + Guid.NewGuid().ToString("N");
            string pathId = "ImportedBrokenPassing" + Guid.NewGuid().ToString("N");
            CreatePathFile(contentRoot, routeName, pathId,
                """
                        TrPathNode ( 00000000 1 2 0 )
                        TrPathNode ( 00000000 3 4294967295 1 )
                        TrPathNode ( 00000004 4294967295 4294967295 2 )
                        TrPathNode ( 00000000 4294967295 4294967295 3 )
                """);

            FolderModel folderModel = new FolderModel("Folder" + Guid.NewGuid().ToString("N"), contentRoot, null);
            RouteModel routeModel = new RouteModel(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                Id = routeName,
                Name = routeName,
                Tags = ImmutableDictionary.Create<string, string>().Add(RouteModelImportHandler.SourceNameKey, routeName),
            };
            routeModel.Initialize(folderModel);

            ImmutableArray<PathModelHeader> importedPaths = await PathModelImportHandler.ExpandPathModels(routeModel, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel importedPath = importedPaths.OfType<PathModel>().Single(path => string.Equals(path.Id, pathId, StringComparison.OrdinalIgnoreCase));

            PathRouteResolution result = PathRouteResolver.Resolve(importedPath, null, TestContext.CancellationToken);

            Assert.AreEqual(-1, importedPath.PathNodes[2].NextSidingNode);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain && diagnostic.FromNodeIndex == 0 && diagnostic.ToNodeIndex == 2));
        }

        private static string CreateContentRoot()
        {
            string contentRoot = Path.Combine(Path.GetTempPath(), "FreeTrainSimulator", "ImportedPathTests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(contentRoot);
            return contentRoot;
        }

        private static void CreatePathFile(string contentRoot, string routeName, string pathId, string pathNodes)
        {
            string pathsFolder = Path.Combine(contentRoot, "Routes", routeName, "Paths");
            _ = Directory.CreateDirectory(pathsFolder);
            string pathFile = Path.Combine(pathsFolder, pathId + ".pat");
            File.WriteAllText(pathFile,
                $"""
                SIMISA@@@@@@@@@@JINX0P0t______

                Serial ( 1 )
                TrackPDPs (
                    TrackPDP ( 0 0 0 0 0 1 1 )
                    TrackPDP ( 0 0 100 0 0 2 0 )
                    TrackPDP ( 0 0 100 0 100 1 1 )
                    TrackPDP ( 0 0 200 0 0 1 1 )
                )
                TrackPath (
                    TrPathName ( {pathId} )
                    Name ( "Imported Passing" )
                    TrPathStart ( Start )
                    TrPathEnd ( End )
                    TrPathNodes ( 4
                {pathNodes}
                    )
                )
                """);
        }

        public TestContext TestContext { get; set; }
    }
}
