using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class TextInput : Label
    {
        private const int caretFrequency = 500;
        private long nextCaretTick;
        private bool caretBlink;

        private Texture2D caretTexture;

        private readonly string caretSymbol;

        public event EventHandler<EventArgs> TextChanged;
        public event EventHandler<EventArgs> OnEscapeKey;
        public event EventHandler<EventArgs> OnEnterKey;

        public const string SearchIcon = " ⌕"; // \u2315

        public TextInput(FormBase window, int width, int height, char caretSymbol = '_') : this(window, 0, 0, width, height, caretSymbol)
        {
        }

        public TextInput(FormBase window, int x, int y, int width, int height, char caretSymbol = '_') : base(window, x, y, width, height, null)
        {
            this.caretSymbol = caretSymbol.ToString();
            caretTexture = resourceHolder.PrepareResource(this.caretSymbol, font);
        }

        internal override bool HandleTextInput(TextInputEventArgs e)
        {
            if (Window is WindowBase frameWindow && frameWindow.ActiveControl == this)
            {
                frameWindow.Owner.UserCommandController.SuppressDownLevelEventHandling = true;
                switch (e.Character)
                {
                    case '\b':
                        if (!string.IsNullOrEmpty(Text))
                        {
                            Text = Text.Remove(text.Length - 1);
                            TextChanged?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                    case '\r':
                        frameWindow.ActiveControl = null;
                        frameWindow.Owner.UserCommandController.SuppressDownLevelEventHandling = false;
                        OnEnterKey?.Invoke(this, EventArgs.Empty);
                        break;
                    case '\u001b':
                        frameWindow.ActiveControl = null;
                        frameWindow.Owner.UserCommandController.SuppressDownLevelEventHandling = false;
                        OnEscapeKey?.Invoke(this, EventArgs.Empty);
                        break;
                    default:
                        if (texture.Width + caretTexture.Width < Bounds.Width)
                        {
                            Text += e.Character;
                            TextChanged?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                }
                return true;
            }
            return false;
        }

        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ReleaseFocus()
        {
            if (Window is WindowBase frameWindow && frameWindow.ActiveControl == this)
            {
                frameWindow.ActiveControl = null;
                frameWindow.Owner.UserCommandController.SuppressDownLevelEventHandling = false;
                OnEnterKey?.Invoke(this, EventArgs.Empty);
            }
        }

        internal override bool HandleMouseClicked(WindowMouseEvent e)
        {
            if (Window is WindowBase framedWindow)
                framedWindow.ActiveControl = this;
            return base.HandleMouseClicked(e);
        }

        internal override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (Environment.TickCount64 > nextCaretTick)
            {
                caretBlink = !caretBlink && Window is WindowBase framedWindow && framedWindow.ActiveControl == this;
                nextCaretTick = Environment.TickCount64 + caretFrequency;
            }
            base.Update(gameTime, shouldUpdate);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Rectangle target = Bounds;
            target.Offset(offset);
            spriteBatch.Draw(Window.Owner.BackgroundTexture, target, Color.White);
            base.Draw(spriteBatch, offset);
            if (caretBlink)
                spriteBatch.Draw(caretTexture, (Bounds.Location + offset + new Point(texture?.Width ?? 0, 0)).ToVector2(), TextColor);
        }

        private protected override void RefreshResources(object sender, EventArgs e)
        {
            base.RefreshResources(sender, e);
            caretTexture = resourceHolder.PrepareResource(caretSymbol, font);
        }

        protected override void Dispose(bool disposing)
        {
            caretTexture?.Dispose();
            base.Dispose(disposing);
        }
    }
}
