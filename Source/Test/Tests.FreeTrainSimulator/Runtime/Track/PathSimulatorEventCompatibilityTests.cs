using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    /// <summary>Checks current Simulator event support through public helpers, independently of track geometry.</summary>
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class PathSimulatorEventCompatibilityTests
    {
        [TestMethod]
        public void ValidateNodeWhenUiCultureChangesThenRuntimeDiagnosticRemainsStableEnglish()
        {
            CultureInfo previousCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

                PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.ValidateNode(Node(PathNodeType.Wait, -1, -1), 7, false, true);

                Assert.AreEqual(("Unsupported siding event at node 7; branch waits/reversals cannot be preserved.",
                    "Place the event on the main path outside the passing interior."), (diagnostic.Message, diagnostic.SuggestedAction));
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
            }
        }

        /// <summary>A passing-interior marker is unsupported even when no wait payload exists.</summary>
        [TestMethod]
        [DataRow(PathNodeType.Wait)]
        [DataRow(PathNodeType.Reversal)]
        public void ValidateNodeWhenPassingInteriorHasMarkerOnlyThenReportsEvent(PathNodeType nodeType)
        {
            PathNode node = Node(nodeType, -1, -1);

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.ValidateNode(node, 7, false, true);

            Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedPassingInteriorEvent, 7, PathRouteDiagnosticSeverity.Error),
                (diagnostic.Code, diagnostic.NodeIndex, diagnostic.Severity));
        }

        /// <summary>Zero-duration wait markers and nonzero payloads without markers both count as passing events.</summary>
        [TestMethod]
        [DataRow(PathNodeType.Wait, 0)]
        [DataRow(PathNodeType.Via, 37)]
        public void ValidateWhenPassingInteriorHasWaitMarkerOrPayloadThenReportsBranchNode(PathNodeType nodeType, int waitTime)
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 2), Node(PathNodeType.End, -1, -1),
                    Node(nodeType, -1, 1) with { WaitInfo = new PathNodeWaitInfo { WaitTime = waitTime } }),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedPassingInteriorEvent, 2), (diagnostic.Code, diagnostic.NodeIndex));
        }

        /// <summary>A terminal main node is not a passing interior and can retain either supported event.</summary>
        [TestMethod]
        [DataRow(PathNodeType.End | PathNodeType.Wait, 37)]
        [DataRow(PathNodeType.End | PathNodeType.Reversal, 0)]
        public void ValidateWhenTerminalMainNodeHasEventThenAllowsIt(PathNodeType nodeType, int waitTime)
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, -1),
                    Node(nodeType, -1, -1) with { WaitInfo = new PathNodeWaitInfo { WaitTime = waitTime } }),
            };

            ImmutableArray<PathRouteDiagnostic> diagnostics = PathSimulatorEventCompatibility.Validate(path, 0);

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>The combined marker is refused even away from first nodes, junctions, and passing interiors.</summary>
        [TestMethod]
        public void ValidateNodeWhenWaitAndReversalAreCombinedThenReportsCombination()
        {
            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.ValidateNode(
                PathNodeType.Wait | PathNodeType.Reversal, 0, 7, false, false, false);

            Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal, 7), (diagnostic.Code, diagnostic.NodeIndex));
        }

        /// <summary>First-node and junction restrictions apply to both event markers.</summary>
        [TestMethod]
        [DataRow(PathNodeType.Start | PathNodeType.Wait, true, PathRouteDiagnosticCode.UnsupportedFirstMainEvent)]
        [DataRow(PathNodeType.Start | PathNodeType.Reversal, true, PathRouteDiagnosticCode.UnsupportedFirstMainEvent)]
        [DataRow(PathNodeType.Junction | PathNodeType.Wait, false, PathRouteDiagnosticCode.UnsupportedJunctionEvent)]
        [DataRow(PathNodeType.Junction | PathNodeType.Reversal, false, PathRouteDiagnosticCode.UnsupportedJunctionEvent)]
        public void ValidateNodeWhenFirstOrJunctionHasEventThenReportsContext(PathNodeType nodeType, bool isFirst, PathRouteDiagnosticCode expectedCode)
        {
            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.ValidateNode(Node(nodeType, -1, -1), 7, isFirst, false);

            Assert.AreEqual((expectedCode, 7), (diagnostic.Code, diagnostic.NodeIndex));
        }

        /// <summary>Resolved junction context overrides the stored marker in either direction.</summary>
        [TestMethod]
        [DataRow(PathNodeType.Junction | PathNodeType.Wait, false, PathRouteDiagnosticCode.None)]
        [DataRow(PathNodeType.Reversal, true, PathRouteDiagnosticCode.UnsupportedJunctionEvent)]
        public void ValidateNodeWhenJunctionContextIsResolvedThenUsesCallerContext(PathNodeType nodeType, bool isJunction, PathRouteDiagnosticCode expectedCode)
        {
            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.ValidateNode(Node(nodeType, -1, -1), 7, false, false, isJunction);

            Assert.AreEqual(expectedCode, diagnostic?.Code ?? PathRouteDiagnosticCode.None);
        }

        /// <summary>Only the caller's junction context resets accumulated traversal reversal state.</summary>
        [TestMethod]
        public void ValidateTraversalNodeWhenJunctionIsResolvedThenResetsReversalState()
        {
            bool reversed = false;
            PathRouteDiagnostic first = PathSimulatorEventCompatibility.ValidateTraversalNode(Node(PathNodeType.Reversal, -1, -1), 1, false, ref reversed);
            bool afterFirst = reversed;
            PathRouteDiagnostic via = PathSimulatorEventCompatibility.ValidateTraversalNode(Node(PathNodeType.Via, -1, -1), 2, false, ref reversed);
            bool afterVia = reversed;
            PathRouteDiagnostic junction = PathSimulatorEventCompatibility.ValidateTraversalNode(Node(PathNodeType.Via, -1, -1), 3, true, ref reversed);
            bool afterJunction = reversed;
            PathRouteDiagnostic second = PathSimulatorEventCompatibility.ValidateTraversalNode(Node(PathNodeType.Reversal, -1, -1), 4, false, ref reversed);

            Assert.AreSequenceEqual(new[] { (PathRouteDiagnosticCode.None, true), (PathRouteDiagnosticCode.None, true),
                (PathRouteDiagnosticCode.None, false), (PathRouteDiagnosticCode.None, true) },
                new[] { (first?.Code ?? PathRouteDiagnosticCode.None, afterFirst), (via?.Code ?? PathRouteDiagnosticCode.None, afterVia),
                    (junction?.Code ?? PathRouteDiagnosticCode.None, afterJunction), (second?.Code ?? PathRouteDiagnosticCode.None, reversed) });
        }

        /// <summary>A wait cannot reset reversal state, whereas a junction permits another reversal.</summary>
        [TestMethod]
        [DataRow(PathNodeType.Wait, PathRouteDiagnosticCode.MultipleReversalsInContinuousVector, 3)]
        [DataRow(PathNodeType.Junction, PathRouteDiagnosticCode.None, -1)]
        public void ValidateWhenMainHasMultipleReversalsThenOnlyJunctionResetsState(PathNodeType separator, PathRouteDiagnosticCode expectedCode, int expectedIndex)
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, -1), Node(PathNodeType.Reversal, 2, -1),
                    Node(separator, 3, -1), Node(PathNodeType.Reversal, 4, -1), Node(PathNodeType.End, -1, -1)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).SingleOrDefault();

            Assert.AreEqual((expectedCode, expectedIndex), (diagnostic?.Code ?? PathRouteDiagnosticCode.None, diagnostic?.NodeIndex ?? -1));
        }

        /// <summary>The second linked reversal, not the second stored reversal, owns the refusal.</summary>
        [TestMethod]
        public void ValidateWhenStorageOrderIsReversedThenFollowsLinksFromExplicitRoot()
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.End, -1, -1), Node(PathNodeType.Reversal, 0, -1),
                    Node(PathNodeType.Wait, 1, -1), Node(PathNodeType.Reversal, 2, -1), Node(PathNodeType.Start, 3, -1)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 4).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.MultipleReversalsInContinuousVector, 1), (diagnostic.Code, diagnostic.NodeIndex));
        }

        /// <summary>Separate passing branches are allowed, with main-event semantics at their endpoints.</summary>
        [TestMethod]
        public void ValidateWhenMultipleSeparatedBranchesHaveMainEndpointEventsThenAllowsThem()
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, -1),
                    Node(PathNodeType.Wait, 2, 6) with { WaitInfo = new PathNodeWaitInfo { WaitTime = 37 } },
                    Node(PathNodeType.Via, 3, -1), Node(PathNodeType.Via, 4, 7), Node(PathNodeType.Reversal, 5, -1),
                    Node(PathNodeType.End, -1, -1), Node(PathNodeType.Via, -1, 2), Node(PathNodeType.Via, -1, 4)),
            };

            ImmutableArray<PathRouteDiagnostic> diagnostics = PathSimulatorEventCompatibility.Validate(path, 0);

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>An uninitialized node collection produces a fatal diagnostic rather than being walked.</summary>
        [TestMethod]
        public void ValidateWhenNodesAreDefaultThenReportsEmptyPath()
        {
            PathModel path = new PathModel { PathNodes = default };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.EmptyPath, -1, PathRouteDiagnosticSeverity.Fatal),
                (diagnostic.Code, diagnostic.NodeIndex, diagnostic.Severity));
        }

        /// <summary>Negative and out-of-range roots are rejected before dereferencing nodes.</summary>
        [TestMethod]
        [DataRow(-1)]
        [DataRow(2)]
        public void ValidateWhenRootIsInvalidThenReportsMissingStart(int startNodeIndex)
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, -1), Node(PathNodeType.End, -1, -1)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, startNodeIndex).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.MissingStartNode, startNodeIndex, PathRouteDiagnosticSeverity.Fatal),
                (diagnostic.Code, diagnostic.NodeIndex, diagnostic.Severity));
        }

        /// <summary>A link to a missing node is reported without dereferencing the target during traversal.</summary>
        [TestMethod]
        public void ValidateWhenLinkedNodeIsMissingThenReportsLinkAndMissingNode()
        {
            PathModel path = new PathModel { PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, -1), null) };

            ImmutableArray<PathRouteDiagnostic> diagnostics = PathSimulatorEventCompatibility.Validate(path, 0);

            Assert.AreSequenceEqual(new[] { (PathRouteDiagnosticCode.InvalidMainLink, 0), (PathRouteDiagnosticCode.UnreachableNode, 1) },
                diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.NodeIndex)).ToArray());
        }

        /// <summary>Invalid link targets are diagnosed in both link domains before graph traversal.</summary>
        [TestMethod]
        [DataRow(-2, -1, PathRouteDiagnosticCode.InvalidMainLink, -2)]
        [DataRow(2, -1, PathRouteDiagnosticCode.InvalidMainLink, 2)]
        [DataRow(1, -2, PathRouteDiagnosticCode.InvalidSidingLink, -2)]
        [DataRow(1, 2, PathRouteDiagnosticCode.InvalidSidingLink, 2)]
        public void ValidateWhenLinkTargetIsInvalidThenReportsItsSourceAndTarget(int main, int siding, PathRouteDiagnosticCode expectedCode, int expectedTarget)
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, main, siding), Node(PathNodeType.End, -1, -1)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).Single();

            Assert.AreEqual((expectedCode, 0, 0, expectedTarget),
                (diagnostic.Code, diagnostic.NodeIndex, diagnostic.FromNodeIndex, diagnostic.ToNodeIndex));
        }

        /// <summary>A cyclic main traversal terminates with a diagnostic at the repeated node.</summary>
        [TestMethod]
        public void ValidateWhenMainLinksCycleThenReportsCycle()
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, -1), Node(PathNodeType.Via, 0, -1)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedGraphCycle, 0), (diagnostic.Code, diagnostic.NodeIndex));
        }

        /// <summary>A cyclic passing interior terminates without attempting an unbounded siding walk.</summary>
        [TestMethod]
        public void ValidateWhenSidingLinksCycleThenReportsCycle()
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 2), Node(PathNodeType.End, -1, -1),
                    Node(PathNodeType.Via, -1, 3), Node(PathNodeType.Via, -1, 2)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedGraphCycle, 2), (diagnostic.Code, diagnostic.NodeIndex));
        }

        /// <summary>A passing branch must rejoin downstream, not terminate or return to its split.</summary>
        [TestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        public void ValidateWhenBranchDoesNotRejoinDownstreamThenReportsSplit(int rejoin)
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 2), Node(PathNodeType.End, -1, -1), Node(PathNodeType.Via, -1, rejoin)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain, 0), (diagnostic.Code, diagnostic.NodeIndex));
        }

        /// <summary>Stored nodes outside both traversals are reported rather than silently omitted.</summary>
        [TestMethod]
        public void ValidateWhenNodeIsUnreachableThenReportsStoredIndex()
        {
            PathModel path = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, -1), Node(PathNodeType.End, -1, -1), Node(PathNodeType.Via, -1, -1)),
            };

            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.Validate(path, 0).Single();

            Assert.AreEqual((PathRouteDiagnosticCode.UnreachableNode, 2), (diagnostic.Code, diagnostic.NodeIndex));
        }

        private static PathNode Node(PathNodeType nodeType, int main, int siding)
            => new PathNode(default) { NodeType = nodeType, NextMainNode = main, NextSidingNode = siding };
    }
}