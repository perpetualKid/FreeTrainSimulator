using System;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.Window.Controls
{
    public class RadioButton : Label
    {
        private const float fontOversize = 1.5f;
        private static Point oversizeOffset;
        private bool state;

        public bool State 
        {
            get => state;
            set
            { 
                state = value;
                Text = state ? "⚫" : "⚪";
                if (value)
                {
                    foreach (RadioButton button in Container?.Controls.OfType<RadioButton>() ?? Enumerable.Empty<RadioButton>())
                    {
                        if (button != this)
                            button.State = false;
                    }
                }
            }
        }

        public Color BorderColor { get; set; } = Color.White;

        public RadioButton(WindowBase window) :
            base(window ?? throw new ArgumentNullException(nameof(window)), 0, 0,
            (int)(window.Owner.DefaultFontSize * 1.30 * window.Owner.DpiScaling), (int)(window.Owner.DefaultFontSize * 1.30 * window.Owner.DpiScaling),
            "⚪", HorizontalAlignment.Center, FontManager.Scaled(window.Owner.DefaultFont, System.Drawing.FontStyle.Regular)[(int)(window.Owner.DefaultFontSize * fontOversize)], Color.White)
        {
            oversizeOffset = new Point((int)(3 * window.Owner.DpiScaling), (int)(5 * window.Owner.DpiScaling));
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            if (BorderColor != Color.Transparent)
            {
                BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(0, 1)).ToVector2(), (offset + Bounds.Location + new Point(Bounds.Width, 1)).ToVector2(), spriteBatch);
                BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(0, Bounds.Height)).ToVector2(), (offset + Bounds.Location + new Point(Bounds.Width, Bounds.Height)).ToVector2(), spriteBatch);
                BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(0, 1)).ToVector2(), (offset + Bounds.Location + new Point(0, Bounds.Height)).ToVector2(), spriteBatch);
                BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(Bounds.Width, 1)).ToVector2(), (offset + Bounds.Location + Bounds.Size).ToVector2(), spriteBatch);
            }
            offset -= oversizeOffset;
            //base.Draw(spriteBatch, offset); // custom drawing to adjust for oversize
            spriteBatch.Draw(texture, (Bounds.Location + offset).ToVector2(), null, TextColor, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        }

        internal override void MouseClick(WindowMouseEvent e)
        {
            State = true;
            base.MouseClick(e);
        }

    }
}
