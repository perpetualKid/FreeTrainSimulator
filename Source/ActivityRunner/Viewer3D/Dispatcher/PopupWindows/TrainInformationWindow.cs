
using System;
using System.Collections.Generic;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    internal class TrainInformationWindow : WindowBase
    {

        private class TrainInformation : INameValueInformationProvider
        {
            public InformationDictionary DetailInfo { get; } = new InformationDictionary();

            public Dictionary<string, FormatOption> FormattingOptions { get; } = new Dictionary<string, FormatOption>();
        }

        private readonly TrainInformation trainInformation = new TrainInformation();

        private Train train;
        private TrainCar locomotive;

        public TrainInformationWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Information"), relativeLocation, new Point(200, 180), catalog)
        {
        }

        public void UpdateTrain(ITrain train)
        {
            if (train is Train physicalTrain)
            {
                this.train = physicalTrain;
                locomotive = physicalTrain.LeadLocomotive ?? physicalTrain.Cars.OfType<MSTSLocomotive>().FirstOrDefault();
                trainInformation.DetailInfo[Catalog.GetString("Train")] = train.Name;
                trainInformation.DetailInfo[Catalog.GetString("Speed")] = FormatStrings.FormatSpeedDisplay(physicalTrain.SpeedMpS, Simulator.Instance.MetricUnits);
                trainInformation.DetailInfo["Gradient"] = $"{locomotive?.CurrentElevationPercent:F1}%";
                trainInformation.DetailInfo["Direction"] = Math.Abs(physicalTrain.MUReverserPercent) != 100 ? $"{Math.Abs(physicalTrain.MUReverserPercent):F0} {physicalTrain.MUDirection.GetLocalizedDescription()}" : $"{physicalTrain.MUDirection.GetLocalizedDescription()}";
                trainInformation.DetailInfo["Cars"] = $"{physicalTrain.Cars.Count}";
                trainInformation.DetailInfo["Type"] = $"{(physicalTrain.IsFreight ? Catalog.GetString("Freight") : Catalog.GetString("Passenger"))}";
                trainInformation.DetailInfo["Train Type"] = $"{physicalTrain.TrainType}";
                trainInformation.DetailInfo["Control Mode"] = $"{physicalTrain.ControlMode}";
            }
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate && train != null)
            {
                trainInformation.DetailInfo[Catalog.GetString("Speed")] = FormatStrings.FormatSpeedDisplay(train.SpeedMpS, Simulator.Instance.MetricUnits);
                double gradient = Math.Round(locomotive?.CurrentElevationPercent ?? train.Cars[0].CurrentElevationPercent, 1);
                if (gradient == 0) // to avoid negative zero string output if gradient after rounding is -0.0
                    gradient = 0;
                trainInformation.DetailInfo["Gradient"] = $"{gradient:F1}%";
                trainInformation.DetailInfo["Direction"] = Math.Abs(train.MUReverserPercent) != 100 ? $"{Math.Abs(train.MUReverserPercent):F0} {train.MUDirection.GetLocalizedDescription()}" : $"{train.MUDirection.GetLocalizedDescription()}";
            }
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout = layout.AddLayoutVertical();
            NameValueTextGrid signalStates = new NameValueTextGrid(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight)
            {
                InformationProvider = trainInformation,
                ColumnWidth = new int[] { (int)(layout.RemainingWidth / 2 / Owner.DpiScaling) },
            };
            layout.Add(signalStates);
            return layout;
        }

    }
}
