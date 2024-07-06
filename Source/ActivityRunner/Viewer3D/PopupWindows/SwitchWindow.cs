using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Graphics.Xna;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.Track;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class SwitchWindow : WindowBase
    {
        private const int SwitchImageSize = 32;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private ImageControl forwardEye;
        private ImageControl backwardEye;
        private ImageControl trainDirection;
        private ImageControl forwardSwitch;
        private ImageControl backwardSwitch;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private Rectangle eyeSection = new Rectangle(0, (int)(4.25 * SwitchImageSize), SwitchImageSize, SwitchImageSize / 2);
        private Rectangle directionSection = new Rectangle(0, 4 * SwitchImageSize, SwitchImageSize, SwitchImageSize);
        private Rectangle switchSection = new Rectangle(0, 0, SwitchImageSize, SwitchImageSize);
        private Texture2D switchStatesTexture;

        public SwitchWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Switch"), relativeLocation, new Point(74, 94), catalog)
        {
            CloseButton = false;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling).AddLayoutHorizontal();
            ControlLayout verticalLayout = layout.AddLayoutVertical(SwitchImageSize);
            verticalLayout.Add(forwardEye = new ImageControl(this, switchStatesTexture, 0, 0, eyeSection));
            verticalLayout.Add(trainDirection = new ImageControl(this, switchStatesTexture, 0, 0, directionSection));
            verticalLayout.Add(backwardEye = new ImageControl(this, switchStatesTexture, 0, 0, eyeSection));
            verticalLayout = layout.AddLayoutVertical(layout.RemainingWidth);
            verticalLayout.Add(forwardSwitch = new ImageControl(this, switchStatesTexture, 0, 0, switchSection));
            verticalLayout.Add(backwardSwitch = new ImageControl(this, switchStatesTexture, 0, 0, switchSection));
            forwardSwitch.OnClick += SwitchForward_OnClick;
            backwardSwitch.OnClick += SwitchBackward_OnClick;
            return layout;
        }

        private void SwitchBackward_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = new ToggleSwitchBehindCommand(Simulator.Instance.Log);
        }

        private void SwitchForward_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = new ToggleSwitchAheadCommand(Simulator.Instance.Log);
        }

        protected override void Initialize()
        {
            switchStatesTexture = TextureManager.GetTextureStatic(System.IO.Path.Combine(RuntimeInfo.ContentFolder, "SwitchStates.png"), Owner.Game);
            base.Initialize();
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                UpdateEye();
                UpdateDirection();
                UpdateSwitchState();
            }
            base.Update(gameTime, shouldUpdate);
        }

        private void UpdateEye()
        {
            bool flipped = Simulator.Instance.PlayerLocomotive.Flipped ^ Simulator.Instance.PlayerLocomotive.GetCabFlipped();
            eyeSection.X = (flipped) ? 0 : 3 * SwitchImageSize;
            forwardEye.ClippingRectangle = eyeSection;
            eyeSection.X = (!flipped) ? 0 : 3 * SwitchImageSize;
            backwardEye.ClippingRectangle = eyeSection;
        }

        private void UpdateDirection()
        {
            directionSection.X = Simulator.Instance.PlayerLocomotive.Train.MUDirection switch
            {
                MidpointDirection.Forward => 2 * SwitchImageSize,
                MidpointDirection.Reverse => 1 * SwitchImageSize,
                _ => 0,
            };
            trainDirection.ClippingRectangle = directionSection;
        }

        private void UpdateSwitchState()
        {
            void UpdateSwitch(bool front)
            {
                switchSection.Location = Point.Zero;
                Traveller traveller = front ? new Traveller(Simulator.Instance.PlayerLocomotive.Train.FrontTDBTraveller) : new Traveller(Simulator.Instance.PlayerLocomotive.Train.RearTDBTraveller, true);
                TrackNode previousNode = traveller.TrackNode;
                TrackJunctionNode switchNode = null;
                while (traveller.NextSection())
                {
                    if (traveller.TrackNode is TrackJunctionNode junctionNode)
                    {
                        switchNode = junctionNode;
                        break;
                    }
                    previousNode = traveller.TrackNode;
                }
                if (switchNode == null)
                    return;
                int switchPreviousNodeId = previousNode.Index;
                bool switchBranchesAwayFromUs = switchNode.TrackPins[0].Link == switchPreviousNodeId;
                bool switchMainRouteIsLeft = switchNode.Angle > 0;  // align the switch

                switchSection.X = ((switchBranchesAwayFromUs == front ? 1 : 3) + (switchMainRouteIsLeft ? 1 : 0)) * SwitchImageSize;
                switchSection.Y = switchNode.SelectedRoute * SwitchImageSize;
                TrackCircuitSection switchCircuitSection = TrackCircuitSection.TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
                if (switchCircuitSection.CircuitState.Occupied() || switchCircuitSection.CircuitState.SignalReserved >= 0 ||
                    (switchCircuitSection.CircuitState.TrainReserved != null && switchCircuitSection.CircuitState.TrainReserved.Train.ControlMode != TrainControlMode.Manual))
                    switchSection.Y += 2 * SwitchImageSize;
            }
            UpdateSwitch(true);
            forwardSwitch.ClippingRectangle = switchSection;
            UpdateSwitch(false);
            backwardSwitch.ClippingRectangle = switchSection;
        }

        protected override void Dispose(bool disposing)
        {
            switchStatesTexture?.Dispose();
            base.Dispose(disposing);
        }
    }
}
