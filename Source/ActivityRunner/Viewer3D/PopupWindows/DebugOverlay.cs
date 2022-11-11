using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;
using Orts.Common;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Settings;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class DebugOverlay : OverlayBase
    {
        private enum TabSettings
        {
            Common,
            Clr,
        }

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly UserSettings settings;
        private NameValueTextGrid systemInformation;
        private TabLayout<TabSettings> tabLayout;
        private readonly System.Drawing.Font textFont = FontManager.Exact("Arial", System.Drawing.FontStyle.Regular)[12];

        private GraphControl graphControl;

        public DebugOverlay(WindowManager owner, UserSettings settings, Viewer viewer, Catalog catalog = null) : base(owner, catalog ?? CatalogManager.Catalog)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            this.settings = settings;
            userCommandController = viewer.UserCommandController;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            tabLayout = new TabLayout<TabSettings>(this, 10, 10, layout.RemainingWidth - 20, layout.RemainingHeight - 20);
            tabLayout.TabLayouts[TabSettings.Common] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(systemInformation = new NameValueTextGrid(this, 0, 0, textFont) { OutlineRenderOptions = OutlineRenderOptions.Default, ColumnWidth = 250 });
                systemInformation.InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.System];

                layoutContainer.Add(graphControl = new GraphControl(this, 0, 200, 1024, 40, "Min", "Max", "") { BorderColor = Color.GreenYellow});
            };
            tabLayout.TabLayouts[TabSettings.Clr] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(systemInformation = new NameValueTextGrid(this, 0, 0, textFont) { OutlineRenderOptions = OutlineRenderOptions.Default, ColumnWidth = 250 });
                systemInformation.InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.Clr];
            };
            layout.Add(tabLayout);
            return layout;
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayHUD, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayHUD, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.DebugOverlay], out TabSettings tab))
                tabLayout.TabAction(tab);
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                tabLayout.TabAction();
                settings.PopupSettings[ViewerWindowType.DebugOverlay] = tabLayout.CurrentTab.ToString();
            }
        }

    }
}
