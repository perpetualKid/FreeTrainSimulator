﻿
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Graphics;
using Orts.Graphics.MapView;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    public class LocationWindow : WindowBase
    {
        private const double piRad = 180 / Math.PI;
        private ContentArea contentArea;
        private Label locationLabel;
        private Label tileLabel;
        private PointD previousWorldPoint;
        private bool useWorldCoordinates = true;
        private bool updateRequired;

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly ToolboxSettings toolboxSettings;

        public LocationWindow(WindowManager owner, ToolboxSettings settings, ContentArea contentArea, Point relativeLocation) :
            base(owner, "World Coordinates", relativeLocation, new Point(200, 48))
        {
            this.contentArea = contentArea;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
            toolboxSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!bool.TryParse(toolboxSettings.PopupSettings[WindowType.LocationWindow], out useWorldCoordinates))
                useWorldCoordinates = true;
            Resize();
        }

        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            layout = base.Layout(layout, headerScaling);

            if (!useWorldCoordinates)
            {
                ControlLayoutHorizontal tileTextLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.0));
                tileLabel = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, string.Empty, HorizontalAlignment.Center, Color.Orange);
                tileTextLine.Add(tileLabel);
            }
            ControlLayoutHorizontal statusTextLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            locationLabel = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, string.Empty, HorizontalAlignment.Center, Color.Orange);
            statusTextLine.Add(locationLabel);
            return layout;
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                useWorldCoordinates = !useWorldCoordinates;
                toolboxSettings.PopupSettings[WindowType.LocationWindow] = useWorldCoordinates.ToString();
                updateRequired = true;
                Resize();
            }
        }

        private void Resize()
        {
            Caption = useWorldCoordinates ? Catalog.GetString("World Coordinates") : CatalogManager.Catalog.GetString("Tile Coordinates");
            Resize(useWorldCoordinates ? new Point(200, 44) : new Point(220, 56));
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayLocationWindow, KeyEventType.KeyPressed, TabAction, true);
            updateRequired = true;
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayLocationWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        protected override void Update(GameTime gameTime)
        {
            ref readonly PointD worldPoint = ref (contentArea == null ? ref PointD.None : ref contentArea.WorldPosition);
            if (previousWorldPoint != worldPoint || updateRequired)
            {
                updateRequired = false;
                previousWorldPoint = worldPoint;
                WorldLocation location = PointD.ToWorldLocation(worldPoint);
                if (useWorldCoordinates)
                {
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

                    locationLabel.Text = $"{latitudeDegree}°{latitudeMinute,2:00}'{latitudeSecond,2:00}\"{hemisphere} {longitudeDegree}°{longitudeMinute,2:00}'{longitudeSecond,2:00}\"{direction}";
                }
                else
                {
                    tileLabel.Text = $"Tile (X:Z) {location.TileX}:{location.TileZ}";
                    locationLabel.Text = $"Location (x, z) {location.Location.X,4:00.##} {location.Location.Z,4:00.##}";
                }
            }
            base.Update(gameTime);
        }
    }
}
