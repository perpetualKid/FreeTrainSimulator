﻿
using System;

using Microsoft.Xna.Framework;

using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Toolbox.PopupWindows
{
    public class StatusTextWindow : WindowBase
    {
        private Label routeLabel;

        public string RouteName { get => routeLabel?.Text; set => routeLabel.Text = value; }

        public StatusTextWindow(WindowManager owner, Point relativeLocation) :
            base(owner, "Loading", relativeLocation, new Point(300, 70))
        {
            Interactive = false;
            ZOrder = 70;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            layout = base.Layout(layout, 1.5f);
            ControlLayoutHorizontal statusTextLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            routeLabel = new Label(this, 0, 0, layout.RemainingWidth, Owner.TextFontDefault.Height, RouteName, HorizontalAlignment.Center, Owner.TextFontDefault, Color.Orange);
            statusTextLine.Add(routeLabel);
            return layout;
        }
    }
}
