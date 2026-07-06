using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Models.Content
{
    // PathModel derives from PathModelHeader, whose base (ModelBase) is NOT [MemoryPackable]. MemoryPack therefore
    // flattens the inherited members into PathModel's own sequential layout, so the field order that matters for
    // reading pre-existing .path files is:
    //   [Id, Name, Version, Tags, Start, End, PlayerPath, (ValidationState), PathNodes].
    // ValidationState was added to PathModelHeader after paths had already been written. Because it lands BEFORE
    // PathNodes in the flattened sequence, these tests reproduce the "old format" (no ValidationState) with stand-in
    // types that mirror the previous layout, then deserialize the bytes as the current PathModel to prove that
    // adding ValidationState did not shift/corrupt PathNodes for existing files.
    [TestClass]
    public class PathModelLegacyFormatSerializationTests
    {
        [TestMethod]
        public void WhenLegacyPathFileIsReadWithCurrentModelThenPathNodesArePreserved()
        {
            LegacyPathModel legacy = CreateLegacyPathModel();

            byte[] legacyBytes = MemoryPackSerializer.Serialize(legacy);
            PathModel restored = MemoryPackSerializer.Deserialize<PathModel>(legacyBytes);

            Assert.AreEqual(legacy.PathNodes.Length, restored.PathNodes.Length);
        }

        [TestMethod]
        public void WhenLegacyPathFileIsReadWithCurrentModelThenNodeLinksArePreserved()
        {
            LegacyPathModel legacy = CreateLegacyPathModel();

            byte[] legacyBytes = MemoryPackSerializer.Serialize(legacy);
            PathModel restored = MemoryPackSerializer.Deserialize<PathModel>(legacyBytes);

            Assert.AreEqual(1, restored.PathNodes[0].NextMainNode);
            Assert.AreEqual(3, restored.PathNodes[1].NextSidingNode);
            Assert.AreEqual(-1, restored.PathNodes[^1].NextMainNode);
        }

        [TestMethod]
        public void WhenLegacyPathFileIsReadWithCurrentModelThenHeaderFieldsArePreserved()
        {
            LegacyPathModel legacy = CreateLegacyPathModel();

            byte[] legacyBytes = MemoryPackSerializer.Serialize(legacy);
            PathModel restored = MemoryPackSerializer.Deserialize<PathModel>(legacyBytes);

            Assert.AreEqual(legacy.Id, restored.Id);
            Assert.AreEqual(legacy.Start, restored.Start);
            Assert.AreEqual(legacy.End, restored.End);
            Assert.AreEqual(legacy.PlayerPath, restored.PlayerPath);
        }

        [TestMethod]
        public void WhenLegacyPathFileIsReadWithCurrentModelThenValidationStateDefaultsToNotValidated()
        {
            LegacyPathModel legacy = CreateLegacyPathModel();

            byte[] legacyBytes = MemoryPackSerializer.Serialize(legacy);
            PathModel restored = MemoryPackSerializer.Deserialize<PathModel>(legacyBytes);

            Assert.AreEqual(PathValidationState.NotValidated, restored.ValidationState);
        }

        private static LegacyPathModel CreateLegacyPathModel()
        {
            return new LegacyPathModel()
            {
                Id = "legacy-path",
                Name = "Legacy Path",
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
                        NodeType = PathNodeType.Intermediate,
                        NodeIndex = 42,
                        NextMainNode = 2,
                        NextSidingNode = 3,
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

    // Stand-in for the pre-ValidationState base header. Mirrors the serialized member order of ModelBase +
    // PathModelHeader as it existed before ValidationState was appended. Not [MemoryPackable]; flattened by the
    // derived LegacyPathModel exactly like ModelBase is flattened by PathModelHeader/PathModel.
    public abstract record LegacyPathModelHeaderBase
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string Version { get; init; }
        public ImmutableDictionary<string, string> Tags { get; init; }
        public string Start { get; init; }
        public string End { get; init; }
        public bool PlayerPath { get; init; }
    }

    // Stand-in for the pre-ValidationState full path model: header members flattened, then PathNodes. Serializing
    // an instance produces bytes in the old on-disk layout that a current PathModel deserialize must still read.
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record LegacyPathModel : LegacyPathModelHeaderBase
    {
        public ImmutableArray<PathNode> PathNodes { get; init; } = ImmutableArray<PathNode>.Empty;

        [MemoryPackConstructor]
        public LegacyPathModel()
        { }
    }
}
