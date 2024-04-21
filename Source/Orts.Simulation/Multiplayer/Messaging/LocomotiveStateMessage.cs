using MemoryPack;

using Orts.Common;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class LocomotiveStateMessage : LocomotiveStateBaseMessage
    {
        public float SteamHeat { get; set; }
        public float EngineBrake { get; set; }
        public float DynamicBrake { get; set; }
        public float Throttle { get; set; }
        public float ElectricFilterVoltage { get; set; }
        public float Cutoff { get; set; }
        public float Blower { get; set; }
        public float Damper { get; set; }
        public float FiringRate { get; set; }
        public float Injector1 { get; set; }
        public float Injector2 { get; set; }
        public float SmallEjector { get; set; }
        public float LargeEjector { get; set; }

        [MemoryPackConstructor]
        public LocomotiveStateMessage() { }

        public LocomotiveStateMessage(MSTSLocomotive locomotive): base(locomotive)
        {
            if (locomotive.SteamHeatController != null)
            {
                SteamHeat = locomotive.SteamHeatController.CurrentValue;
            }
            if (locomotive.EngineBrakeController != null)
            {
                EngineBrake = locomotive.EngineBrakeController.CurrentValue;
            }
            if (locomotive.DynamicBrakeController != null)
            {
                DynamicBrake = locomotive.DynamicBrakeController.CurrentValue;
            }
            Throttle = locomotive.ThrottleController.CurrentValue;
            if (locomotive is MSTSElectricLocomotive electricLocomotive)
            {
                ElectricFilterVoltage = electricLocomotive.ElectricPowerSupply.FilterVoltageV;
            }
            else if (locomotive is MSTSSteamLocomotive steamLocomotive)
            {
                Cutoff = steamLocomotive.CutoffController.CurrentValue;
                Blower = steamLocomotive.BlowerController.CurrentValue;
                Damper = steamLocomotive.DamperController.CurrentValue;
                FiringRate = steamLocomotive.FiringRateController.CurrentValue;
                Injector1 = steamLocomotive.Injector1Controller.CurrentValue;
                Injector2 = steamLocomotive.Injector2Controller.CurrentValue;
                SmallEjector = steamLocomotive.SmallEjectorController.CurrentValue;
                LargeEjector = steamLocomotive.LargeEjectorController.CurrentValue;
            }

        }

        public override void HandleMessage()
        {
            foreach (Train train in Simulator.Instance.Trains)
            {
                if (train.TrainType != TrainType.Remote && train.Number == TrainNumber)
                {
                    foreach (TrainCar trainCar in train.Cars)
                    {
                        if (trainCar.CarID.StartsWith(User, System.StringComparison.OrdinalIgnoreCase) && trainCar is MSTSLocomotive locomotive)
                        {
                            if (locomotive.SteamHeatController != null)
                            {
                                locomotive.SteamHeatController.CurrentValue = SteamHeat;
                                locomotive.SteamHeatController.UpdateValue = 0f;
                            }
                            if (locomotive.EngineBrakeController != null)
                            {
                                locomotive.EngineBrakeController.CurrentValue = EngineBrake;
                                locomotive.EngineBrakeController.UpdateValue = 0f;
                            }
                            if (locomotive.DynamicBrakeController != null)
                            {
                                locomotive.DynamicBrakeController.CurrentValue = DynamicBrake;
                                locomotive.DynamicBrakeController.UpdateValue = 0f;
                            }

                            locomotive.ThrottleController.CurrentValue = Throttle;
                            locomotive.ThrottleController.UpdateValue = 0f;
                            if (locomotive is MSTSElectricLocomotive electricLocomotive)
                            {
                                electricLocomotive.ElectricPowerSupply.FilterVoltageV = ElectricFilterVoltage;
                            }
                            else if (locomotive is MSTSSteamLocomotive steamLocomotive)
                            {
                                steamLocomotive.CutoffController.CurrentValue = Cutoff;
                                steamLocomotive.CutoffController.UpdateValue = 0f;
                                steamLocomotive.BlowerController.CurrentValue = Blower;
                                steamLocomotive.BlowerController.UpdateValue = 0f;
                                steamLocomotive.DamperController.CurrentValue = Damper;
                                steamLocomotive.DamperController.UpdateValue = 0f;
                                steamLocomotive.FiringRateController.CurrentValue = FiringRate;
                                steamLocomotive.FiringRateController.UpdateValue = 0f;
                                steamLocomotive.Injector1Controller.CurrentValue = Injector1;
                                steamLocomotive.Injector1Controller.UpdateValue = 0f;
                                steamLocomotive.Injector2Controller.CurrentValue = Injector2;
                                steamLocomotive.Injector2Controller.UpdateValue = 0f;
                                steamLocomotive.SmallEjectorController.CurrentValue = SmallEjector;
                                steamLocomotive.SmallEjectorController.UpdateValue = 0f;
                                steamLocomotive.LargeEjectorController.CurrentValue = LargeEjector;
                                steamLocomotive.LargeEjectorController.UpdateValue = 0f;

                            }
                        }
                    }
                    break;
                }
            }
        }
    }
}
