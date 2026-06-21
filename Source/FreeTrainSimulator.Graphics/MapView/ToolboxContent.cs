using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    public enum ToolboxContentMode
    {
        ViewRoute,
        ViewPath,
        EditPath,
    }

    public sealed class ToolboxContent : ContentBase, IPathEditorContext, IPathEditorContextServicesAccessor, ITrackNodeInfoContext, ITrackItemInfoContext
    {
        private (double distance, INameValueInformationProvider statusItem) nearestSegmentForStatus;
        private (double distance, INameValueInformationProvider statusItem) nearestItemForStatus;

        private ToolboxContentMode contentMode;

        public PathEditorBase PathEditor { get; set; }

        public INameValueInformationProvider TrackNodeInfo { get; } = new DetailInfoProxy();

        public INameValueInformationProvider TrackItemInfo { get; } = new DetailInfoProxy();

        // Detail rows for the entity last selected in the route-navigation search lists. Distinct from the
        // hover-driven TrackNodeInfo/TrackItemInfo proxies: this is pinned until the next selection, so the
        // navigation window can show the selected item's details even when the cursor is not over the map.
        private readonly PinnedInfoProvider pinnedNavigationInfo = new PinnedInfoProvider();

        /// <summary>Read-only provider of the detail rows for the last route-navigation selection.</summary>
        public INameValueInformationProvider PinnedNavigationInfo => pinnedNavigationInfo;

        // Materialized navigation lists for the route-navigation tool window. The map entity widgets stay
        // internal, so each entry pairs a widget with the public Index/Name/GroupName item it backs; the public
        // lists below are projections of these entries. Pairing widget and item in a single index-aligned source
        // guarantees the index the WPF shell sends back to NavigateTo resolves to the matching map entity.
        private ImmutableArray<NavEntry<PlatformPath>> navigationPlatforms = ImmutableArray<NavEntry<PlatformPath>>.Empty;
        private ImmutableArray<NavEntry<SidingPath>> navigationSidings = ImmutableArray<NavEntry<SidingPath>>.Empty;
        private ImmutableArray<NavEntry<StationNameItem>> navigationStations = ImmutableArray<NavEntry<StationNameItem>>.Empty;

        /// <summary>Selectable station entries (by aggregated platform location) for route navigation.</summary>
        public ImmutableArray<RouteNavigationItem> Stations { get; private set; } = ImmutableArray<RouteNavigationItem>.Empty;

        /// <summary>Selectable platform entries, grouped by their owning station, for route navigation.</summary>
        public ImmutableArray<RouteNavigationItem> Platforms { get; private set; } = ImmutableArray<RouteNavigationItem>.Empty;

        /// <summary>Selectable siding entries, grouped by their nearest station, for route navigation.</summary>
        public ImmutableArray<RouteNavigationItem> Sidings { get; private set; } = ImmutableArray<RouteNavigationItem>.Empty;

        /// <summary>
        /// Centers the map viewport on the route entity of the given <paramref name="kind"/> at
        /// <paramref name="index"/> and highlights it on the map. Out-of-range indices are ignored. Must be
        /// called on the game thread (the viewport and highlight state are game-thread owned).
        /// </summary>
        public void NavigateTo(RouteNavigationKind kind, int index)
        {
            switch (kind)
            {
                case RouteNavigationKind.Station when index >= 0 && index < navigationStations.Length:
                    StationNameItem station = navigationStations[index].Widget;
                    Viewport?.UpdateScaleToFit(station.TopLeftBound, station.BottomRightBound);
                    Viewport?.SetTrackingPosition(station.Location);
                    HighlightItem(MapContentType.StationNames, station);
                    pinnedNavigationInfo.Set(BuildStationDetails(station));
                    break;

                case RouteNavigationKind.Platform when index >= 0 && index < navigationPlatforms.Length:
                    PlatformPath platform = navigationPlatforms[index].Widget;
                    Viewport?.UpdateScaleToFit(platform.TopLeftBound, platform.BottomRightBound);
                    Viewport?.SetTrackingPosition(platform.MidPoint);
                    HighlightItem(MapContentType.Platforms, platform);
                    pinnedNavigationInfo.Set(BuildPlatformDetails(platform));
                    break;

                case RouteNavigationKind.Siding when index >= 0 && index < navigationSidings.Length:
                    SidingPath siding = navigationSidings[index].Widget;
                    Viewport?.UpdateScaleToFit(siding.TopLeftBound, siding.BottomRightBound);
                    Viewport?.SetTrackingPosition(siding.MidPoint);
                    HighlightItem(MapContentType.Sidings, siding);
                    pinnedNavigationInfo.Set(BuildSidingDetails(siding));
                    break;
            }
        }

        /// <summary>
        /// Navigates the map to the track item with the given index and pins its details. Out-of-range indices
        /// are ignored. Must be called on the game thread.
        /// </summary>
        public void NavigateToTrackItem(int index)
        {
            Models.Track.TrackItemBase trackItem = trackWorld?.TrackItemByIndex(index);
            if (trackItem == null)
                return;

            Viewport?.SetTrackingPosition(trackItem.Location);
            pinnedNavigationInfo.Set(BuildTrackItemDetails(index, trackItem));
        }

        /// <summary>
        /// Navigates the map to the track node with the given index, fitting and highlighting it, and pins its
        /// details. When <paramref name="searchRoads"/> is true the search targets road nodes; otherwise rail
        /// nodes. Out-of-range indices are ignored. Must be called on the game thread.
        /// </summary>
        public void NavigateToTrackNode(int index, bool searchRoads)
        {
            if (trackWorld == null || index < 0)
                return;

            ImmutableArray<TrackSegmentSection> sections = searchRoads ? trackWorld.RoadSegmentSections : trackWorld.SegmentSections;
            if (index >= sections.Length)
                return;

            TrackSegmentSection section = sections[index];
            if (section == null)
                return;

            Viewport?.UpdateScaleToFit(section.TopLeftBound, section.BottomRightBound);
            Viewport?.SetTrackingPosition(section.MidPoint);
            HighlightItem(searchRoads ? MapContentType.Roads : MapContentType.Tracks, section.SectionSegments[0]);
            pinnedNavigationInfo.Set(BuildTrackNodeDetails(index, searchRoads, section));
        }

        public ToolboxContentMode ContentMode
        {
            get => contentMode;
            set
            {
                contentMode = value;
                if (value == ToolboxContentMode.ViewPath)
                    viewSettings[MapContentType.Paths] = true;
            }
        }

        private readonly IPathEditorServices pathEditorServices;

        internal ToolboxContent(MapContentContext context) :
            base(context)
        {
            pathEditorServices = new PathEditorServices(runtimeServices.TrackWorld);
            FormattingOptions.Add("Route Information", FormatOption.Bold);
            DetailInfo.Add("Route Information", null);
            DetailInfo["Route Name"] = runtimeServices.RouteName;
        }

        public override async Task Initialize()
        {
            await Task.Run(AddTrackSegments).ConfigureAwait(true);
            await Task.Run(AddTrackItems).ConfigureAwait(true);

            ShellServices.Initialize();
            //just put an empty list so the draw method does not skip the paths
            ContentByTile[MapContentType.Paths] = new TileIndexedList<EditorTrainPath>(new List<EditorTrainPath>() { });

            DetailInfo["Metric Scale"] = runtimeServices.UseMetricUnits.ToString();
            DetailInfo["Track Nodes"] = $"{trackWorld.SegmentSections.Length}";
            DetailInfo["Track Segments"] = $"{ContentByTile[MapContentType.Tracks].ItemCount}";
            DetailInfo["Track End Segments"] = $"{ContentByTile[MapContentType.EndNodes].ItemCount}";
            DetailInfo["Junction Segments"] = $"{ContentByTile[MapContentType.JunctionNodes].ItemCount}";
            DetailInfo["Road Nodes"] = $"{trackWorld.RoadSegmentSections.Length}";
            DetailInfo["Road Segments"] = $"{ContentByTile[MapContentType.Roads].ItemCount}";
            DetailInfo["Road End Segments"] = $"{ContentByTile[MapContentType.RoadEndNodes].ItemCount}";
            DetailInfo["Tiles"] = $"{ContentByTile[MapContentType.Grid].Count}";
        }

        public void UpdateWidgetColorSettings(EnumArray<string, ColorSetting> colorPreferences, bool fontOutlining, bool limitTrackWidth)
        {
            ArgumentNullException.ThrowIfNull(colorPreferences);

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                ShellServices.UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]), fontOutlining);
            }
            ShellServices.UpdateTrackWidthSettings(limitTrackWidth);
        }

        IPathEditorServices IPathEditorContextServicesAccessor.Services => pathEditorServices;

        IMapRenderer IPathEditorContext.Renderer => Renderer;

        IMapViewport IPathEditorContext.Viewport => Viewport;

        ToolboxContentMode IPathEditorContext.ContentMode
        {
            get => ContentMode;
            set => ContentMode = value;
        }

        PathEditorBase IPathEditorContext.PathEditor
        {
            get => PathEditor;
            set => PathEditor = value;
        }

        IMapViewport ITrackNodeInfoContext.Viewport => Viewport;

        IMapHostControl ITrackNodeInfoContext.HostControl => HostControl;

        ToolboxContent ITrackNodeInfoContext.Content => this;

        TrackWorld ITrackNodeInfoContext.TrackWorld => trackWorld;

        INameValueInformationProvider ITrackItemInfoContext.TrackItemInfo => TrackItemInfo;

        IMapViewport ITrackItemInfoContext.Viewport => Viewport;

        TrackWorld ITrackItemInfoContext.TrackWorld => trackWorld;

        internal override void UpdatePointerLocation(in PointD position, in Tile bottomLeft, in Tile topRight)
        {
            nearestSegmentForStatus = (float.MaxValue, null);
            nearestItemForStatus = (float.MaxValue, null);
            GridTile nearestGridTile = ContentByTile[MapContentType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapContentType.Grid] as GridTile)
                nearestItems[MapContentType.Grid] = nearestGridTile;

            foreach (MapContentType viewItem in EnumExtension.GetValues<MapContentType>())
            {
                double distanceSquared = double.MaxValue;
                if (viewItem == MapContentType.Grid)
                    //already checked above
                    continue;
                if (viewSettings[viewItem] && ContentByTile[viewItem] != null)
                {
                    foreach (ITileCoordinate item in ContentByTile[viewItem].BoundingBox(bottomLeft, topRight))
                    {
                        if (item is VectorPrimitive vectorPrimitive)
                        {
                            double itemDistance = vectorPrimitive.DistanceSquared(position);
                            if (itemDistance < distanceSquared)
                            {
                                nearestItems[viewItem] = vectorPrimitive;
                                distanceSquared = itemDistance;
                            }
                        }
                        else if (item is PointPrimitive pointPrimitive)
                        {
                            double itemDistance = pointPrimitive.Location.DistanceSquared(position);
                            if (itemDistance < distanceSquared)
                            {
                                nearestItems[viewItem] = pointPrimitive;
                                distanceSquared = itemDistance;
                            }
                        }
                    }
                }
                if (distanceSquared < 1000)
                {
                    switch (viewItem)
                    {
                        case MapContentType.Tracks:
                        case MapContentType.JunctionNodes:
                        case MapContentType.EndNodes:
                        case MapContentType.Roads:
                        case MapContentType.RoadCrossings:
                        case MapContentType.RoadEndNodes:
                            if (distanceSquared < 1 || distanceSquared < nearestSegmentForStatus.distance)
                                nearestSegmentForStatus = (distanceSquared, nearestItems[viewItem] as INameValueInformationProvider);
                            break;
                        default:
                            if (distanceSquared < 1 || distanceSquared < nearestItemForStatus.distance)
                                nearestItemForStatus = (distanceSquared, nearestItems[viewItem] as INameValueInformationProvider);
                            break;
                    }
                }
                else
                    nearestItems[viewItem] = null;
            }

            (TrackNodeInfo as DetailInfoProxy).Source = nearestSegmentForStatus.statusItem;
            (TrackItemInfo as DetailInfoProxy).Source = nearestItemForStatus.statusItem;

            if (ContentMode == ToolboxContentMode.EditPath)
            {
                PathEditor?.UpdatePointerLocation(position, nearestItems[MapContentType.Tracks] as TrackSegment);
                ShellServices.RequestRedraw();
            }
        }

        internal override void Draw(in Tile bottomLeft, in Tile topRight)
        {
            foreach (MapContentType viewItemSetting in EnumExtension.GetValues<MapContentType>())
            {
                if (viewSettings[viewItemSetting] && ContentByTile[viewItemSetting] != null)
                {
                    if (viewItemSetting == MapContentType.Paths)
                        PathEditor?.Draw();
                    else
                    {
                        foreach (ITileCoordinate item in ContentByTile[viewItemSetting].BoundingBox(bottomLeft, topRight))
                        {
                            // this could also be resolved otherwise also if rather vectorwidget & pointwidget implement InsideScreenArea() function
                            // but the performance impact/overhead seems invariant
                            if (item is VectorPrimitive vectorPrimitive && Viewport.InsideScreenArea(vectorPrimitive))
                                (item as IDrawable<VectorPrimitive>).Draw(Renderer);
                            else if (item is PointPrimitive pointPrimitive && Viewport.InsideScreenArea(pointPrimitive))
                            {
                                (item as IDrawable<PointPrimitive>).Draw(Renderer);
                            }
                        }
                    }
                }
            }
            if (ContentMode == ToolboxContentMode.ViewRoute || !viewSettings[MapContentType.Paths])
            {
                if (null != nearestItems[MapContentType.Tracks])
                {
                    foreach (TrackSegmentBase segment in trackWorld.SegmentSections[(nearestItems[MapContentType.Tracks] as TrackSegmentBase).TrackNodeIndex].SectionSegments)
                    {
                        (segment as IDrawable<VectorPrimitive>).Draw(Renderer, ColorVariation.ComplementHighlight);
                    }
                }
                if (null != nearestItems[MapContentType.Roads])
                {
                    foreach (TrackSegmentBase segment in trackWorld.RoadSegmentSections[(nearestItems[MapContentType.Roads] as TrackSegmentBase).TrackNodeIndex].SectionSegments)
                    {
                        (segment as IDrawable<VectorPrimitive>).Draw(Renderer, ColorVariation.ComplementHighlight);
                    }
                }

                foreach (MapContentType viewItemSettings in EnumExtension.GetValues<MapContentType>())
                {
                    if (viewSettings[viewItemSettings] && nearestItems[viewItemSettings] != null)
                    {
                        if (nearestItems[viewItemSettings] is VectorPrimitive vectorPrimitive && Viewport.InsideScreenArea(vectorPrimitive))
                            (vectorPrimitive as IDrawable<VectorPrimitive>).Draw(Renderer, ColorVariation.Complement);
                        else if (nearestItems[viewItemSettings] is PointPrimitive pointPrimitive && Viewport.InsideScreenArea(pointPrimitive))
                            (pointPrimitive as IDrawable<PointPrimitive>).Draw(Renderer, ColorVariation.Complement);
                    }
                }
            }
        }

        #region build content database
        private void AddTrackSegments()
        {
            TrackDatabase trackDatabase = runtimeServices.TrackWorld.TrackDatabase;
            TrackDatabase roadDatabase = runtimeServices.TrackWorld.RoadDatabase;

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<Widgets.EndNode> endSegments = new ConcurrentBag<Widgets.EndNode>();
            ConcurrentBag<Widgets.JunctionNode> junctionSegments = new ConcurrentBag<Widgets.JunctionNode>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            if (trackDatabase != null)
            {
                Parallel.ForEach(trackDatabase.TrackNodes, trackNode =>
                {
                    switch (trackNode)
                    {
                        case Models.Track.EndNode endNode:
                            endSegments.Add(new Widgets.EndNode(endNode));
                            break;
                        case VectorNode trackVectorNode:
                            foreach ((VectorSectionNode section, int index) in trackVectorNode.VectorSections.IndexedSelect())
                            {
                                trackSegments.Add(new TrackSegment(section, trackVectorNode.NodeIndex, index));
                            }
                            break;
                        case Models.Track.JunctionNode trackJunctionNode:
                            junctionSegments.Add(new Widgets.JunctionNode(trackJunctionNode,
                                trackDatabase.TrackNodeConnectors[trackJunctionNode.NodeIndex].OutConnectors[trackJunctionNode.MainRoute].Link));
                            break;
                    }
                });
            }

            InsetHost?.SetTrackSegments(trackSegments);

            ContentByTile[MapContentType.Tracks] = new TileIndexedList<TrackSegmentBase>(trackSegments);
            ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNodeBase>(junctionSegments);
            ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNodeBase>(endSegments);

            if (roadDatabase != null)
            {
                Parallel.ForEach(roadDatabase.TrackNodes, trackNode =>
                {
                    switch (trackNode)
                    {
                        case Models.Track.EndNode trackEndNode:
                            roadEndSegments.Add(new Widgets.RoadEndSegment(trackEndNode));
                            break;
                        case VectorNode trackVectorNode:
                            foreach ((VectorSectionNode section, int index) in trackVectorNode.VectorSections.IndexedSelect())
                            {
                                roadSegments.Add(new RoadSegment(section, trackVectorNode.NodeIndex, index));
                            }
                            break;
                    }
                });
            }

            ContentByTile[MapContentType.Roads] = new TileIndexedList<TrackSegmentBase>(roadSegments);
            ContentByTile[MapContentType.RoadEndNodes] = new TileIndexedList<EndNodeBase>(roadEndSegments);

            trackWorld = runtimeServices.TrackWorld;
            trackWorld.SetSegmentSections(trackSegments.GroupBy(t => t.TrackNodeIndex).Select(group => new TrackSegmentSection(group.Key, group)));
            trackWorld.SetRoadSegmentSections(roadSegments.GroupBy(t => t.TrackNodeIndex).Select(group => new TrackSegmentSection(group.Key, group)));
            trackWorld.SetJunctions(junctionSegments);

            ContentByTile[MapContentType.Grid] = new TileIndexedList<GridTile>(
                ContentByTile[MapContentType.Tracks].Select(d => d.Tile).Distinct()
                .Union(ContentByTile[MapContentType.EndNodes].Select(d => d.Tile).Distinct())
                .Union(ContentByTile[MapContentType.Roads].Select(d => d.Tile).Distinct())
                .Union(ContentByTile[MapContentType.RoadEndNodes].Select(d => d.Tile).Distinct())
                .Select(t => new GridTile(t)));

            InitializeBounds();
        }

        private void AddTrackItems()
        {
            // Materialized once to avoid repeated enumeration of the concatenated sequence (CA1851).
            List<TrackItemWidget> trackItems = TrackItemWidget.CreateTrackItems(
                trackWorld.TrackDatabase,
                trackWorld).Concat(TrackItemWidget.CreateRoadItems(trackWorld.RoadDatabase)).ToList();

            IEnumerable<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackWorld, trackItems.OfType<Widgets.PlatformTrackItem>());
            ContentByTile[MapContentType.Platforms] = new TileIndexedList<PlatformPath>(platforms);

            IEnumerable<SidingPath> sidings = SidingPath.CreateSidings(trackWorld, trackItems.OfType<Widgets.SidingTrackItem>());
            ContentByTile[MapContentType.Sidings] = new TileIndexedList<SidingPath>(sidings);

            ContentByTile[MapContentType.Signals] = new TileIndexedList<Widgets.SignalTrackItem>(trackItems.OfType<Widgets.SignalTrackItem>().Where(s => s.Normal));
            ContentByTile[MapContentType.OtherSignals] = new TileIndexedList<Widgets.SignalTrackItem>(trackItems.OfType<Widgets.SignalTrackItem>().Where(s => !s.Normal));

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName, StringComparer.OrdinalIgnoreCase);
            ContentByTile[MapContentType.StationNames] = new TileIndexedList<StationNameItem>(StationNameItem.CreateStationItems(stations));
            ContentByTile[MapContentType.PlatformNames] = new TileIndexedList<PlatformNameItem>(platforms.Select(p => new PlatformNameItem(p)));
            ContentByTile[MapContentType.SidingNames] = new TileIndexedList<SidingNameItem>(sidings.Select(p => new SidingNameItem(p)));
            ContentByTile[MapContentType.LevelCrossings] = new TileIndexedList<Widgets.LevelCrossingTrackItem>(trackItems.OfType<Widgets.LevelCrossingTrackItem>());
            ContentByTile[MapContentType.SpeedPosts] = new TileIndexedList<SpeedPostTrackItem>(trackItems.OfType<SpeedPostTrackItem>());
            ContentByTile[MapContentType.MilePosts] = new TileIndexedList<MilePostTrackItem>(trackItems.OfType<MilePostTrackItem>());
            ContentByTile[MapContentType.Hazards] = new TileIndexedList<Widgets.HazardTrackItem>(trackItems.OfType<Widgets.HazardTrackItem>());
            ContentByTile[MapContentType.Pickups] = new TileIndexedList<Widgets.PickupTrackItem>(trackItems.OfType<Widgets.PickupTrackItem>());
            ContentByTile[MapContentType.SoundRegions] = new TileIndexedList<Widgets.SoundRegionTrackItem>(trackItems.OfType<Widgets.SoundRegionTrackItem>());
            ContentByTile[MapContentType.CarSpawners] = new TileIndexedList<Widgets.CarSpawnerTrackItem>(trackItems.OfType<Widgets.CarSpawnerTrackItem>());
            ContentByTile[MapContentType.RoadCrossings] = new TileIndexedList<CrossOverTrackItem>(trackItems.OfType<CrossOverTrackItem>());

            BuildNavigationData(platforms, sidings);
        }

        // Builds the route-navigation lists from the materialized platform/siding widgets. Platforms keep their
        // owning station name; sidings (which have no station link) are grouped by their nearest station marker.
        // Each entry pairs the widget with its public item under a single shared index, so the public projections
        // and NavigateTo can never drift out of alignment. Runs on the AddTrackItems background thread.
        private void BuildNavigationData(IEnumerable<PlatformPath> platforms, IEnumerable<SidingPath> sidings)
        {
            ImmutableArray<PlatformPath> orderedPlatforms = platforms.OrderBy(p => p.StationName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.PlatformName, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
            ImmutableArray<SidingPath> orderedSidings = sidings.OrderBy(s => s.SidingName, StringComparer.OrdinalIgnoreCase).ToImmutableArray();

            // Reuse the single aggregated StationNameItem per station that AddTrackItems already built for the
            // StationNames render layer instead of re-grouping platforms. This is the same set the map labels
            // draw from, so "jump to station" centers on the exact averaged point the label renders at, and the
            // duplicate GroupBy/CreateStationItems pass is avoided.
            ImmutableArray<StationNameItem> orderedStations = ContentByTile[MapContentType.StationNames]
                .OfType<StationNameItem>()
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToImmutableArray();

            navigationStations = BuildEntries(orderedStations, (station, index) => new RouteNavigationItem(index, station.Name, null));
            navigationPlatforms = BuildEntries(orderedPlatforms, (platform, index) => new RouteNavigationItem(index, platform.PlatformName, platform.StationName));
            navigationSidings = BuildEntries(orderedSidings, (siding, index) => new RouteNavigationItem(index, siding.SidingName, NearestStationName(orderedStations, siding.MidPoint)));

            Stations = ProjectItems(navigationStations);
            Platforms = ProjectItems(navigationPlatforms);
            Sidings = ProjectItems(navigationSidings);
        }

        // Pairs each widget with the public item it backs, assigning the shared index used by NavigateTo.
        private static ImmutableArray<NavEntry<T>> BuildEntries<T>(ImmutableArray<T> widgets, Func<T, int, RouteNavigationItem> itemFactory)
        {
            ImmutableArray<NavEntry<T>>.Builder builder = ImmutableArray.CreateBuilder<NavEntry<T>>(widgets.Length);
            for (int i = 0; i < widgets.Length; i++)
                builder.Add(new NavEntry<T>(widgets[i], itemFactory(widgets[i], i)));
            return builder.ToImmutable();
        }

        // Projects the public Index/Name/GroupName items out of the paired entries for the WPF shell.
        private static ImmutableArray<RouteNavigationItem> ProjectItems<T>(ImmutableArray<NavEntry<T>> entries)
        {
            ImmutableArray<RouteNavigationItem>.Builder builder = ImmutableArray.CreateBuilder<RouteNavigationItem>(entries.Length);
            foreach (NavEntry<T> entry in entries)
                builder.Add(entry.Item);
            return builder.ToImmutable();
        }

        // Returns the name of the station marker closest to the given location, or null when no stations exist.
        // Used to give sidings a reasonable grouping since they carry no station reference of their own.
        private static string NearestStationName(ImmutableArray<StationNameItem> stations, in PointD location)
        {
            string nearest = null;
            double nearestDistance = double.MaxValue;
            foreach (StationNameItem station in stations)
            {
                double distance = station.Location.DistanceSquared(location);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = station.Name;
                }
            }
            return nearest;
        }

        // Builds the pinned detail rows for a selected station: its name and the number of platforms covered.
        private static InformationDictionary BuildStationDetails(StationNameItem station)
        {
            return new InformationDictionary
            {
                ["Item Type"] = "Station",
                ["Name"] = station.Name,
                ["Platforms"] = station.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        // Builds the pinned detail rows for a selected platform from its existing provider plus station context.
        private static InformationDictionary BuildPlatformDetails(PlatformPath platform)
        {
            return CopyDetails(((INameValueInformationProvider)platform).DetailInfo);
        }

        // Builds the pinned detail rows for a selected siding from its existing provider.
        private static InformationDictionary BuildSidingDetails(SidingPath siding)
        {
            return CopyDetails(((INameValueInformationProvider)siding).DetailInfo);
        }

        // Snapshots an information dictionary so the pinned rows are decoupled from the widget's shared,
        // hash-cached DetailInfo instance (which other widgets of the same type reuse and mutate).
        private static InformationDictionary CopyDetails(InformationDictionary source)
        {
            InformationDictionary copy = new InformationDictionary();
            if (source != null)
            {
                foreach (string key in source.Keys)
                    copy[key] = source[key];
            }
            return copy;
        }

        // Builds the pinned detail rows for a track item located by its database index.
        private static InformationDictionary BuildTrackItemDetails(int index, Models.Track.TrackItemBase trackItem)
        {
            return new InformationDictionary
            {
                ["Item Type"] = "Track Item",
                ["Item Index"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Node Index"] = trackItem.NodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Location"] = trackItem.Location.ToString(),
            };
        }

        // Builds the pinned detail rows for a track node (rail or road) located by its index.
        private static InformationDictionary BuildTrackNodeDetails(int index, bool road, TrackSegmentSection section)
        {
            return new InformationDictionary
            {
                ["Item Type"] = road ? "Road Node" : "Track Node",
                ["Node Index"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Segments"] = section.SectionSegments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        // Pairs an internal map entity widget with the public navigation item it backs under a shared index.
        private readonly record struct NavEntry<T>(T Widget, RouteNavigationItem Item);

        // Holds a single, pinned set of detail rows for the last route-navigation selection. The rows are
        // replaced wholesale per selection; FormattingOptions stays empty (selection details are plain text).
        private sealed class PinnedInfoProvider : INameValueInformationProvider
        {
            public InformationDictionary DetailInfo { get; private set; } = new InformationDictionary();

            public Dictionary<string, FormatOption> FormattingOptions { get; } = new Dictionary<string, FormatOption>();

            public void Set(InformationDictionary details)
            {
                DetailInfo = details ?? new InformationDictionary();
            }
        }

        private sealed class DetailInfoProxy : DetailInfoProxyBase
        {
            public INameValueInformationProvider Source { get; set; }

            public override InformationDictionary DetailInfo => Source?.DetailInfo;

            public override Dictionary<string, FormatOption> FormattingOptions => Source?.FormattingOptions;
        }
        #endregion
    }
}
