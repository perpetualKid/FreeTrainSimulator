
using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Toolbox.PopupWindows
{
    internal class AboutWindow : WindowBase
    {
        private readonly UserCommandController<UserCommand> userCommandController;

        public AboutWindow(WindowManager owner, Point location, Catalog catalog = null) : 
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("About"), location, new Point(180, 60), catalog)
        {
            Modal = true;
            ZOrder = 100;
            userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            Label label = new Label(this, layout.RemainingWidth, layout.RemainingHeight, $"{RuntimeInfo.ApplicationName}\r\nv{VersionInfo.FullVersion}", Graphics.HorizontalAlignment.Center);
            label.OnClick += Label_OnClick;
            layout.Add(label);
            return layout;
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelQuit, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelQuit);
            return base.Close();
        }

        private void CancelQuit(UserCommandArgs args)
        {
            args.Handled = true;
            Close();
        }

        private void Label_OnClick(object sender, MouseClickEventArgs e)
        {
            Close();
        }
    }
}
