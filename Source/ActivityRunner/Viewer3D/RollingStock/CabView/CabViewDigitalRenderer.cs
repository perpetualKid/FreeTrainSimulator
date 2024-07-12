// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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
using System.Globalization;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    /// <summary>
    /// Digital Cab Control renderer
    /// Uses fonts instead of graphic
    /// </summary>
    public class CabViewDigitalRenderer : CabViewControlRenderer
    {
        public enum DigitalAlignment
        {
            Left,
            Center,
            Right,
            // Next ones are used for 3D cabs; digitals of old 3D cab will continue to be displayed left aligned for compatibility
            Cab3DLeft,
            Cab3DCenter,
            Cab3DRight
        }
        internal DigitalAlignment Alignment { get; }
        private string format = "{0}";
        private readonly string format1 = "{0}";
        private readonly string format2 = "{0}";
        private float numericValue;

        private protected Rectangle digitalPosition;
        private string text;
        private Color color;
        private readonly float rotation;

        private readonly CabTextRenderer textRenderer;
        private Texture2D textTexture;
        private readonly System.Drawing.Font textFont;
        private readonly HorizontalAlignment alignment;


        public CabViewDigitalRenderer(Viewer viewer, MSTSLocomotive car, CabViewDigitalControl digital, CabShader shader)
            : base(viewer, car, digital, shader)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            ArgumentNullException.ThrowIfNull(digital);

            textRenderer = CabTextRenderer.Instance(viewer.Game);
            int fontSize = (int)Math.Round(base.viewer.CabHeightPixels * digital.FontSize / 480 * 96 / 72);
            textFont = FontManager.Exact(digital.FontFamily, digital.FontStyle == 0 ? System.Drawing.FontStyle.Regular : System.Drawing.FontStyle.Bold)[fontSize];

            base.position.X = control.Bounds.X;
            base.position.Y = control.Bounds.Y;

            Alignment = digital.Justification switch
            {
                1 => DigitalAlignment.Center,
                2 => DigitalAlignment.Left,
                3 => DigitalAlignment.Right,
                // Used for 3D cabs
                4 => DigitalAlignment.Cab3DCenter,
                5 => DigitalAlignment.Cab3DLeft,
                6 => DigitalAlignment.Cab3DRight,
                _ => DigitalAlignment.Left,
            };

            alignment = digital.Justification switch
            {
                1 => HorizontalAlignment.Center,
                2 => HorizontalAlignment.Left,
                3 => HorizontalAlignment.Right,
                // Used for 3D cabs
                4 => HorizontalAlignment.Center,
                5 => HorizontalAlignment.Left,
                6 => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left,
            };

            // Clock defaults to centered.
            if (control.ControlType.CabViewControlType == CabViewControlType.Clock)
            {
                Alignment = DigitalAlignment.Center;
                alignment = HorizontalAlignment.Center;
            }

            format1 = $"{{0:0{new string('0', digital.LeadingZeros)}{(digital.Accuracy > 0 ? $".{new string('0', (int)digital.Accuracy)}" : "")}}}";
            format2 = "{0:0" + new string('0', digital.LeadingZeros) + (digital.AccuracySwitch > 0 ? "." + new string('0', (int)(digital.Accuracy + 1)) : "") + "}";

            var xScale = base.viewer.CabWidthPixels / 640f;
            var yScale = base.viewer.CabHeightPixels / 480f;
            // Cab view position adjusted to allow for letterboxing.
            digitalPosition.X = (int)(base.position.X * xScale) + (base.viewer.CabExceedsDisplayHorizontally > 0 ? textFont.Height / 4 : 0) - base.viewer.CabXOffsetPixels + base.viewer.CabXLetterboxPixels;
            digitalPosition.Y = (int)((base.position.Y + control.Bounds.Height / 2) * yScale) - textFont.Height / 2 + base.viewer.CabYOffsetPixels + base.viewer.CabYLetterboxPixels;
            digitalPosition.Width = (int)(control.Bounds.Width * xScale);
            digitalPosition.Height = (int)(control.Bounds.Height * yScale);
            rotation = digital.Rotation;
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            CabViewDigitalControl digital = control as CabViewDigitalControl;
            if (!IsPowered && control.ValueIfDisabled != null)
                numericValue = (float)control.ValueIfDisabled;
            else
                numericValue = locomotive.GetDataOf(control);

            if (digital.ScaleRangeMin < digital.ScaleRangeMax)
                numericValue = MathHelper.Clamp(numericValue, digital.ScaleRangeMin, digital.ScaleRangeMax);
            format = Math.Abs(numericValue) < digital.AccuracySwitch ? format2 : format1;

            if (control.ControlType.CabViewControlType == CabViewControlType.Clock)
            {
                text = digital.ControlStyle == CabViewControlStyle.Hour12
                    ? digital.Accuracy > 0 ? $"{DateTime.MinValue.AddSeconds(Simulator.Instance.ClockTime):hh:mm:ss}" : $"{DateTime.MinValue.AddSeconds(Simulator.Instance.ClockTime):hh:mm}"
                    : digital.Accuracy > 0 ? FormatStrings.FormatTime(Simulator.Instance.ClockTime) : FormatStrings.FormatApproximateTime(Simulator.Instance.ClockTime);
                color = digital.PositiveColors[0];
            }
            else if (digital.PreviousValue != 0 && digital.PreviousValue > numericValue && digital.DecreaseColor.A != 0)
            {
                text = string.Format(CultureInfo.CurrentCulture, format, Math.Abs(numericValue));
                color = new Color(digital.DecreaseColor.R, digital.DecreaseColor.G, digital.DecreaseColor.B, digital.DecreaseColor.A);
            }
            else if (numericValue < 0 && digital.NegativeColors[0].A != 0)
            {
                text = string.Format(CultureInfo.CurrentCulture, format, Math.Abs(numericValue));
                color = digital.NegativeColors.Length >= 2 && numericValue < digital.NegativeTrigger
                    ? digital.NegativeColors[1]
                    : digital.NegativeColors[0];
            }
            else if (digital.PositiveColors[0].A != 0)
            {
                text = string.Format(CultureInfo.CurrentCulture, format, numericValue);
                color = digital.PositiveColors.Length >= 2 && numericValue > digital.PositiveTrigger
                    ? digital.PositiveColors[1]
                    : digital.PositiveColors[0];
            }
            else
            {
                text = string.Format(CultureInfo.CurrentCulture, format, numericValue);
                color = Color.White;
            }

            base.PrepareFrame(frame, elapsedTime);
            textTexture = textRenderer.Prepare(text, textFont, OutlineRenderOptions.Default);
        }

        public override void Draw()
        {
            CabTextRenderer.DrawTextTexture(controlView.SpriteBatch, textTexture, digitalPosition, color, rotation, alignment);
        }

        public string Get3DDigits(out bool alert) //used in 3D cab, with AM/PM added, and determine if we want to use alert color
        {
            alert = false;
            CabViewDigitalControl digital = control as CabViewDigitalControl;
            string displayedText = "";
            if (!IsPowered && control.ValueIfDisabled != null)
                numericValue = (float)control.ValueIfDisabled;
            else
                numericValue = locomotive.GetDataOf(control);
            if (digital.ScaleRangeMin < digital.ScaleRangeMax)
                numericValue = MathHelper.Clamp(numericValue, (float)digital.ScaleRangeMin, (float)digital.ScaleRangeMax);
            if (Math.Abs(numericValue) < digital.AccuracySwitch)
                format = format2;
            else
                format = format1;

            if (control.ControlType.CabViewControlType == CabViewControlType.Clock)
            {
                displayedText = digital.ControlStyle == CabViewControlStyle.Hour12
                    ? digital.Accuracy > 0 ? $"{DateTime.MinValue.AddSeconds(Simulator.Instance.ClockTime):hh:mm:sst}" : $"{DateTime.MinValue.AddSeconds(Simulator.Instance.ClockTime):hh:mmt}"
                    : digital.Accuracy > 0 ? FormatStrings.FormatTime(Simulator.Instance.ClockTime) : FormatStrings.FormatApproximateTime(Simulator.Instance.ClockTime);
            }
            else if (digital.PreviousValue != 0 && digital.PreviousValue > numericValue && digital.DecreaseColor.A != 0)
            {
                displayedText = string.Format(CultureInfo.CurrentCulture, format, Math.Abs(numericValue));
            }
            else if (numericValue < 0 && digital.NegativeColors[0].A != 0)
            {
                displayedText = string.Format(CultureInfo.CurrentCulture, format, Math.Abs(numericValue));
                if (digital.NegativeColors.Length >= 2 && numericValue < digital.NegativeTrigger)
                    alert = true;
            }
            else if (digital.PositiveColors[0].A != 0)
            {
                displayedText = string.Format(CultureInfo.CurrentCulture, format, numericValue);
                if (digital.PositiveColors.Length >= 2 && numericValue > digital.PositiveTrigger)
                    alert = true;
            }
            else
            {
                displayedText = string.Format(CultureInfo.CurrentCulture, format, numericValue);
            }
            return displayedText;
        }
    }
}
