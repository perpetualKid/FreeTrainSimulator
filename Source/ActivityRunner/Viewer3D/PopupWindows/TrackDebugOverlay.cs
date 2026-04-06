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
using FreeTrainSimulator.Runtime.Track;

using GetText;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class TrackDebugOverlay : OverlayBase
    {
        private const int SegmentLength = 10;
        private const float Tolerance = 0.1f;

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

        private readonly TrackDB trackDb = RuntimeData.Instance.TrackDB;
        private readonly RoadTrackDB roadTrackDb = RuntimeData.Instance.RoadTrackDB;

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

                void AddTrackItems(TrackNodes trackNodes, List<TrackItem> trackItems, bool roadTracks)
                {
                    ref readonly WorldLocation cameraLocation = ref viewer.Camera.CameraWorldLocation;
                    TrackDataBaseType dbType = roadTracks ? TrackDataBaseType.Road : TrackDataBaseType.Rail;
                    foreach (TrackVectorNode trackVectorNode in trackNodes.VectorNodes)
                    {
                        if (Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileX - cameraLocation.TileX) < 2 &&
                            Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileZ - cameraLocation.TileZ) < 2)
                        {
                            TrackTraveller? init = TrackTraveller.InitializeTraveller(
                                trackVectorNode.TrackVectorSections[0].Location, trackVectorNode.Index, TrackDirection.Ahead, dbType);
                            if (init is TrackTraveller currentPosition)
                            {
                                int startNodeIndex = currentPosition.TrackNodeIndex;
                                while (true)
                                {
                                    WorldLocation previousLocation = currentPosition.Location;
                                    double previousOffset = currentPosition.VectorNodeOffset;
                                    currentPosition = currentPosition.Move(SegmentLength);
                                    trackOverlay.Add(previousLocation, currentPosition.Location, roadTracks ? Color.LightSalmon : Color.LightBlue);
                                    if (currentPosition.TrackNodeIndex != startNodeIndex || Math.Abs(currentPosition.VectorNodeOffset - previousOffset) < Tolerance)
                                        break;
                                }
                            }

                            IEnumerable<IGrouping<float, TrackItem>> grouping = trackVectorNode.TrackItemIndices.Select(i => trackItems[i]).GroupBy(item => item.SData1);
                            foreach (IGrouping<float, TrackItem> item in grouping)
                            {
                                labelList.Add(labelCache.Get(HashCode.Combine(trackVectorNode.Index, item.Key),
                                    () =>
                                    {
                                        string line = string.Join(System.Environment.NewLine, item.Select(t => $"{t.TrackItemId} {t.GetType().Name[..^4]} {t.ItemName}"));
                                        WorldLocation labelLocation = trackVectorNode.TrackVectorSections[0].Location;
                                        TrackTraveller? ttInit = TrackTraveller.InitializeTraveller(labelLocation, trackVectorNode.Index, TrackDirection.Ahead, dbType);
                                        if (ttInit is TrackTraveller ttLabel)
                                            labelLocation = ttLabel.Move(item.Key).Location;
                                        return new Label3DOverlay(this, line, roadTracks ? LabelType.RoadTrackDebug : LabelType.TrackDebug, 0,
                                            new FixedWorldPositionSource(new WorldPosition(labelLocation)), cameraViewProjection);
                                    }));
                            }
                        }
                    }
                }

                AddTrackItems(trackDb.TrackNodes, trackDb.TrackItems, false);

                if (roadTrackDb?.TrackNodes != null)
                    AddTrackItems(roadTrackDb.TrackNodes, roadTrackDb.TrackItems, true);

                controlLayout.Controls.Clear();
                foreach (Label3DOverlay item in labelList)
                    controlLayout.Controls.Add(item);

            }
            base.Update(gameTime, shouldUpdate);
        }

    }
}
