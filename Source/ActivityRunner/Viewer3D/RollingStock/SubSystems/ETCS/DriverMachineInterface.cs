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

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Graphics;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.Xna;
using Orts.Scripting.Api.Etcs;
using Orts.Simulation.RollingStocks;

using static Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems.Etcs.DriverMachineInterface;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems.Etcs
{
    public class DriverMachineInterface
    {
        public readonly MSTSLocomotive Locomotive;
        public readonly bool GaugeOnly;
        public readonly Viewer Viewer;
        private List<DMIWindow> Windows = new List<DMIWindow>();
        private float PrevScale = 1;
        public ETCSStatus ETCSStatus { get; private set; }

        private bool Active;
        public float Scale { get; private set; }
        public float MipMapScale { get; private set; }

        internal readonly int Height = 480;
        internal readonly int Width = 640;

        public readonly ETCSDefaultWindow ETCSDefaultWindow;

        public readonly DriverMachineInterfaceShader Shader;

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
        public static readonly Color ColorShadow = new Color(8, 24, 57);

        // Some DMIs use black for the background and white for borders, instead of blue scale
        public readonly bool BlackWhiteTheme;

        public Texture2D ColorTexture { get; private set; }

        public bool Blinker2Hz { get; private set; }
        public bool Blinker4Hz { get; private set; }

        public enum DMIMode
        {
            FullSize,
            SpeedArea,
            PlanningArea,
            GaugeOnly
        }

        public DMIMode CurrentDMIMode { get; private set; }

        private float BlinkerTime;

        public float CurrentTime => (float)Viewer.Simulator.ClockTime;

        /// <summary>
        /// True if the screen is sensitive
        /// </summary>
        public bool IsTouchScreen = true;
        /// <summary>
        /// Controls the layout of the DMI screen depending.
        /// Must be true if there are physical buttons to control the DMI, even if it is a touch screen.
        /// If false, the screen must be tactile.
        /// </summary>
        public bool IsSoftLayout;
        public DMIWindow ActiveWindow;
        private DMIButton ActiveButton;
        public DriverMachineInterface(float height, float width, MSTSLocomotive locomotive, Viewer viewer, CabViewControl control)
        {
            if (control is CabViewScreenControl)
            {
                CurrentDMIMode = DMIMode.FullSize;
                if ((control as CabViewScreenControl).CustomParameters.TryGetValue("mode", out string mode))
                {
                    if (mode == "planningarea")
                        CurrentDMIMode = DMIMode.PlanningArea;
                    else if (mode == "speedarea")
                        CurrentDMIMode = DMIMode.SpeedArea;
                }
            }
            else
            {
                CurrentDMIMode = DMIMode.GaugeOnly;
            }
            switch (CurrentDMIMode)
            {
                case DMIMode.GaugeOnly:
                    Width = 280;
                    Height = 300;
                    break;
                case DMIMode.FullSize:
                    Width = 640;
                    Height = 480;
                    break;
                case DMIMode.PlanningArea:
                case DMIMode.SpeedArea:
                    Width = 334;
                    Height = 480;
                    break;
            }
            Viewer = viewer;
            Locomotive = locomotive;
            Scale = Math.Min(width / Width, height / Height);
            if (Scale < 0.5)
                MipMapScale = 2;
            else
                MipMapScale = 1;

            Shader = new DriverMachineInterfaceShader(viewer.Game.GraphicsDevice);
            ETCSDefaultWindow = new ETCSDefaultWindow(this, control);
            ETCSDefaultWindow.Visible = true;

            AddToLayout(ETCSDefaultWindow, Point.Zero);
            ActiveWindow = ETCSDefaultWindow;
        }

        public void ShowSubwindow(DMISubwindow window)
        {
            AddToLayout(window, new Point(window.FullScreen ? 0 : 334, 15));
        }

        public void AddToLayout(DMIWindow window, Point position)
        {
            window.Position = position;
            window.Parent = ActiveWindow;
            ActiveWindow = window;
            Windows.Add(window);
        }
        public Texture2D LoadTexture(string name)
        {
            string path;
            if (MipMapScale == 2)
                path = System.IO.Path.Combine(Viewer.ContentPath, "ETCS", "mipmap-2", name);
            else
                path = System.IO.Path.Combine(Viewer.ContentPath, "ETCS", name);
            return SharedTextureManager.Get(Viewer.Game.GraphicsDevice, path);
        }
        public void PrepareFrame(double elapsedSeconds)
        {
            ETCSStatus currentStatus = Locomotive.TrainControlSystem.ETCSStatus;
            ETCSStatus = currentStatus;
            Active = currentStatus != null && currentStatus.DMIActive;
            if (!Active)
                return;

            BlinkerTime += (float)elapsedSeconds;
            BlinkerTime -= (int)BlinkerTime;
            Blinker2Hz = BlinkerTime < 0.5;
            Blinker4Hz = BlinkerTime < 0.25 || (BlinkerTime > 0.5 && BlinkerTime < 0.75);

            foreach (var area in Windows)
            {
                area.PrepareFrame(currentStatus);
            }
        }
        public void SizeTo(float width, float height)
        {
            Scale = Math.Min(width / Width, height / Height);

            if (Math.Abs(1f - PrevScale / Scale) > 0.1f)
            {
                PrevScale = Scale;
                if (Scale < 0.5)
                    MipMapScale = 2;
                else
                    MipMapScale = 1;
                foreach (var area in Windows)
                {
                    area.ScaleChanged();
                }
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
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearWrap, null, null, null); // TODO: Handle brightness via DMI shader
            if (!Active)
                return;
            foreach (var area in Windows)
            {
                area.Draw(spriteBatch, new Point(position.X + (int)(area.Position.X * Scale), position.Y + (int)(area.Position.Y * Scale)));
            }
        }

        internal void MouseClickedEvent(Point location)
        {
            //PressedButton = null;

            //foreach (Button button in SensitiveButtons)
            //{
            //    if (button.SensitiveArea.Contains(location))
            //    {
            //        ActiveButton = button;
            //        if (!button.UpType && button.Enabled)
            //            PressedButton = ActiveButton;
            //        break;
            //    }
            //}
        }

        internal void MouseReleasedEvent(Point location)
        {
            //PressedButton = null;
            //if (ActiveButton != null)
            //{
            //    if (ActiveButton.Enabled && ActiveButton.UpType && ActiveButton.SensitiveArea.Contains(location))
            //    {
            //        PressedButton = ActiveButton;
            //    }
            //}
            //ActiveButton = null;
        }

        public void HandleMouseInput(bool pressed, int x, int y)
        {
            DMIButton pressedButton = null;
            if (ActiveButton != null)
            {
                if (!ActiveButton.Enabled)
                {
                    ActiveButton.Pressed = false;
                    ActiveButton = null;
                }
                else if (ActiveButton.SensitiveArea(ActiveWindow.Position).Contains(x, y))
                {
                    if (ActiveButton.UpType)
                    {
                        if (ActiveButton.DelayType && ActiveButton.FirstPressed + 2 > CurrentTime)
                        {
                            ActiveButton.Pressed = ((int)((CurrentTime - ActiveButton.FirstPressed) * 4)) % 2 == 0;
                        }
                        else
                        {
                            ActiveButton.Pressed = true;
                            if (!pressed)
                            {
                                pressedButton = ActiveButton;
                            }
                        }
                    }
                    else
                    {
                        ActiveButton.Pressed = false;
                        if (ActiveButton.FirstPressed + 1.5 < CurrentTime)
                        {
                            if (ActiveButton.LastPressed + 0.3 < CurrentTime)
                            {
                                pressedButton = ActiveButton;
                                ActiveButton.Pressed = true;
                                ActiveButton.LastPressed = CurrentTime;
                            }
                        }
                    }
                }
                else
                {
                    ActiveButton.FirstPressed = CurrentTime;
                    ActiveButton.Pressed = false;
                }
            }
            else if (pressed)
            {
                foreach (var area in ActiveWindow.SubAreas)
                {
                    if (!(area is DMIButton))
                        continue;
                    var b = (DMIButton)area;
                    b.Pressed = false;
                    if (b.SensitiveArea(ActiveWindow.Position).Contains(x, y))
                    {
                        ActiveButton = b;
                        ActiveButton.Pressed = true;
                        ActiveButton.FirstPressed = CurrentTime;
                        if (!b.UpType && b.Enabled)
                            pressedButton = ActiveButton;
                        break;
                    }
                }
            }
            if (!pressed && ActiveButton != null)
            {
                ActiveButton.Pressed = false;
                ActiveButton = null;
            }
            pressedButton?.PressedAction();
        }
        public void ExitWindow(DMIWindow window)
        {
            var windows = new List<DMIWindow>(Windows);
            windows.Remove(window);
            Windows = windows;
            if (window.Parent == null)
                ActiveWindow = ETCSDefaultWindow;
            else
                ActiveWindow = window.Parent;
        }
    }
    public class ETCSDefaultWindow : DMIWindow
    {
        private CircularSpeedGauge CircularSpeedGauge;
        private PlanningWindow PlanningWindow;
        private MessageArea MessageArea;
        private TargetDistance TargetDistance;
        private TTIandLSSMArea TTIandLSSMArea;
        private MenuBar MenuBar;
        public ETCSDefaultWindow(DriverMachineInterface dmi, CabViewControl control) : base(dmi, dmi.Width, dmi.Height)
        {
            if (dmi.CurrentDMIMode == DMIMode.GaugeOnly)
            {
                var dig = control as CabViewDigitalControl;
                CircularSpeedGauge = new CircularSpeedGauge(
                (int)dig.ScaleRangeMax,
                dig.ControlUnit != CabViewControlUnit.Miles_Per_Hour,
                dig.ControlUnit != CabViewControlUnit.None,
                dig.ScaleRangeMax == 240 || dig.ScaleRangeMax == 260,
                (int)dig.ScaleRangeMin,
                DMI);
                AddToLayout(CircularSpeedGauge, new Point(0, 0));
                return;
            }
            if (dmi.CurrentDMIMode != DMIMode.PlanningArea)
            {
                var param = (control as CabViewScreenControl).CustomParameters;
                int maxSpeed = 400;
                if (param.TryGetValue("maxspeed", out string value))
                    int.TryParse(value, out maxSpeed);
                int maxVisibleSpeed = maxSpeed;
                if (param.TryGetValue("maxvisiblespeed", out value))
                    int.TryParse(value, out maxVisibleSpeed);
                CircularSpeedGauge = new CircularSpeedGauge(
                       maxSpeed,
                       control.ControlUnit != CabViewControlUnit.Miles_Per_Hour,
                       param.ContainsKey("displayunits") && param["displayunits"] == "1",
                       maxSpeed == 240 || maxSpeed == 260,
                       maxVisibleSpeed,
                       dmi
                   );

                TTIandLSSMArea = new TTIandLSSMArea(dmi);
                TargetDistance = new TargetDistance(dmi);
                MessageArea = new MessageArea(dmi);

                CircularSpeedGauge.Layer = -1;
                TargetDistance.Layer = -1;
                TTIandLSSMArea.Layer = -1;
                MessageArea.Layer = -1;
                AddToLayout(CircularSpeedGauge, new Point(54, DMI.IsSoftLayout ? 0 : 15));
                AddToLayout(TTIandLSSMArea, new Point(0, DMI.IsSoftLayout ? 0 : 15));
                AddToLayout(TargetDistance, new Point(0, 54 + (DMI.IsSoftLayout ? 0 : 15)));
                AddToLayout(MessageArea, new Point(54, DMI.IsSoftLayout ? 350 : 365));
                AddToLayout(MessageArea.ButtonScrollUp, new Point(54 + 234, DMI.IsSoftLayout ? 350 : 365));
                AddToLayout(MessageArea.ButtonScrollDown, new Point(54 + 234, MessageArea.Height / 2 + (DMI.IsSoftLayout ? 350 : 365)));
            }
            if (dmi.CurrentDMIMode != DMIMode.SpeedArea)
            {
                // Calculate start position of the planning area when a two-screen display is used
                // Real width of the left area in ETCS specs is 306 px, however in order to have
                // both screens with the same size I assumed both have 334 px
                // To be checked
                int startPos = dmi.CurrentDMIMode == DMIMode.FullSize ? 334 : (334 - 306) / 2;
                PlanningWindow = new PlanningWindow(dmi);
                MenuBar = new MenuBar(dmi);
                AddToLayout(PlanningWindow, new Point(startPos, DMI.IsSoftLayout ? 0 : 15));
                AddToLayout(PlanningWindow.ButtonScaleDown, new Point(startPos, DMI.IsSoftLayout ? 0 : 15));
                AddToLayout(PlanningWindow.ButtonScaleUp, new Point(startPos, 285 + (DMI.IsSoftLayout ? 0 : 15)));

                for (int i = 0; i < MenuBar.Buttons.Count; i++)
                {
                    AddToLayout(MenuBar.Buttons[i], new Point(580, 15 + 50 * i));
                }
            }
        }
    }

    public class DMIArea
    {
        public Point Position;
        public readonly DriverMachineInterface DMI;
        protected Texture2D ColorTexture => DMI.ColorTexture;
        public float Scale => DMI.Scale;
        public readonly int Height;
        public readonly int Width;
        protected List<RectanglePrimitive> Rectangles = new List<RectanglePrimitive>();
        protected List<TextPrimitive> Texts = new List<TextPrimitive>();
        protected List<TexturePrimitive> Textures = new List<TexturePrimitive>();
        public int Layer;
        protected bool FlashingFrame;
        public Color BackgroundColor = Color.Transparent;
        public bool Pressed;
        public bool Visible;
        public class TextPrimitive
        {
            private readonly CabTextRenderer textRenderer;
            public Point Position;
            public Color Color;
            public System.Drawing.Font Font;
            public string Text;

            public TextPrimitive(Game game, Point position, Color color, string text, System.Drawing.Font font)
            {
                textRenderer = CabTextRenderer.Instance(game);
                Position = position;
                Color = color;
                Text = text;
                Font = font;
            }

            public void Draw(SpriteBatch spriteBatch, Point position)
            {
                textRenderer.DrawString(spriteBatch, position.ToVector2(), Text, Font, Color);
            }
        }
        public struct TexturePrimitive
        {
            public readonly Texture2D Texture;
            public readonly Vector2 Position;
            public TexturePrimitive(Texture2D texture, Vector2 position)
            {
                Texture = texture;
                Position = position;
            }
            public TexturePrimitive(Texture2D texture, float x, float y)
            {
                Texture = texture;
                Position = new Vector2(x, y);
            }
        }
        public struct RectanglePrimitive
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Width;
            public readonly float Height;
            public readonly bool DrawAsInteger;
            public Color Color;
        }
        public DMIArea(DriverMachineInterface dmi)
        {
            DMI = dmi;
        }
        public DMIArea(DriverMachineInterface dmi, int width, int height)
        {
            DMI = dmi;
            Width = width;
            Height = height;
        }
        public virtual void Draw(SpriteBatch spriteBatch, Point position)
        {
            if (BackgroundColor != Color.Transparent)
                DrawRectangle(spriteBatch, position, 0, 0, Width, Height, BackgroundColor);

            foreach (var r in Rectangles)
            {
                if (r.DrawAsInteger)
                    DrawIntRectangle(spriteBatch, position, r.X, r.Y, r.Width, r.Height, r.Color);
                else
                    DrawRectangle(spriteBatch, position, r.X, r.Y, r.Width, r.Height, r.Color);
            }
            foreach (var text in Texts)
            {
                int x = position.X + (int)Math.Round(text.Position.X * Scale);
                int y = position.Y + (int)Math.Round(text.Position.Y * Scale);
                text.Draw(spriteBatch, new Point(x, y));
            }
            foreach (var tex in Textures)
            {
                DrawSymbol(spriteBatch, tex.Texture, position, tex.Position.Y, tex.Position.Y);
            }
            if (FlashingFrame && DMI.Blinker4Hz)
            {
                DrawIntRectangle(spriteBatch, position, 0, 0, 2, Height, ColorYellow);
                DrawIntRectangle(spriteBatch, position, Width - 2, 0, 2, Height, ColorYellow);
                DrawIntRectangle(spriteBatch, position, 0, 0, Width, 2, ColorYellow);
                DrawIntRectangle(spriteBatch, position, 0, Height - 2, Width, 2, ColorYellow);
            }
            else if (DMI.BlackWhiteTheme)
            {
                DrawIntRectangle(spriteBatch, position, 0, 0, 1, Height, Color.White);
                DrawIntRectangle(spriteBatch, position, Width - 1, 0, 1, Height, Color.White);
                DrawIntRectangle(spriteBatch, position, 0, 0, Width, 1, Color.White);
                DrawIntRectangle(spriteBatch, position, 0, Height - 1, Width, 1, Color.White);
            }
            else if (this is DMIButton && (this as DMIButton).ShowButtonBorder)
            {
                DrawIntRectangle(spriteBatch, position, 0, 0, 1, Height, Color.Black);
                DrawIntRectangle(spriteBatch, position, Width - 1, 0, 1, Height, ColorShadow);
                DrawIntRectangle(spriteBatch, position, 0, 0, Width, 1, Color.Black);
                DrawIntRectangle(spriteBatch, position, 0, Height - 1, Width, 1, ColorShadow);

                if (!Pressed)
                {
                    DrawIntRectangle(spriteBatch, position, 1, 1, 1, Height - 2, ColorShadow);
                    DrawIntRectangle(spriteBatch, position, Width - 2, 1, 1, Height - 2, Color.Black);
                    DrawIntRectangle(spriteBatch, position, 1, 1, Width - 2, 1, ColorShadow);
                    DrawIntRectangle(spriteBatch, position, 1, Height - 2, Width - 2, 1, Color.Black);
                }
            }
            else if (Layer < 0)
            {
                DrawIntRectangle(spriteBatch, position, 0, 0, 1, Height, Color.Black);
                DrawIntRectangle(spriteBatch, position, Width - 1, 0, 1, Height, ColorShadow);
                DrawIntRectangle(spriteBatch, position, 0, 0, Width, 1, Color.Black);
                DrawIntRectangle(spriteBatch, position, 0, Height - 1, Width, 1, ColorShadow);
            }
        }
        public virtual void PrepareFrame(ETCSStatus status) { }

        public void DrawRectangle(SpriteBatch spriteBatch, Point drawPosition, float x, float y, float width, float height, Color color)
        {
            spriteBatch.Draw(ColorTexture, new Vector2(drawPosition.X + x * Scale, drawPosition.Y + y * Scale), null, color, 0f, Vector2.Zero, new Vector2(width * Scale, height * Scale), SpriteEffects.None, 0);
        }
        public void DrawIntRectangle(SpriteBatch spriteBatch, Point drawPosition, float x, float y, float width, float height, Color color)
        {
            spriteBatch.Draw(ColorTexture, new Rectangle(drawPosition.X + (int)(x * Scale), drawPosition.Y + (int)(y * Scale), Math.Max((int)(width * Scale), 1), Math.Max((int)(height * Scale), 1)), null, color);
        }
        public void DrawSymbol(SpriteBatch spriteBatch, Texture2D texture, Point origin, float x, float y)
        {
            spriteBatch.Draw(texture, new Vector2(origin.X + x * Scale, origin.Y + y * Scale), null, Color.White, 0, Vector2.Zero, Scale * DMI.MipMapScale, SpriteEffects.None, 0);
        }

        public System.Drawing.Font GetTextFont(float size, bool bold = false)
        {
            return FontManager.Exact("Arial", bold ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular)[(int)(size * 96 / 72)];
        }
        /// <summary>
        /// Get scaled font size, increasing it if result is small
        /// </summary>
        /// <param name="requiredSize"></param>
        /// <returns></returns>
        public float GetScaledFontSize(float requiredSize)
        {
            float size = requiredSize * Scale;
            if (size < 5)
                return size * 1.2f;
            return size;
        }
        public virtual void ScaleChanged() { }
    }
    public class DMIWindow : DMIArea
    {
        public DMIWindow Parent;
        public List<DMIArea> SubAreas = new List<DMIArea>();
        public bool FullScreen;
        protected DMIWindow(DriverMachineInterface dmi, int width, int height) : base(dmi, width, height)
        {
        }
        public override void PrepareFrame(ETCSStatus status)
        {
            if (!Visible)
                return;
            base.PrepareFrame(status);
            foreach (var area in SubAreas)
            {
                area.PrepareFrame(status);
            }
        }
        public override void Draw(SpriteBatch spriteBatch, Point position)
        {
            if (!Visible)
                return;
            base.Draw(spriteBatch, position);
            foreach (var area in SubAreas)
            {
                if (area.Visible)
                    area.Draw(spriteBatch, new Point((int)Math.Round(position.X + area.Position.X * Scale), (int)Math.Round(position.Y + area.Position.Y * Scale)));
            }
        }
        public void AddToLayout(DMIArea area, Point position)
        {
            area.Position = position;
            area.Visible = true;
            SubAreas.Add(area);
        }
        public override void ScaleChanged()
        {
            base.ScaleChanged();
            foreach (var area in SubAreas)
            {
                area.ScaleChanged();
            }
        }
    }
    public class DMISubwindow : DMIWindow
    {
        public string WindowTitle { get; private set; }

        private TextPrimitive WindowTitleText;
        private System.Drawing.Font WindowTitleFont;
        private readonly int FontHeightWindowTitle = 12;
        protected readonly DMIIconButton CloseButton;
        public DMISubwindow(string title, bool fullScreen, DriverMachineInterface dmi) : base(dmi, fullScreen ? 640 : 306, 450)
        {
            WindowTitle = title;
            FullScreen = fullScreen;
            CloseButton = new DMIIconButton("NA_11.bmp", "NA_12.bmp", Viewer.Catalog.GetString("Close"), true, () => dmi.ExitWindow(this), 82, 50, dmi);
            CloseButton.Enabled = true;
            BackgroundColor = DMI.BlackWhiteTheme ? Color.Black : ColorBackground;
            SetFont();
            AddToLayout(CloseButton, new Point(fullScreen ? 334 : 0, 400));
        }

        public override void ScaleChanged()
        {
            base.ScaleChanged();
            SetFont();
        }

        private void SetFont()
        {
            WindowTitleFont = GetTextFont(FontHeightWindowTitle);
            SetTitle(WindowTitle);
        }

        public override void Draw(SpriteBatch spriteBatch, Point position)
        {
            if (!Visible)
                return;
            base.Draw(spriteBatch, position);
            DrawRectangle(spriteBatch, position, 0, 0, FullScreen ? 334 : 306, 24, Color.Black);
            int x = position.X + (int)Math.Round(WindowTitleText.Position.X * Scale);
            int y = position.Y + (int)Math.Round(WindowTitleText.Position.Y * Scale);
            WindowTitleText.Draw(spriteBatch, new Point(x, y));
        }

        public void SetTitle(string s)
        {
            WindowTitle = s;
            int length = (int)(TextTextureRenderer.Instance(Parent.DMI.Viewer.Game).Measure(s, WindowTitleFont).Width / Scale);
            int x = FullScreen ? (334 - length - 5) : 5;
            WindowTitleText = new TextPrimitive(Parent.DMI.Viewer.Game, new Point(x, (24 - FontHeightWindowTitle) / 2), ColorGrey, WindowTitle, WindowTitleFont);
        }
    }
    public class DMIButton : DMIArea
    {
        public Rectangle SensitiveArea(Point WindowPosition) => new Rectangle(WindowPosition.X + Position.X - ExtendedSensitiveArea.X, WindowPosition.Y + Position.Y - ExtendedSensitiveArea.Y, Width + ExtendedSensitiveArea.Width + ExtendedSensitiveArea.X, Height + ExtendedSensitiveArea.Height + ExtendedSensitiveArea.Y);
        public Rectangle ExtendedSensitiveArea;
        public Action PressedAction;
        public string ConfirmerCaption;
        public readonly string DisplayName;
        public bool Enabled;
        public bool PressedEffect;
        public readonly bool UpType;
        public bool DelayType;
        public bool ShowButtonBorder;
        public float FirstPressed;
        public float LastPressed;

        public DMIButton(string displayName, bool upType, Action pressedAction, int width, int height, DriverMachineInterface dmi, bool showButtonBorder = false) : base(dmi, width, height)
        {
            DisplayName = displayName;
            Enabled = false;
            UpType = upType;
            PressedAction = pressedAction;
            ShowButtonBorder = showButtonBorder;
        }
    }
    public class DMITextButton : DMIButton
    {
        private string[] Caption;
        private System.Drawing.Font CaptionFont;
        private int FontHeightButton = 12;
        private TextPrimitive[] CaptionText;
        public DMITextButton(string caption, string displayName, bool upType, Action pressedAction, int width, int height, DriverMachineInterface dmi, int fontHeight = 12) :
            base(displayName, upType, pressedAction, width, height, dmi, true)
        {
            Caption = caption.Split('\n');
            CaptionText = new TextPrimitive[Caption.Length];
            ConfirmerCaption = caption;
            FontHeightButton = fontHeight;
            SetFont();
            SetText();
        }

        private void SetText()
        {
            for (int i = 0; i < Caption.Length; i++)
            {
                int fontWidth = (int)(TextTextureRenderer.Instance(DMI.Viewer.Game).Measure(Caption[i], CaptionFont).Width / Scale);
                CaptionText[i] = new TextPrimitive(DMI.Viewer.Game, new Point((Width - fontWidth) / 2, (Height - FontHeightButton) / 2 + FontHeightButton * (2 * i - Caption.Length + 1)), Color.White, Caption[i], CaptionFont);
            }
        }
        public override void ScaleChanged()
        {
            base.ScaleChanged();
            SetFont();
            SetText();
        }

        private void SetFont()
        {
            CaptionFont = GetTextFont(FontHeightButton);
        }

        public override void PrepareFrame(ETCSStatus status)
        {
            base.PrepareFrame(status);
            foreach (var text in CaptionText)
                text.Color = Enabled ? ColorGrey : ColorDarkGrey;
        }
        public override void Draw(SpriteBatch spriteBatch, Point position)
        {
            base.Draw(spriteBatch, position);
            foreach (var text in CaptionText)
            {
                int x = position.X + (int)Math.Round(text.Position.X * Scale);
                int y = position.Y + (int)Math.Round(text.Position.Y * Scale);
                text.Draw(spriteBatch, new Point(x, y));
            }
        }
    }
    public class DMIIconButton : DMIButton
    {
        private readonly string DisabledSymbol;
        private readonly string EnabledSymbol;
        private TexturePrimitive DisabledTexture;
        private TexturePrimitive EnabledTexture;
        public DMIIconButton(string enabledSymbol, string disabledSymbol, string displayName, bool upType, Action pressedAction, int width, int height, DriverMachineInterface dmi) :
            base(displayName, upType, pressedAction, width, height, dmi, true)
        {
            DisabledSymbol = disabledSymbol;
            EnabledSymbol = enabledSymbol;
            SetIcon();
        }

        private void SetIcon()
        {
            Texture2D tex1 = DMI.LoadTexture(EnabledSymbol);
            Texture2D tex2 = DMI.LoadTexture(DisabledSymbol);
            EnabledTexture = new TexturePrimitive(tex1, new Vector2((Width - tex1.Width * DMI.MipMapScale) / 2, (Height - tex1.Height * DMI.MipMapScale) / 2));
            DisabledTexture = new TexturePrimitive(tex2, new Vector2((Width - tex2.Width * DMI.MipMapScale) / 2, (Height - tex2.Height * DMI.MipMapScale) / 2));
        }
        public override void ScaleChanged()
        {
            base.ScaleChanged();
            SetIcon();
        }
        public override void Draw(SpriteBatch spriteBatch, Point position)
        {
            base.Draw(spriteBatch, position);
            var tex = Enabled ? EnabledTexture : DisabledTexture;
            DrawSymbol(spriteBatch, tex.Texture, position, tex.Position.X, tex.Position.Y);
        }
    }
    public class DMITextLabel : DMIArea
    {
        private string[] Caption;
        private System.Drawing.Font CaptionFont;
        private int FontHeightButton = 12;
        private TextPrimitive[] CaptionText;
        public DMITextLabel(string caption, int width, int height, DriverMachineInterface dmi) :
            base(dmi, width, height)
        {
            Caption = caption.Split('\n');
            CaptionText = new TextPrimitive[Caption.Length];
            SetFont();
            SetText();
        }

        private void SetText()
        {
            for (int i = 0; i < Caption.Length; i++)
            {
                int fontWidth = (int)(TextTextureRenderer.Instance(DMI.Viewer.Game).Measure(Caption[i], CaptionFont).Width / Scale);
                CaptionText[i] = new TextPrimitive(DMI.Viewer.Game, new Point((Width - fontWidth) / 2, (Height - FontHeightButton) / 2 + FontHeightButton * (2 * i - Caption.Length + 1)), ColorGrey, Caption[i], CaptionFont);
            }
        }
        public override void ScaleChanged()
        {
            base.ScaleChanged();
            SetFont();
            SetText();
        }

        private void SetFont()
        {
            CaptionFont = GetTextFont(FontHeightButton);
        }
        public override void Draw(SpriteBatch spriteBatch, Point position)
        {
            base.Draw(spriteBatch, position);
            foreach (var text in CaptionText)
            {
                int x = position.X + (int)Math.Round(text.Position.X * Scale);
                int y = position.Y + (int)Math.Round(text.Position.Y * Scale);
                text.Draw(spriteBatch, new Point(x, y));
            }
        }
    }

    public class CircularSpeedGaugeRenderer : CabViewDigitalRenderer
    {
        private readonly DriverMachineInterface driverMachineInterface;
        private bool mouseRightButtonPressed;
        public CircularSpeedGaugeRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewDigitalControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            // Height is adjusted to keep compatibility
            driverMachineInterface = new DriverMachineInterface((int)(Control.Bounds.Width * 640 / 280), (int)(Control.Bounds.Height * 480 / 300), locomotive, viewer, control);
            viewer.UserCommandController.AddEvent(CommonUserCommand.AlternatePointerPressed, MouseRightButtonPressed);
            viewer.UserCommandController.AddEvent(CommonUserCommand.AlternatePointerReleased, MouseRightButtonReleased);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            base.PrepareFrame(frame, elapsedTime);

            driverMachineInterface.SizeTo(position.Width, position.Height);
            driverMachineInterface.ETCSDefaultWindow.BackgroundColor = Color.Transparent;
            driverMachineInterface.PrepareFrame(elapsedTime.ClockSeconds);
        }
        public override void Draw()
        {
            driverMachineInterface.Draw(ControlView.SpriteBatch, new Point(position.X, position.Y));
            ControlView.SpriteBatch.End();
            ControlView.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, DepthStencilState.Default, null, Shader);
        }

        private void MouseRightButtonPressed(UserCommandArgs userCommandArgs)
        {
            mouseRightButtonPressed = true;
        }

        private void MouseRightButtonReleased(UserCommandArgs userCommandArgs)
        {
            mouseRightButtonPressed = false;
        }
    }

    public class DriverMachineInterfaceRenderer : CabViewControlRenderer, ICabViewMouseControlRenderer
    {
        private DriverMachineInterface driverMachineInterface;
        private bool Zoomed;
        protected Rectangle DrawPosition;
        private bool mouseRightButtonPressed;

        public DriverMachineInterfaceRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewScreenControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            Position.X = Control.Bounds.X;
            Position.Y = Control.Bounds.Y;
            if (Control.Bounds.Height == 102 && Control.Bounds.Width == 136)
            {
                // TODO Hack for ETR400 cab, which was built with a bugged size calculation of digital displays
                //Control.Bounds.Height *= 0.75f;
                //Control.Bounds.Width *= 0.75f;
            }
            driverMachineInterface = new DriverMachineInterface(Control.Bounds.Height, Control.Bounds.Width, locomotive, viewer, control);
            viewer.UserCommandController.AddEvent(CommonUserCommand.PointerPressed, MouseClickedEvent);
            viewer.UserCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedEvent);
            viewer.UserCommandController.AddEvent(CommonUserCommand.AlternatePointerPressed, MouseRightButtonPressed);
            viewer.UserCommandController.AddEvent(CommonUserCommand.AlternatePointerReleased, MouseRightButtonReleased);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (!IsPowered && Control.HideIfDisabled)
                return;

            base.PrepareFrame(frame, elapsedTime);
            var xScale = Viewer.CabWidthPixels / 640f;
            var yScale = Viewer.CabHeightPixels / 480f;
            DrawPosition.X = (int)(Position.X * xScale) - Viewer.CabXOffsetPixels + Viewer.CabXLetterboxPixels;
            DrawPosition.Y = (int)(Position.Y * yScale) + Viewer.CabYOffsetPixels + Viewer.CabYLetterboxPixels;
            DrawPosition.Width = (int)(Control.Bounds.Width * xScale);
            DrawPosition.Height = (int)(Control.Bounds.Height * yScale);
            if (Zoomed)
            {
                DrawPosition.Width = driverMachineInterface.Width;
                DrawPosition.Height = driverMachineInterface.Height;
                driverMachineInterface.SizeTo(DrawPosition.Width, DrawPosition.Height);
                DrawPosition.X -= driverMachineInterface.Width / 2;
                DrawPosition.Y -= driverMachineInterface.Height / 2;
                driverMachineInterface.ETCSDefaultWindow.BackgroundColor = ColorBackground;
            }
            else
            {
                driverMachineInterface.SizeTo(DrawPosition.Width, DrawPosition.Height);
                driverMachineInterface.ETCSDefaultWindow.BackgroundColor = Color.Transparent;
            }
            driverMachineInterface.PrepareFrame(elapsedTime.ClockSeconds);
        }

        public bool IsMouseWithin(Point mousePoint)
        {
            int x = (int)((mousePoint.X - DrawPosition.X) / driverMachineInterface.Scale);
            int y = (int)((mousePoint.Y - DrawPosition.Y) / driverMachineInterface.Scale);
            if (mouseRightButtonPressed && new Rectangle(0, 0, driverMachineInterface.Width, driverMachineInterface.Height).Contains(x, y))
                Zoomed = !Zoomed;
            foreach (var area in driverMachineInterface.ActiveWindow.SubAreas)
            {
                if (!(area is DMIButton))
                    continue;
                var b = (DMIButton)area;
                if (b.SensitiveArea(driverMachineInterface.ActiveWindow.Position).Contains(x, y) && b.Enabled)
                    return true;
            }
            return false;
        }

        public void HandleUserInput(GenericButtonEventType buttonEventType, Point position, Vector2 delta)
        {
            driverMachineInterface.HandleMouseInput(buttonEventType == GenericButtonEventType.Down, (int)((position.X - DrawPosition.X) / driverMachineInterface.Scale), (int)((position.Y - DrawPosition.Y) / driverMachineInterface.Scale));
        }

        public string GetControlName(Point mousePoint)
        {
            int x = (int)((mousePoint.X - DrawPosition.X) / driverMachineInterface.Scale);
            int y = (int)((mousePoint.Y - DrawPosition.Y) / driverMachineInterface.Scale);
            foreach (var area in driverMachineInterface.ActiveWindow.SubAreas)
            {
                if (!(area is DMIButton))
                    continue;
                var b = (DMIButton)area;
                if (b.SensitiveArea(driverMachineInterface.ActiveWindow.Position).Contains(x, y))
                    return b.DisplayName;
            }
            return "";
        }
        public string GetControlLabel(Point mousePoint)
        {
            return GetControlName(mousePoint);
        }

        public override void Draw()
        {
            driverMachineInterface.Draw(ControlView.SpriteBatch, new Point(DrawPosition.X, DrawPosition.Y));
            ControlView.SpriteBatch.End();
            ControlView.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, DepthStencilState.Default, null, Shader);
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

        private void MouseRightButtonPressed(UserCommandArgs userCommandArgs)
        {
            mouseRightButtonPressed = true;
        }

        private void MouseRightButtonReleased(UserCommandArgs userCommandArgs)
        {
            mouseRightButtonPressed = false;
        }
    }






}
