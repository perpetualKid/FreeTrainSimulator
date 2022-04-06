
using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Graphics.MapView;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;

namespace Orts.TrackViewer.PopupWindows
{
    public enum DebugScreenInformation
    {
        Common,
        Graphics,
        Route,
    }

    public class DebugScreen : OverlayWindowBase
    {
        private readonly EnumArray<NameValueTextGrid, DebugScreenInformation> currentProvider = new EnumArray<NameValueTextGrid, DebugScreenInformation>();
        private readonly UserCommandController<UserCommand> userCommandController;

        private DebugScreenInformation currentDebugScreen;

        public DebugScreen(WindowManager owner, string caption, Color backgroundColor) :
            base(owner, caption, Point.Zero, Point.Zero)
        {
            ZOrder = 0;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
            currentProvider[DebugScreenInformation.Common] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(30 * Owner.DpiScaling));
            currentProvider[DebugScreenInformation.Graphics] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(150 * Owner.DpiScaling)) { Visible = false };
            currentProvider[DebugScreenInformation.Route] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(150 * Owner.DpiScaling)) { Visible = false, ColumnWidth = 120 };
            UpdateBackgroundColor(backgroundColor);
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            foreach (NameValueTextGrid item in currentProvider)
            {
                layout?.Add(item);
            }
            return base.Layout(layout, headerScaling);
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
            foreach(NameValueTextGrid item in currentProvider)
                item.TextColor = backgroundColor.ComplementColor();
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

        public override void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                if (currentDebugScreen != DebugScreenInformation.Common)
                    currentProvider[currentDebugScreen].Visible = false;
                currentDebugScreen = currentDebugScreen.Next();
                currentProvider[currentDebugScreen].Visible = true;
            }
        }
    }
}
