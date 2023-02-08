﻿using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
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
                base.Update(gameTime);
            }
        }
    }
}
