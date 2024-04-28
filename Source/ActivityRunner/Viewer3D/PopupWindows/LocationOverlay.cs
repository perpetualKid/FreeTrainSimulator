using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;

using GetText;

using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.Signalling;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class LocationOverlay : OverlayBase
    {
        private enum ViewMode
        {
            Platforms,
            Sidings,
            Auto,
            All,
        }

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly UserSettings settings;
        private ViewMode viewMode;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private ControlLayout controlLayout;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private Tile cameraTile;
        private readonly ResourceGameComponent<Label3DOverlay, int> labelCache;
        private readonly List<Label3DOverlay> labelList = new List<Label3DOverlay>();
        private readonly CameraViewProjectionHolder cameraViewProjection;

        private readonly HashSet<string> autoPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> autoSidings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private long nextActivityUpdate;
        private int stationStopsCount;
        private ActivityTask activityTask;

        public LocationOverlay(WindowManager owner, UserSettings settings, Viewer viewer, Catalog catalog = null) :
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
            UpdateLabelLists();
            ref readonly WorldLocation cameraLocation = ref viewer.Camera.CameraWorldLocation;
            if (shouldUpdate && cameraTile != new Tile(cameraLocation.TileX, cameraLocation.TileZ))
            {
                cameraTile = new Tile(cameraLocation.TileX, cameraLocation.TileZ);
                labelList.Clear();
                foreach (WorldFile worldFile in viewer.World.Scenery.WorldFiles)
                {
                    if (Math.Abs(worldFile.Tile.X - cameraLocation.TileX) < 2 && Math.Abs(worldFile.Tile.Z - cameraLocation.TileZ) < 2)
                    {
                        if (viewMode != ViewMode.Sidings)
                        {
                            foreach (TrItemLabel platform in worldFile.Platforms)
                            {
                                if (viewMode != ViewMode.Auto || autoPlatforms.TryGetValue(platform.ItemName, out _))
                                    labelList.Add(labelCache.Get(platform.GetHashCode(), () => new Label3DOverlay(this, platform.ItemName, LabelType.Platform, 0, new FixedWorldPositionSource(platform.Location), cameraViewProjection)));
                            }
                        }
                        if (viewMode != ViewMode.Platforms)
                        {
                            foreach (TrItemLabel siding in worldFile.Sidings)
                            {
                                if (viewMode != ViewMode.Auto || autoSidings.TryGetValue(siding.ItemName, out _))
                                    labelList.Add(labelCache.Get(siding.GetHashCode(), () => new Label3DOverlay(this, siding.ItemName, LabelType.Sidings, 0, new FixedWorldPositionSource(siding.Location), cameraViewProjection)));
                            }
                        }
                    }
                }
                controlLayout.Controls.Clear();
                foreach (Label3DOverlay item in labelList)
                    controlLayout.Controls.Add(item);
            }
            base.Update(gameTime, shouldUpdate);
        }

        public override bool Open()
        {
            ChangeMode();
            userCommandController.AddEvent(UserCommand.DisplayStationLabels, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayStationLabels, KeyEventType.KeyPressed, TabAction);

            Simulator.Instance.Confirmer.Information(Catalog.GetString("Platform and siding labels hidden."));
            return base.Close();
        }

        protected override void Initialize()
        {
            if (EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.LocationsOverlay], out viewMode))
            {
                ChangeMode();
            }
            base.Initialize();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                viewMode = viewMode.Next();
                settings.PopupSettings[ViewerWindowType.LocationsOverlay] = viewMode.ToString();
                ChangeMode();
            }
        }

        private void ChangeMode()
        {
            switch (viewMode)
            {
                case ViewMode.Platforms:
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Platform labels visible."));
                    break;
                case ViewMode.Sidings:
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Siding labels visible."));
                    break;
                case ViewMode.Auto:
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Automatic platform and siding labels visible."));
                    break;
                case ViewMode.All:
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Platform and siding labels visible."));
                    break;
            }
        }

        private void UpdateLabelLists()
        {
            TrackDB tdb = RuntimeData.Instance.TrackDB;
            List<StationStop> stationStops = Simulator.Instance.PlayerLocomotive.Train.StationStops;
            Simulation.Activities.Activity activity = Simulator.Instance.ActivityRun;

            // Update every 10s or when the current activity task changes.
            if (System.Environment.TickCount64 > nextActivityUpdate || stationStopsCount != (stationStopsCount = stationStops.Count) ||
                activityTask != (activityTask = activity?.ActivityTask))
            {
                nextActivityUpdate = System.Environment.TickCount64 + 1000;
                autoPlatforms.Clear();
                autoSidings.Clear();

                if (tdb.TrackItems != null)
                {
                    foreach (StationStop stop in stationStops)
                    {
                        int platformId = stop.PlatformReference;
                        if (0 <= platformId && platformId < tdb.TrackItems.Count && tdb.TrackItems[platformId] is PlatformItem)
                        {
                            autoPlatforms.Add(tdb.TrackItems[platformId].ItemName);
                        }
                    }

                    if (activity?.EventList != null)
                    {
                        foreach (EventWrapper activityEvent in activity.EventList)
                        {
                            if (activityEvent.ActivityEvent is ActionActivityEvent eventAction)
                            {
                                int sidingId1 = eventAction.SidingId;
                                int sidingId2 = eventAction.WorkOrderWagons != null && eventAction.WorkOrderWagons.Count > 0 ? eventAction.WorkOrderWagons[0].SidingId : -1;
                                int sidingId = sidingId1 > -1 ? sidingId1 : sidingId2;
                                if (sidingId > -1 && sidingId < tdb.TrackItems.Count && tdb.TrackItems[sidingId] is SidingItem)
                                {
                                    autoSidings.Add(tdb.TrackItems[sidingId].ItemName);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
