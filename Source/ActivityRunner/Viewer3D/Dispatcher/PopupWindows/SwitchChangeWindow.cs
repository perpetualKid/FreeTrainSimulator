using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    public class SwitchChangeWindow : WindowBase
    {
        private readonly Point offset;
        private IJunction junction;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private RadioButton rbtnMain;
        private RadioButton rbtnSiding;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public SwitchChangeWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), (catalog ??= CatalogManager.Catalog).GetString("Change Switch"), relativeLocation, new Point(120, 65), catalog)
        {
            Modal = true;
            offset = new Point((int)((Borders.Width / -3) * owner.DpiScaling), (int)(10 * owner.DpiScaling));
        }

        public void OpenAt(Point point, IJunction junction)
        {
            this.junction = junction;
            switch (junction?.State)
            {
                case SwitchState.MainRoute:
                    rbtnMain.State = true;
                    break;
                case SwitchState.SideRoute:
                    rbtnSiding.State = true;
                    break;
                default:
                    rbtnSiding.State = false;
                    rbtnMain.State = false;
                    break;
            }
            Relocate(point + offset);
            Open();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            Label label;
            layout = base.Layout(layout, headerScaling);
            ControlLayout rbLayout = layout.AddLayoutVertical();
            RadioButtonGroup radioButtonGroup = new RadioButtonGroup();
            ControlLayout line = rbLayout.AddLayoutHorizontalLineOfText();
            line.Add(rbtnMain= new RadioButton(this, radioButtonGroup) { TextColor = Color.White, State = true, Tag = SwitchState.MainRoute });
            rbtnMain.OnClick += Button_OnClick;
            line.Add(label = new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString("Main Route")) { Tag = SwitchState.MainRoute});
            label.OnClick += Button_OnClick;

            line = rbLayout.AddLayoutHorizontalLineOfText();
            line.Add(rbtnSiding = new RadioButton(this, radioButtonGroup) { TextColor = Color.Red, Tag = SwitchState.SideRoute });
            rbtnSiding.OnClick += Button_OnClick;
            line.Add(label = new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString("Side Route")) { Tag = SwitchState.SideRoute });
            label.OnClick += Button_OnClick;
            return layout;
        }

        private void Button_OnClick(object sender, MouseClickEventArgs e)
        {
            if (sender is WindowControl control && control.Tag != null)
            {
                //aider can send message to the server for a switch
                if (MultiPlayerManager.IsMultiPlayer() && MultiPlayerManager.Instance().AmAider)
                {
                    //aider selects and throws the switch, but need to confirm by the dispatcher
                    MultiPlayerManager.Notify((new MSGSwitch(MultiPlayerManager.GetUserName(), junction, (SwitchState)control.Tag, true)).ToString());
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Switching Request Sent to the Server"));
                }
                else
                {
                    Simulator.Instance.SignalEnvironment.RequestSetSwitch(junction, (SwitchState)control.Tag);
                }
            }
            Close();
        }

        protected override void FocusLost()
        {
            Close();
            base.FocusLost();
        }
    }
}
