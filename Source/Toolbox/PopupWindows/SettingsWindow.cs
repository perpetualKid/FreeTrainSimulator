
using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    internal class SettingsWindow : WindowBase
    {
        private readonly ToolboxSettings settings;

        public SettingsWindow(WindowManager owner, ToolboxSettings settings, Point relativeLocation, Catalog catalog = null) : 
            base(owner, "Settings", relativeLocation, new Point(360, 200), catalog)
        {
            this.settings = settings;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);
            ControlLayoutHorizontal line = layout.AddLayoutHorizontalLineOfText();
            int width = (int)(line.RemainingWidth * 0.8);
            line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Enable Logging")));
            Checkbox chkLoggingEnabled = new Checkbox(this);
            chkLoggingEnabled.OnClick += (object sender, MouseClickEventArgs e) => settings.UserSettings.Logging = (sender as Checkbox).State.Value;
            chkLoggingEnabled.State = settings.UserSettings.Logging;
            line.Add(chkLoggingEnabled);

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Restore Last View on Start")));
            Checkbox chkRestoreView = new Checkbox(this);
            chkRestoreView.OnClick += (object sender, MouseClickEventArgs e) => settings.RestoreLastView = (sender as Checkbox).State.Value;
            chkRestoreView.State = settings.RestoreLastView;
            line.Add(chkRestoreView);

            return layout;

        }
    }
}
