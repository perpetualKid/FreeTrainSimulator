
using System;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;

using UserCommand = Orts.Toolbox.Control.UserCommand;

namespace Orts.Toolbox.PopupWindows
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

        public EnumArray<INameValueInformationProvider, DebugScreenInformation> DebugScreens { get; } = new EnumArray<INameValueInformationProvider, DebugScreenInformation>();


        public DebugScreen(WindowManager owner, string caption, Color backgroundColor) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), caption, Point.Zero, Point.Zero)
        {
            ZOrder = 0;
            userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
            currentProvider[DebugScreenInformation.Common] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(30 * Owner.DpiScaling))
            {
                TextColor = backgroundColor.ComplementColor(),
            };
            currentProvider[DebugScreenInformation.Graphics] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(150 * Owner.DpiScaling)) { Visible = false };
            currentProvider[DebugScreenInformation.Route] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(150 * Owner.DpiScaling)) { Visible = false, ColumnWidth = 120 };
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            foreach (NameValueTextGrid item in currentProvider)
            {
                layout?.Add(item);
            }
            return base.Layout(layout, headerScaling);
        }

        protected override void Initialize()
        {
            foreach (DebugScreenInformation item in EnumExtension.GetValues<DebugScreenInformation>())
            {
                currentProvider[item].DebugInformationProvider = DebugScreens[item];
            }
            base.Initialize();
        }

        public void UpdateBackgroundColor(Color backgroundColor)
        {
            //TODO 2021-12-12 consider TextColor for all text pages
            currentProvider[DebugScreenInformation.Common].TextColor = backgroundColor.ComplementColor();
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DebugScreenTab, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DebugScreenTab, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        public override void TabAction(UserCommandArgs args)
        {
            if (currentDebugScreen != DebugScreenInformation.Common)
                currentProvider[currentDebugScreen].Visible = false;
            currentDebugScreen = currentDebugScreen.Next();
            currentProvider[currentDebugScreen].Visible = true;
        }
    }
}
