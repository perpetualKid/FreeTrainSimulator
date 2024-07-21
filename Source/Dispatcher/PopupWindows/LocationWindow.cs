
using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Dispatcher.Settings;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Dispatcher.PopupWindows
{
    public class LocationWindow : WindowBase
    {
        private ContentArea contentArea;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label locationLabel;
        private Label tileLabel;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private PointD previousWorldPoint;
        private bool useWorldCoordinates = true;
        private bool updateRequired;

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly DispatcherSettings toolboxSettings;

        public LocationWindow(WindowManager owner, DispatcherSettings settings, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("World Coordinates"), relativeLocation, new Point(200, 48), catalog)
        {
            this.contentArea = contentArea;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
            toolboxSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!bool.TryParse(toolboxSettings.PopupSettings[DispatcherWindowType.LocationWindow], out useWorldCoordinates))
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
                toolboxSettings.PopupSettings[DispatcherWindowType.LocationWindow] = useWorldCoordinates.ToString();
                updateRequired = true;
                Resize();
            }
        }

        private void Resize()
        {
            Caption = useWorldCoordinates ? Catalog.GetString("World Coordinates") : Catalog.GetString("Tile Coordinates");
            Resize(useWorldCoordinates ? new Point(200, 48) : new Point(220, 60));
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

        protected override void Update(GameTime gameTime, bool shouldUpdate)
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
                    (string latitudeText, string longitudeText) = EarthCoordinates.ToString(latitude, longitude);
                    locationLabel.Text = $"{latitudeText} {longitudeText}";

                }
                else
                {
                    tileLabel.Text = $"Tile (X:Z) {location.Tile.X}:{location.Tile.Z}";
                    locationLabel.Text = $"Location (x, z) {location.Location.X,4:00.##} {location.Location.Z,4:00.##}";
                }
            }
            base.Update(gameTime, shouldUpdate);
        }
    }
}
