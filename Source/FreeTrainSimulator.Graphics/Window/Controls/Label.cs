using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class Label : TextControl
    {
        private HorizontalAlignment alignment;
        private Point alignmentOffset;
        private Rectangle? clippingRectangle;

        public virtual string Text
        {
            get => text;
            set
            {
                if (value != text)
                {
                    text = value;
                    Initialize();
                }
            }
        }

        public HorizontalAlignment Alignment
        {
            get => alignment;
            set { alignment = value; Initialize(); }
        }


        public Label(FormBase window, int x, int y, int width, int height, string text, HorizontalAlignment alignment, System.Drawing.Font font, Color color, OutlineRenderOptions outlineRenderOptions = null)
            : base(window, x, y, width, height)
        {
            this.text = text;
            this.alignment = alignment;
            TextColor = color;
            this.font = font ?? window?.Owner.TextFontDefault;
            this.outlineRenderOptions = outlineRenderOptions;
        }

        public Label(FormBase window, int width, int height, string text, System.Drawing.Font font)
            : this(window, 0, 0, width, height, text, HorizontalAlignment.Left, font, Color.White)
        {
        }

        public Label(FormBase window, int x, int y, int width, int height, string text)
            : this(window, x, y, width, height, text, HorizontalAlignment.Left, null, Color.White)
        {
        }

        public Label(FormBase window, int width, int height, string text, HorizontalAlignment align)
            : this(window, 0, 0, width, height, text, align, null, Color.White)
        {
        }

        public Label(FormBase window, int width, int height, string text)
            : this(window, 0, 0, width, height, text, HorizontalAlignment.Left, null, Color.White)
        {
        }

        public Label(FormBase window, int width, int height, string text, HorizontalAlignment align, Color color)
            : this(window, 0, 0, width, height, text, align, null, color)
        {
        }

        internal System.Drawing.Font Font
        {
            get => font;
            set
            {
                font = value ?? Window?.Owner.TextFontDefault;
                Initialize();
            }
        }

        internal override void Initialize()
        {
            base.Initialize();
            InitializeText(Text);
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
            if (null != texture && texture != resourceHolder.EmptyTexture)
                spriteBatch.Draw(texture, (Bounds.Location + offset + alignmentOffset).ToVector2(), clippingRectangle, TextColor, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            base.Draw(spriteBatch, offset);
        }
    }
}
