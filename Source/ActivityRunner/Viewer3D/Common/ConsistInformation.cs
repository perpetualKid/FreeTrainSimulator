using System;
using System.Globalization;

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
    internal class ConsistInformation : DebugInfoBase
    {
        private readonly Catalog catalog;
        private Train train;

        public ConsistInformation(Catalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            this.catalog = catalog;

            AddHeader();
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                if (train != (train = Simulator.Instance.PlayerLocomotive.Train))
                {
                    AddHeader();
                    bool isUK = Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK;
                    double length = 0;
                    for (int i = 0; i < train.Cars.Count; i++)
                    {
                        TrainCar car = train.Cars[i];
                        this[$"{i + 1}"] =
                            $"{car.CarID}\t" +
                            $"{(car.Flipped ? catalog.GetString("Yes") : catalog.GetString("No"))}\t" +
                            $"{car.WagonType}\t" +
                            $"{FormatStrings.FormatShortDistanceDisplay(car.CarLengthM, Simulator.Instance.MetricUnits)}\t" +
                            $"{FormatStrings.FormatLargeMass(car.MassKG, Simulator.Instance.MetricUnits, isUK)}\t" +
                            $"{(car is MSTSLocomotive ? catalog.GetParticularString("Cab", "D") : "") + (car.HasFrontCab || car.HasFront3DCab ? catalog.GetParticularString("Cab", "F") : "") + (car.HasRearCab || car.HasRear3DCab ? catalog.GetParticularString("Cab", "R") : "")}\t" +
                            $"{car.WheelAxleInformation}\t" +
                            $"{(car.WagonType == WagonType.Passenger || car.WagonSpecialType == WagonSpecialType.Heated ? FormatStrings.FormatTemperature(car.CarInsideTempC, Simulator.Instance.MetricUnits) : string.Empty)}";
                        length += car.CarLengthM;
                    }
                }

                base.Update(gameTime);
            }
        }

        private void AddHeader()
        {
            Clear();
            this["#"] =
                $"{catalog.GetString("Car")}\t" +
                $"{catalog.GetString("Flipped")}\t" +
                $"{catalog.GetString("Type")}\t" +
                $"{catalog.GetString("Length")}\t" +
                $"{catalog.GetString("Weight")}\t" +
                $"{catalog.GetString("Drv/Cabs")}\t" +
                $"{catalog.GetString("Wheels")}\t" +
                $"{catalog.GetString("Temp")}";
        }
    }
}
