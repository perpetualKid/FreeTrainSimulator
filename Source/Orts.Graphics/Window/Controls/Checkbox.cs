using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.Window.Controls
{
    public class Checkbox : Label
    {
        private bool? state;
        private readonly bool tristate;

        public bool? State
        {
            get => state;
            set
            {
                state = tristate ? value : value.GetValueOrDefault();
                TextColor = state.HasValue ? Color.LimeGreen : Color.LightGray;
                Text = state.HasValue ? state.Value ? "✔" : "" : "■";
            }
        }

        public Checkbox(WindowBase window, bool threeStates = false) :
            base(window ?? throw new ArgumentNullException(nameof(window)), window.Owner.TextFontDefault.Height, window.Owner.TextFontDefault.Height, "✔", HorizontalAlignment.Center)
        {
            tristate = threeStates;
            State = null;
        }

        internal override void MouseClick(WindowMouseEvent e)
        {
            State = tristate ? (!State.HasValue ? false : (State.Value ? (bool?)null : true)) : State = !State;
            base.MouseClick(e);
        }
    }
}
