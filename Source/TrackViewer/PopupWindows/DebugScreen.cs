
using System;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;

using UserCommand = Orts.TrackViewer.Control.UserCommand;

namespace Orts.TrackViewer.PopupWindows
{
    public enum DebugScreenInformation
    {
        Common,
    }

    public class DebugScreen : OverlayWindowBase
    {
        private readonly NameValueTextGrid commonInfo;
        private readonly UserCommandController<UserCommand> userCommandController;
        private DebugScreenInformation currentDebugScreen;

        public EnumArray<INameValueInformationProvider, DebugScreenInformation> DebugScreens { get; } = new EnumArray<INameValueInformationProvider, DebugScreenInformation>();

        public DebugScreen(WindowManager owner, string caption, Color backgroundColor) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), caption, Point.Zero, Point.Zero)
        {
            userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
            ZOrder = 0;
            commonInfo = new NameValueTextGrid(this, 10, 30)
            {
                TextColor = backgroundColor.ComplementColor(),
            };
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            layout?.Add(commonInfo);
            return base.Layout(layout);
        }

        protected override void Initialize()
        {
            commonInfo.DebugInformationProvider = DebugScreens[DebugScreenInformation.Common];
            base.Initialize();
        }

        public void UpdateBackgroundColor(Color backgroundColor)
        {
            commonInfo.TextColor = backgroundColor.ComplementColor();
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.TabAction, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.TabAction, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void TabAction(UserCommandArgs args)
        {
            currentDebugScreen = currentDebugScreen.Next();
        }
    }
}
