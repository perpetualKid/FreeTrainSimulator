using System.ComponentModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal sealed class DispatcherInformation : DetailInfoBase
    {
        private enum DispatcherDetailColumn
        {
            [Description("Name")] Name,
            [Description("Travelled")] Travelled,
            [Description("Speed")] Speed,
            [Description("Max")] AllowedSpeed,
            [Description("Delay")] Delay,
            [Description("AI mode")] AiMode,
            [Description("AI data")] AiData,
            [Description("Mode")] ControlMode,
            [Description("Authorization")] Authorization,
            [Description("Distance")] AuthDistance,
            [Description("Signal")] Signal,
            [Description("Distance")] SignalDistance,
            [Description("Path")] Path,
        }

        private TrainList trains;
        private int numberTrains;
        private readonly Catalog catalog;
        private readonly string trainKey;
        private readonly EnumArray<DetailInfoBase, DispatcherDetailColumn> dispatcherDetails = new EnumArray<DetailInfoBase, DispatcherDetailColumn>(() => new DetailInfoBase());

        public DispatcherInformation(Catalog catalog)
        {
            dispatcherDetails[0] = this;
            MultiColumnCount = EnumExtension.GetLength<DispatcherDetailColumn>();
            this.catalog = catalog;
            trainKey = catalog.GetString("Train");
            foreach (DispatcherDetailColumn column in EnumExtension.GetValues<DispatcherDetailColumn>())
            {
                dispatcherDetails[column].NextColumn = dispatcherDetails[column.Next()];
            }
            dispatcherDetails[DispatcherDetailColumn.Path].NextColumn = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                if (trains != (trains = Simulator.Instance.Trains) | numberTrains != (numberTrains = trains.Count))
                {
                    AddHeader();
                }
                foreach (Train train in trains)
                {
                    string trainid = $"{train.Number}";
                    if (train.DispatcherInfo.DetailInfo.Count == 0)
                        continue;
                    dispatcherDetails[DispatcherDetailColumn.Name][trainid] = train.DispatcherInfo.DetailInfo["Name"];
                    dispatcherDetails[DispatcherDetailColumn.Travelled][trainid] = train.DispatcherInfo.DetailInfo["Travelled"];
                    dispatcherDetails[DispatcherDetailColumn.Speed][trainid] = train.DispatcherInfo.DetailInfo["Speed"];
                    dispatcherDetails[DispatcherDetailColumn.AllowedSpeed][trainid] = train.DispatcherInfo.DetailInfo["AllowedSpeed"];
                    dispatcherDetails[DispatcherDetailColumn.Delay][trainid] = train.DispatcherInfo.DetailInfo["Delay"];
                    dispatcherDetails[DispatcherDetailColumn.ControlMode][trainid] = train.DispatcherInfo.DetailInfo["ControlMode"];
                    dispatcherDetails[DispatcherDetailColumn.Authorization][trainid] = train.DispatcherInfo.DetailInfo["Authorization"];
                    dispatcherDetails[DispatcherDetailColumn.AuthDistance][trainid] = train.DispatcherInfo.DetailInfo["AuthDistance"];
                    dispatcherDetails[DispatcherDetailColumn.Signal][trainid] = train.DispatcherInfo.DetailInfo["Signal"];
                    dispatcherDetails[DispatcherDetailColumn.SignalDistance][trainid] = train.DispatcherInfo.DetailInfo["SignalDistance"];
                    dispatcherDetails[DispatcherDetailColumn.Path][trainid] = train.DispatcherInfo.DetailInfo["Path"];
                    dispatcherDetails[DispatcherDetailColumn.AiMode][trainid] = train.DispatcherInfo.DetailInfo["AiMode"];
                    dispatcherDetails[DispatcherDetailColumn.AiData][trainid] = train.DispatcherInfo.DetailInfo["AiData"];
                }
                base.Update(gameTime);
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
        }
    }
}
