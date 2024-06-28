using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class TrainInformation : DetailInfoBase
    {
        private Train train;
        private readonly Catalog catalog;

        public TrainInformation(Catalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            this.catalog = catalog;
            this[catalog.GetString("Player")] = null;
            this[catalog.GetString("Tilting")] = null;
            this[catalog.GetString("Type")] = null;
            this[catalog.GetString("Length")] = null;
            this[catalog.GetString("Weight")] = null;
            this[catalog.GetString("Tonnage")] = null;
            this[catalog.GetString("Control Mode")] = null;
            this[catalog.GetString("Out of Control")] = null;
            this[catalog.GetString("Cab Aspect")] = null;
            this[catalog.GetString(".Cruise control")] = null;
            this[catalog.GetString("Cruise control")] = null;
            this[catalog.GetString("Target Speed")] = null;
            this[catalog.GetString("Max Acceleration")] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                MSTSLocomotive locomotive = Simulator.Instance.PlayerLocomotive;
                bool isUK = Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK;
                if (train != (train = Simulator.Instance.PlayerLocomotive.Train))
                {

                    double tonnage = 0;
                    foreach (TrainCar car in train.Cars)
                    {
                        if (car.WagonType is WagonType.Freight or WagonType.Passenger)
                            tonnage += car.MassKG;
                    }

                    this[catalog.GetString("Player")] = $"{locomotive.CarID} {(locomotive.UsingRearCab ? catalog.GetParticularString("Cab", "R") : catalog.GetParticularString("Cab", "F"))}";
                    this[catalog.GetString("Tilting")] = train.IsTilting ? catalog.GetString("Yes") : catalog.GetString("No");
                    this[catalog.GetString("Type")] = train.IsFreight ? catalog.GetString("Freight") : catalog.GetString("Passenger");
                    this[catalog.GetString("Tonnage")] = FormatStrings.FormatLargeMass(tonnage, Simulator.Instance.MetricUnits, isUK);
                }
                this[catalog.GetString("Length")] = FormatStrings.FormatShortDistanceDisplay(train.Length, Simulator.Instance.MetricUnits);
                this[catalog.GetString("Weight")] = FormatStrings.FormatLargeMass(train.MassKg, Simulator.Instance.MetricUnits, isUK);
                this[catalog.GetString("Control Mode")] = train.ControlMode.GetLocalizedDescription();
                this[catalog.GetString("Out of Control")] = train.OutOfControlReason.GetLocalizedDescription();
                this[catalog.GetString("Cab Aspect")] = locomotive.TrainControlSystem.CabSignalAspect.GetDescription();
                if (locomotive.CruiseControl != null)
                {
                    this[catalog.GetString("Cruise control")] = locomotive.CruiseControl.SpeedRegulatorMode.GetLocalizedDescription();
                    if (locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Auto)
                    {
                        this[catalog.GetString("Target Speed")] = FormatStrings.FormatSpeedDisplay(locomotive.CruiseControl.SelectedSpeedMpS, Simulator.Instance.MetricUnits);
                        this[catalog.GetString("Max Acceleration")] = $"{locomotive.CruiseControl.SelectedMaxAccelerationPercent}";
                    }
                }
                else
                {
                    this[catalog.GetString("Cruise control")] = catalog.GetString("n/a");
                    this[catalog.GetString("Target Speed")] = null;
                    this[catalog.GetString("Max Acceleration")] = null;
                }
                base.Update(gameTime);
            }
        }
    }
}
