
using System;
using System.Linq;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FreeTrainSimulator.Graphics.DrawableComponents
{
    public class WorldCoordinatesComponent : VolatileTextComponent
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly MouseInputGameComponent input;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private MouseState lastMouseState;
        private const double piRad = 180 / Math.PI;

        public WorldCoordinatesComponent(Game game, System.Drawing.Font font, Color color, Vector2 position) :
            base(game, font, color, position)
        {
            Enabled = false;
            Visible = false;
            input = Game.Components.OfType<MouseInputGameComponent>().Single();
            this.font = font;
        }

        public override void Update(GameTime gameTime)
        {
            ref readonly MouseState mouseState = ref input.MouseState;
            if (mouseState != lastMouseState)
            {
                lastMouseState = mouseState;
                Point worldPoint = content.ScreenToWorldCoordinates(mouseState.Position);
                WorldLocation location = PointD.ToWorldLocation(worldPoint);
                (double latitude, double longitude) = EarthCoordinates.ConvertWTC(location);

                longitude *= piRad; // E/W
                latitude *= piRad;  // N/S
                char hemisphere = latitude >= 0 ? 'N' : 'S';
                char direction = longitude >= 0 ? 'E' : 'W';
                longitude = Math.Abs(longitude);
                latitude = Math.Abs(latitude);
                int longitudeDegree = (int)Math.Truncate(longitude);
                int latitudeDegree = (int)Math.Truncate(latitude);

                longitude -= longitudeDegree;
                latitude -= latitudeDegree;
                longitude *= 60;
                latitude *= 60;
                int longitudeMinute = (int)Math.Truncate(longitude);
                int latitudeMinute = (int)Math.Truncate(latitude);
                longitude -= longitudeMinute;
                latitude -= latitudeMinute;
                longitude *= 60;
                latitude *= 60;
                int longitudeSecond = (int)Math.Truncate(longitude);
                int latitudeSecond = (int)Math.Truncate(latitude);

                string locationText = string.Format(System.Globalization.CultureInfo.CurrentCulture, $"{latitudeDegree}°{latitudeMinute,2:00}'{latitudeSecond,2:00}\"{hemisphere} {longitudeDegree}°{longitudeMinute,2:00}'{longitudeSecond,2:00}\"{direction}");

                RenderText(locationText);
            }
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (texture == null)
                return;
            spriteBatch.Begin();
            spriteBatch.Draw(texture, position, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);

            spriteBatch.End();
            base.Draw(gameTime);
        }

        internal protected override void Enable(ContentArea content)
        {
            Resize("01234567890123456789012345");//about 25 chars needed for full lat/lon coordinates
            base.Enable(content);
        }
    }
}
