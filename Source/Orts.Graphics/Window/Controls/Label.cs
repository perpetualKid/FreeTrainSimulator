
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

        internal override void Initialize()
        {
            base.Initialize();
            InitializeText(Text);
            RenderText(Text);
            switch (Alignment)
            {
                case HorizontalAlignment.Left:
                    alignmentOffset = Point.Zero;
                    break;
                case HorizontalAlignment.Center:
                    alignmentOffset = new Point((Bounds.Width - texture.Width) / 2, 0);
                    break;
                case HorizontalAlignment.Right:
                    alignmentOffset = new Point((Bounds.Width - texture.Width), 0);
                    break;
            }
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(texture, (Bounds.Location + offset + alignmentOffset).ToVector2(), null, TextColor, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }
    }
}
