
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.View.Window.Controls
{
    public enum LabelAlignment
    {
        Left,
        Center,
        Right,
    }

    public class Label : TextControl
    {
        public string Text { get; private set; }
        public LabelAlignment Alignment { get; }
        private Point alignmentOffset;

        public Label(int x, int y, int width, int height, string text, LabelAlignment alignment)
            : base(x, y, width, height)
        {
            Text = text;
            Alignment = alignment;
            color = Color.White;
        }

        public Label(int x, int y, int width, int height, string text)
            : this(x, y, width, height, text, LabelAlignment.Left)
        {
        }

        public Label(int width, int height, string text, LabelAlignment align)
            : this(0, 0, width, height, text, align)
        {
        }

        public Label(int width, int height, string text)
            : this(0, 0, width, height, text, LabelAlignment.Left)
        {
        }

        public override void Initialize(WindowManager windowManager)
        {
            font = windowManager?.TextFontDefault;
            base.Initialize(windowManager);
            InitializeSize(Text, windowManager);
            DrawString(Text);
            switch (Alignment)
            {
                case LabelAlignment.Left:
                    alignmentOffset = Point.Zero;
                    break;
                case LabelAlignment.Center:
                    alignmentOffset = new Point((Position.Width - texture.Width) / 2, 0);
                    break;
                case LabelAlignment.Right:
                    alignmentOffset = new Point((Position.Width - texture.Width), 0);
                    break;
            }
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, null, null);
            spriteBatch.Draw(texture, (Position.Location + offset + alignmentOffset).ToVector2(), null, Color.White, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            spriteBatch.End();
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
