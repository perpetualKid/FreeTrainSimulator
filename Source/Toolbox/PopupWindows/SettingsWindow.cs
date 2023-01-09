
using System.ComponentModel;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.MapView;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    internal class SettingsWindow : WindowBase
    {
        private readonly ToolboxSettings toolboxSettings;
        private ContentArea contentArea;

        private enum TabSettings
        {
            [Description("Common")]
            Common,
            [Description("Toolbox")]
            Toolbox,
            [Description("Graphics")]
            Graphics
        }

#pragma warning disable CA2213 // Disposable fields should be disposed
        private TabControl<TabSettings> tabControl;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly UserCommandController<UserCommand> userCommandController;

        public SettingsWindow(WindowManager owner, ToolboxSettings settings, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Settings"), relativeLocation, new Point(360, 200), catalog)
        {
            toolboxSettings = settings;
            this.contentArea = contentArea;
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
                chkLoggingEnabled.OnClick += (object sender, MouseClickEventArgs e) => toolboxSettings.UserSettings.Logging = (sender as Checkbox).State.Value;
                chkLoggingEnabled.State = toolboxSettings.UserSettings.Logging;
                line.Add(chkLoggingEnabled);

                line = layoutContainer.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Restore Last View on Start")));
                Checkbox chkRestoreView = new Checkbox(this);
                chkRestoreView.OnClick += (object sender, MouseClickEventArgs e) => toolboxSettings.RestoreLastView = (sender as Checkbox).State.Value;
                chkRestoreView.State = toolboxSettings.RestoreLastView;
                line.Add(chkRestoreView);

                line = layoutContainer.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, width, line.RemainingHeight, Catalog.GetString("Complement Font Color")));
                Checkbox chkFontColorComplement = new Checkbox(this);
                chkFontColorComplement.OnClick += (object sender, MouseClickEventArgs e) =>
                {
                    toolboxSettings.ComplementFontColor = (sender as Checkbox).State.Value;
                    if (null != contentArea)
                        contentArea.FontOutlineOptions = (sender as Checkbox).State.Value ? null : OutlineRenderOptions.Default;
                    ((Owner as WindowManager<ToolboxWindowType>)[ToolboxWindowType.DebugScreen] as DebugScreen).UpdateBackgroundColor(ColorExtension.FromName(toolboxSettings.ColorSettings[ColorSetting.Background]));
                };
                chkFontColorComplement.State = toolboxSettings.ComplementFontColor;
                line.Add(chkFontColorComplement);
            };
            layout.Add(tabControl);

            return layout;
        }

        private void TabControl_TabChanged(object sender, TabChangedEventArgs<TabSettings> e)
        {
            toolboxSettings.PopupSettings[ToolboxWindowType.SettingsWindow] = e.Tab.ToString();
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (toolboxSettings.RestoreLastView && EnumExtension.GetValue(toolboxSettings.PopupSettings[ToolboxWindowType.SettingsWindow], out TabSettings tab))
                tabControl.TabAction(tab);
            tabControl.TabChanged += TabControl_TabChanged;
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

        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
        }

    }
}
