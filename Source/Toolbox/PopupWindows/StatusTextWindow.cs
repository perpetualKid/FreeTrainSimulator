﻿
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Toolbox.PopupWindows
{
    public class StatusTextWindow : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label routeLabel;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public string RouteName { get => routeLabel?.Text; set => routeLabel.Text = value; }

        public StatusTextWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Loading Route"), relativeLocation, new Point(300, 70), catalog)
        {
            Interactive = false;
            CloseButton = false;
            ZOrder = 70;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            layout = base.Layout(layout, 1.5f).AddLayoutVertical();
            layout.VerticalChildAlignment = VerticalAlignment.Center;
            ControlLayoutHorizontal statusTextLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            routeLabel = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, RouteName, HorizontalAlignment.Center, Color.Orange);
            statusTextLine.Add(routeLabel);
            return layout;
        }
    }
}
