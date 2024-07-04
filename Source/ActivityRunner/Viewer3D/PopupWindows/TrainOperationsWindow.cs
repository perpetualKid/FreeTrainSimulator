using System.Collections.Generic;

using FreeTrainSimulator.Common;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class TrainOperationsWindow : WindowBase
    {
        private Texture2D couplerTexture;
        private Texture2D carTexture;
        private MSTSLocomotive playerLocomotive;
        private int numberCars;
        private readonly List<Label> carLabels = new List<Label>();
        private readonly Viewer viewer;
        private readonly WindowManager<ViewerWindowType> windowManager;

        public TrainOperationsWindow(WindowManager owner, Point relativeLocation, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Operations"), relativeLocation, new Point(600, 72), catalog)
        {
            this.viewer = viewer;
            windowManager = owner as WindowManager<ViewerWindowType>;
        }

        protected override void Initialize()
        {
            couplerTexture = TextureManager.GetTextureStatic(System.IO.Path.Combine(RuntimeInfo.ContentFolder, "TrainOperationsCoupler.png"), Owner.Game);
            carTexture = TextureManager.GetTextureStatic(System.IO.Path.Combine(RuntimeInfo.ContentFolder, "SimpleCar.png"), Owner.Game);
            base.Initialize();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);

            ControlLayout scrollbox = layout.AddLayoutScrollboxHorizontal(layout.RemainingHeight);
            scrollbox.VerticalChildAlignment = VerticalAlignment.Bottom;
            scrollbox.Container.OnClick += Scrollbox_OnClick;
            carLabels.Clear();

            int carIndex = 0;
            foreach (TrainCar car in Simulator.Instance.PlayerLocomotive.Train.Cars)
            {
                ImageControl imageControl;
                Label carLabel;
                ControlLayoutVertical carLayout = scrollbox.AddLayoutVertical((int)(car.CarID.Length * Owner.TextFontDefault.Height / 1.5));
                carLayout.Add(carLabel = new Label(this, carLayout.RemainingWidth, Owner.TextFontDefault.Height, car.CarID, HorizontalAlignment.Center));
                carLabels.Add(carLabel);
                carLayout.Add(imageControl = new ImageControl(this, carTexture, 0, 0, (int)(32 * Owner.DpiScaling), (int)(10 * Owner.DpiScaling)));
                if (car is MSTSLocomotive)
                {
                    imageControl.Color = car == Simulator.Instance.PlayerLocomotive ? Color.LimeGreen : Color.DarkGreen;
                }
                carLayout.OnClick += CarControl_OnClick;
                carLayout.Tag = car;
                if (car != Simulator.Instance.PlayerLocomotive.Train.Cars[^1])
                {
                    scrollbox.Add(imageControl = new ImageControl(this, couplerTexture, 0, 0));
                    imageControl.Tag = carIndex++;
                    imageControl.OnClick += CouplerControl_OnClick;
                }
            }
            return layout;
        }

        private void Scrollbox_OnClick(object sender, MouseClickEventArgs e)
        {
            windowManager[ViewerWindowType.CarOperationsWindow].Close();
        }

        public override bool Close()
        {
            windowManager[ViewerWindowType.CarOperationsWindow].Close();
            return base.Close();
        }

        private void CouplerControl_OnClick(object sender, MouseClickEventArgs e)
        {
            if (Simulator.Instance.TimetableMode)
            {
                Simulator.Instance.Confirmer.Information(Catalog.GetString("Uncoupling using this window is not allowed in Timetable mode"));
            }
            else
            {
                _ = new UncoupleCommand(viewer.Log, (int)(sender as WindowControl).Tag);
            }
        }

        private void CarControl_OnClick(object sender, MouseClickEventArgs e)
        {
            bool openAbove  = Borders.Top > Owner.Size.Y / 2;
            Point openPoint = new Point(Mouse.GetState(Owner.Game.Window).Position.X, openAbove ? Borders.Top : Borders.Bottom);
            (windowManager[ViewerWindowType.CarOperationsWindow] as CarOperationsWindow).OpenAt(openPoint, openAbove, (sender as WindowControl).Tag as TrainCar);
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
            {
                if (playerLocomotive != (playerLocomotive = Simulator.Instance.PlayerLocomotive) || numberCars != (numberCars = playerLocomotive.Train.Cars.Count))
            {
                    Layout();
                }
                foreach (Label carLabel in carLabels)
                {
                    if ((carLabel.Container.Tag is TrainCar trainCar) && trainCar.BrakesStuck || ((carLabel.Container.Tag is MSTSLocomotive locomotive) && locomotive.PowerReduction > 0))
                        carLabel.TextColor = Color.Red;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            couplerTexture?.Dispose();
            carTexture?.Dispose();
            base.Dispose(disposing);
        }
    }
}
