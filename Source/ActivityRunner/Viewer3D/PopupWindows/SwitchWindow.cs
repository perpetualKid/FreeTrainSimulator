
using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.Track;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class SwitchWindow : WindowBase
    {
        private const int SwitchImageSize = 32;
        private readonly Viewer viewer;
        private ImageControl forwardEye;
        private ImageControl backwardEye;
        private ImageControl trainDirection;
        private ImageControl forwardSwitch;
        private ImageControl backwardSwitch;
        private Rectangle eyeSection = new Rectangle(0, (int)(4.25 * SwitchImageSize), SwitchImageSize, SwitchImageSize / 2);
        private Rectangle directionSection = new Rectangle(0, 4 * SwitchImageSize, SwitchImageSize, SwitchImageSize);
        private Rectangle switchSection = new Rectangle(0, 0, SwitchImageSize, SwitchImageSize);
        private Texture2D switchStatesTexture;

        public SwitchWindow(WindowManager owner, Point relativeLocation, Viewer viewer) :
            base(owner, "Switch", relativeLocation, new Point(74, 94))
        {
            this.viewer = viewer;
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
            _ = new ToggleSwitchBehindCommand(viewer.Log);
        }

        private void SwitchForward_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = new ToggleSwitchAheadCommand(viewer.Log);
        }

        protected override void Initialize()
        {
            switchStatesTexture = SharedTextureManager.Get(viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(viewer.ContentPath, "SwitchStates.png"));
            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            UpdateEye();
            UpdateDirection();
            UpdateSwitchState();
            base.Update(gameTime);
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
            directionSection.X = viewer.PlayerTrain.MUDirection switch
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
                Traveller traveller = front ? new Traveller(viewer.PlayerTrain.FrontTDBTraveller) : new Traveller(viewer.PlayerTrain.RearTDBTraveller, true);
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
    }
}
