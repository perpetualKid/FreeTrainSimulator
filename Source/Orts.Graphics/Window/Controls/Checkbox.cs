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

        public Color BorderColor { get; set; } = Color.White;

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
            base(window ?? throw new ArgumentNullException(nameof(window)),
            (int)(window.Owner.DefaultFontSize * 1.35 * window.Owner.DpiScaling), (int)(window.Owner.DefaultFontSize * 1.35 * window.Owner.DpiScaling),
            "✔", HorizontalAlignment.Center)
        {
            tristate = threeStates;
            State = null;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(0, 1)).ToVector2(), (offset + Bounds.Location + new Point(Bounds.Width, 1)).ToVector2(), spriteBatch);
            BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(0, Bounds.Height)).ToVector2(), (offset + Bounds.Location + new Point(Bounds.Width, Bounds.Height)).ToVector2(), spriteBatch);
            BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(0, 1)).ToVector2(), (offset + Bounds.Location + new Point(0, Bounds.Height)).ToVector2(), spriteBatch);
            BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(Bounds.Width, 1)).ToVector2(), (offset + Bounds.Location + Bounds.Size).ToVector2(), spriteBatch);
            base.Draw(spriteBatch, offset);
        }

        internal override void MouseClick(WindowMouseEvent e)
        {
            State = (tristate) ? (!State.HasValue ? false : (State.Value ? (bool?)null : true)) : State = !State;
            base.MouseClick(e);
        }
    }
}
