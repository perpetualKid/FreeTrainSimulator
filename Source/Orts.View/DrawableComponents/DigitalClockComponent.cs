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
        public Vector2 Position { get; private set; }

        private readonly TimeType timeType;
        private readonly SpriteBatch spriteBatch;
        private Color color = Color.Black;
        private string formatMask = "hh\\:mm\\:ss";

        public DigitalClockComponent(Game game, SpriteBatch spriteBatch, TimeType timeType, System.Drawing.Font font, Color color, Vector2 position, bool visibleImmediately) :
            base(game, font)
        {
            this.timeType = timeType;
            this.spriteBatch = spriteBatch;
            this.color = color;
            Position = position;
            if (!visibleImmediately)
            {
                Enabled = false;
                Visible = false;
            }
            Position = new Vector2(400, 400);
            InitializeSize(TimeSpan.Zero.ToString(formatMask, CultureInfo.DefaultThreadCurrentUICulture));
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
            DrawString(timestamp);
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            spriteBatch.Begin();
            spriteBatch.Draw(texture, Position, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);

            base.Draw(gameTime);
            spriteBatch.End();
        }
    }
}
