﻿
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Info;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Toolbox.Control;

namespace Orts.Toolbox.PopupWindows
{
    public class QuitWindow : WindowBase
    {
        private Label quitButton;
        private Label cancelButton;
        private Label printScreenButton;

        public event EventHandler OnQuitGame;
        public event EventHandler OnQuitCancel;
        public event EventHandler OnPrintScreen;

        public QuitWindow(WindowManager owner, Point relativeLocation) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), CatalogManager.Catalog.GetString($"Exit {RuntimeInfo.ApplicationName}"), relativeLocation,
                new Point(owner.DefaultFontSize * 16, (int)(owner.DefaultFontSize * 4.5 + 20)))
        {
            Modal = true;
            ZOrder = 100;
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            if (null == layout)
                throw new ArgumentNullException(nameof(layout));

            quitButton = new Label(this, layout.RemainingWidth / 2, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString($"Quit ({InputSettings.UserCommands[UserCommand.QuitGame]})"), LabelAlignment.Center);
            quitButton.OnClick += QuitButton_OnClick;
            cancelButton = new Label(this, layout.RemainingWidth / 2, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString("Cancel"), LabelAlignment.Center);
            cancelButton.OnClick += CancelButton_OnClick;
            layout = base.Layout(layout);
            ControlLayout buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            buttonLine.Add(quitButton);
            buttonLine.AddVerticalSeparator();
            buttonLine.Add(cancelButton);
            layout.AddHorizontalSeparator(false);
            printScreenButton = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString($"Take Screenshot ({InputSettings.UserCommands[UserCommand.PrintScreen]})"), LabelAlignment.Center);
            printScreenButton.OnClick += PrintScreenButton_OnClick;
            layout.Add(printScreenButton);
            return layout;
        }

        public override bool Open()
        {
            return base.Open();
        }

        private void PrintScreenButton_OnClick(object sender, MouseClickEventArgs e)
        {
            Close();
            Owner.Game.RunOneFrame();// allow the Window to be closed before taking a screenshot
            OnPrintScreen?.Invoke(this, e);
        }

        private void CancelButton_OnClick(object sender, MouseClickEventArgs e)
        {
            Close();
            OnQuitCancel?.Invoke(this, e);
        }

        private void QuitButton_OnClick(object sender, MouseClickEventArgs e)
        {
            OnQuitGame.Invoke(this, e);
        }
    }
}
