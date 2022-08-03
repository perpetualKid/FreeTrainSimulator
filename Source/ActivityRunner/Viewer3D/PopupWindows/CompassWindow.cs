
using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class CompassWindow : WindowBase
    {
        private Label latitudeLabel;
        private Label longitudeLabel;
        private CompassControl compassControl;
        private readonly Viewer viewer;
        private bool forceUpdate = true;

        private readonly string lat;
        private readonly string lon;
             
        public CompassWindow(WindowManager owner, Point relativeLocation, Viewer viewer) :
            base(owner, "Compass", relativeLocation, new Point(240, 80))
        {
            this.viewer = viewer;
            lat = Catalog.GetString($"Lat:");
            lon = Catalog.GetString($"Lon:");
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling).AddLayoutVertical();
            layout.Add(compassControl = new CompassControl(this, layout.RemainingWidth, layout.RemainingHeight - Owner.TextFontDefault.Height));
            ControlLayout textLine = layout.AddLayoutHorizontalLineOfText();
            int width = textLine.RemainingWidth / 2;
            textLine.Add(latitudeLabel = new Label(this, width, textLine.RemainingHeight, lat, HorizontalAlignment.Right));
            textLine.Add(longitudeLabel = new Label(this, width, textLine.RemainingHeight, lon, HorizontalAlignment.Left));
            return layout;
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (viewer.Camera.ViewChanged || forceUpdate)
            {
                double heading = Math.Acos(viewer.Camera.XnaView.M11);
                if (viewer.Camera.XnaView.M13 > 0)
                    heading = 2 * Math.PI - heading;
                compassControl.Heading = (int)MathHelper.ToDegrees((float)heading);

                (double latitude, double longitude) = EarthCoordinates.ConvertWTC(viewer.Camera.CameraWorldLocation);
                (string latitudeText, string longitudeText) = EarthCoordinates.ToString(latitude, longitude);
                latitudeLabel.Text = $"{lat} {latitudeText}";
                longitudeLabel.Text = $"{lon} {longitudeText}";

                forceUpdate = false;
            }
        }
    }
}
