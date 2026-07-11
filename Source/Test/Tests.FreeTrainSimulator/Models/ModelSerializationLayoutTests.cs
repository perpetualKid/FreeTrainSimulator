using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models
{
    // Enforces the append-only serialization policy for ALL persisted models. MemoryPack Sequential layout serializes
    // members in base-first declaration order, and because non-[MemoryPackable] base records (e.g. ModelBase) are
    // flattened into their derived types, inserting a member ahead of an existing (including derived) member silently
    // shifts and corrupts reads of pre-existing files. Every [MemoryPackable] type in FreeTrainSimulator.Models has a
    // checked-in expected member order below; three guards keep this complete and honest:
    //   * MemberOrderMatchesSnapshot - each type's reflected order must equal its expected list.
    //   * EveryMemoryPackableTypeHasSnapshot - a newly added [MemoryPackable] type with no snapshot fails the build.
    //   * SnapshotDictionaryHasNoStaleEntries - a removed/renamed type must be dropped from the dictionary.
    // When a change is INTENTIONAL: append the new member LAST in the model, then update its expected list here. Never
    // insert ahead of existing members or reorder them (see the versioning remarks on ModelBase and ADR 0001).
    [TestClass]
    public class ModelSerializationLayoutTests
    {
        // Type -> exact expected serialized member order (base-first flattened). Keep alphabetical by full name.
        private static readonly IReadOnlyDictionary<Type, string[]> Expected = new Dictionary<Type, string[]>
        {
            [typeof(ActivityModel)] = ["Id", "Name", "Version", "Tags", "Description", "Briefing", "StartTime", "Season", "Weather", "Difficulty", "Duration", "ActivityType", "PathId", "ConsistId", "InitialSpeed", "FuelLevels", "HazardProbability", "Settings"],
            [typeof(ActivityModelHeader)] = ["Id", "Name", "Version", "Tags", "Description", "Briefing", "StartTime", "Season", "Weather", "Difficulty", "Duration", "ActivityType", "PathId", "ConsistId"],
            [typeof(ContentModel)] = ["Id", "Name", "Version", "Tags", "ContentFolders"],
            [typeof(FolderModel)] = ["Id", "Name", "Version", "Tags", "ContentPath"],
            [typeof(PathModel)] = ["Id", "Name", "Version", "Tags", "Start", "End", "PlayerPath", "ValidationState", "PathNodes"],
            [typeof(PathModelHeader)] = ["Id", "Name", "Version", "Tags", "Start", "End", "PlayerPath", "ValidationState"],
            [typeof(PathNode)] = ["Location", "NodeType", "NodeIndex", "NextMainNode", "NextSidingNode", "WaitInfo"],
            [typeof(PathNodeWaitInfo)] = ["WaitTime"],
            [typeof(RouteConditionModel)] = ["TrackGauge", "Electrified", "MaxLineVoltage", "OverheadWireHeight", "DoubleWireEnabled", "DoubleWireHeight", "TriphaseEnabled", "TriphaseWidth"],
            [typeof(RouteModel)] = ["Id", "Name", "Version", "Tags", "Description", "MetricUnits", "Graphics", "EnvironmentConditions", "RouteKey", "RouteSounds", "RouteConditions", "SpeedRestrictions", "Settings", "SuperElevationRadiusSettings"],
            [typeof(RouteModelHeader)] = ["Id", "Name", "Version", "Tags", "Description", "MetricUnits", "Graphics"],
            [typeof(TestActivityModel)] = ["Id", "Name", "Version", "Tags", "Description", "Briefing", "StartTime", "Season", "Weather", "Difficulty", "Duration", "ActivityType", "PathId", "ConsistId", "Folder", "Route", "Activity", "Tested", "Passed", "Errors", "Load", "FPS"],
            [typeof(TimetableModel)] = ["Id", "Name", "Version", "Tags", "TimetableTrains"],
            [typeof(TimetableTrainModel)] = ["Id", "Name", "Version", "Tags", "Group", "Briefing", "WagonSet", "WagonSetReverse", "Path", "StartTime"],
            [typeof(WagonReferenceModel)] = ["Id", "Name", "Version", "Tags", "TrainCarType", "Description", "Uid", "Reverse", "Reference"],
            [typeof(WagonSetModel)] = ["Id", "Name", "Version", "Tags", "MaximumSpeed", "AccelerationFactor", "Durability", "TrainCars"],
            [typeof(WeatherModelHeader)] = ["Id", "Name", "Version", "Tags"],

            [typeof(AllProfileSettingsModel)] = ["Id", "Name", "Version", "Tags", "Profile", "UpdateMode"],
            [typeof(ProfileDispatcherSettingsModel)] = ["Id", "Name", "Version", "Tags", "WindowSettings", "WindowScreen"],
            [typeof(ProfileKeyboardSettingsModel)] = ["Id", "Name", "Version", "Tags"],
            [typeof(ProfileModel)] = ["Id", "Name", "Version", "Tags"],
            [typeof(ProfileRailDriverSettingsModel)] = ["Id", "Name", "Version", "Tags"],
            [typeof(ProfileSelectionsModel)] = ["Id", "Name", "Version", "Tags", "FolderName", "RouteId", "ActivityType", "PathId", "ActivityId", "LocomotiveId", "WagonSetId", "StartTime", "TimetableSet", "TimetableName", "TimetableTrain", "TimetableDay", "WeatherChanges", "Season", "Weather", "GamePlayAction", "GameSaveFile"],
            [typeof(ProfileUserSettingsModel)] = ["Id", "Name", "Version", "Tags", "LogLevel", "LogFileName", "LogFilePath", "ErrorDialogEnabled", "ShapeWarnings", "ConfigurationMessages", "Language", "PressureUnit", "MeasurementUnit", "PerformanceTuner", "PerformanceTunerTarget", "PauseAtStart", "TcsScripts", "NotificationsTimeout", "Confirmations", "Alerter", "AlerterExternal", "SpeedControl", "WindowSettings", "ScreenMode", "WindowScreen", "PopupLocations", "PopupStatus", "PopupSettings", "OdometerShortDistances", "VibrationLevel", "SoundVolumePercent", "SoundDetailLevel", "ExternalSoundPassThruPercent", "MultiSamplingCount", "DynamicShadows", "ShadowAllShapes", "ModelInstancing", "OverheadWireType", "VerticalSync", "Cab2DStretch", "ViewingDistance", "FarMountainsViewingDistance", "FieldOfView", "ExtendedDetailLevelView", "DetailLevelBias", "VisibleDetailLevel", "AmbientBrightness", "ShadowMapBlur", "ShadowMapCount", "ShadowMapResolution", "SignalLightGlow", "AdvancedAdhesion", "AdhesionFilterSize", "AdhesionFactor", "AdhesionFactorChange", "WeatherDependentAdhesion", "CouplersBreak", "CurveDependentSpeedLimits", "SimplifiedControls", "SteamHotStart", "DieselEngineRun", "ElectricPowerConnected", "ActivityRandomizationLevel", "WeatherRandomizationLevel", "ComputerTrainDoors", "GraduatedRelease", "RetainersOnAllCars", "BrakePipeChargingRate", "SuperElevationLevel", "TrackGauge", "UseLocationPassingPaths", "MstsEnvironment", "ForcedRedStationStops", "ValidateBrakingParams", "MultiplayerUser", "MultiplayerHost", "MultiplayerPort", "WebServer", "WebServerPort", "DataLogger", "DataLogSeparator", "DataLogSpeedUnits", "DataLogStart", "DataLogPerformance", "DataLogPhysics", "DataLogMisc", "DataLogSteamPerformance", "EvaluationTrainSpeed", "EvaluationInterval", "EvaluationContent", "EvaluationStationStops", "Profiling", "ProfilingFrameCount", "ProfilingTime", "ProfilingFps", "ReplayPause", "ReplayPauseDuration", "MultiPlayer"],
            [typeof(SavePointModel)] = ["Id", "Name", "Version", "Tags", "Route", "Path", "GameTime", "RealTime", "CurrentTile", "DistanceTravelled", "ValidState", "MultiplayerGame", "DebriefEvaluation"],

            [typeof(SignalAspect)] = ["Aspect", "DrawStateName", "SpeedLimit", "AspectFlags"],
            [typeof(SignalConfigurationModel)] = ["Id", "Name", "Version", "Tags", "LightTextures", "SignalTypes", "SignalShapes", "ScriptFiles", "CustomFunctionTypes", "CustomNormalSubTypes"],
            [typeof(SignalDrawState)] = ["Index", "Name", "SemaphorePosition", "DrawStateLights"],
            [typeof(SignalLight)] = ["Name", "Radius", "SemaphoreChange"],
            [typeof(SignalLightTexture)] = ["Name", "TextureFile", "U0", "V0", "U1", "V1"],
            [typeof(SignalShape)] = ["ShapeFileName", "Description", "SubObjects"],
            [typeof(SignalSubObject)] = ["MatrixName", "Description", "SignalSubType", "SignalSubSignalType", "SubObjectFlags"],
            [typeof(SignalType)] = ["Name", "Script", "FunctionType", "NormalSubType", "SignalFlags", "FlashTimeOn", "FlashTimeOff", "TransitionTime", "LightTexture", "SemaphoreAnimationnDuration", "DayGlow", "NightGlow", "DayLight", "SignalClearAheadMode", "ClearAheadNumber", "Lights", "DrawStates", "SignalAspects", "ApproachControlLimitPosition", "ApproachControlLimitSpeed"],

            [typeof(CarSpawnerTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags"],
            [typeof(CrossoverTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags", "ShapeIndex", "LinkedCrossoverItem"],
            [typeof(EmptyTrackItem)] = ["TrackItemIndex", "NodeIndex", "SectionDistance", "Flags"],
            [typeof(EndNode)] = ["Location", "WorldTile", "Direction", "NodeIndex", "WorldId"],
            [typeof(HazardTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags"],
            [typeof(JunctionNode)] = ["Location", "WorldTile", "Direction", "NodeIndex", "WorldId", "OpeningAngle", "MainRoute", "ClearanceDistance", "ShapeIndex"],
            [typeof(LevelCrossingTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags"],
            [typeof(MilepostTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags", "DistanceValue"],
            [typeof(PickupTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags"],
            [typeof(PlatformTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags", "PlatformFlags", "LinkedPlatformItem", "StationName", "PlatformName", "MinWaitingTime", "PassengersWaiting"],
            [typeof(RoadLevelCrossingTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags"],
            [typeof(SidingTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags", "SidingFlags", "LinkedSidingItem", "SidingName"],
            [typeof(SignalDirection)] = ["NodeIndex", "JunctionPath"],
            [typeof(SignalTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags", "SignalFlags", "Direction", "SignalData", "SignalType", "SignalDirection", "NormalSignal"],
            [typeof(SoundRegionTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags", "SoundRegionData1", "SoundRegionData2", "SoundRegionData3"],
            [typeof(SpeedpostTrackItem)] = ["Location", "TrackItemIndex", "NodeIndex", "SectionDistance", "Flags", "SpeedValue", "AlternativeSpeedValue", "SpeedpostType", "Angle"],
            [typeof(TrackDatabase)] = ["TrackDataBaseType", "TrackItemSelectors", "TrackNodeConnectors"],
            [typeof(TrackItemIndex)] = ["TrackItems"],
            [typeof(TrackModel)] = ["Id", "Name", "Version", "Tags", "TrackDatabase", "RoadDatabase"],
            [typeof(TrackNodeConnector)] = ["ConnectorType", "Link", "Direction"],
            [typeof(TrackNodeConnectorIndex)] = ["NodeIndex", "InboundCount", "TrackNodeConnectors"],
            [typeof(TrackSection)] = ["SectionIndex", "Gauge", "Length", "Curved", "Radius", "Angle"],
            [typeof(TrackSectionModel)] = ["Id", "Name", "Version", "Tags", "TrackSections", "TrackShapes", "TrackShapePaths"],
            [typeof(TrackShape)] = ["ShapeIndex", "FileName", "MainRoute", "ClearanceDistance", "ShapeType"],
            [typeof(TrackShapeOffset)] = ["Offset", "AngularOffset"],
            [typeof(TrackShapePath)] = ["TrackSections", "ShapeOffset"],
            [typeof(VectorNode)] = ["Location", "WorldTile", "NodeIndex", "WorldId", "VectorSections", "EndLocation"],
            [typeof(VectorSectionNode)] = ["Location", "WorldTile", "Direction", "NodeIndex", "WorldId", "EndLocation", "Flag1", "Flag2", "ShapeIndex"],
        };

        [TestMethod]
        [DynamicData(nameof(ExpectedTypes))]
        public void MemberOrderMatchesSnapshot(Type modelType)
        {
            ArgumentNullException.ThrowIfNull(modelType);

            string[] expected = Expected[modelType];
            IReadOnlyList<string> actual = SerializedMemberOrder(modelType);
            CollectionAssert.AreEqual(expected, actual.ToArray(),
                $"Serialized member order for {modelType.FullName} changed.{Environment.NewLine}" +
                $"Expected: [{string.Join(", ", expected)}]{Environment.NewLine}" +
                $"Actual:   [{string.Join(", ", actual)}]{Environment.NewLine}" +
                "Append new members LAST (never insert ahead of existing/derived members) and update the expected list. See ADR 0001.");
        }

        [TestMethod]
        public void EveryMemoryPackableTypeHasSnapshot()
        {
            IEnumerable<Type> discovered = DiscoverMemoryPackableModelTypes();
            List<string> missing = discovered.Where(type => !Expected.ContainsKey(type)).Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal).ToList();

            Assert.IsEmpty(missing,
                $"These [MemoryPackable] model types have no member-order snapshot and are unguarded against the append-only rule:{Environment.NewLine}" +
                $"{string.Join(Environment.NewLine, missing)}{Environment.NewLine}" +
                "Add each to the Expected dictionary with its serialized member order.");
        }

        [TestMethod]
        public void SnapshotDictionaryHasNoStaleEntries()
        {
            HashSet<Type> discovered = DiscoverMemoryPackableModelTypes().ToHashSet();
            List<string> stale = Expected.Keys.Where(type => !discovered.Contains(type)).Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal).ToList();

            Assert.IsEmpty(stale,
                $"These Expected entries no longer correspond to a [MemoryPackable] model type and should be removed:{Environment.NewLine}" +
                $"{string.Join(Environment.NewLine, stale)}");
        }

        public static IEnumerable<object[]> ExpectedTypes => Expected.Keys.Select(type => new object[] { type });

        private static IEnumerable<Type> DiscoverMemoryPackableModelTypes()
        {
            return typeof(PathModel).Assembly.GetTypes()
                .Where(type => type.GetCustomAttribute<MemoryPackableAttribute>() != null && !type.IsAbstract && !type.IsGenericTypeDefinition);
        }

        // Approximates the MemoryPack Sequential flattened member order: walk the type hierarchy base-first and, for
        // each type, take its declared public instance properties (metadata/declaration order) that MemoryPack
        // serializes. A property is serialized when it has a getter and is either settable (set/init) or backed by a
        // [MemoryPackConstructor] parameter (get-only records such as PathNode.Location). [MemoryPackIgnore] members
        // (inherited by overrides) and the compiler-generated record EqualityContract are excluded.
        private static List<string> SerializedMemberOrder(Type modelType)
        {
            HashSet<string> constructorParameters = MemoryPackConstructorParameterNames(modelType);

            List<Type> hierarchy = new List<Type>();
            for (Type current = modelType; current != null && current != typeof(object); current = current.BaseType)
                hierarchy.Add(current);
            hierarchy.Reverse();

            List<string> members = new List<string>();
            foreach (Type type in hierarchy)
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (property.GetMethod == null)
                        continue;
                    bool settable = property.SetMethod != null;
                    bool constructorBacked = constructorParameters.Contains(property.Name);
                    if (!settable && !constructorBacked)
                        continue;
                    if (property.GetCustomAttribute<MemoryPackIgnoreAttribute>(inherit: true) != null)
                        continue;
                    if (property.Name == "EqualityContract")
                        continue;
                    members.Add(property.Name);
                }
            }
            return members;
        }

        private static HashSet<string> MemoryPackConstructorParameterNames(Type modelType)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConstructorInfo constructor in modelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                if (constructor.GetCustomAttribute<MemoryPackConstructorAttribute>() == null)
                    continue;
                foreach (ParameterInfo parameter in constructor.GetParameters())
                    _ = names.Add(parameter.Name);
            }
            return names;
        }
    }
}
