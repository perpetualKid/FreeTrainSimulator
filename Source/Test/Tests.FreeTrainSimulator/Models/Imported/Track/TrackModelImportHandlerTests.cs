using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace Tests.FreeTrainSimulator.Models.Imported.Track
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class TrackModelImportHandlerTests
    {
        [TestMethod]
        [DataRow(false, 0)]
        [DataRow(false, 1)]
        [DataRow(false, 2)]
        [DataRow(true, 0)]
        [DataRow(true, 1)]
        [DataRow(true, 2)]
        public void ConvertTrackItemsPreservesEverySourceSlot(bool unsupported, int badId)
        {
            List<TrackItem> source = CreateSourceItems(3);
            source[badId] = CreateBadItem(unsupported, badId);
            TrackDatabase database = CreateDatabase(new[] { badId }, false);
            List<TrackItemBase> expected = CreateExpectedItems(3, 0);
            expected[badId] = new EmptyTrackItem { TrackItemIndex = badId };

            ImmutableArray<TrackItemBase> actual = ConvertItems(source, database);

            Assert.AreSequenceEqual(expected.ToArray(), actual.ToArray());
        }

        [TestMethod]
        [DataRow(false, false, 3)]
        [DataRow(false, false, 5)]
        [DataRow(false, true, 3)]
        [DataRow(false, true, 5)]
        [DataRow(true, false, 3)]
        [DataRow(true, false, 5)]
        [DataRow(true, true, 3)]
        [DataRow(true, true, 5)]
        public void ConvertTrackItemsNormalizesPairsAfterBadSlotWithoutOverwritingSentinels(bool unsupported, bool platform, int count)
        {
            List<TrackItem> source = CreateSourceItems(count);
            source[0] = CreateBadItem(unsupported, 0);
            source[1] = CreateEndpoint(platform, 1, 2, "Long name");
            source[2] = CreateEndpoint(platform, 2, 1, "X");
            List<TrackItemBase> expected = CreateExpectedItems(count, 0);
            expected[0] = new EmptyTrackItem { TrackItemIndex = 0 };
            expected[1] = CreateExpectedEndpoint(platform, 1, 2, "Long name");
            expected[2] = CreateExpectedEndpoint(platform, 2, 1, "Long name");

            ImmutableArray<TrackItemBase> actual = ConvertItems(source, CreateDatabase(Enumerable.Range(0, 1), false));

            Assert.AreSequenceEqual(expected.ToArray(), actual.ToArray());
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void ConvertTrackItemsReplacesOrphanLinkedToBadSlotWithoutOverwritingKnownItem(bool unsupported, bool platform)
        {
            List<TrackItem> source = CreateSourceItems(3);
            source[0] = CreateBadItem(unsupported, 0);
            source[1] = CreateEndpoint(platform, 1, 0, "Orphan");
            TrackItemBase[] expected = { new EmptyTrackItem { TrackItemIndex = 0 }, new EmptyTrackItem { TrackItemIndex = 1 }, CreateExpectedCrossover(2, 0) };

            ImmutableArray<TrackItemBase> actual = ConvertItems(source, CreateDatabase(Enumerable.Range(0, 1), false));

            Assert.AreSequenceEqual(expected, actual.ToArray());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ConvertedDatabaseRoundTripPreservesBadMiddleSlotAndKnownPayload(bool unsupported)
        {
            List<TrackItem> source = CreateSourceItems(3);
            source[1] = CreateBadItem(unsupported, 1);
            TrackDatabase database = CreateDatabase(Enumerable.Range(0, 3), false);
            database = database with { TrackItems = ConvertItems(source, database) };
            TrackItemBase[] expected = { CreateExpectedCrossover(0, 1), new EmptyTrackItem { TrackItemIndex = 1 }, CreateExpectedCrossover(2, 1) };

            TrackDatabase restored = MemoryPackSerializer.Deserialize<TrackDatabase>(MemoryPackSerializer.Serialize(database));

            Assert.AreSequenceEqual(expected, restored.TrackItems.ToArray());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ConvertedDatabaseRoundTripPreservesSelectorsAddressingEverySourceId(bool unsupported)
        {
            List<TrackItem> source = CreateSourceItems(3);
            source[1] = CreateBadItem(unsupported, 1);
            TrackDatabase database = CreateDatabase(Enumerable.Range(0, 3), false);
            database = database with { TrackItems = ConvertItems(source, database) };

            TrackDatabase restored = MemoryPackSerializer.Deserialize<TrackDatabase>(MemoryPackSerializer.Serialize(database));

            Assert.AreSequenceEqual(new[] { (1, 0, 0, typeof(CrossoverTrackItem)), (1, 1, 1, typeof(EmptyTrackItem)), (1, 2, 2, typeof(CrossoverTrackItem)) }, restored.TrackItemSelectors.SelectMany(selector => selector.Value.TrackItems.Select(id =>
                    (selector.Key, id, restored.TrackItems[id].TrackItemIndex, restored.TrackItems[id].GetType()))).ToArray());
        }

        [TestMethod]
        [DataRow("located")]
        [DataRow("missing")]
        [DataRow("empty")]
        [DataRow("vector")]
        public void ConvertTrackItemsRetainsExistingSupportedAndEmptyBehavior(string scenario)
        {
            (TrackItem source, TrackDatabase database, TrackItemBase expected) = CreateControl(scenario);

            ImmutableArray<TrackItemBase> actual = ConvertItems(new List<TrackItem> { source }, database);

            Assert.AreSequenceEqual(new[] { expected }, actual.ToArray());
        }

        private static ImmutableArray<TrackItemBase> ConvertItems(List<TrackItem> source, TrackDatabase database)
        {
            MethodInfo converter = typeof(TrackModelImportHandler).GetMethod("ConvertTrackItems", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(TrackModelImportHandler).FullName, "ConvertTrackItems");
            return (ImmutableArray<TrackItemBase>)converter.Invoke(null, new object[]
            {
                source, database, new TrackSectionModel(), "synthetic.tdb", new SignalConfigurationModel(),
            });
        }

        private static TrackDatabase CreateDatabase(IEnumerable<int> references, bool vector)
        {
            WorldLocation location = ItemLocation();
            TrackNodeBase owner = vector
                ? new VectorNode(location, location.Tile, location) { NodeIndex = 1 }
                : new EndNode(location, location.Tile, Vector3.Zero) { NodeIndex = 1 };
            // Index zero is reserved; a nonzero EndNode owner must reach non-vector recovery, not the no-owner fallback.
            return new TrackDatabase
            {
                TrackDataBaseType = TrackDataBaseType.Rail,
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, owner),
                TrackItemSelectors = ImmutableDictionary<int, TrackItemIndex>.Empty.Add(1,
                    new TrackItemIndex { TrackItems = references.ToImmutableArray() }),
            };
        }

        private static List<TrackItem> CreateSourceItems(int count)
        {
            return Enumerable.Range(0, count).Select(id => ParseItem<CrossoverItem>(id,
                FormattableString.Invariant($"CrossoverTrItemData ( 7 {100 + id} )"))).Cast<TrackItem>().ToList();
        }

        private static List<TrackItemBase> CreateExpectedItems(int count, int nodeIndex)
        {
            return Enumerable.Range(0, count).Select(id => (TrackItemBase)CreateExpectedCrossover(id, nodeIndex)).ToList();
        }

        private static CrossoverTrackItem CreateExpectedCrossover(int id, int nodeIndex)
        {
            return new CrossoverTrackItem(ItemLocation())
            {
                TrackItemIndex = id, NodeIndex = nodeIndex, SectionDistance = 12.5f, Flags = 10,
                LinkedCrossoverItem = 7, ShapeIndex = 100 + id,
            };
        }

        private static TrackItem CreateBadItem(bool unsupported, int id)
        {
            if (unsupported)
                return new UnsupportedTrackItem(id);
            TrackItem item = ParseItem<CrossoverItem>(id, FormattableString.Invariant($"CrossoverTrItemData ( 7 {100 + id} )"));
            SetLocation(item, WorldLocation.None);
            return item;
        }

        private static TrackItem CreateEndpoint(bool platform, int id, int linkedId, string name)
        {
            return platform
                ? ParseItem<PlatformItem>(id, FormattableString.Invariant($"PlatformTrItemData ( 00000002 {linkedId} ) PlatformName ( \"Platform\" ) Station ( \"{name}\" ) PlatformMinWaitingTime ( 30 ) PlatformNumPassengersWaiting ( 12 )"))
                : ParseItem<SidingItem>(id, FormattableString.Invariant($"SidingTrItemData ( 00000002 {linkedId} ) SidingName ( \"{name}\" )"));
        }

        private static TrackItemBase CreateExpectedEndpoint(bool platform, int id, int linkedId, string name)
        {
            return platform
                ? new PlatformTrackItem(ItemLocation())
                {
                    TrackItemIndex = id, LinkedPlatformItem = linkedId, PlatformName = "Platform", StationName = name,
                    SectionDistance = 12.5f, Flags = 10, PlatformFlags = 2, MinWaitingTime = 30, PassengersWaiting = 12,
                }
                : new SidingTrackItem(ItemLocation())
                {
                    TrackItemIndex = id, LinkedSidingItem = linkedId, SidingName = name,
                    SectionDistance = 12.5f, Flags = 10, SidingFlags = 2,
                };
        }

        private static (TrackItem Source, TrackDatabase Database, TrackItemBase Expected) CreateControl(string scenario)
        {
            TrackItem source = scenario == "empty" ? ParseItem<EmptyItem>(0, string.Empty) : ParseItem<CrossoverItem>(0, "CrossoverTrItemData ( 7 100 )");
            if (scenario is "missing" or "vector")
                SetLocation(source, WorldLocation.None);
            TrackDatabase database = CreateDatabase(Enumerable.Range(0, scenario == "vector" ? 1 : 0), scenario == "vector");
            TrackItemBase expected = scenario switch
            {
                "located" => CreateExpectedCrossover(0, 0),
                "vector" => new CrossoverTrackItem(WorldLocation.None)
                {
                    TrackItemIndex = 0, NodeIndex = 1, SectionDistance = 12.5f, Flags = 10, LinkedCrossoverItem = 7, ShapeIndex = 100,
                },
                "missing" or "empty" => new EmptyTrackItem { TrackItemIndex = 0 },
                _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
            };
            return (source, database, expected);
        }

        private static T ParseItem<T>(int id, string payload) where T : TrackItem
        {
            string text = string.Create(CultureInfo.InvariantCulture,
                $"( TrItemId ( {id} ) TrItemRData ( 10 20 30 2 3 ) TrItemSData ( 12.5 0000000A ) {payload} )");
            using MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(text));
            using STFReader reader = new STFReader(stream, "synthetic.tdb", Encoding.ASCII, false);
            ConstructorInfo constructor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(STFReader), typeof(int) }, null)
                ?? throw new MissingMethodException(typeof(T).FullName, ".ctor(STFReader, int)");
            return (T)constructor.Invoke(new object[] { reader, id });
        }

        private static WorldLocation ItemLocation()
        {
            return new WorldLocation(2, 3, 10, 20, 30);
        }

        private static void SetLocation(TrackItem item, WorldLocation location)
        {
            FieldInfo field = typeof(TrackItem).GetField("_location", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(TrackItem).FullName, "_location");
            field.SetValue(item, location);
        }

        private sealed class UnsupportedTrackItem : TrackItem
        {
            public UnsupportedTrackItem(int id)
            {
                TrackItemId = id;
                // A located direct subtype reaches the unsupported switch arm rather than location recovery.
                SetLocation(this, ItemLocation());
            }
        }
    }
}
