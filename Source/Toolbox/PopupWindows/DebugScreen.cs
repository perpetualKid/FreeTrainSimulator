
using FreeTrainSimulator.Common;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    public enum DebugScreenInformation
    {
        Common,
        Graphics,
        Route,
    }

    public class DebugScreen : OverlayBase
    {
        private readonly EnumArray<NameValueTextGrid, DebugScreenInformation> currentProvider = new EnumArray<NameValueTextGrid, DebugScreenInformation>();
        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly ToolboxSettings toolboxSettings;
        private DebugScreenInformation currentDebugScreen;

        public DebugScreen(WindowManager owner, ToolboxSettings settings, Color backgroundColor) :
            base(owner, CatalogManager.Catalog)
        {
            ZOrder = 0;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
            toolboxSettings = settings;
            currentProvider[DebugScreenInformation.Common] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(30 * Owner.DpiScaling));
            currentProvider[DebugScreenInformation.Graphics] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(150 * Owner.DpiScaling)) { Visible = false };
            currentProvider[DebugScreenInformation.Route] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(150 * Owner.DpiScaling)) { Visible = false, ColumnWidth = new int[] { 150, -1 } };
            UpdateBackgroundColor(backgroundColor);
            _ = EnumExtension.GetValue(toolboxSettings.PopupSettings[ToolboxWindowType.DebugScreen], out currentDebugScreen);
            currentProvider[currentDebugScreen].Visible = true;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            layout = base.Layout(layout, headerScaling);
            foreach (NameValueTextGrid item in currentProvider)
            {
                layout?.Add(item);
            }
            return layout;
        }

        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            currentProvider[DebugScreenInformation.Route].InformationProvider = e.ContentArea?.Content;
        }

        public void SetInformationProvider(DebugScreenInformation informationType, INameValueInformationProvider provider)
        {
            currentProvider[informationType].InformationProvider = provider;
        }

        public void UpdateBackgroundColor(Color backgroundColor)
        {
            bool outlineFont = toolboxSettings.OutlineFont;
            foreach (NameValueTextGrid item in currentProvider)
            {
                item.OutlineRenderOptions = !outlineFont ? OutlineRenderOptions.Default : null;
                item.TextColor = outlineFont ? backgroundColor.ComplementColor() : Color.White;
            }
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayDebugScreen, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayDebugScreen, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                if (currentDebugScreen != DebugScreenInformation.Common)
                    currentProvider[currentDebugScreen].Visible = false;
                currentDebugScreen = currentDebugScreen.Next();
                currentProvider[currentDebugScreen].Visible = true;
                toolboxSettings.PopupSettings[ToolboxWindowType.DebugScreen] = currentDebugScreen.ToString();
            }
        }
    }
}
