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
    internal class DispatcherInformation : DetailInfoBase
    {
        private enum DispatcherDetailColumn
        {
            //            [Description("Train")] Train,
            [Description("Name")] Name,
            [Description("Travelled")] Travelled,
            [Description("Speed")] Speed,
            [Description("Max")] Max,
            [Description("AI mode")] AiMode,
            [Description("AI data")] AiData,
            [Description("Mode")] Mode,
            [Description("Authorization")] Authorization,
            [Description("Distance")] AuthDistance,
            [Description("Signal")] Signal,
            [Description("Distance")] SignalDistance,
            [Description("Consist")] Consist,
            [Description("Path")] Path,
        }

        private TrainList trains;
        private int numberTrains;
        private readonly Catalog catalog;
        private readonly string trainKey;
        private EnumArray<DetailInfoBase, DispatcherDetailColumn> dispatcherDetails = new EnumArray<DetailInfoBase, DispatcherDetailColumn>(() => new DetailInfoBase());

        public DispatcherInformation(Catalog catalog)
        {
            dispatcherDetails[0] = this;
            MultiColumnCount = EnumExtension.GetLength<DispatcherDetailColumn>();
            this.catalog = catalog;
            trainKey = catalog.GetString("Train");
            MultiColumnCount = 13;
            foreach (DispatcherDetailColumn column in EnumExtension.GetValues<DispatcherDetailColumn>())
            {
                dispatcherDetails[column].Next = dispatcherDetails[column.Next()];
            }
            dispatcherDetails[DispatcherDetailColumn.Path].Next = null;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (trains != (trains = Simulator.Instance.Trains) | numberTrains != (numberTrains = trains.Count))
            {
                AddHeader();
            }
        }

        private void AddHeader()
        {
            foreach (DetailInfoBase item in dispatcherDetails)
                item.Clear();
            foreach (DispatcherDetailColumn column in EnumExtension.GetValues<DispatcherDetailColumn>())
            {
                dispatcherDetails[column][trainKey] = column.GetLocalizedDescription();
            }
            foreach (Train train in trains)
            {
                string trainid = $"{train.Number}";
                dispatcherDetails[DispatcherDetailColumn.Name][trainid] = train.Name;
            }
        }
    }
}
