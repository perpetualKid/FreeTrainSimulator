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
        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly UserSettings settings;
        private ControlLayout controlLayout;
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
            return controlLayout = base.Layout(layout, headerScaling);
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                labelList.Clear();

                void AddTrackItems(TrackNodes trackNodes, List<TrackItem> trackItems, bool roadTracks)
                {
                    ref readonly WorldLocation cameraLocation = ref viewer.Camera.CameraWorldLocation;

                    foreach (TrackVectorNode trackVectorNode in trackNodes.VectorNodes)
                    {
                        if (Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileX - cameraLocation.TileX) < 2 &&
                            Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileZ - cameraLocation.TileZ) < 2)
                        {
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
