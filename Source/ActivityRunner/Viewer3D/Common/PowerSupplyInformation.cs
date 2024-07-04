using System.ComponentModel;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class PowerSupplyInformation: DetailInfoBase
    {
        private enum PowerSupplyColumns
        {
            [Description("Car Id")] Car,
            [Description("Wagon Type")] WagonType,
            [Description("Pantograph")] Pantograph,
            [Description("Engine")] Engine,
            [Description("CircuitBreaker")] CircuitBreaker,
            [Description("Traction CutOff")] TractionCutOffRelay,
            [Description("Main")] MainPower,
            [Description("Auxiliary")] AuxPower,
            [Description("Battery")] Battery,
            [Description("Low Voltage")] LowVoltagePower,
            [Description("Cab")] CabPower,
            [Description("ETS")] Ets,
            [Description("ETS Cable")] EtsCable,
            [Description("Power")] Power,
        }

    private readonly EnumArray<DetailInfoBase, PowerSupplyColumns> columns = new EnumArray<DetailInfoBase, PowerSupplyColumns>(() => new DetailInfoBase());
        private Train train;
        private int numberCars;

        public PowerSupplyInformation()
        {
            columns[0] = this;
            MultiColumnCount = EnumExtension.GetLength<PowerSupplyColumns>();
            foreach (PowerSupplyColumns column in EnumExtension.GetValues<PowerSupplyColumns>())
            {
                columns[column].NextColumn = columns[column.Next()];
            }
            columns[EnumExtension.GetValues<PowerSupplyColumns>().Last()].NextColumn = null;

        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                if (train != (train = Simulator.Instance.PlayerLocomotive.Train) | numberCars != (numberCars = train.Cars.Count))
                {
                    AddHeader();
                }
                for (int i = 0; i < train.Cars.Count; i++)
                {
                    if (train.Cars[i].PowerSupply == null)
                        continue;
                    TrainCar car = train.Cars[i];
                    string key = $"{i + 1}";
                    foreach (PowerSupplyColumns detailColumn in EnumExtension.GetValues<PowerSupplyColumns>())
                    {
                        columns[detailColumn][key] = car.PowerSupplyInfo.DetailInfo[detailColumn.ToString()];
                    }
                }
                base.Update(gameTime);
            }
        }

        private void AddHeader()
        {
            foreach (DetailInfoBase item in columns)
                item.Clear();
            foreach (PowerSupplyColumns column in EnumExtension.GetValues<PowerSupplyColumns>())
            {
                columns[column]["#"] = column.GetLocalizedDescription();
            }
        }
    }
}
