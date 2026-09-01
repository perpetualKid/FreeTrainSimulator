using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Models.Content
{
    // PathModel is persisted (saved/loaded) via MemoryPack by the content handler layer
    // (ContentHandlerBase.ToFile/FromFile). These tests exercise that serialize -> deserialize
    // round-trip directly to guard the path data fidelity that save/load depends on, without
    // touching disk or the graphics editor.
    [TestClass]
    public class PathModelSerializationTests
    {
        [TestMethod]
        public void PathNodeTypeViaRetainsPersistedValue()
        {
            Assert.AreEqual(0x2, (int)PathNodeType.Via);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenNodeCountIsPreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(original.PathNodes.Length, restored.PathNodes.Length);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenHeaderFieldsArePreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(original.Id, restored.Id);
            Assert.AreEqual(original.Name, restored.Name);
            Assert.AreEqual(original.Start, restored.Start);
            Assert.AreEqual(original.End, restored.End);
            Assert.AreEqual(original.PlayerPath, restored.PlayerPath);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenStartNodeTypeIsPreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(PathNodeType.Start, restored.PathNodes[0].NodeType);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenEndNodeTypeIsPreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(PathNodeType.End, restored.PathNodes[^1].NodeType);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenMainLinksArePreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(1, restored.PathNodes[0].NextMainNode);
            Assert.AreEqual(2, restored.PathNodes[1].NextMainNode);
            Assert.AreEqual(-1, restored.PathNodes[^1].NextMainNode);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenSidingLinkIsPreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(3, restored.PathNodes[1].NextSidingNode);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenNodeIndexIsPreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(42, restored.PathNodes[1].NodeIndex);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenNodeLocationIsPreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(original.PathNodes[1].Location, restored.PathNodes[1].Location);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenWaitInfoIsPreserved()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.IsNotNull(restored.PathNodes[1].WaitInfo);
            Assert.AreEqual(120, restored.PathNodes[1].WaitInfo.WaitTime);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenAbsentWaitInfoStaysNull()
        {
            PathModel original = CreatePathModel();

            PathModel restored = RoundTrip(original);

            Assert.IsNull(restored.PathNodes[0].WaitInfo);
        }

        [TestMethod]
        public void WhenEmptyPathModelRoundTripsThenPathNodesAreEmpty()
        {
            PathModel original = new PathModel()
            {
                Id = "empty-path",
                Name = "Empty Path",
            };

            PathModel restored = RoundTrip(original);

            Assert.IsTrue(restored.PathNodes.IsEmpty);
        }

        [TestMethod]
        public void WhenPathModelRoundTripsThenValidationStateIsPreserved()
        {
            PathModel original = CreatePathModel() with { ValidationState = PathValidationState.Invalid };

            PathModel restored = RoundTrip(original);

            Assert.AreEqual(PathValidationState.Invalid, restored.ValidationState);
        }

        [TestMethod]
        public void WhenPathModelSerializedAndReadBackAsHeaderThenValidationStateIsPreserved()
        {
            // The content handler writes the full PathModel but reads the summary back as a PathModelHeader
            // (ContentHandlerBase.FromFile<PathModelHeader>). The validation marker in the toolbox path list
            // depends on that cross-type read preserving ValidationState.
            PathModel original = CreatePathModel() with { ValidationState = PathValidationState.Invalid };

            byte[] serialized = MemoryPackSerializer.Serialize(original);
            PathModelHeader header = MemoryPackSerializer.Deserialize<PathModelHeader>(serialized);

            Assert.AreEqual(PathValidationState.Invalid, header.ValidationState);
        }

        private static PathModel RoundTrip(PathModel pathModel)
        {
            byte[] serialized = MemoryPackSerializer.Serialize(pathModel);
            return MemoryPackSerializer.Deserialize<PathModel>(serialized);
        }

        // A three-node main route (Start -> Via -> End) with a passing-branch link, a populated
        // anchor NodeIndex, and a wait node, covering every field ToPathModel emits and the persistence
        // layer must round-trip.
        private static PathModel CreatePathModel()
        {
            return new PathModel()
            {
                Id = "test-path",
                Name = "Test Path",
                Start = "Start Location",
                End = "End Location",
                PlayerPath = true,
                PathNodes = ImmutableArray.Create(
                    new PathNode(new WorldLocation(new Tile(1, 2), new Vector3(10, 1, 20)))
                    {
                        NodeType = PathNodeType.Start,
                        NodeIndex = 7,
                        NextMainNode = 1,
                        NextSidingNode = -1,
                    },
                    new PathNode(new WorldLocation(new Tile(1, 2), new Vector3(30, 2, 40)))
                    {
                        NodeType = PathNodeType.Via,
                        NodeIndex = 42,
                        NextMainNode = 2,
                        NextSidingNode = 3,
                        WaitInfo = new PathNodeWaitInfo() { WaitTime = 120 },
                    },
                    new PathNode(new WorldLocation(new Tile(1, 3), new Vector3(50, 3, 60)))
                    {
                        NodeType = PathNodeType.End,
                        NodeIndex = 9,
                        NextMainNode = -1,
                        NextSidingNode = -1,
                    }),
            };
        }
    }
}
