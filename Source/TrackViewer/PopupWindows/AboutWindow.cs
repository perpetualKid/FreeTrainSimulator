
using Microsoft.Xna.Framework;

using Orts.Common.Info;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.TrackViewer.PopupWindows
{
    internal class AboutWindow : WindowBase
    {
        public AboutWindow(WindowManager owner, Point location) : 
            base(owner, "About", location, new Point(180, 64))
        {
            Modal = true;
            ZOrder = 100;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            Label label = new Label(this, layout.RemainingWidth, layout.RemainingHeight, $"{RuntimeInfo.ApplicationName}\r\nv{VersionInfo.FullVersion}", Graphics.HorizontalAlignment.Center);
            label.OnClick += Label_OnClick;
            layout.Add(label);
            return layout;
        }

        private void Label_OnClick(object sender, MouseClickEventArgs e)
        {
            Close();
        }
    }
}
