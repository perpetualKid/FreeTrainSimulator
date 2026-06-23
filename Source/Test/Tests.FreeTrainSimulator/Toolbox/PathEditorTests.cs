using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class PathEditorTests
    {
        [TestMethod]
        public void WhenPathHasFatalResolverDiagnosticThenCanInitializePathReturnsFalse()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 4), CreateNode(PathNodeType.End, -1)),
            };

            bool canInitialize = PathEditor.CanInitializePath(pathModel, null, out PathRouteResolution resolution);

            Assert.IsFalse(canInitialize);
            Assert.AreEqual(PathRouteDiagnosticSeverity.Fatal, resolution.HighestSeverity);
        }

        [TestMethod]
        public void WhenMainRouteDoesNotReachEndThenCanInitializePathReturnsFalse()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Intermediate, -1), CreateNode(PathNodeType.End, -1)),
            };

            bool canInitialize = PathEditor.CanInitializePath(pathModel, null, out PathRouteResolution resolution);

            Assert.IsFalse(canInitialize);
            Assert.IsTrue(HasDiagnostic(resolution, PathRouteDiagnosticCode.MainRouteDoesNotReachEnd));
        }

        [TestMethod]
        public void WhenPathHasNoFatalResolverDiagnosticThenCanInitializePathReturnsTrue()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.End, -1)),
            };

            bool canInitialize = PathEditor.CanInitializePath(pathModel, null, out PathRouteResolution resolution);

            Assert.IsTrue(canInitialize);
            Assert.IsTrue(resolution.HighestSeverity < PathRouteDiagnosticSeverity.Fatal);
        }

        private static PathNode CreateNode(PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = nodeType,
                NextMainNode = nextMainNode,
            };
        }

        private static bool HasDiagnostic(PathRouteResolution resolution, PathRouteDiagnosticCode code)
        {
            foreach (PathRouteDiagnostic diagnostic in resolution.Diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }
    }
}
