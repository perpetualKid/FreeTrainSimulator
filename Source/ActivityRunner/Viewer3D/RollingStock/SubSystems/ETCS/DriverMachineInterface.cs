// COPYRIGHT 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Popups;
using Orts.Common;
using Orts.Common.Input;
using Orts.Formats.Msts.Models;
using Orts.Scripting.Api.Etcs;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.Subsystems.Etcs
{
    public class DriverMachineInterface
    {
        public readonly MSTSLocomotive Locomotive;
        readonly Viewer Viewer;
        public readonly CircularSpeedGauge CircularSpeedGauge;
        public readonly PlanningWindow PlanningWindow;
        float PrevScale = 1;
        public float Scale { get; private set; }
        readonly int Height = 480;
        readonly int Width = 640;

        // Color RGB values are from ETCS specification
        public static readonly Color ColorGrey = new Color(195, 195, 195);
        public static readonly Color ColorMediumGrey = new Color(150, 150, 150);
        public static readonly Color ColorDarkGrey = new Color(85, 85, 85);
        public static readonly Color ColorYellow = new Color(223, 223, 0);
        public static readonly Color ColorOrange = new Color(234, 145, 0);
        public static readonly Color ColorRed = new Color(191, 0, 2);
        public static readonly Color ColorBackground = new Color(3, 17, 34); // dark blue
        public static readonly Color ColorPASPlight = new Color(41, 74, 107);
        public static readonly Color ColorPASPdark = new Color(33, 49, 74);

        readonly Point SpeedAreaLocation;
        readonly Point PlanningLocation;

        Texture2D ColorTexture;

        public bool IsTouchScreen = true;
        public bool IsSoftLayout;

        public class Button
        {
            public readonly string Name;
            public bool Enabled;
            public readonly bool UpType;
            public readonly Rectangle SensitiveArea;
            public Button(string name, bool upType, Rectangle area)
            {
                Name = name;
                Enabled = false;
                UpType = upType;
                SensitiveArea = area;
            }
        }

        public readonly List<Button> SensitiveButtons = new List<Button>();

        Button ActiveButton; // Name of the button currently being pressed without valid pulsation yet
        public Button PressedButton; // Name of the button with a valid pulsation in current frame 

        ETCSStatus CurrentStatus;

        public DriverMachineInterface(float height, float width, MSTSLocomotive locomotive, Viewer viewer)
        {
            Viewer = viewer;
            Locomotive = locomotive;
            Scale = Math.Min(width / Width, height / Height);

            PlanningLocation = new Point(334, IsSoftLayout ? 0 : 15);
            SpeedAreaLocation = new Point(54, IsSoftLayout ? 0 : 15);

            CircularSpeedGauge = new CircularSpeedGauge(
                   (int)(280 * Scale),
                   (int)(300 * Scale),
                   /*(int)Control.MaxValue*/400,
                   true/**/,
                   false/*true*/,
                   false/*Control.MaxValue == 240 || Control.MaxValue == 260*/,
                   /*(int)Control.MinValue*/400,
                   Locomotive,
                   Viewer,
                   null
               );
            PlanningWindow = new PlanningWindow(this, Viewer, PlanningLocation);

            CircularSpeedGauge.Scale = Scale;
            PlanningWindow.Scale = Scale;
        }

        public void PrepareFrame()
        {
            CurrentStatus = Locomotive.TrainControlSystem.ETCSStatus?.Clone(); // Clone the status class so everything can be accessed safely
            if (CurrentStatus == null || !CurrentStatus.DMIActive) return;
            CircularSpeedGauge.PrepareFrame(CurrentStatus);
            PlanningWindow.PrepareFrame(CurrentStatus);
        }
        public void SizeTo(float width, float height)
        {
            Scale = Math.Min(width / Width, height / Height);
            CircularSpeedGauge.Scale = Scale;
            PlanningWindow.Scale = Scale;

            if (Math.Abs(1f - PrevScale / Scale) > 0.1f)
            {
                PrevScale = Scale;
                CircularSpeedGauge.SetFont();
                PlanningWindow.SetFont();
            }
        }

        public void Draw(SpriteBatch spriteBatch, Point position)
        {
            if (ColorTexture == null)
            {
                ColorTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                ColorTexture.SetData(new[] { Color.White });
            }
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, DepthStencilState.Default, null, null);
            if (CurrentStatus == null || !CurrentStatus.DMIActive) return;
            spriteBatch.Draw(ColorTexture, new Rectangle(position, new Point((int)(640 * Scale), (int)(480 * Scale))), ColorBackground);
            CircularSpeedGauge.Draw(spriteBatch, new Point(position.X + (int)(SpeedAreaLocation.X * Scale), position.Y + (int)(SpeedAreaLocation.Y * Scale)));
            PlanningWindow.Draw(spriteBatch, new Point(position.X + (int)(PlanningLocation.X * Scale), position.Y + (int)(PlanningLocation.Y * Scale)));
        }

        internal void MouseClickedEvent(Point location)
        {
            PressedButton = null;

            foreach (Button button in SensitiveButtons)
            {
                if (button.SensitiveArea.Contains(location))
                {
                    ActiveButton = button;
                    if (!button.UpType && button.Enabled) 
                        PressedButton = ActiveButton;
                    break;
                }
            }

        }

        internal void MouseReleasedEvent(Point location)
        {
            PressedButton = null;
            if (ActiveButton != null)
            {
                if (ActiveButton.Enabled && ActiveButton.UpType && ActiveButton.SensitiveArea.Contains(location))
                {
                    PressedButton = ActiveButton;
                }
            }
            ActiveButton = null;
        }

        public void HandleMouseInput(bool pressed, int x, int y)
        {
            PressedButton = null;
            if (ActiveButton != null)
            {
                if (!pressed && ActiveButton.Enabled && ActiveButton.UpType && ActiveButton.SensitiveArea.Contains(x, y))
                {
                    PressedButton = ActiveButton;
                }
            }
            else if (pressed)
            {
                foreach (Button b in SensitiveButtons)
                {
                    if (b.SensitiveArea.Contains(x, y))
                    {
                        ActiveButton = b;
                        if (!b.UpType && b.Enabled) PressedButton = ActiveButton;
                        break;
                    }
                }
            }
            if (!pressed) ActiveButton = null;
            if (PressedButton != null)
            {
                PlanningWindow.HandleInput();
                PressedButton = null;
            }
        }
        /*public void HandleButtonInput(string button, bool pressed)
        {
            if (pressed)
            {
                if ()
            }
            else if (ActiveButton == button && SensitiveButtons[ActiveButton].UpType)
            {
                PressedButton = ActiveButton;
                ActiveButton = null;
            }
        }*/
    }
    public class DriverMachineInterfaceRenderer : CabViewDigitalRenderer, ICabViewMouseControlRenderer
    {
        private readonly DriverMachineInterface driverMachineInterface;

        public DriverMachineInterfaceRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewDigitalControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            driverMachineInterface = new DriverMachineInterface((int)Control.Bounds.Width, (int)Control.Bounds.Height, locomotive, viewer);

            viewer.UserCommandController.AddEvent(CommonUserCommand.PointerPressed, MouseClickedEvent);
            viewer.UserCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedEvent);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            base.PrepareFrame(frame, elapsedTime);
            driverMachineInterface.PrepareFrame();
            driverMachineInterface.SizeTo(DrawPosition.Width, DrawPosition.Height);
        }

        public bool IsMouseWithin(Point mousePoint)
        {
            int x = (int)((mousePoint.X - DrawPosition.X) / driverMachineInterface.Scale);
            int y = (int)((mousePoint.Y - DrawPosition.Y) / driverMachineInterface.Scale);
            foreach (DriverMachineInterface.Button button in driverMachineInterface.SensitiveButtons)
            {
                if (button.SensitiveArea.Contains(x, y)) 
                    return true;
            }
            return false;
        }

        public void HandleUserInput(GenericButtonEventType buttonEventType, Point position, Vector2 delta)
        {
            throw new NotImplementedException();
        }

        public string GetControlName(Point mousePoint)
        {
            int x = (int)((mousePoint.X - DrawPosition.X) / driverMachineInterface.Scale);
            int y = (int)((mousePoint.Y - DrawPosition.Y) / driverMachineInterface.Scale);

            foreach (DriverMachineInterface.Button button in driverMachineInterface.SensitiveButtons)
            {
                if (button.SensitiveArea.Contains(x, y)) 
                    return $"ETCS {button.Name}";
            }
            return "";
        }

        public override void Draw()
        {
            //var spriteBatch = CabShaderControlView.SpriteBatch;
            //spriteBatch.End();
            //spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointWrap, DepthStencilState.Default, null, Shader);
            driverMachineInterface.Draw(CabShaderControlView.SpriteBatch, new Point(DrawPosition.X, DrawPosition.Y));
            CabShaderControlView.SpriteBatch.End();
            CabShaderControlView.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, DepthStencilState.Default, null, Shader);
        }

        private void MouseClickedEvent(UserCommandArgs userCommandArgs)
        {
            Point pointerLocation = (userCommandArgs as PointerCommandArgs).Position;
            driverMachineInterface.MouseClickedEvent(new Point((int)((pointerLocation.X - DrawPosition.X) / driverMachineInterface.Scale), (int)((pointerLocation.Y - DrawPosition.Y) / driverMachineInterface.Scale)));
        }

        private void MouseReleasedEvent(UserCommandArgs userCommandArgs)
        {
            Point pointerLocation = (userCommandArgs as PointerCommandArgs).Position;
            driverMachineInterface.MouseReleasedEvent(new Point((int)((pointerLocation.X - DrawPosition.X) / driverMachineInterface.Scale), (int)((pointerLocation.Y - DrawPosition.Y) / driverMachineInterface.Scale)));
        }
    }

    internal class TextPrimitive
    {
        public Point Position;
        public Color Color;
        public WindowTextFont Font;
        public string Text;

        public TextPrimitive(Point position, Color color, string text, WindowTextFont font)
        {
            Position = position;
            Color = color;
            Text = text;
            Font = font;
        }

        public void Draw(SpriteBatch spriteBatch, Point position)
        {
            Font.Draw(spriteBatch, position, Text, Color);
        }
    }
}