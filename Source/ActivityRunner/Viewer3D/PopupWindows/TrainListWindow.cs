using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Simulation;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class TrainListWindow : WindowBase
    {
        private readonly Viewer viewer;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private ControlLayout scrollbox;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private int columnWidth;

        public TrainListWindow(WindowManager owner, Point relativeLocation, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train List"), relativeLocation, new Point(360, 200), catalog)
        {
            this.viewer = viewer;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            columnWidth = layout.RemainingWidth / 20;

            ControlLayout line = layout.AddLayoutHorizontalLineOfText();
            line.AddSpace(columnWidth, line.RemainingHeight);
            line.Add(new Label(this, columnWidth * 4, line.RemainingHeight, Catalog.GetString("Number")));
            line.Add(new Label(this, columnWidth * 15, line.RemainingHeight, Catalog.GetString("Service Name")));
            layout.AddHorizontalSeparator();
            scrollbox = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);
            UpdateTrains();
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate && Simulator.Instance.AI.TrainListChanged)
            {
                Simulator.Instance.AI.TrainListChanged = false;
                Layout();
            }
        }

        private void UpdateTrains()
        {
            AddTrainToList(Simulator.Instance.PlayerLocomotive?.Train);
            foreach (AITrain train in Simulator.Instance.AI.AITrains)
            {
                if (train.MovementState != AiMovementState.Static && train.TrainType != TrainType.Player
                        && !(train.TrainType == TrainType.AiIncorporated && !train.IncorporatingTrain.IsPathless))
                    AddTrainToList(train);
            }

            // Now list static trains with loco and cab
            if (Simulator.Instance.IsAutopilotMode)
            {
                foreach (Train train in Simulator.Instance.Trains.Where(t => t.TrainType == TrainType.Static && t.IsPlayable))
                {
                    AddTrainToList(train);
                }
            }
        }

        private void AddTrainToList(Train train)
        {
            if (train == null)
                return;
            ControlLayout line = scrollbox.AddLayoutHorizontalLineOfText();
            if (train == viewer.SelectedTrain)
            {
                line.Add(new Label(this, columnWidth, line.RemainingHeight, "*", HorizontalAlignment.Center) { TextColor = Color.Red });
            }
            else
                line.AddSpace(columnWidth, line.RemainingHeight);

            Color color = train.IsActualPlayerTrain
                ? Color.Red
                : train is AITrain aiTrain && aiTrain.MovementState == AiMovementState.Suspended
                ? Color.Orange
                : train.TrainType == TrainType.Static 
                ? Color.Yellow
                : Simulator.Instance.IsAutopilotMode && train.IsPlayable ? Color.LightGreen : Color.White;

            Label label;
            line.Add(label = new Label(this, columnWidth * 4, line.RemainingHeight, $"{train.Number}", HorizontalAlignment.Center) { TextColor = color, Tag = train });
            label.OnClick += Label_OnClick;
            line.Add(label = new Label(this, columnWidth * 15, line.RemainingHeight, train.Name) { TextColor = color, Tag = train });
            label.OnClick += Label_OnClick;
        }

        private void Label_OnClick(object sender, MouseClickEventArgs e)
        {
            if (sender is Label label && label.Tag is Train train)
            {
                if (train.ControlMode == TrainControlMode.TurnTable)
                {
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Train on turntable not aligned to a track can't be selected"));
                    return;
                }
                if (Simulator.Instance.PlayerLocomotive?.Train?.ControlMode == TrainControlMode.TurnTable)
                {
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Player train can't be switched when in turntable not aligned to a track"));
                    return;
                }
                Simulator.Instance.TrainSwitcher.SuspendOldPlayer = false;
                if (train != viewer.SelectedTrain)
                {
                    //Ask for change of viewed train
                    Simulator.Instance.TrainSwitcher.PickedTrainFromList = train;
                    Simulator.Instance.TrainSwitcher.ClickedTrainFromList = true;

                }
                if ((train == viewer.SelectedTrain || (train.TrainType == TrainType.AiIncorporated && train is AITrain aiTrain && 
                    aiTrain.IncorporatingTrain.IsPathless && aiTrain.IncorporatingTrain == viewer.SelectedTrain)) && !train.IsActualPlayerTrain &&
                    Simulator.Instance.IsAutopilotMode && train.IsPlayable)
                {
                    if (e.KeyModifiers.HasFlag(viewer.Settings.Input.GameSuspendOldPlayerModifier))
                        Simulator.Instance.TrainSwitcher.SuspendOldPlayer = true;
                    //Ask for change of driven train
                    Simulator.Instance.TrainSwitcher.SelectedAsPlayer = train;
                    Simulator.Instance.TrainSwitcher.ClickedSelectedAsPlayer = true;
                }
                else if (train != viewer.SelectedTrain)
                {
                    //Ask for change of viewed train
                    Simulator.Instance.TrainSwitcher.PickedTrainFromList = train;
                    Simulator.Instance.TrainSwitcher.ClickedTrainFromList = true;
                }
            }
        }
    }
}
