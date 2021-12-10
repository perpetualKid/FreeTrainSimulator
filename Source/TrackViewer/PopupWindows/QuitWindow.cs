
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.TrackViewer.Control;

using UserCommand = Orts.TrackViewer.Control.UserCommand;

namespace Orts.TrackViewer.PopupWindows
{
    public class QuitWindow : WindowBase
    {
        private Label quitButton;
        private Label cancelButton;
        private Label printScreenButton;

        public event EventHandler OnQuitGame;
        public event EventHandler OnQuitCancel;
        public event EventHandler OnPrintScreen;

        private readonly UserCommandController<UserCommand> userCommandController;

        public QuitWindow(WindowManager owner, Point relativeLocation) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), CatalogManager.Catalog.GetString($"Exit {RuntimeInfo.ApplicationName}"), relativeLocation,
                new Point(owner.DefaultFontSize * 16, (int)(owner.DefaultFontSize * 5 + 20)))
        {
            Modal = true;
            ZOrder = 100;
            userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            if (null == layout)
                throw new ArgumentNullException(nameof(layout));

            layout = base.Layout(layout);
            quitButton = new Label(this, layout.RemainingWidth / 2, (int)(Owner.TextFontDefault.Height), CatalogManager.Catalog.GetString($"Quit ({InputSettings.UserCommands[UserCommand.QuitGame].ToString().Max(3)})"), HorizontalAlignment.Center);
            quitButton.OnClick += QuitButton_OnClick;
            cancelButton = new Label(this, layout.RemainingWidth / 2, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString($"Cancel ({InputSettings.UserCommands[UserCommand.Cancel].ToString().Max(3)})"), HorizontalAlignment.Center);
            cancelButton.OnClick += CancelButton_OnClick;
            ControlLayout buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            buttonLine.Add(quitButton);
            buttonLine.AddVerticalSeparator();
            buttonLine.Add(cancelButton);
            layout.AddHorizontalSeparator(true);
            printScreenButton = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString($"Take Screenshot ({InputSettings.UserCommands[UserCommand.PrintScreen].ToString().Max(3)})"), HorizontalAlignment.Center);
            printScreenButton.OnClick += PrintScreenButton_OnClick;
            buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            buttonLine.Add(printScreenButton);
            return layout;
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.QuitGame, KeyEventType.KeyPressed, QuitGame, true);
            userCommandController.AddEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelQuit, true);
            userCommandController.AddEvent(UserCommand.PrintScreen, KeyEventType.KeyPressed, PrintScreen, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelQuit);
            userCommandController.RemoveEvent(UserCommand.QuitGame, KeyEventType.KeyPressed, QuitGame);
            userCommandController.RemoveEvent(UserCommand.PrintScreen, KeyEventType.KeyPressed, PrintScreen);
            return base.Close();
        }

        private void PrintScreenButton_OnClick(object sender, MouseClickEventArgs e)
        {
            PrintScreen(UserCommandArgs.Empty);
        }

        private void CancelButton_OnClick(object sender, MouseClickEventArgs e)
        {
            CancelQuit(UserCommandArgs.Empty);
        }

        private void QuitButton_OnClick(object sender, MouseClickEventArgs e)
        {
            QuitGame(UserCommandArgs.Empty);
        }

        private void PrintScreen(UserCommandArgs args)
        {
            args.Handled = true;
            Close();
            Owner.Game.RunOneFrame();// allow the Window to be closed before taking a screenshot
            Owner.Game.RunOneFrame();// allow the Window to be closed before taking a screenshot
            OnPrintScreen?.Invoke(this, EventArgs.Empty);
        }

        private void QuitGame(UserCommandArgs args)
        {
            args.Handled = true;
            OnQuitGame?.Invoke(this, EventArgs.Empty);
        }

        private void CancelQuit(UserCommandArgs args)
        {
            args.Handled= true;
            OnQuitCancel?.Invoke(this, EventArgs.Empty);
            Close();
        }
    }
}
