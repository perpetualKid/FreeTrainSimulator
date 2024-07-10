
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

namespace Orts.Toolbox.PopupWindows
{
    internal sealed class AboutWindow : WindowBase
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
            Label label = new Label(this, layout.RemainingWidth, layout.RemainingHeight, $"{RuntimeInfo.ApplicationName}\r\nv{VersionInfo.FullVersion}", HorizontalAlignment.Center);
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
