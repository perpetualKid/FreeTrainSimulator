
using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class CompassWindow : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label latitudeLabel;
        private Label longitudeLabel;
        private CompassControl compassControl;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly Viewer viewer;

        private readonly string lat;
        private readonly string lon;
             
        public CompassWindow(WindowManager owner, Point relativeLocation, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Compass"), relativeLocation, new Point(240, 80), catalog)
        {
            this.viewer = viewer;
            lat = Catalog.GetString($"Lat:");
            lon = Catalog.GetString($"Lon:");
            CloseButton = false;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout.Add(compassControl = new CompassControl(this, layout.RemainingWidth, layout.RemainingHeight - Owner.TextFontDefault.Height));
            ControlLayout textLine = layout.AddLayoutHorizontalLineOfText();
            int width = textLine.RemainingWidth / 2;
            textLine.Add(latitudeLabel = new Label(this, width, textLine.RemainingHeight, lat, HorizontalAlignment.Right));
            textLine.Add(longitudeLabel = new Label(this, width, textLine.RemainingHeight, lon, HorizontalAlignment.Left));
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);

            if (viewer.Camera.ViewChanged || shouldUpdate)
            {
                double heading = Math.Round(Math.Acos(viewer.Camera.XnaView.M11), 3);
                if (viewer.Camera.XnaView.M13 > 0)
                    heading = 2 * Math.PI - heading;
                compassControl.Heading = (int)(heading * 180 / Math.PI);

                (double latitude, double longitude) = EarthCoordinates.ConvertWTC(viewer.Camera.CameraWorldLocation);
                (string latitudeText, string longitudeText) = EarthCoordinates.ToString(latitude, longitude);
                latitudeLabel.Text = $"{lat} {latitudeText}";
                longitudeLabel.Text = $"{lon} {longitudeText}";
            }
        }
    }
}
