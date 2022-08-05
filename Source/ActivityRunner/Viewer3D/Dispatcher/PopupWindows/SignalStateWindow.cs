
using System.Collections.Generic;
using System.Collections.Specialized;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation.Signalling;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    public class SignalStateWindow : WindowBase
    {
        private class SignalStateInformation : INameValueInformationProvider
        {
            public NameValueCollection DebugInfo { get; } = new NameValueCollection();

            public Dictionary<string, FormatOption> FormattingOptions { get; } = new Dictionary<string, FormatOption>();
        }

        private readonly SignalStateInformation signalStateInformation = new SignalStateInformation();
        private Signal signal;

        public SignalStateWindow(WindowManager owner, Point relativeLocation) :
            base(owner, "Signal State", relativeLocation, new Point(220, 140))
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout = layout.AddLayoutVertical();
            NameValueTextGrid signalStates = new NameValueTextGrid(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight)
            {
                InformationProvider = signalStateInformation,
                ColumnWidth = 100,
            };
            layout.Add(signalStates);
            return layout;
        }

        public void UpdateSignal(ISignal signal)
        {
            signalStateInformation.DebugInfo.Clear();

            if (signal is Signal signalState)
            {
                this.signal = signalState;
                signalStateInformation.DebugInfo[Catalog.GetString("Train")] = signalState.EnabledTrain != null ? $"{signalState.EnabledTrain.Train.Number} - {signalState.EnabledTrain.Train.Name}" : Catalog.GetString("---");
                signalStateInformation.DebugInfo[Catalog.GetString("Signal")] = $"{signalState.Index}";
                signalStateInformation.FormattingOptions[Catalog.GetString("Train")] = FormatOption.BoldYellow;
                signalStateInformation.FormattingOptions[Catalog.GetString("Signal")] = FormatOption.BoldOrange;
                foreach (SignalHead signalHead in signalState.SignalHeads)
                {
                    signalStateInformation.DebugInfo[signalHead.SignalType.Name] = $"{signalHead.SignalIndicationState}";
                }
            }
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
{
            if (shouldUpdate && null != signal)
            {
                foreach (SignalHead signalHead in signal.SignalHeads)
                {
                    signalStateInformation.DebugInfo[signalHead.SignalType.Name] = $"{signalHead.SignalIndicationState}";
                }
            }
            base.Update(gameTime, shouldUpdate);
        }

        public override bool Close()
        {
            signal = null;
            return base.Close();
        }
    }
}
