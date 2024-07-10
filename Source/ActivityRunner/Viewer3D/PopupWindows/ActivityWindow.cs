
using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;
using Orts.Simulation;
using Orts.Simulation.Activities;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class ActivityWindow : WindowBase
    {
        private readonly UserCommandController<UserCommand> userCommandController;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private TextBox messageText;
        private Label eventHeader;
        private Label gameSaveLabel;
        private Label resumeGameLabel;
        private Label quitLabel;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private EventWrapper currentEvent;
        private readonly Viewer viewer;
        private long closeTimeout;
        private bool autoClose;
        private DateTime openTime;

        public ActivityWindow(WindowManager owner, Point relativeLocation, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Activity Events"), relativeLocation, new Point(600, 220), catalog)
        {
            Modal = true;
            userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
            this.viewer = viewer;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            ControlLayout line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, 80, line.RemainingHeight, Catalog.GetString("Event:")));
            line.Add(eventHeader = new Label(this, line.RemainingWidth, line.RemainingHeight, null));
            layout.AddHorizontalSeparator();
            layout.Add(messageText = new TextBox(this, layout.RemainingWidth, layout.RemainingHeight - (int)(1.5 * Owner.TextFontDefault.Height), null, true));
            layout.AddHorizontalSeparator();
            line = layout.AddLayoutHorizontalLineOfText();
            int columnWidth = line.RemainingWidth / 3;
            line.Add(gameSaveLabel = new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString($"Save game ({viewer.Settings.Input.UserCommands[UserCommand.GameSave]})"), HorizontalAlignment.Center));
            gameSaveLabel.OnClick += GameSaveLabel_OnClick;
            line.Add(resumeGameLabel = new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString($"Resume ({viewer.Settings.Input.UserCommands[UserCommand.GamePause]})"), HorizontalAlignment.Center));
            resumeGameLabel.OnClick += ResumeGameLabel_OnClick;
            line.Add(quitLabel = new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString($"Quit game ({viewer.Settings.Input.UserCommands[UserCommand.GameQuit]})"), HorizontalAlignment.Center));
            quitLabel.OnClick += QuitLabel_OnClick;
            return layout;
        }

        public void QuitActivityCommand()
        {
            if (Simulator.Instance.IsReplaying)
                Simulator.Instance.Confirmer.Confirm(CabControl.Activity, CabSetting.On);
            (Owner.Game as GameHost).PopState();
        }

        public void CloseAndResumeCommand()
        {
            if (Simulator.Instance.IsReplaying)
                Simulator.Instance.Confirmer.Confirm(CabControl.Activity, CabSetting.On);
            Close();
        }

        private void QuitLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = new QuitActivityCommand(viewer.Log, eventHeader.Text, (DateTime.UtcNow - openTime).TotalMilliseconds / 1000);
        }

        private void ResumeGameLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = new CloseAndResumeActivityCommand(viewer.Log, eventHeader.Text, (DateTime.UtcNow - openTime).TotalMilliseconds / 1000);
        }

        private async void GameSaveLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            await viewer.Game.State.Save().ConfigureAwait(false);
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (autoClose && System.Environment.TickCount64 > closeTimeout)
                Close();
            base.Update(gameTime, shouldUpdate);
        }

        public void OpenFromEvent(EventWrapper triggeredEvent)
        {
            if (!string.IsNullOrEmpty(triggeredEvent.ActivityEvent.Outcomes.DisplayMessage))
            {
                currentEvent = triggeredEvent;
                messageText.SetText(currentEvent.ActivityEvent.Outcomes.DisplayMessage);
                eventHeader.Text = currentEvent.ActivityEvent.Name;
                openTime = DateTime.UtcNow;
                if (currentEvent.ActivityEvent.OrtsContinue >= 0)
                {
                    autoClose = true;
                    closeTimeout = System.Environment.TickCount64 + (currentEvent.ActivityEvent.OrtsContinue * 1000);
                }
                _ = Open();
            }
            else
                Simulator.Instance.ActivityRun?.AcknowledgeEvent(triggeredEvent);
        }

        public override bool Open()
        {
            bool result = base.Open();
            if (result)
            {
                userCommandController.AddEvent(UserCommand.GamePause, KeyEventType.KeyPressed, CloseAction, true);
            }
            return result;
        }

        public override bool Close()
        {
            autoClose = false;
            Simulator.Instance.ActivityRun?.AcknowledgeEvent(currentEvent);
            userCommandController.RemoveEvent(UserCommand.GamePause, KeyEventType.KeyPressed, CloseAction);
            return base.Close();
        }

        private void CloseAction(UserCommandArgs args)
        {
            args.Handled = true;
            _ = Close();
        }


    }
}
