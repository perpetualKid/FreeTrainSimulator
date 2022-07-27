
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Orts.Graphics.Window.Controls
{
    public class Label : TextControl
    {
        private string text;
        public string Text
        {
            get => text;
            set { text = value; Initialize(); }
        }

        public HorizontalAlignment Alignment { get; }
        private Point alignmentOffset;
        private Rectangle? clippingRectangle;

        public Label(WindowBase window, int x, int y, int width, int height, string text, HorizontalAlignment alignment, System.Drawing.Font font, Color color)
            : base(window, x, y, width, height)
        {
            this.text = text;
            Alignment = alignment;
            TextColor = color;
            this.font = font ?? window?.Owner.TextFontDefault;
        }

        public Label(WindowBase window, int x, int y, int width, int height, string text)
            : this(window, x, y, width, height, text, HorizontalAlignment.Left, null, Color.White)
        {
        }

        public Label(WindowBase window, int width, int height, string text, HorizontalAlignment align)
            : this(window, 0, 0, width, height, text, align, null, Color.White)
        {
        }

        public Label(WindowBase window, int width, int height, string text)
            : this(window, 0, 0, width, height, text, HorizontalAlignment.Left, null, Color.White)
        {
        }

        public Label(WindowBase window, int width, int height, string text, HorizontalAlignment align, Color color)
            : this(window, 0, 0, width, height, text, align, null, color)
        {
        }

        internal System.Drawing.Font Font
        {
            get => font;
            set
            {
                this.font = value ?? Window?.Owner.TextFontDefault;
                Initialize();
            }
        }

        internal override void Initialize()
        {
            base.Initialize();
            InitializeText(Text);
            RenderText(Text);
            clippingRectangle = null;
            switch (Alignment)
            {
                case HorizontalAlignment.Left:
                    alignmentOffset = Point.Zero;
                    if (texture.Width > Bounds.Width)
                        clippingRectangle = new Rectangle(Point.Zero, Bounds.Size);
                    break;
                case HorizontalAlignment.Center:
                    alignmentOffset = new Point((Bounds.Width - System.Math.Min(texture.Width, Bounds.Width)) / 2, 0);
                    if (texture.Width > Bounds.Width)
                        clippingRectangle = new Rectangle(new Point((texture.Width - Bounds.Width) / 2, 0), Bounds.Size);
                    break;
                case HorizontalAlignment.Right:
                    alignmentOffset = new Point(Bounds.Width - System.Math.Min(texture.Width, Bounds.Width), 0);
                    if (texture.Width > Bounds.Width)
                        clippingRectangle = new Rectangle(new Point(texture.Width - Bounds.Width, 0), Bounds.Size);
                    break;
            }
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(texture, (Bounds.Location + offset + alignmentOffset).ToVector2(), clippingRectangle, TextColor, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            base.Draw(spriteBatch, offset);
        }
    }
}
