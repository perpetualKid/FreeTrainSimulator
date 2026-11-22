using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Files;

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

        [TestMethod]
        public async Task SaveWhenImportedPathHasDisconnectedTerminalNodeRefusesNormalizationAndPreservesNativeModel()
        {
            string contentRoot = CreateContentRoot();
            string routeName = "Route" + Guid.NewGuid().ToString("N");
            string pathId = "ImportedDisconnected" + Guid.NewGuid().ToString("N");
            string pathFile = CreatePathFile(contentRoot, routeName, pathId,
                """
                        TrPathNode ( 00000000 1 4294967295 0 )
                        TrPathNode ( 00000000 3 4294967295 1 )
                        TrPathNode ( 00000000 4294967295 4294967295 2 )
                        TrPathNode ( 00000000 4294967295 4294967295 3 )
                """);
            string sourceContent = await File.ReadAllTextAsync(pathFile, CancellationToken.None).ConfigureAwait(false);
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
            ImmutableArray<PathNode> importedNodes = importedPath.PathNodes;

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(importedPath, routeModel, null,
                TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader reloadedHeader = (await routeModel.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == pathId);
            PathModel reloaded = await reloadedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.AreEqual(PathRouteDiagnosticCode.UnreachableNode, result.HighestActionableDiagnostic?.Code);
            Assert.Contains("Connect the node to the main or passing path, or remove it.", result.FailureMessage);
            Assert.AreSequenceEqual(importedNodes, reloaded.PathNodes);
            Assert.AreEqual(sourceContent, await File.ReadAllTextAsync(pathFile, CancellationToken.None).ConfigureAwait(false));
        }

        /// <summary>
        /// Preserves canonical source flags through parsing, native import, and reload without rewriting the source.
        /// </summary>
        [TestMethod]
        [DataRow(0, 0x00000001u, PathNodeType.Start | PathNodeType.Reversal, 1, 1)]
        [DataRow(0, 0x00000002u, PathNodeType.Start | PathNodeType.Wait, 1, 1)]
        [DataRow(0, 0x00000003u, PathNodeType.Start | PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(0, 0x001E0001u, PathNodeType.Start | PathNodeType.Reversal, 1, 1)]
        [DataRow(0, 0x001E0002u, PathNodeType.Start | PathNodeType.Wait, 1, 1)]
        [DataRow(0, 0x001E0003u, PathNodeType.Start | PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(1, 0x00000001u, PathNodeType.Reversal, 1, 1)]
        [DataRow(1, 0x00000002u, PathNodeType.Wait, 1, 1)]
        [DataRow(1, 0x00000003u, PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(1, 0x001E0001u, PathNodeType.Reversal, 1, 1)]
        [DataRow(1, 0x001E0002u, PathNodeType.Wait, 1, 1)]
        [DataRow(1, 0x001E0003u, PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(3, 0x00000001u, PathNodeType.End | PathNodeType.Reversal, 1, 1)]
        [DataRow(3, 0x00000002u, PathNodeType.End | PathNodeType.Wait, 1, 1)]
        [DataRow(3, 0x00000003u, PathNodeType.End | PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(3, 0x001E0001u, PathNodeType.End | PathNodeType.Reversal, 1, 1)]
        [DataRow(3, 0x001E0002u, PathNodeType.End | PathNodeType.Wait, 1, 1)]
        [DataRow(3, 0x001E0003u, PathNodeType.End | PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(0, 0x00000003u, PathNodeType.Start | PathNodeType.Wait | PathNodeType.Reversal | PathNodeType.Junction | PathNodeType.Invalid, 2, 8)]
        [DataRow(1, 0x00000002u, PathNodeType.Wait | PathNodeType.Junction, 2, 0)]
        [DataRow(1, 0x00000001u, PathNodeType.Reversal | PathNodeType.Junction | PathNodeType.Invalid, 2, 9)]
        [DataRow(1, 0x00000003u, PathNodeType.Wait | PathNodeType.Reversal | PathNodeType.Invalid, 1, 12)]
        [DataRow(3, 0x001E0002u, PathNodeType.End | PathNodeType.Wait | PathNodeType.Junction | PathNodeType.Invalid, 2, 13)]
        [DataRow(1, 0x001E0000u, PathNodeType.None, 1, 1)]
        [DataRow(1, 0x001EFFFCu, PathNodeType.Via, 1, 1)]
        [DataRow(1, 0x0000FFF8u, PathNodeType.None, 1, 1)]
        [DataRow(1, 0x001EFFFFu, PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(1, 0x00000005u, PathNodeType.Reversal, 1, 1)]
        [DataRow(1, 0x00000006u, PathNodeType.Wait, 1, 1)]
        [DataRow(1, 0x00000007u, PathNodeType.Wait | PathNodeType.Reversal, 1, 1)]
        [DataRow(0, 0x00000004u, PathNodeType.Start, 1, 1)]
        [DataRow(3, 0x00000004u, PathNodeType.End, 1, 1)]
        public async Task ImportWhenSourceHasKnownFlagsThenParserImportAndReloadAgree(int eventIndex, uint sourceFlags,
            PathNodeType expectedType, int junctionFlag, int invalidFlag)
        {
            string contentRoot = CreateContentRoot();
            string routeName = "Route" + Guid.NewGuid().ToString("N");
            string pathId = "ImportedEvents" + Guid.NewGuid().ToString("N");
            string dataPoints = string.Join(Environment.NewLine, Enumerable.Range(0, 4).Select(index =>
                $"    TrackPDP ( 0 0 {index * 50} 0 0 {(index == eventIndex ? junctionFlag : 1)} {(index == eventIndex ? invalidFlag : 1)} )"));
            string nodes = string.Join(Environment.NewLine, Enumerable.Range(0, 4).Select(index =>
                $"        TrPathNode ( {(index == eventIndex ? sourceFlags : index is 1 or 2 ? 4u : 0u):X8} {(index == 3 ? uint.MaxValue : (uint)(index + 1))} {(index == 0 ? 2u : uint.MaxValue)} {index} )"));
            string sourceFile = CreatePathFile(contentRoot, routeName, pathId, dataPoints, nodes);
            byte[] sourceBytes = await File.ReadAllBytesAsync(sourceFile, TestContext.CancellationToken).ConfigureAwait(false);
            PathFile source = new PathFile(sourceFile);
            FolderModel folder = new FolderModel("Folder" + Guid.NewGuid().ToString("N"), contentRoot, null);
            RouteModel route = new RouteModel(WorldLocation.None)
            {
                Id = routeName,
                Tags = ImmutableDictionary<string, string>.Empty.Add(RouteModelImportHandler.SourceNameKey, routeName),
            };
            route.Initialize(folder);

            PathModel imported = (await PathModelImportHandler.ExpandPathModels(route, TestContext.CancellationToken).ConfigureAwait(false)).OfType<PathModel>().Single();
            PathModel reloaded = await ContentHandlerBase<PathModelHeader>.FromFile<PathModel, RouteModelHeader>(pathId, route, TestContext.CancellationToken).ConfigureAwait(false);

            Assert.HasCount(4, source.PathNodes);
            Assert.HasCount(4, imported.PathNodes);
            for (int index = 0; index < source.PathNodes.Count; index++)
            {
                PathNodeType expected = index == eventIndex ? expectedType : index == 0 ? PathNodeType.Start
                    : index == 3 ? PathNodeType.End : PathNodeType.Via;
                Assert.AreEqual(expected, source.PathNodes[index].NodeType, $"Parsed flags at {index}");
                Assert.AreEqual(source.PathNodes[index].NodeType, imported.PathNodes[index].NodeType, $"Imported flags at {index}");
                Assert.AreEqual(source.PathNodes[index].Location, imported.PathNodes[index].Location);
                Assert.AreEqual(source.PathNodes[index].NextMainNode, imported.PathNodes[index].NextMainNode);
                Assert.AreEqual(source.PathNodes[index].NextSidingNode, imported.PathNodes[index].NextSidingNode);
                int waitTime = index == eventIndex ? (int)(sourceFlags >> 16) : 0;
                Assert.AreEqual(waitTime, source.PathNodes[index].WaitTime);
                Assert.AreEqual(waitTime > 0 ? (int?)waitTime : null, imported.PathNodes[index].WaitInfo == null
                    ? null : PathWaitOperationCodec.GetMstsWaitCode(imported.PathNodes[index].WaitInfo));
            }
            Assert.AreSequenceEqual(imported.PathNodes, reloaded.PathNodes);
            Assert.AreSequenceEqual(sourceBytes, await File.ReadAllBytesAsync(sourceFile, TestContext.CancellationToken).ConfigureAwait(false));
        }

        private static string CreateContentRoot()
        {
            string contentRoot = Path.Combine(Path.GetTempPath(), "FreeTrainSimulator", "ImportedPathTests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(contentRoot);
            return contentRoot;
        }

        private static string CreatePathFile(string contentRoot, string routeName, string pathId, string pathNodes)
        {
            return CreatePathFile(contentRoot, routeName, pathId,
                """
                    TrackPDP ( 0 0 0 0 0 1 1 )
                    TrackPDP ( 0 0 100 0 0 2 0 )
                    TrackPDP ( 0 0 100 0 100 1 1 )
                    TrackPDP ( 0 0 200 0 0 1 1 )
                """, pathNodes);
        }

        private static string CreatePathFile(string contentRoot, string routeName, string pathId, string pathDataPoints, string pathNodes)
        {
            string pathsFolder = Path.Combine(contentRoot, "Routes", routeName, "Paths");
            _ = Directory.CreateDirectory(pathsFolder);
            string pathFile = Path.Combine(pathsFolder, pathId + ".pat");
            File.WriteAllText(pathFile,
                $"""
                SIMISA@@@@@@@@@@JINX0P0t______

                Serial ( 1 )
                TrackPDPs (
                {pathDataPoints}
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
            return pathFile;
        }

        public TestContext TestContext { get; set; }
    }
}
