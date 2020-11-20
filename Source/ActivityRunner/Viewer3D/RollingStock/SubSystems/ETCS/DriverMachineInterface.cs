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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Popups;
using Orts.Common;
using Orts.Formats.Msts.Models;
using Orts.Scripting.Api.Etcs;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.Subsystems.Etcs
{
    public class DriverMachineInterface
    {
        public readonly MSTSLocomotive Locomotive;
        public readonly CircularSpeedGauge CircularSpeedGauge;
        public readonly PlanningWindow PlanningWindow;
        float PrevScale = 1;
        float Scale = 1;
        readonly int Height = 480;
        readonly int Width = 640;

        ETCSStatus CurrentStatus;

        public DriverMachineInterface(float height, float width, MSTSLocomotive locomotive, CircularSpeedGauge csg, PlanningWindow planning)
        {
            Locomotive = locomotive;
            CircularSpeedGauge = csg;
            PlanningWindow = planning;

            Scale = Math.Min(width / Width, height / Height);
            CircularSpeedGauge.Scale = Scale;
            PlanningWindow.Scale = Scale;
        }

        public void PrepareFrame()
        {
            CurrentStatus = Locomotive.TrainControlSystem.ETCSStatus?.Clone(); // Clone the status class so everything can be accessed safely
            if (CurrentStatus == null || !CurrentStatus.DMIActive) return;
            CircularSpeedGauge.PrepareFrame();
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
            if (CurrentStatus == null || !CurrentStatus.DMIActive) return;
            CircularSpeedGauge.Draw(spriteBatch, new Point(position.X + (int)(54 * Scale), position.Y));
            PlanningWindow.Draw(spriteBatch, new Point(position.X + (int)(334 * Scale), position.Y));
        }
    }
    public class DriverMachineInterfaceRenderer : CabViewDigitalRenderer
    {
        DriverMachineInterface DMI;
        public DriverMachineInterfaceRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewDigitalControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            DMI = new DriverMachineInterface((float)Control.Bounds.Width, (float)Control.Bounds.Height, locomotive,
                new CircularSpeedGauge(
                    (int)Control.Bounds.Width * 280 / 640,
                    (int)Control.Bounds.Height * 300 / 480,
                    (int)Control.ScaleRangeMax,
                    Control.ControlUnit== Formats.Msts.CabViewControlUnit.Km_Per_Hour,
                    true,
                    Control.ScaleRangeMax == 240 || Control.ScaleRangeMax == 260,
                    (int)Control.ScaleRangeMin,
                    Locomotive,
                    Viewer,
                    shader
                ), new PlanningWindow(Viewer));
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            base.PrepareFrame(frame, elapsedTime);
            DMI.PrepareFrame();

            DMI.SizeTo(DrawPosition.Width, DrawPosition.Height);
        }

        public override void Draw()
        {
            DMI.Draw(CabShaderControlView.SpriteBatch, new Point(DrawPosition.X, DrawPosition.Y));
        }
    }

    class TextPrimitive
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