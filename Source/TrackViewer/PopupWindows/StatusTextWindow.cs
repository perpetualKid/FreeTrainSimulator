
using System;

using Microsoft.Xna.Framework;

using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.TrackViewer.PopupWindows
{
    public class StatusTextWindow : WindowBase
    {
        private Label headerLabel;
        private Label routeLabel;

        public string RouteName { get => routeLabel?.Text; set => routeLabel.Text = value; }

        public StatusTextWindow(WindowManager owner, Point relativeLocation) : 
            base(owner, "Loading", relativeLocation, new Point (300, 70))
        {
            Interactive = false;
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            System.Drawing.Font headerFont = FontManager.Instance("Segoe UI", System.Drawing.FontStyle.Bold)[(int)Math.Round(24 * Owner.DpiScaling)];
            // Pad window by 4px, add caption and separator between to content area.
            layout = layout?.AddLayoutOffset((int)(4 * Owner.DpiScaling)).AddLayoutVertical() ?? throw new ArgumentNullException(nameof(layout));
            headerLabel = new Label(this, 0, 0, layout.RemainingWidth, headerFont.Height, Caption, LabelAlignment.Center, headerFont, Color.White);
            layout.Add(headerLabel);
            layout.AddHorizontalSeparator(true);
            routeLabel = new Label(this, 0, 0, layout.RemainingWidth, Owner.TextFontDefault.Height, RouteName, LabelAlignment.Center, Owner.TextFontDefault, Color.Orange);
            layout.Add(routeLabel);
            return layout;
        }
    }
}
