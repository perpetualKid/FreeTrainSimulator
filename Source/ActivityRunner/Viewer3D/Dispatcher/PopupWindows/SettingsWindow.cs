
using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Settings;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    internal class SettingsWindow : WindowBase
    {
        private readonly DispatcherSettings settings;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Checkbox chkShowPlatforms;
        private Checkbox chkShowStations;
        private Checkbox chkShowSidings;
        private Checkbox chkShowTrainNames;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public SettingsWindow(WindowManager owner, DispatcherSettings settings, 
            Point relativeLocation, Catalog catalog = null) : base(owner, (catalog??= CatalogManager.Catalog).GetString("Settings"), relativeLocation, new Point(200, 100), catalog)
        {
            this.settings = settings;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
//            layout = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);

            ControlLayoutHorizontal line = layout.AddLayoutHorizontalLineOfText();
            int width = (int)(line.RemainingWidth * 0.8);
            line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Show Platform Names")));
            chkShowPlatforms = new Checkbox(this);
            chkShowPlatforms.OnClick += (object sender, MouseClickEventArgs e) => settings.ViewSettings[MapContentType.PlatformNames] = (sender as Checkbox).State.Value;
            chkShowPlatforms.State = settings.ViewSettings[MapContentType.PlatformNames];
            line.Add(chkShowPlatforms);

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Show Siding Names")));
            chkShowSidings = new Checkbox(this);
            chkShowSidings.OnClick += (object sender, MouseClickEventArgs e) => settings.ViewSettings[MapContentType.SidingNames] = (sender as Checkbox).State.Value;
            chkShowSidings.State = settings.ViewSettings[MapContentType.SidingNames];
            line.Add(chkShowSidings);

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Show Station Names")));
            chkShowStations = new Checkbox(this);
            chkShowStations.OnClick += (object sender, MouseClickEventArgs e) => settings.ViewSettings[MapContentType.StationNames] = (sender as Checkbox).State.Value;
            chkShowStations.State = settings.ViewSettings[MapContentType.StationNames];
            line.Add(chkShowStations);

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Show Train Names")));
            chkShowTrainNames = new Checkbox(this);
            chkShowTrainNames.OnClick += (object sender, MouseClickEventArgs e) => settings.ViewSettings[MapContentType.TrainNames] = (sender as Checkbox).State.Value;
            chkShowTrainNames.State = settings.ViewSettings[MapContentType.TrainNames];
            line.Add(chkShowTrainNames);

            return layout;
        }
    }
}
