using System.ComponentModel;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class ForceInformation : DetailInfoBase
    {
        private enum ForceDetailColumn
        {
            [Description("Car Id")] Car,
            [Description("Total")] Total,
            [Description("Motive")] Motive,
            [Description("Brake")] Brake,
            [Description("Friction")] Friction,
            [Description("Gravity")] Gravity,
            [Description("Curve")] Curve,
            [Description("Tunnel")] Tunnel,
            [Description("Wind")] Wind,
            [Description("Coupler")] Coupler,
            [Description("Indication")] CouplerIndication,
            [Description("Slack")] Slack,
            [Description("Mass")] Mass,
            [Description("Gradient")] Gradient,
            [Description("Curve Radius")] CurveRadius,
            [Description("Brake Friction")] BrakeFriction,
            [Description("Brake Slide")] BrakeSlide,
            [Description("Bearing Temp")] BearingTemp,
            [Description("Derail Coeff")] DerailCoefficient,
        }

        private readonly Catalog catalog;
        private readonly EnumArray<DetailInfoBase, ForceDetailColumn> columns = new EnumArray<DetailInfoBase, ForceDetailColumn>(() => new DetailInfoBase());
        private Train train;
        private int numberCars;

        public ForceInformation(Catalog catalog) : base(true)
        {
            columns[0] = this;
            MultiColumnCount = EnumExtension.GetLength<ForceDetailColumn>();
            this.catalog = catalog;
            foreach (ForceDetailColumn column in EnumExtension.GetValues<ForceDetailColumn>())
            {
                columns[column].NextColumn = columns[column.Next()];
            }
            columns[EnumExtension.GetValues<ForceDetailColumn>().Last()].NextColumn = null;
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
                    TrainCar car = train.Cars[i];
                    string key = $"{i + 1}";
                    foreach(ForceDetailColumn detailColumn in EnumExtension.GetValues<ForceDetailColumn>())
                    {
                        columns[detailColumn][key] = car.ForceInfo.DetailInfo[detailColumn.ToString()];
                    }
                }

            }
            base.Update(gameTime);
        }

        private void AddHeader()
        {
            foreach (DetailInfoBase item in columns)
                item.Clear();
            foreach (ForceDetailColumn column in EnumExtension.GetValues<ForceDetailColumn>())
            {
                columns[column]["#"] = column.GetLocalizedDescription();
            }
        }
    }
}
