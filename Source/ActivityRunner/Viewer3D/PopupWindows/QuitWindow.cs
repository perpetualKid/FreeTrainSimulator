
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Processes;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation.MultiPlayer;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class QuitWindow : WindowBase
    {
        private readonly UserCommandController<UserCommand> userCommandController;

        public QuitWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Pause Menu"), relativeLocation, new Point(280, 112), catalog)
        {
            Modal = true;
            if (MultiPlayerManager.IsMultiPlayer())
                Resize(new Point(300, 95));
            OnWindowClosed += QuitWindow_OnWindowClosed;
            userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
        }

        private void QuitWindow_OnWindowClosed(object sender, EventArgs e)
        {
            Program.Viewer.ResumeReplaying();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            layout = base.Layout(layout, 1.5f);

            ControlLayout buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.5));
            Label quitLabel = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString($"Quit {RuntimeInfo.ApplicationName} ({Program.Viewer.Settings.Input.UserCommands[UserCommand.GameQuit]})"), HorizontalAlignment.Center);
            quitLabel.OnClick += QuitLabel_OnClick;
            buttonLine.Add(quitLabel);
            layout.AddHorizontalSeparator();
            if (!MultiPlayerManager.IsMultiPlayer())
            {
                buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.5));
                Label saveLabel = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString($"Save your game ({Program.Viewer.Settings.Input.UserCommands[UserCommand.GameSave]})"), HorizontalAlignment.Center);
                saveLabel.OnClick += SaveLabel_OnClick;
                buttonLine.Add(saveLabel);
                layout.AddHorizontalSeparator();
            }
            buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.5));
            Label continueLabel = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString($"Continue playing ({Program.Viewer.Settings.Input.UserCommands[UserCommand.GamePauseMenu]})"), HorizontalAlignment.Center);
            continueLabel.OnClick += ContinueLabel_OnClick;
            buttonLine.Add(continueLabel);
            return layout;
        }

        private void ContinueLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = Close();
        }

        private void SaveLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            GameStateRunActivity.Save();
        }

        private void QuitLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            (Owner.Game as GameHost).PopState();
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.GamePauseMenu, KeyEventType.KeyPressed, QuitGame, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.GamePauseMenu, KeyEventType.KeyPressed, QuitGame);
            return base.Close();
        }

        private void QuitGame(UserCommandArgs args)
        {
            args.Handled = true;
            _ = Close();
        }
    }
}
