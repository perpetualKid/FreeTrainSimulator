
using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Toolbox.Settings;

using GetText;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox.PopupWindows
{
    public class QuitWindow : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label quitButton;
        private Label cancelButton;
        private Label printScreenButton;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public event EventHandler OnQuitGame;
        public event EventHandler OnQuitCancel;
        public event EventHandler OnPrintScreen;

        private readonly UserCommandController<UserCommand> userCommandController;

        public QuitWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString($"Exit {RuntimeInfo.ApplicationName}"), relativeLocation, new Point(380, 82), catalog)
        {
            Modal = true;
            ZOrder = 100;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            layout = base.Layout(layout, 1.5f);
            quitButton = new Label(this, layout.RemainingWidth / 2, Owner.TextFontDefault.Height, Catalog.GetString($"Quit ({InputSettings.UserCommands[UserCommand.QuitWindow].ToString().Max(3)})"), HorizontalAlignment.Center);
            quitButton.OnClick += QuitButton_OnClick;
            cancelButton = new Label(this, layout.RemainingWidth / 2, Owner.TextFontDefault.Height, Catalog.GetString($"Cancel ({InputSettings.UserCommands[UserCommand.Cancel].ToString().Max(3)})"), HorizontalAlignment.Center);
            cancelButton.OnClick += CancelButton_OnClick;
            ControlLayout buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            buttonLine.Add(quitButton);
            buttonLine.AddVerticalSeparator();
            buttonLine.Add(cancelButton);
            layout.AddHorizontalSeparator(true);
            printScreenButton = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString($"Take Screenshot ({InputSettings.UserCommands[UserCommand.PrintScreen].ToString().Max(3)})"), HorizontalAlignment.Center);
            printScreenButton.OnClick += PrintScreenButton_OnClick;
            buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            buttonLine.Add(printScreenButton);
            return layout;
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.QuitWindow, KeyEventType.KeyPressed, QuitGame, true);
            userCommandController.AddEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelQuit, true);
            userCommandController.AddEvent(UserCommand.PrintScreen, KeyEventType.KeyPressed, PrintScreen, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelQuit);
            userCommandController.RemoveEvent(UserCommand.QuitWindow, KeyEventType.KeyPressed, QuitGame);
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
            args.Handled = true;
            OnQuitCancel?.Invoke(this, EventArgs.Empty);
            Close();
        }
    }
}
