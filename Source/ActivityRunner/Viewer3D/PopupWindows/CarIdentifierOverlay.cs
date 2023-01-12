using System;
using System.Collections.Generic;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal partial class CarIdentifierOverlay : OverlayBase
    {

        private enum ViewMode
        {
            Cars,
            Trains,
        }

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly UserSettings settings;
        private ViewMode viewMode;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private ControlLayout controlLayout;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly ResourceGameComponent<Label3DOverlay, int> labelCache;
        private readonly List<Label3DOverlay> labelList = new List<Label3DOverlay>();
        private readonly CameraViewProjectionHolder cameraViewProjection;

        public CarIdentifierOverlay(WindowManager owner, UserSettings settings, Viewer viewer, Catalog catalog = null) : 
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
                ref readonly WorldLocation cameraLocation = ref viewer.Camera.CameraWorldLocation;
                labelList.Clear();
                foreach (Train train in Simulator.Instance.Trains)
                {
                    ref readonly WorldPosition firstCarPosition = ref train.FirstCar.WorldPosition;
                    ref readonly WorldPosition lastCarPosition = ref train.LastCar.WorldPosition;
                    //only consider trains which are within 1 tile max distance from current camera position
                    if ((Math.Abs(firstCarPosition.TileX - cameraLocation.TileX) < 2 && Math.Abs(firstCarPosition.TileZ - cameraLocation.TileZ) < 2) ||
                        ((Math.Abs(lastCarPosition.TileX - cameraLocation.TileX) < 2 && Math.Abs(lastCarPosition.TileZ - cameraLocation.TileZ) < 2)))
                    {
                        switch (viewMode)
                        {
                            case ViewMode.Cars:
                                foreach (TrainCar car in train.Cars)
                                {
                                    labelList.Add(labelCache.Get(car.GetHashCode(), () => new Label3DOverlay(this, car.CarID, LabelType.Car, car.CarHeightM, car, cameraViewProjection)));
                                }
                                break;
                            case ViewMode.Trains:
                                labelList.Add(labelCache.Get(HashCode.Combine(train.GetHashCode(), train.FirstCar.GetHashCode()), () => new Label3DOverlay(this, train.Name, LabelType.Car, train.FirstCar.CarHeightM, train.FirstCar, cameraViewProjection)));
                                if (train.Cars.Count > 2)
                                    labelList.Add(labelCache.Get(HashCode.Combine(train.GetHashCode(), train.LastCar.GetHashCode()), () => new Label3DOverlay(this, train.Name, LabelType.Car, train.LastCar.CarHeightM, train.LastCar, cameraViewProjection)));
                                break;
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
            userCommandController.AddEvent(UserCommand.DisplayCarLabels, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayCarLabels, KeyEventType.KeyPressed, TabAction);
            Simulator.Instance.Confirmer.Information(Catalog.GetString("Train and car labels hidden."));
            return base.Close();
        }

        protected override void Initialize()
        {
            if (EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.CarIdentifierOverlay], out viewMode))
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
                settings.PopupSettings[ViewerWindowType.CarIdentifierOverlay] = viewMode.ToString();
                ChangeMode();
            }
        }

        private void ChangeMode()
        {
            switch (viewMode)
            {
                case ViewMode.Trains:
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Train labels visible."));
                    break;
                case ViewMode.Cars:
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Car labels visible."));
                    break;
            }
        }
    }
}
