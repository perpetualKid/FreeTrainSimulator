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
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Simulation = Orts.Simulation.Simulator;

namespace Tests.FreeTrainSimulator.Simulator
{
    /// <summary>Original generated content for public simulator startup; no installed route or stock is required.</summary>
    internal sealed class SyntheticPathFixture
    {
        private static readonly Tile tile = new Tile(1, 1);

        /// <summary>The isolated generated MSTS content folder, retained for failure investigation.</summary>
        public string ContentRoot { get; }
        /// <summary>The route owning the native infrastructure and paths.</summary>
        public RouteModel Route { get; }
        /// <summary>The serialized and reloaded synthetic infrastructure.</summary>
        public TrackModel Track { get; }
        /// <summary>The geometry definitions used by every synthetic section.</summary>
        public TrackSectionModel Sections { get; }
        /// <summary>The generated consist filename resolved by the public explore workflow.</summary>
        public string ConsistFile { get; }

        public SyntheticPathFixture(TestContext context)
        {
            string identity = Guid.NewGuid().ToString("N");
            ContentRoot = Path.Combine(Path.GetTempPath(), "FreeTrainSimulator", "SimulatorPathTests", identity);
            context.WriteLine("Synthetic content retained at: {0}", ContentRoot);
            string routeName = "Synthetic" + identity;
            Directory.CreateDirectory(Path.Combine(ContentRoot, "Routes", routeName, "World"));
            Directory.CreateDirectory(Path.Combine(ContentRoot, "Trains", "Consists"));
            Directory.CreateDirectory(Path.Combine(ContentRoot, "Trains", "Trainset", "BridgeEngine"));
            FolderModel folder = new FolderModel("Bridge" + identity, ContentRoot, null);
            ContentModel content = new ContentModel(ImmutableArray.Create(folder));
            content.Initialize(null);
            Route = new RouteModel(Location(-50, 0))
            {
                Id = routeName,
                Name = routeName,
                Tags = ImmutableDictionary<string, string>.Empty.Add(RouteModelImportHandler.SourceNameKey, routeName),
                RouteConditions = new RouteConditionModel(),
                SpeedRestrictions = new EnumArray<float, SpeedRestrictionType>(_ => 30),
                RouteSounds = new EnumArray<string, DefaultSoundType>(),
            };
            Route.Initialize(folder);
            Sections = new TrackSectionModel
            {
                Id = Route.Id,
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                    new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 }),
            };
            // Approach 1 -> split 3 -> main 4 / passing 5 -> rejoin 6 -> departure 2.
            // The passing vector has three 100 m sections, making its interior physically distinct.
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    Vector(1, Section(-100, 0, 0, 0, MathHelper.PiOver2)),
                    Vector(2, Section(100, 0, 200, 0, MathHelper.PiOver2)),
                    new JunctionNode(Location(0, 0), tile, Vector3.Zero) { NodeIndex = 3 },
                    Vector(4, Section(0, 0, 100, 0, MathHelper.PiOver2)),
                    Vector(5, Section(0, 0, 0, 100, 0), Section(0, 100, 100, 100, MathHelper.PiOver2),
                        Section(100, 100, 100, 0, MathHelper.Pi)),
                    new JunctionNode(Location(100, 0), tile, Vector3.Zero) { NodeIndex = 6 },
                    new EndNode(Location(-100, 0), tile, Vector3.Zero) { NodeIndex = 7 },
                    new EndNode(Location(200, 0), tile, Vector3.Zero) { NodeIndex = 8 }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    Pins(1, 1, (7, TrackDirection.Ahead), (3, TrackDirection.Reverse)),
                    Pins(2, 1, (6, TrackDirection.Reverse), (8, TrackDirection.Reverse)),
                    Pins(3, 1, (1, TrackDirection.Ahead), (4, TrackDirection.Reverse), (5, TrackDirection.Reverse)),
                    Pins(4, 1, (3, TrackDirection.Ahead), (6, TrackDirection.Ahead)),
                    Pins(5, 1, (3, TrackDirection.Ahead), (6, TrackDirection.Ahead)),
                    Pins(6, 1, (2, TrackDirection.Reverse), (4, TrackDirection.Ahead), (5, TrackDirection.Ahead)),
                    Pins(7, 0, (1, TrackDirection.Reverse)), Pins(8, 1, (2, TrackDirection.Ahead))),
            };
            Track = MemoryPackSerializer.Deserialize<TrackModel>(MemoryPackSerializer.Serialize(new TrackModel { Id = Route.Id, TrackDatabase = database }));
            ConsistFile = Path.Combine(ContentRoot, "Trains", "Consists", "Bridge.con");
            File.WriteAllText(ConsistFile,
                """
                SIMISA@@@@@@@@@@JINX0D0t______
                Train ( TrainCfg ( Bridge
                    Name ( "Synthetic bridge engine" )
                    MaxVelocity ( 30 1 )
                    Engine ( UiD ( 0 ) EngineData ( BridgeEngine BridgeEngine ) )
                ) )
                """);
            File.WriteAllText(Path.Combine(ContentRoot, "Trains", "Trainset", "BridgeEngine", "BridgeEngine.eng"),
                """
                SIMISA@@@@@@@@@@JINX0D0t______
                Wagon ( BridgeEngine
                    Type ( Engine )
                    Size ( 3m 4m 10m )
                    Mass ( 40t )
                    WheelRadius ( 0.5m )
                    NumWheels ( 4 )
                    BrakeEquipmentType ( "Triple_valve, Auxilary_reservoir, Emergency_brake_reservoir" )
                    BrakeSystemType ( Air_single_pipe )
                    MaxBrakeForce ( 40kN )
                )
                Engine ( BridgeEngine
                    Type ( Diesel )
                    MaxPower ( 100kW )
                    MaxForce ( 20kN )
                    MaxVelocity ( 30m/s )
                    MaxDieselLevel ( 100l )
                )
                """);
        }

        /// <summary>Loads infrastructure through the same model handlers used by Simulator.CreateAsync.</summary>
        public Task<Simulation> CreateSimulator(bool locationPassingPaths, CancellationToken cancellationToken)
            => CreateSimulator(Track, locationPassingPaths, cancellationToken);

        /// <summary>Persists the supplied native infrastructure before loading it through public simulator creation.</summary>
        public async Task<Simulation> CreateSimulator(TrackModel track, bool locationPassingPaths, CancellationToken cancellationToken)
        {
            await RouteModelHandler.ToFile(Route, cancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(track, Route, cancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(Sections, Route, cancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel
            {
                Id = Route.Id,
                Tags = ImmutableDictionary<string, string>.Empty,
            }, Route, cancellationToken).ConfigureAwait(false);
            ProfileUserSettingsModel settings = new ProfileUserSettingsModel
            {
                UseLocationPassingPaths = locationPassingPaths,
                ErrorDialogEnabled = false,
                SoundVolumePercent = 0,
                SoundDetailLevel = 0,
                TcsScripts = false,
            };
            return await Simulation.CreateAsync(settings, Route, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Resolves a user-selected main or passing traversal, generates anchors, and persists its round trip.</summary>
        public Task<PathModel> GenerateSelectedPath(int selectedVector, CancellationToken cancellationToken)
        {
            PathModel source = new PathModel
            {
                Id = "Selected" + Guid.NewGuid().ToString("N"),
                Name = "Synthetic selected route",
                PathNodes = selectedVector == 5
                    ? ImmutableArray.Create(Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                        Node(PathNodeType.Via, 5, 50, 100, 2, -1),
                        Node(PathNodeType.End, 2, 150, 0, -1, -1))
                    : ImmutableArray.Create(Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                        Node(PathNodeType.End, 2, 150, 0, -1, -1)),
            };
            return GeneratePathAsync(source, 0, selectedVector, cancellationToken);
        }

        /// <summary>Builds a passing graph with authored boundaries on vectors or physical switches.</summary>
        public static PathModel CreatePassingSource(bool vectorBound)
        {
            return new PathModel
            {
                Id = "Passing" + Guid.NewGuid().ToString("N"),
                Name = "Synthetic passing route",
                PathNodes = vectorBound
                    ? ImmutableArray.Create(Node(PathNodeType.Start, 1, -50, 0, 1, 2),
                        Node(PathNodeType.End, 2, 150, 0, -1, -1),
                        Node(PathNodeType.Via, 5, 50, 100, -1, 1))
                    : ImmutableArray.Create(Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                        Node(PathNodeType.Junction | PathNodeType.Via, 3, 0, 0, 2, 4),
                        Node(PathNodeType.Junction | PathNodeType.Via, 6, 100, 0, 3, -1),
                        Node(PathNodeType.End, 2, 150, 0, -1, -1),
                        Node(PathNodeType.Via, 5, 50, 100, -1, 2)),
            };
        }

        /// <summary>Generates and reloads the selected main route together with its distinct passing route.</summary>
        public Task<PathModel> GeneratePassingPath(bool vectorBound, CancellationToken cancellationToken)
        {
            return GeneratePathAsync(CreatePassingSource(vectorBound), vectorBound ? 0 : 1, 4, cancellationToken);
        }

        /// <summary>Generates a passing graph beginning at the physical split, without an approach anchor.</summary>
        public Task<PathModel> GenerateSplitStartPassingPathAsync(CancellationToken cancellationToken)
        {
            PathModel source = new PathModel
            {
                Id = "SplitStart" + Guid.NewGuid().ToString("N"),
                Name = "Synthetic split-start passing route",
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start | PathNodeType.Junction, 3, 0, 0, 1, 3),
                    Node(PathNodeType.Junction | PathNodeType.Via, 6, 100, 0, 2, -1),
                    Node(PathNodeType.End, 2, 150, 0, -1, -1),
                    Node(PathNodeType.Via, 5, 50, 100, -1, 1)),
            };
            return GeneratePathAsync(source, 0, 4, cancellationToken);
        }

        /// <summary>Serializes and reloads a native path through the isolated production model store.</summary>
        public async Task<PathModel> RoundTripPathAsync(PathModel source, CancellationToken cancellationToken)
        {
            PathModel roundTrip = MemoryPackSerializer.Deserialize<PathModel>(MemoryPackSerializer.Serialize(source));
            await PathModelHandler.UpdatePath(roundTrip, Route, cancellationToken).ConfigureAwait(false);
            return await PathModelHandler.GetExtended(roundTrip.Id, Route, cancellationToken).ConfigureAwait(false);
        }

        private async Task<PathModel> GeneratePathAsync(PathModel source, int sourceIndex, int selectedVector, CancellationToken cancellationToken)
        {
            TrackWorld world = TrackWorld.Instance;
            PathRouteResolution initial = PathRouteResolver.Resolve(source, world, cancellationToken);
            ResolvedRouteCandidate choice = initial.MainRoute.Spans.Single(span => span.FromNodeIndex == sourceIndex)
                .Candidates.Single(candidate => candidate.RouteNodeIndexes.Contains(selectedVector));
            PathRouteResolution selected = PathRouteResolver.ResolveSelectedRoutes(source, world, PathRouteResolverOptions.Default,
                ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(sourceIndex, choice), cancellationToken);
            PathGenerationResult generated = PathModelRouteGenerator.GeneratePath(source, selected, world, PathRouteResolverOptions.Default);
            Assert.IsTrue(generated.Success, generated.Message);
            return await RoundTripPathAsync(generated.PathModel, cancellationToken).ConfigureAwait(false);
        }

        public static WorldLocation Location(float x, float z) => new WorldLocation(tile, new Vector3(x, 0, z));

        public static PathNode Node(PathNodeType type, int trackNode, float x, float z, int main, int siding)
            => new PathNode(Location(x, z)) { NodeType = type, NodeIndex = trackNode, NextMainNode = main, NextSidingNode = siding };

        private static VectorSectionNode Section(float x, float z, float endX, float endZ, float heading)
            => new VectorSectionNode(Location(x, z), tile, new Vector3(0, heading, 0), Location(endX, endZ)) { NodeIndex = 1 };

        private static VectorNode Vector(int index, params VectorSectionNode[] sections)
            => new VectorNode(sections[0].Location, tile, sections[^1].EndLocation)
            { NodeIndex = index, VectorSections = sections.ToImmutableArray() };

        private static TrackNodeConnectorIndex Pins(int index, int inbound, params (int Link, TrackDirection Direction)[] pins)
            => new TrackNodeConnectorIndex
            {
                NodeIndex = index,
                InboundCount = inbound,
                TrackNodeConnectors = pins.Select((pin, pinIndex) => new TrackNodeConnector
                {
                    Link = pin.Link,
                    Direction = pin.Direction,
                    ConnectorType = pinIndex < inbound ? ConnectorType.InPin : ConnectorType.OutPin,
                }).ToImmutableArray(),
            };
    }
}
