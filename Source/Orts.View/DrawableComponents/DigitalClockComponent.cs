using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.View.Xna;

namespace Orts.View.DrawableComponents
{
    public enum TimeType
    {
        GameTotalTime,
        GameElapsedTime,
        RealWorlUtcTime,
        RealWorldLocalTime,
    }

    /// <summary>
    /// this class is primarily used for testing of various graphical aspects
    /// </summary>
    public class DigitalClockComponent : QuickRepeatableDrawableTextComponent
    {
        private Vector2 position;
        private Vector2 positionOffset;

        private readonly TimeType timeType;
        private readonly SpriteBatch spriteBatch;
        private Color color = Color.Black;
        private string formatMask = "hh\\:mm\\:ss";
        private string previousTimestamp;

        public DigitalClockComponent(Game game, TimeType timeType, System.Drawing.Font font, Color color, Vector2 position, bool visibleImmediately) :
            base(game, font)
        {
            this.timeType = timeType;
            spriteBatch = new SpriteBatch(game?.GraphicsDevice);
            this.color = color;
            this.position = position;
            if (!visibleImmediately)
            {
                Enabled = false;
                Visible = false;
            }
            if (position.X < 0 || position.Y < 0)
            {
                positionOffset = position;
            }
            InitializeSize(TimeSpan.Zero.ToString(formatMask, CultureInfo.DefaultThreadCurrentUICulture));
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
            Window_ClientSizeChanged(this, EventArgs.Empty);
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            if (null != texture && (positionOffset.X < 0 || positionOffset.Y < 0))
                position = new Vector2(positionOffset.X > 0 ? positionOffset.X : Game.Window.ClientBounds.Width + positionOffset.X - texture.Width, positionOffset.Y > 0 ? positionOffset.Y : Game.Window.ClientBounds.Height + positionOffset.Y - texture.Height);
        }

        public string FormatMask
        {
            get => formatMask;
            set 
            {
                InitializeSize(TimeSpan.Zero.ToString(value, CultureInfo.DefaultThreadCurrentUICulture));
                formatMask = value;
            }
        }

        public override void Update(GameTime gameTime)
        {
            string timestamp = null;
            switch (timeType)
            {
                case TimeType.GameElapsedTime:
                    timestamp = gameTime?.ElapsedGameTime.ToString(formatMask, CultureInfo.DefaultThreadCurrentUICulture); break;
                case TimeType.GameTotalTime:
                    timestamp = gameTime?.TotalGameTime.ToString(formatMask, CultureInfo.DefaultThreadCurrentUICulture); break;
                case TimeType.RealWorlUtcTime:
                    timestamp = DateTime.UtcNow.TimeOfDay.ToString(formatMask, CultureInfo.DefaultThreadCurrentUICulture); break;
                case TimeType.RealWorldLocalTime:
                    timestamp = DateTime.Now.TimeOfDay.ToString(formatMask, CultureInfo.DefaultThreadCurrentUICulture); break;
            }
            if (timestamp != previousTimestamp)
            {
                DrawString(timestamp);
                previousTimestamp = timestamp;
            }
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            spriteBatch.Begin();
            spriteBatch.Draw(texture, position, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);

            base.Draw(gameTime);
            spriteBatch.End();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                spriteBatch?.Dispose();
                Game.Window.ClientSizeChanged -= Window_ClientSizeChanged;
            }
            base.Dispose(disposing);
        }
    }
}
