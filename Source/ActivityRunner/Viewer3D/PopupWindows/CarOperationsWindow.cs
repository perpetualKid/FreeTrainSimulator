using FreeTrainSimulator.Common;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class CarOperationsWindow : WindowBase
    {
        private readonly Viewer viewer;
        private TrainCar currentCar;
        private Point anchorPoint;
        private bool positionAbove;

        public CarOperationsWindow(WindowManager owner, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Car Operations"), new Point(50, 50), new Point(100, 200), catalog)
        {
            this.viewer = viewer;
            ZOrder = 50;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            MSTSWagon wagon = currentCar as MSTSWagon;
            ControlLayout line;
            Checkbox checkbox;

            layout = base.Layout(layout, headerScaling);
            if (currentCar == null)
                return layout;

            layout.Add(new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, currentCar.CarID, HorizontalAlignment.Center) { TextColor = Color.Red });
            layout.AddHorizontalSeparator();
            int labelWidth = layout.RemainingWidth / 6 * 5;
            if (wagon.HandBrakePresent)
            {
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Toggle Handbrake")));
                line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = wagon.BrakeSystem.HandbrakePercent > 0 });
                checkbox.OnClick += Handbrake_OnClick;
            }
            if (currentCar is MSTSElectricLocomotive or MSTSDieselLocomotive)
            {
                MSTSLocomotive locomotive = currentCar as MSTSLocomotive;
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Toggle Power")));
                line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = locomotive.LocomotivePowerSupply.MainPowerSupplyOn });
                checkbox.OnClick += Power_OnClick;
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Toggle Multi Unit")));
                line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = locomotive.RemoteControlGroup != RemoteControlGroup.Unconnected });
                checkbox.OnClick += MultiUnit_OnClick;
            }
            if (wagon.PowerSupply != null)
            {
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Battery Switch")));
                line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = wagon.PowerSupply.BatterySwitch.On });
                checkbox.OnClick += Battery_OnClick;
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Electric Train Supply")));
                line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = wagon.PowerSupply.FrontElectricTrainSupplyCableConnected });
                checkbox.OnClick += TrainPower_OnClick;
            }
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Front Brake Hose")));
            line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = wagon.BrakeSystem.FrontBrakeHoseConnected });
            checkbox.OnClick += BrakeHose_OnClick;
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Front Angle Cock")));
            line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = wagon.BrakeSystem.AngleCockAOpen });
            checkbox.OnClick += FrontAngleCock_OnClick;
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Rear Angle Cock")));
            line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = wagon.BrakeSystem.AngleCockBOpen });
            checkbox.OnClick += RearAngleCock_OnClick;
            if (currentCar.BrakeSystem is SingleTransferPipe singlePipe)
            {
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, labelWidth, line.RemainingHeight, Catalog.GetString("Bleed Off Valve")));
                line.Add(checkbox = new Checkbox(this, false, CheckMarkStyle.Ballot, true) { State = wagon.BrakeSystem.BleedOffValveOpen });
                checkbox.OnClick += BleedValve_OnClick;
            }
            return layout;
        }

        private void BleedValve_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSWagon wagon = currentCar as MSTSWagon;
            _ = new ToggleBleedOffValveCommand(viewer.Log, wagon, !wagon.BrakeSystem.BleedOffValveOpen);
            Simulator.Instance.Confirmer.Information(wagon.BrakeSystem.BleedOffValveOpen ? Viewer.Catalog.GetString("Bleed off valve opened") : Catalog.GetString("Bleed off valve closed"));
        }

        private void RearAngleCock_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSWagon wagon = currentCar as MSTSWagon;
            _ = new ToggleAngleCockBCommand(viewer.Log, wagon, !wagon.BrakeSystem.AngleCockBOpen);
            Simulator.Instance.Confirmer.Information(wagon.BrakeSystem.AngleCockBOpen ? Viewer.Catalog.GetString("Rear angle cock opened") : Catalog.GetString("Rear angle cock closed"));
        }

        private void FrontAngleCock_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSWagon wagon = currentCar as MSTSWagon;
            _ = new ToggleAngleCockACommand(viewer.Log, wagon, !wagon.BrakeSystem.AngleCockAOpen);
            Simulator.Instance.Confirmer.Information(wagon.BrakeSystem.AngleCockAOpen ? Viewer.Catalog.GetString("Front angle cock opened") : Catalog.GetString("Front angle cock closed"));
        }

        private void BrakeHose_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSWagon wagon = currentCar as MSTSWagon;
            _ = new WagonBrakeHoseConnectCommand(viewer.Log, wagon, !wagon.BrakeSystem.FrontBrakeHoseConnected);
            Simulator.Instance.Confirmer.Information(wagon.BrakeSystem.FrontBrakeHoseConnected ? Catalog.GetString("Front brake hose connected") : Catalog.GetString("Front brake hose disconnected"));
        }

        private void TrainPower_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSWagon wagon = currentCar as MSTSWagon;
            _ = new ConnectElectricTrainSupplyCableCommand(viewer.Log, wagon, !wagon.PowerSupply.FrontElectricTrainSupplyCableConnected);
            Simulator.Instance.Confirmer.Information(wagon.PowerSupply.FrontElectricTrainSupplyCableConnected ? Catalog.GetString("Front ETS cable connected") : Catalog.GetString("Front ETS cable disconnected"));
        }

        private void Battery_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSWagon wagon = currentCar as MSTSWagon;
            _ = new ToggleBatterySwitchCommand(viewer.Log, wagon, !wagon.PowerSupply.BatterySwitch.On);
            Simulator.Instance.Confirmer.Information(wagon.PowerSupply.BatterySwitch.On ? Catalog.GetString("Switch off battery command sent") : Catalog.GetString("Switch on battery command sent"));
        }

        private void MultiUnit_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSLocomotive locomotive = currentCar as MSTSLocomotive;
            _ = new ToggleMUCommand(viewer.Log, locomotive, locomotive.RemoteControlGroup == RemoteControlGroup.Unconnected);
            Simulator.Instance.Confirmer.Information(locomotive.RemoteControlGroup != RemoteControlGroup.Unconnected ? Catalog.GetString("MultiUnit signal connected") : Catalog.GetString("MU signal disconnected"));
        }

        private void Power_OnClick(object sender, MouseClickEventArgs e)
        {
            MSTSLocomotive locomotive = currentCar as MSTSLocomotive;
            _ = new PowerCommand(viewer.Log, locomotive, !locomotive.LocomotivePowerSupply.MainPowerSupplyOn);
            Simulator.Instance.Confirmer.Information(locomotive.LocomotivePowerSupply.MainPowerSupplyOn ? Catalog.GetString("Power OFF command sent") : Catalog.GetString("Power ON command sent"));
        }

        private void Handbrake_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = new WagonHandbrakeCommand(viewer.Log, currentCar as MSTSWagon, currentCar.BrakeSystem.HandbrakePercent == 0);
            Simulator.Instance.Confirmer.Information(currentCar.BrakeSystem.HandbrakePercent > 0 ? Catalog.GetString("Handbrake set") : Catalog.GetString("Handbrake off"));
        }

        public void OpenAt(Point location, bool openAbove, TrainCar car)
        {
            if (null == car)
            {
                _ = Close();
                return;
            }

            currentCar = car;
            anchorPoint = location;
            positionAbove = openAbove;

            Resize();
            _ = Open();
        }

        private void Resize()
        {
            int height = 52 + (int)(LinesNeeded() * Owner.TextFontDefault.Height / Owner.DpiScaling);
            Point size = new Point(180, height);
            base.Resize(size);

            Point location = anchorPoint;
            if (positionAbove)
                location.Y -= size.Y;
            location.X -= size.X / 2;
            Relocate(location);
        }

        private int LinesNeeded()
        {
            int result = 3; // Brakehose, Front and Rear Angle Cock
            result += (currentCar is MSTSWagon mstsWagon && mstsWagon.HandBrakePresent) ? 1 : 0; //Handbrake
            result += currentCar is MSTSElectricLocomotive or MSTSDieselLocomotive ? 2 : 0; // Power, MU Control
            result += (currentCar as MSTSWagon)?.PowerSupply != null ? 2 : 0; // Battery, Electric Supply
            result += currentCar.BrakeSystem is SingleTransferPipe ? 1 : 0; //Bleed Valve
            return result;
        }

        public override bool Open()
        {
            return base.Open();
        }

        public override bool Close()
        {
            return base.Close();
        }
    }
}
