using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using GetText;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Shapes;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class TrackDebugOverlay : OverlayBase
    {
        private const int SegmentLength = 10;

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly ProfileUserSettingsModel userSettings;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private ControlLayout controlLayout;
        private Track3DOverlay trackOverlay;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private Tile cameraTile;
        private readonly ResourceGameComponent<Label3DOverlay, int> labelCache;
        private readonly List<Label3DOverlay> labelList = new List<Label3DOverlay>();
        private readonly CameraViewProjectionHolder cameraViewProjection;

        private readonly TrackWorld trackWorld = RuntimeDataResolver.Instance.TrackWorld;
        private readonly TrackDatabase trackDb = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
        private readonly TrackDatabase roadTrackDb = RuntimeDataResolver.Instance.TrackWorld.RoadDatabase;

        public TrackDebugOverlay(WindowManager owner, ProfileUserSettingsModel userSettings, Viewer viewer, Catalog catalog = null) :
            base(owner, catalog ?? CatalogManager.Catalog)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            this.userSettings = userSettings;
            userCommandController = viewer.UserCommandController;
            this.viewer = viewer;
            ZOrder = -5;

            labelCache = Owner.Game.Components.OfType<ResourceGameComponent<Label3DOverlay, int>>().FirstOrDefault() ?? new ResourceGameComponent<Label3DOverlay, int>(Owner.Game);
            cameraViewProjection = new CameraViewProjectionHolder(viewer);
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout.Add(trackOverlay = new Track3DOverlay(this));
            trackOverlay.CameraView = cameraViewProjection;
            trackOverlay.ViewDistance = userSettings.ViewingDistance;
            controlLayout = layout.AddLayoutPanel(0, 0);
            return controlLayout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            ref readonly WorldLocation cameraLocation = ref viewer.Camera.CameraWorldLocation;
            if (shouldUpdate && cameraTile != cameraLocation.Tile)
            {
                cameraTile = cameraLocation.Tile;
                labelList.Clear();
                trackOverlay.Clear();

                void AddTrackContent(MapContentType contentType, TrackDatabase trackDatabase, bool roadTracks)
                {
                    ITileIndexedList<ITileCoordinate> tileIndex = trackWorld.ContentByTile[contentType];
                    if (tileIndex == null)
                        return;

                    Color segmentColor = roadTracks ? Color.LightSalmon : Color.LightBlue;
                    LabelType labelType = roadTracks ? LabelType.RoadTrackDebug : LabelType.TrackDebug;
                    HashSet<int> processedNodeIndices = new HashSet<int>();

                    foreach (VectorSectionNode section in tileIndex.BoundingBox(cameraTile, 1).Cast<VectorSectionNode>())
                    {
                        bool hasGeometry = trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geometry);

                        // Draw section segments using pre-computed geometry
                        if (hasGeometry && geometry.HasGeometry && geometry.Length > 0)
                        {
                            for (double offset = 0; offset < geometry.Length; offset += SegmentLength)
                            {
                                WorldLocation from = trackWorld.ComputeSectionLocation(section, offset);
                                WorldLocation to = trackWorld.ComputeSectionLocation(section, Math.Min(offset + SegmentLength, geometry.Length));
                                trackOverlay.Add(from, to, segmentColor);
                            }
                        }
                        else
                        {
                            trackOverlay.Add(section.Location, section.EndLocation, segmentColor);
                        }

                        // Process track items once per parent VectorNode
                        if (hasGeometry && trackDatabase != null &&
                            processedNodeIndices.Add(geometry.Node.NodeIndex) &&
                            trackDatabase.TrackItemSelectors.TryGetValue(geometry.Node.NodeIndex, out TrackItemIndex trackItemIndex))
                        {
                            IEnumerable<IGrouping<float, FreeTrainSimulator.Models.Track.TrackItemBase>> grouping = trackItemIndex.TrackItems
                                .Select(i => trackDatabase.TrackItems[i])
                                .GroupBy(item => item.SectionDistance);
                            foreach (IGrouping<float, FreeTrainSimulator.Models.Track.TrackItemBase> item in grouping)
                            {
                                labelList.Add(labelCache.Get(HashCode.Combine(geometry.Node.NodeIndex, item.Key),
                                    () =>
                                    {
                                        string line = string.Join(System.Environment.NewLine, item.Select(t => $"{t.TrackItemIndex} {TrackItemLabel(t)}"));
                                        WorldLocation labelLocation = ComputeLocationAlongNode(geometry.Node, item.Key);
                                        return new Label3DOverlay(this, line, labelType, 0,
                                            new FixedWorldPositionSource(new WorldPosition(labelLocation)), cameraViewProjection);
                                    }));
                            }
                        }
                    }
                }

                AddTrackContent(MapContentType.Tracks, trackDb, false);
                AddTrackContent(MapContentType.Roads, roadTrackDb, true);

                controlLayout.Controls.Clear();
                foreach (Label3DOverlay item in labelList)
                    controlLayout.Controls.Add(item);

            }
            base.Update(gameTime, shouldUpdate);
        }

        /// <summary>
        /// Computes the world location at <paramref name="distance"/> metres along <paramref name="node"/> using pre-computed section geometry.
        /// </summary>
        private WorldLocation ComputeLocationAlongNode(VectorNode node, double distance)
        {
            double remaining = distance;
            for (int i = 0; i < node.VectorSections.Length; i++)
            {
                double sectionLength = trackWorld.SectionLength(node, i);
                if (remaining <= sectionLength || i == node.VectorSections.Length - 1)
                    return trackWorld.ComputeSectionLocation(node, i, remaining);
                remaining -= sectionLength;
            }

            return node.Location;
        }

        /// <summary>
        /// Returns a display label for a track item: platform/siding name when available, otherwise the type name without "TrackItem" suffix.
        /// </summary>
        private static string TrackItemLabel(FreeTrainSimulator.Models.Track.TrackItemBase trackItem) => trackItem switch
        {
            PlatformTrackItem platform => $"Platform {platform.PlatformName}",
            SidingTrackItem siding => $"Siding {siding.SidingName}",
            _ => trackItem.GetType().Name[..^9] // strip "TrackItem" suffix
        };
    }
}
