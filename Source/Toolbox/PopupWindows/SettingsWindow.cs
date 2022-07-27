
using System.ComponentModel;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Input;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    internal class SettingsWindow : WindowBase
    {
        private readonly ToolboxSettings settings;

        private enum TabSettings
        {
            [Description("Common")]
            Common,
            [Description("Toolbox")]
            Toolbox,
            [Description("Graphics")]
            Graphics
        }

        private TabControl<TabSettings> tabControl;
        private readonly UserCommandController<UserCommand> userCommandController;

        public SettingsWindow(WindowManager owner, ToolboxSettings settings, Point relativeLocation, Catalog catalog = null) : 
            base(owner, "Settings", relativeLocation, new Point(360, 200), catalog)
        {
            this.settings = settings;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            tabControl = new TabControl<TabSettings>(this, layout.RemainingWidth, layout.RemainingHeight);

            tabControl.TabLayouts[TabSettings.Common] = (layoutContainer) =>
            {
                layoutContainer = layoutContainer.AddLayoutScrollboxVertical(layoutContainer.RemainingWidth);
                ControlLayoutHorizontal line = layoutContainer.AddLayoutHorizontalLineOfText();
                int width = (int)(line.RemainingWidth * 0.8);
                line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Enable Logging")));
                Checkbox chkLoggingEnabled = new Checkbox(this);
                chkLoggingEnabled.OnClick += (object sender, MouseClickEventArgs e) => settings.UserSettings.Logging = (sender as Checkbox).State.Value;
                chkLoggingEnabled.State = settings.UserSettings.Logging;
                line.Add(chkLoggingEnabled);

                line = layoutContainer.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Restore Last View on Start")));
                Checkbox chkRestoreView = new Checkbox(this);
                chkRestoreView.OnClick += (object sender, MouseClickEventArgs e) => settings.RestoreLastView = (sender as Checkbox).State.Value;
                chkRestoreView.State = settings.RestoreLastView;
                line.Add(chkRestoreView);
            };

            layout.Add(tabControl);

            return layout;
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplaySettingsWindow, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplaySettingsWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                tabControl?.TabAction();
            }
        }
    }
}
