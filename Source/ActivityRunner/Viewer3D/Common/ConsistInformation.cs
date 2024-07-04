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
    internal class ConsistInformation : DetailInfoBase
    {
        private const int Columns = 8;
        private readonly Catalog catalog;
        private Train train;
        private int numberCars;

        private readonly DetailInfoBase[] consistDetails = new DetailInfoBase[Columns];

        public ConsistInformation(Catalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            this.catalog = catalog;

            MultiColumnCount = Columns;

            consistDetails[0] = this;
            for (int i = 1; i < Columns; i++)
                consistDetails[i] = new DetailInfoBase();
            for (int i = 0; i < Columns - 1; i++)
                consistDetails[i].NextColumn = consistDetails[i + 1];
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                if (train != (train = Simulator.Instance.PlayerLocomotive.Train) | numberCars != (numberCars = train.Cars.Count))
                {
                    AddHeader(train);
                }
                bool isUK = Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK;
                for (int i = 0; i < train.Cars.Count; i++)
                {
                    TrainCar car = train.Cars[i];
                    string key = $"{i + 1}";
                    consistDetails[3][key] = FormatStrings.FormatShortDistanceDisplay(car.CarLengthM, Simulator.Instance.MetricUnits);
                    consistDetails[4][key] = FormatStrings.FormatLargeMass(car.MassKG, Simulator.Instance.MetricUnits, isUK);
                    consistDetails[7][key] = car.WagonType == WagonType.Passenger || car.WagonSpecialType == WagonSpecialType.Heated ? FormatStrings.FormatTemperature(car.CarInsideTempC, Simulator.Instance.MetricUnits) : string.Empty;
                }
                base.Update(gameTime);
            }
        }

        private void AddHeader(Train train)
        {
            foreach (DetailInfoBase item in consistDetails)
                item.Clear();
            consistDetails[0]["#"] = catalog.GetString("Car");
            consistDetails[1]["#"] = catalog.GetString("Flipped");
            consistDetails[2]["#"] = catalog.GetString("Type");
            consistDetails[3]["#"] = catalog.GetString("Length");
            consistDetails[4]["#"] = catalog.GetString("Weight");
            consistDetails[5]["#"] = catalog.GetString("Drv/Cabs");
            consistDetails[6]["#"] = catalog.GetString("Wheels");
            consistDetails[7]["#"] = catalog.GetString("Temp");

            for (int i = 0; i < train.Cars.Count; i++)
            {
                TrainCar car = train.Cars[i];
                string key = $"{i + 1}";
                consistDetails[0][key] = car.CarID;
                consistDetails[1][key] = car.Flipped ? catalog.GetString("Yes") : catalog.GetString("No");
                consistDetails[2][key] = car.WagonType.ToString();
                consistDetails[5][key] = car is MSTSLocomotive ? catalog.GetParticularString("Cab", "D") : "" + (car.HasFrontCab || car.HasFront3DCab ? catalog.GetParticularString("Cab", "F") : "") + (car.HasRearCab || car.HasRear3DCab ? catalog.GetParticularString("Cab", "R") : "");
                consistDetails[6][key] = car.WheelAxleInformation;
            }

        }
    }
}
