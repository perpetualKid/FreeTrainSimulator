using System;
using System.Collections.Generic;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Settings;
using Orts.ActivityRunner.Viewer3D.Shapes;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class TrackDebugOverlay : OverlayBase
    {
        private const int SegmentLength = 10;
        private const float Tolerance = 0.1f;

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly UserSettings settings;
        private ControlLayout controlLayout;
        private Track3DOverlay trackOverlay;
        private Tile cameraTile;
        private readonly ResourceGameComponent<Label3DOverlay, int> labelCache;
        private readonly List<Label3DOverlay> labelList = new List<Label3DOverlay>();
        private readonly CameraViewProjectionHolder cameraViewProjection;

        private readonly TrackDB trackDb = RuntimeData.Instance.TrackDB;
        private readonly RoadTrackDB roadTrackDb = RuntimeData.Instance.RoadTrackDB;

        public TrackDebugOverlay(WindowManager owner, UserSettings settings, Viewer viewer, Catalog catalog = null) :
            base(owner, catalog ?? CatalogManager.Catalog)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            this.settings = settings;
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
            trackOverlay.ViewDistance = settings.ViewingDistance;
            controlLayout = layout.AddLayoutPanel(0, 0);
            return controlLayout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            ref readonly WorldLocation cameraLocation = ref viewer.Camera.CameraWorldLocation;
            if (shouldUpdate && cameraTile != new Tile(cameraLocation.TileX, cameraLocation.TileZ))
            {
                cameraTile = new Tile(cameraLocation.TileX, cameraLocation.TileZ);
                labelList.Clear();
                trackOverlay.Clear();

                void AddTrackItems(TrackNodes trackNodes, List<TrackItem> trackItems, bool roadTracks)
                {
                    ref readonly WorldLocation cameraLocation = ref viewer.Camera.CameraWorldLocation;
                    foreach (TrackVectorNode trackVectorNode in trackNodes.VectorNodes)
                    {
                        if (Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileX - cameraLocation.TileX) < 2 &&
                            Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileZ - cameraLocation.TileZ) < 2)
                        {
                            Traveller currentPosition = new Traveller(trackVectorNode);
                            while (true)
                            {
                                WorldLocation previousLocation = currentPosition.WorldLocation;
                                float remaining = currentPosition.MoveInSection(SegmentLength);
                                if ((Math.Abs(remaining - SegmentLength) < Tolerance) && !currentPosition.NextVectorSection())
                                    break;
                                trackOverlay.Add(previousLocation, currentPosition.WorldLocation, roadTracks ? Color.LightSalmon : Color.LightBlue);
                            }

                            IEnumerable<IGrouping<float, TrackItem>> grouping = trackVectorNode.TrackItemIndices.Select(i => trackItems[i]).GroupBy(item => item.SData1);
                            foreach (IGrouping<float, TrackItem> item in grouping)
                            {
                                labelList.Add(labelCache.Get(HashCode.Combine(trackVectorNode.Index, item.Key),
                                    () =>
                                    {
                                        string line = string.Join(System.Environment.NewLine, item.Select(t => $"{t.TrackItemId} {t.GetType().Name[..^4]} {t.ItemName}"));
                                        Traveller currentPosition = new Traveller(trackVectorNode, roadTracks);
                                        currentPosition.Move(item.Key);
                                        return new Label3DOverlay(this, line, roadTracks ? LabelType.RoadTrackDebug : LabelType.TrackDebug, 0,
                                            new FixedWorldPositionSource(new WorldPosition(currentPosition.WorldLocation)), cameraViewProjection);
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
