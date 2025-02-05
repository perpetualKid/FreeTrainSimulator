using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class ControlType
    {
        public CabViewControlType CabViewControlType { get; }
        public int Id { get; }

        public ControlType(CabViewControlType cabViewControlType, int subType)
        {
            CabViewControlType = cabViewControlType;
            Id = subType;
        }

        public ControlType(string name)
        {
            CabViewControlType = CabViewControlType.None;
            Id = 0;
            if (name != null && name.StartsWith("ORTS_TCS", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(name.AsSpan(8), out int subtype))
                {
                    CabViewControlType = CabViewControlType.Orts_TCS;
                    Id = subtype;
                }
            }
            else
                if (EnumExtension.GetValue(name, out CabViewControlType controlType))
                CabViewControlType = controlType;
        }

        public override string ToString()
        {
            return CabViewControlType == CabViewControlType.Orts_TCS ? $"{CabViewControlType}{Id}" : CabViewControlType.ToString();
        }
    }

    public class CabViewControls : List<CabViewControl>
    {
        internal CabViewControls(STFReader stf, string basePath)
        {
            stf.MustMatchBlockStart();
            int count = stf.ReadInt(null);

            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("dial", ()=>{ Add(new CabViewDialControl(stf, basePath)); }),
                new STFReader.TokenProcessor("gauge", ()=>{ Add(new CabViewGaugeControl(stf, basePath)); }),
                new STFReader.TokenProcessor("lever", ()=>{ Add(new CabViewDiscreteControl(stf, basePath, CabViewControlDiscreteState.Lever)); }),
                new STFReader.TokenProcessor("twostate", ()=>{ Add(new CabViewDiscreteControl(stf, basePath, CabViewControlDiscreteState.TwoState)); }),
                new STFReader.TokenProcessor("tristate", ()=>{ Add(new CabViewDiscreteControl(stf, basePath, CabViewControlDiscreteState.TriState)); }),
                new STFReader.TokenProcessor("multistate", ()=>{ Add(new CabViewDiscreteControl(stf, basePath, CabViewControlDiscreteState.MultiState)); }),
                new STFReader.TokenProcessor("multistatedisplay", ()=>{ Add(new CabViewMultiStateDisplayControl(stf, basePath)); }),
                new STFReader.TokenProcessor("cabsignaldisplay", ()=>{ Add(new CabViewSignalControl(stf, basePath, CabViewControlDiscreteState.CabSignalDisplay)); }),
                new STFReader.TokenProcessor("digital", ()=>{ Add(new CabViewDigitalControl(stf, basePath)); }),
                new STFReader.TokenProcessor("combinedcontrol", ()=>{ Add(new CabViewDiscreteControl(stf, basePath, CabViewControlDiscreteState.CombinedControl)); }),
                new STFReader.TokenProcessor("firebox", ()=>{ Add(new CabViewFireboxControl(stf, basePath)); }),
                new STFReader.TokenProcessor("dialclock", ()=>{ ProcessDialClock(stf, basePath);  }),
                new STFReader.TokenProcessor("digitalclock", ()=>{ Add(new CabViewDigitalClockControl(stf, basePath)); }),
                new STFReader.TokenProcessor("screendisplay", ()=>{ Add(new CabViewScreenControl(stf, basePath)); }),
                new STFReader.TokenProcessor("ortsanimateddisplay", ()=>{ Add(new CabViewAnimatedDisplayControl(stf, basePath)); })
            });

            if (count != Count)
                STFException.TraceInformation(stf, $"CabViewControl count mismatch  - expected {count} but found {Count} controls.");
        }

        private void ProcessDialClock(STFReader stf, string basePath)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[]
            {
                new STFReader.TokenProcessor("hours", ()=>{ Add(new CabViewDialControl(CabViewControlType.Orts_HourDial, 12, stf, basePath));  }),
                new STFReader.TokenProcessor("minutes", ()=>{ Add(new CabViewDialControl(CabViewControlType.Orts_MinuteDial, 60, stf, basePath));  }),
                new STFReader.TokenProcessor("seconds", ()=>{ Add(new CabViewDialControl(CabViewControlType.Orts_SecondDial, 60, stf, basePath));  }),
            });
        }
    }

    #region CabViewControl
    public abstract class CabViewControl
    {
        private protected Rectangle _bounds;
        public ref readonly Rectangle Bounds => ref _bounds;

        public float ScaleRangeMin { get; protected set; }
        public float ScaleRangeMax { get; protected set; } = 1.0f;
        public float PreviousValue { get; protected set; }

        public string AceFile { get; protected set; } = string.Empty;

        public string Label { get; protected set; } = string.Empty;
        public int ControlId { get; protected set; }
        public float Parameter1 { get; set; } // Generic parameter, individually interpreted by the controls using it
        public int Display { get; protected set; }
        public List<string> Screens { get; protected set; }
        public int CabViewpoint { get; protected set; }

        public ControlType ControlType { get; private protected set; }

        public CabViewControlStyle ControlStyle { get; protected set; }
        public CabViewControlUnit ControlUnit { get; protected set; }

        public bool DisabledIfLowVoltagePowerSupplyOff { get; private set; }
        public bool DisabledIfCabPowerSupplyOff { get; private set; }
        public bool HideIfDisabled { get; private set; } = true;
        public float? ValueIfDisabled { get; private set; }

        private protected void ParseType(STFReader stf)
        {
            stf.MustMatchBlockStart();

            string name = stf.ReadString();
            ControlType = new ControlType(name);
            if (ControlType.CabViewControlType == CabViewControlType.None)
            {
                STFException.TraceInformation(stf, "Skipped unknown ControlType " + name);
            }
            stf.SkipRestOfBlock();
        }

        private protected void ParsePosition(STFReader stf)
        {
            stf.MustMatchBlockStart();
            _bounds = new Rectangle(stf.ReadInt(null), stf.ReadInt(null), stf.ReadInt(null), stf.ReadInt(null));

            // skipping additional values in between
            while (!stf.EndOfBlock())
            {
                STFException.TraceWarning(stf, "Ignored additional positional parameters");
                _bounds.Width = _bounds.Height;
                _bounds.Height = stf.ReadInt(null);
            }
        }

        private protected void ParseScaleRange(STFReader stf)
        {
            stf.MustMatchBlockStart();
            ScaleRangeMin = stf.ReadFloat(null);
            ScaleRangeMax = stf.ReadFloat(null);
            stf.SkipRestOfBlock();
        }

        private protected void ParseGraphic(STFReader stf, string basePath)
        {
            AceFile = Path.Combine(basePath, stf.ReadStringBlock(null));
        }

        private protected void ParseStyle(STFReader stf)
        {
            stf.MustMatchBlockStart();
            string styleTemp = stf.ReadString();
            if (char.IsDigit(styleTemp[0]))
            {
                styleTemp = string.Concat(styleTemp.AsSpan(2), styleTemp.AsSpan(0, 2));
            }
            if (!EnumExtension.GetValue(styleTemp, out CabViewControlStyle style))
            {
                stf.StepBackOneItem();
                STFException.TraceInformation(stf, "Skipped unknown ControlStyle " + stf.ReadString());
                ControlStyle = CabViewControlStyle.None;
            }
            ControlStyle = style;
            stf.SkipRestOfBlock();
        }

        private protected void ParseUnits(STFReader stf)
        {
            stf.MustMatchBlockStart();
            string units = stf.ReadItem().Replace('/', '_');
            if (!EnumExtension.GetValue(units, out CabViewControlUnit unit))
            {
                stf.StepBackOneItem();
                STFException.TraceInformation(stf, "Skipped unknown ControlUnit " + stf.ReadItem());
                ControlUnit = CabViewControlUnit.None;
            }
            ControlUnit = unit;
            stf.SkipRestOfBlock();
        }

        private protected void ParseDisabledIfLowVoltagePowerSupplyOff(STFReader stf)
        {
            DisabledIfLowVoltagePowerSupplyOff = stf.ReadBoolBlock(false);
        }

        private protected void ParseDisabledIfCabPowerSupplyOff(STFReader stf)
        {
            DisabledIfCabPowerSupplyOff = stf.ReadBoolBlock(false);
        }

        private protected void ParseHideIfDisabled(STFReader stf)
        {
            HideIfDisabled = stf.ReadBoolBlock(true);
        }

        private protected void ParseValueIfDisabled(STFReader stf)
        {
            ValueIfDisabled = stf.ReadFloatBlock(STFReader.Units.None, 0f);
        }

        // Used by subclasses Gauge and Digital
        private protected virtual Color ParseControlColor(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Color colour = new Color(stf.ReadInt(0) / 255f, stf.ReadInt(0) / 255f, stf.ReadInt(0) / 255f, 1.0f);
            stf.SkipRestOfBlock();
            return colour;
        }

        private protected virtual (Color[] Colors, float TriggerValue) ParseControlColors(STFReader stf)
        {
            float trigger = 0f;
            List<Color> colors = new List<Color>(stf.ReadInt(0));
            if (!stf.EndOfBlock())
            {
                stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ colors.Add(ParseControlColor(stf));}),
                            new STFReader.TokenProcessor("switchval", () => { trigger = ParseSwitchVal(stf); }) });
            }
            else
            {
                colors.Add(default);
            }
            return (colors.ToArray(), trigger);
        }

        private protected virtual float ParseSwitchVal(STFReader stf)
        {
            stf.MustMatchBlockStart();
            float switchVal = stf.ReadFloat(0);
            stf.SkipRestOfBlock();
            return switchVal;
        }

        public override string ToString()
        {
            return ($"{Bounds.X},{Bounds.Y}\t{Bounds.Width}x{Bounds.Height}\t{ControlStyle}\t{ControlType}");
        }

        private protected virtual float ParseRotation(STFReader stf)
        {
            stf.MustMatch("(");
            float rotation = -MathHelper.ToRadians((float)stf.ReadDouble(0));
            stf.SkipRestOfBlock();
            return rotation;
        }

        private protected virtual void ParseDisplay(STFReader stf)
        {
            stf.MustMatch("(");
            Display = stf.ReadInt(0);
            stf.SkipRestOfBlock();
        }

        private protected virtual void ParseScreen(STFReader stf)
        {
            stf.MustMatch("(");
            var newScreen = stf.ReadString();
            stf.SkipRestOfBlock();
            Screens ??= new List<string>();
            Screens.Add(newScreen.ToLowerInvariant());
        }

        private protected virtual void ParseCabViewpoint(STFReader stf)
        {
            stf.MustMatch("(");
            CabViewpoint = stf.ReadInt(0);
            stf.SkipRestOfBlock();
        }
    }
    #endregion

    #region Dial controls
    public class CabViewDialControl : CabViewControl
    {
        public float StartAngle { get; private set; }
        public float EndAngle { get; private set; }
        public float Center { get; private set; }
        public Rotation Direction { get; private set; }

        // constructor for clock dials
        internal CabViewDialControl(CabViewControlType dialtype, int maxValue, STFReader stf, string basePath)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basePath); }),
                new STFReader.TokenProcessor("pivot", ()=>{ Center = stf.ReadFloatBlock(STFReader.Units.None, null); }),
                });
            ControlType = new ControlType(dialtype, 0);
            ControlStyle = CabViewControlStyle.Needle;
            Direction = 0;
            ScaleRangeMax = maxValue;
            ScaleRangeMin = 0;
            StartAngle = 181;
            EndAngle = 179;
        }

        // constructor for standard dials
        internal CabViewDialControl(STFReader stf, string basePath)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basePath); }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                new STFReader.TokenProcessor("disablediflowvoltagepowersupplyoff", ()=>{ ParseDisabledIfLowVoltagePowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("disabledifcabpowersupplyoff", ()=>{ ParseDisabledIfCabPowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("hideifdisabled", ()=>{ ParseHideIfDisabled(stf); }),
                new STFReader.TokenProcessor("valueifdisabled", ()=>{ ParseValueIfDisabled(stf); }),

                new STFReader.TokenProcessor("pivot", ()=>{ Center = stf.ReadFloatBlock(STFReader.Units.None, null); }),
                new STFReader.TokenProcessor("dirincrease", ()=>{ Direction = stf.ReadIntBlock(null) == 0 ? Rotation.Clockwise : Rotation.CounterClockwise; }),
                new STFReader.TokenProcessor("scalepos", ()=>{
                    stf.MustMatchBlockStart();
                    StartAngle = stf.ReadFloat(STFReader.Units.None, null);
                    EndAngle = stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("ortslabel", ()=>{
                    stf.MustMatch("(");
                    Label = stf.ReadString();
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("ortsdisplay", () => { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", ()=>{ ParseCabViewpoint(stf); }),
            });
        }
    }
    #endregion

    #region Gauges
    public class CabViewGaugeControl : CabViewControl
    {
        private protected Rectangle _area;

        public ref readonly Rectangle Area => ref _area;
        public int ZeroPos { get; private set; }
        public int Orientation { get; protected set; }
        public int Direction { get; protected set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public Color[] PositiveColors { get; private set; } = new Color[1];
        public float PositiveTrigger { get; private set; }
        public Color[] NegativeColors { get; private set; } = new Color[1];
#pragma warning restore CA1819 // Properties should not return arrays
        public float NegativeTrigger { get; private set; }
        public Color DecreaseColor { get; private set; }
        public float Rotation { get; private set; }

        public CabViewGaugeControl() { }

        internal CabViewGaugeControl(STFReader stf, string basePath)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basePath); }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                new STFReader.TokenProcessor("disablediflowvoltagepowersupplyoff", ()=>{ ParseDisabledIfLowVoltagePowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("disabledifcabpowersupplyoff", ()=>{ ParseDisabledIfCabPowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("hideifdisabled", ()=>{ ParseHideIfDisabled(stf); }),
                new STFReader.TokenProcessor("valueifdisabled", ()=>{ ParseValueIfDisabled(stf); }),

                new STFReader.TokenProcessor("zeropos", ()=>{ ZeroPos = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("orientation", ()=>{ Orientation = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("dirincrease", ()=>{ Direction = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("area", ()=>{
                    stf.MustMatchBlockStart();
                    _area = new Rectangle(stf.ReadInt(null), stf.ReadInt(null), stf.ReadInt(null), stf.ReadInt(null));
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("positivecolour", ()=>{
                    stf.MustMatchBlockStart();
                    (PositiveColors, PositiveTrigger) = ParseControlColors(stf);
                }),
                new STFReader.TokenProcessor("negativecolour", ()=>{
                    stf.MustMatchBlockStart();
                    (NegativeColors, NegativeTrigger) = ParseControlColors(stf);
                }),
                new STFReader.TokenProcessor("decreasecolour", ()=>{
                    stf.MustMatchBlockStart();
                    stf.ReadInt(0);
                    if(!stf.EndOfBlock())
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ DecreaseColor = ParseControlColor(stf); }) });
                    }
                }),
                new STFReader.TokenProcessor("ortsangle", () => { Rotation = ParseRotation(stf); }),
                new STFReader.TokenProcessor("ortslabel", ()=>{
                    stf.MustMatch("(");
                    Label = stf.ReadString();
                    stf.SkipRestOfBlock();
                }),                new STFReader.TokenProcessor("ortsdisplay", ()=> { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", ()=>{ ParseCabViewpoint(stf); }),

            });
        }
    }

    public class CabViewFireboxControl : CabViewGaugeControl
    {
        public string FireBoxAceFile { get; private set; }

        internal CabViewFireboxControl(STFReader stf, string basePath)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ FireBoxAceFile = Path.Combine(basePath, stf.ReadStringBlock(null)); }),
                new STFReader.TokenProcessor("fuelcoal", ()=>{ ParseGraphic(stf, basePath); }),
            });

            Direction = 1;
            Orientation = 1;
            ScaleRangeMax = 1;
            ScaleRangeMin = 0;
            ControlStyle = CabViewControlStyle.Pointer;
            _area = new Rectangle(Point.Zero, Bounds.Size);
            _bounds.Y += Bounds.Height / 2;
        }
    }
    #endregion

    #region Digital controls
    public class CabViewDigitalControl : CabViewControl
    {
        public int LeadingZeros { get; private set; }
        public float Accuracy { get; private set; }
        public float AccuracySwitch { get; private set; }
        public int Justification { get; private set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public Color[] PositiveColors { get; private set; } = new Color[1];
        public float PositiveTrigger { get; private set; }
        public Color[] NegativeColors { get; private set; } = new Color[1];
#pragma warning restore CA1819 // Properties should not return arrays
        public float NegativeTrigger { get; private set; }
        public Color DecreaseColor { get; private set; }
        public float FontSize { get; protected set; }
        public int FontStyle { get; protected set; }
        public string FontFamily { get; protected set; } = "";
        public float Rotation { get; protected set; }

        public CabViewDigitalControl()
        {
        }

        internal CabViewDigitalControl(STFReader stf, string basePath)
        {
            // Set white as the default positive colour for digital displays
            PositiveColors[0] = Color.White;
            FontSize = 8;
            FontStyle = 0;
            FontFamily = "Lucida Sans";

            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basePath); }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                new STFReader.TokenProcessor("disablediflowvoltagepowersupplyoff", ()=>{ ParseDisabledIfLowVoltagePowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("disabledifcabpowersupplyoff", ()=>{ ParseDisabledIfCabPowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("hideifdisabled", ()=>{ ParseHideIfDisabled(stf); }),
                new STFReader.TokenProcessor("valueifdisabled", ()=>{ ParseValueIfDisabled(stf); }),
                new STFReader.TokenProcessor("leadingzeros", ()=>{ ParseLeadingZeros(stf); }),
                new STFReader.TokenProcessor("accuracy", ()=>{ ParseAccuracy(stf); }),
                new STFReader.TokenProcessor("accuracyswitch", ()=>{ ParseAccuracySwitch(stf); }),
                new STFReader.TokenProcessor("justification", ()=>{ ParseJustification(stf); }),
                new STFReader.TokenProcessor("positivecolour", ()=>{
                    stf.MustMatchBlockStart();
                    (PositiveColors, PositiveTrigger) = ParseControlColors(stf);
                }),
                new STFReader.TokenProcessor("negativecolour", ()=>{
                    stf.MustMatchBlockStart();
                    (NegativeColors, NegativeTrigger) = ParseControlColors(stf);
                }),
                new STFReader.TokenProcessor("decreasecolour", ()=>{
                    stf.MustMatchBlockStart();
                    stf.ReadInt(0);
                    if(stf.EndOfBlock() == false)
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ DecreaseColor = ParseControlColor(stf); }) });
                    }
                }),
                new STFReader.TokenProcessor("ortsfont", ()=>{ParseFont(stf); }),
                new STFReader.TokenProcessor("ortsangle", () => { Rotation = ParseRotation(stf); }),
                new STFReader.TokenProcessor("ortslabel", ()=>{
                    stf.MustMatch("(");
                    Label = stf.ReadString();
                    stf.SkipRestOfBlock();
                }),                new STFReader.TokenProcessor("ortsdisplay", () => { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", ()=>{ ParseCabViewpoint(stf); }),
            });
        }

        private protected virtual void ParseLeadingZeros(STFReader stf)
        {
            stf.MustMatchBlockStart();
            LeadingZeros = stf.ReadInt(0);
            stf.SkipRestOfBlock();
        }

        private protected virtual void ParseAccuracy(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Accuracy = stf.ReadFloat(0);
            stf.SkipRestOfBlock();
        }

        private protected virtual void ParseAccuracySwitch(STFReader stf)
        {
            stf.MustMatchBlockStart();
            AccuracySwitch = stf.ReadFloat(0);
            stf.SkipRestOfBlock();
        }

        private protected virtual void ParseJustification(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Justification = stf.ReadInt(3);
            stf.SkipRestOfBlock();
        }

        private protected void ParseFont(STFReader stf)
        {
            stf.MustMatchBlockStart();
            FontSize = (float)stf.ReadFloat(10);
            FontStyle = stf.ReadInt(0);
            FontFamily = stf.ReadString() ?? string.Empty;
            stf.SkipRestOfBlock();
        }
    }

    public class CabViewDigitalClockControl : CabViewDigitalControl
    {

        internal CabViewDigitalClockControl(STFReader stf, string basePath)
        {
            _ = basePath;
            FontSize = 8;
            FontStyle = 0;
            FontFamily = "Lucida Sans";
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("disablediflowvoltagepowersupplyoff", ()=>{ ParseDisabledIfLowVoltagePowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("disabledifcabpowersupplyoff", ()=>{ ParseDisabledIfCabPowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("hideifdisabled", ()=>{ ParseHideIfDisabled(stf); }),
                new STFReader.TokenProcessor("valueifdisabled", ()=>{ ParseValueIfDisabled(stf); }),
                new STFReader.TokenProcessor("accuracy", ()=>{ ParseAccuracy(stf); }),
                new STFReader.TokenProcessor("controlcolour", ()=>{ PositiveColors[0] = ParseControlColor(stf); }),
                new STFReader.TokenProcessor("ortsfont", ()=>{ParseFont(stf); }),
                new STFReader.TokenProcessor("ortsangle", () => { Rotation = ParseRotation(stf); }),
                new STFReader.TokenProcessor("ortsdisplay", () => { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", ()=>{ ParseCabViewpoint(stf); }),
            });
        }
    }
    #endregion

    #region Frames controls
    public abstract class CabViewFramedControl : CabViewControl
    {
        public int FramesCount { get; protected set; }
        public int FramesX { get; protected set; }
        public int FramesY { get; protected set; }
        public bool MouseControl { get; protected set; }
        public int Orientation { get; protected set; }
        public int Direction { get; protected set; }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<float> Values { get; } = new List<float>();
#pragma warning restore CA1002 // Do not expose generic lists
    }

    public class CabViewDiscreteControl : CabViewFramedControl
    {
#pragma warning disable CA1002 // Do not expose generic lists
        public List<int> Positions { get; } = new List<int>();
#pragma warning restore CA1002 // Do not expose generic lists

        private int valuesRead;
        private int numPositions;
        private bool canFill = true;

#pragma warning disable CA1815 // Override equals and operator equals on value types
        public readonly struct NewScreenData
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public NewScreenData(string newScreen, int newScreenDisplay)
            {
                NewScreen = newScreen;
                NewScreenDisplay = newScreenDisplay;
            }

            public string NewScreen { get; }
            public int NewScreenDisplay { get; }
        }

        public List<NewScreenData> NewScreens { get; private set; }

        internal CabViewDiscreteControl(STFReader stf, string basePath, CabViewControlDiscreteState discreteState)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                    new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                    new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                    new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basePath); }),
                    new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                    new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                    new STFReader.TokenProcessor("disablediflowvoltagepowersupplyoff", ()=>{ ParseDisabledIfLowVoltagePowerSupplyOff(stf); }),
                    new STFReader.TokenProcessor("disabledifcabpowersupplyoff", ()=>{ ParseDisabledIfCabPowerSupplyOff(stf); }),
                    new STFReader.TokenProcessor("hideifdisabled", ()=>{ ParseHideIfDisabled(stf); }),
                    new STFReader.TokenProcessor("valueifdisabled", ()=>{ ParseValueIfDisabled(stf); }),
                    new STFReader.TokenProcessor("mousecontrol", ()=>{ MouseControl = stf.ReadBoolBlock(false); }),
                    new STFReader.TokenProcessor("orientation", ()=>{ Orientation = stf.ReadIntBlock(null); }),
                    new STFReader.TokenProcessor("dirincrease", ()=>{ Direction = stf.ReadIntBlock(null); }),

                    new STFReader.TokenProcessor("numframes", ()=>{
                        stf.MustMatchBlockStart();
                        FramesCount = stf.ReadInt(null);
                        FramesX = stf.ReadInt(null);
                        FramesY = stf.ReadInt(null);
                        stf.SkipRestOfBlock();
                    }),
                    // Code accommodates:
                    // - NumValues before NumPositions or the other way round.
                    // - More NumValues than NumPositions and the other way round - perhaps unwise.
                    // - The count of NumFrames, NumValues and NumPositions is ignored - perhaps unwise.
                    // - Abbreviated definitions so that values at intermediate unspecified positions can be omitted.
                    //   Strangely, these values are set to 0 and worked out later when drawing.
                    // Max and min NumValues which don't match the ScaleRange are ignored - perhaps unwise.

                    new STFReader.TokenProcessor("numpositions", ()=>{
                        stf.MustMatchBlockStart();
                        numPositions = stf.ReadInt(null); // Number of Positions

                        // only Add if this wasn't read before 
                        if (Positions.Count != 0)
                            stf.SkipRestOfBlock();
                        else
                        {
                            while (!stf.EndOfBlock())
                            {
                                Positions.Add(stf.ReadInt(null));
                            }
                        }
                        int minPosition = Positions.Count  > 0 ? Positions.Min() : 0;
                        if (minPosition < 0)
                        {
                            for (int i = 0; i< Positions.Count; i++)
                            {
                                Positions[i] -= minPosition;
                            }
                        }

                        // This is a hack for SLI locomotives which have the positions listed as "1056964608 0 0 0 ...".
                        if (Positions.Any(p => p > 0xFFFF))
                        {
                            STFException.TraceInformation(stf, "Renumbering cab control positions from zero due to value > 0xFFFF");
                            for (int i = 0; i < Positions.Count; i++)
                                Positions[i] = i;
                        }

                        // Check if eligible for filling
                        if (Positions.Count > 1 && Positions[0] != 0)
                            canFill = false;
                        else
                        {
                            for (int i = 1; i < Positions.Count; i++)
                            {
                                if (Positions[i] > Positions[i-1])
                                    continue;
                                canFill = false;
                                break;
                            }
                        }

                        // This is a protection against GP40 locomotives that erroneously have positions pointing beyond frame count limit.
                        if (canFill && Positions.Count > 1 && Positions.Count < FramesCount && Positions[Positions.Count-1] >= FramesCount && Positions[0] == 0)
                        {
                            STFException.TraceInformation(stf, "Some NumPositions entries refer to non-exisiting frames, trying to renumber");

                            Positions[Positions.Count - 1] = FramesCount - 1;
                            for (int i = Positions.Count - 2 ; i >= 1; i--)
                            {
                                if (Positions[i] >= Positions[i + 1])
                                    Positions[i] = Positions[i + 1] - 1;
                                else
                                    break;
                            }
                        }
                    }),
                    new STFReader.TokenProcessor("numvalues", ()=>{
                        stf.MustMatchBlockStart();
                        int numValues = stf.ReadInt(null); // Number of Values
                        while (!stf.EndOfBlock())
                        {
                            float v = stf.ReadFloat(null);
                            // If the Positions are less than expected add new Position(s)
                            while (Positions.Count <= valuesRead)
                            {
                                Positions.Add(valuesRead);
                            }
                            // Avoid later repositioning, put every value to its Position
                            // But before resize Values if needed
                            if (numValues != numPositions)
                            {
                                while (Values.Count <= Positions[valuesRead])
                                {
                                    Values.Add(0);
                                }
                                // Avoid later repositioning, put every value to its Position
                                Values[Positions[valuesRead]] = v;
                            }
                            Values.Add(v);
                            valuesRead++;
                        }
                    }),
                new STFReader.TokenProcessor("ortslabel", ()=>{
                    stf.MustMatch("(");
                    Label = stf.ReadString();
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("controlid", ()=> { ControlId = stf.ReadIntBlock(0); }),
                new STFReader.TokenProcessor("ortsdisplay", () => { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortsnewscreenpage", () => { ParseNewScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", ()=>{ParseCabViewpoint(stf); }),
                new STFReader.TokenProcessor("ortsparameter1", ()=>{ Parameter1 = stf.ReadFloatBlock(STFReader.Units.Any, 0); }),
                });

            // If no ACE, just don't need any fixup
            // Because Values are tied to the image Frame to be shown
            if (string.IsNullOrEmpty(AceFile))
                return;

            // Now, we have an ACE.

            // If read any Values, or the control requires Values to control
            //     The twostate, tristate, signal displays are not in these
            // Need check the Values collection for validity
            if (valuesRead > 0 || ControlStyle == CabViewControlStyle.Sprung || ControlStyle == CabViewControlStyle.Not_Sprung ||
                FramesCount > 0 || (FramesX > 0 && FramesY > 0))
            {
                // Check max number of Frames
                if (FramesCount == 0)
                {
                    // Check valid Frame information
                    if (FramesX == 0 || FramesY == 0)
                    {
                        // Give up, it won't work
                        // Because later we won't know how to display frames from that
                        Trace.TraceWarning("Invalid Frames information given for ACE {0} in {1}", AceFile, stf.FileName);
                        AceFile = "";
                        return;
                    }

                    // Valid frames info, set FramesCount
                    FramesCount = FramesX * FramesY;
                }

                // Now we have an ACE and Frames for it.
                // Only shuffle data in following cases
                if (Values.Count != Positions.Count || (Values.Count < FramesCount && canFill) ||
                    (Values.Count > 0 && Values[0] == Values[Values.Count - 1] && Values[0] == 0))
                {

                    // Fixup Positions and Values collections first

                    // If the read Positions and Values are not match
                    // Or we didn't read Values but have Frames to draw
                    // Do not test if FramesCount equals Values count, we trust in the creator -
                    //     maybe did not want to display all Frames
                    // (If there are more Values than Frames it will checked at draw time)
                    // Need to fix the whole Values
                    if (Positions.Count != valuesRead || (FramesCount > 0 && (Values.Count == 0 || Values.Count == 1)))
                    {
                        //This if clause covers among others following cases:
                        // Case 1 (e.g. engine brake lever of Dash 9):
                        //NumFrames ( 22 11 2 )
                        //NumPositions ( 1 0 )
                        //NumValues ( 1 0 )
                        //Orientation ( 1 )
                        //DirIncrease ( 1 )
                        //ScaleRange ( 0 1 )
                        //
                        // Case 2 (e.g. throttle lever of Acela):
                        //NumFrames ( 25 5 5 )
                        //NumPositions ( 0 )
                        //NumValues ( 0 )
                        //Orientation ( 1 )
                        //DirIncrease ( 1 )
                        //ScaleRange ( 0 1 )
                        //
                        // Clear existing
                        Positions.Clear();
                        Values.Clear();

                        // Add the two sure positions, the two ends
                        Positions.Add(0);
                        // We will need the FramesCount later!
                        // We use Positions only here
                        Positions.Add(FramesCount);

                        // Fill empty Values
                        for (int i = 0; i < FramesCount; i++)
                            Values.Add(0);
                        Values[0] = ScaleRangeMin;

                        Values.Add(ScaleRangeMax);
                    }
                    else if (Values.Count == 2 && Values[0] == 0 && Values[1] < ScaleRangeMax && Positions[0] == 0 &&
                        Positions[1] == 1 && Values.Count < FramesCount)
                    {
                        //This if clause covers among others following cases:
                        // Case 1 (e.g. engine brake lever of gp38):
                        //NumFrames ( 18 2 9 )
                        //NumPositions ( 2 0 1 )
                        //NumValues ( 2 0 0.3 )
                        //Orientation ( 0 )
                        //DirIncrease ( 0 )
                        //ScaleRange ( 0 1 )
                        Positions.Add(FramesCount);
                        // Fill empty Values
                        for (int i = Values.Count; i < FramesCount; i++)
                            Values.Add(Values[1]);
                        Values.Add(ScaleRangeMax);
                    }
                    else
                    {
                        //This if clause covers among others following cases:
                        // Case 1 (e.g. train brake lever of Acela): 
                        //NumFrames ( 12 4 3 )
                        //NumPositions ( 5 0 1 9 10 11 )
                        //NumValues ( 5 0 0.2 0.85 0.9 0.95 )
                        //Orientation ( 1 )
                        //DirIncrease ( 1 )
                        //ScaleRange ( 0 1 )
                        //
                        // Fill empty Values
                        int iValues = 1;
                        for (int i = 1; i < FramesCount && i <= Positions.Count - 1 && Values.Count < FramesCount; i++)
                        {
                            int deltaPos = Positions[i] - Positions[i - 1];
                            while (deltaPos > 1 && Values.Count < FramesCount)
                            {

                                Values.Insert(iValues, 0);
                                iValues++;
                                deltaPos--;
                            }
                            iValues++;
                        }

                        // Add the maximums to the end, the Value will be removed
                        // We use Positions only here
                        if (Values.Count > 0 && Values[0] <= Values[Values.Count - 1])
                            Values.Add(ScaleRangeMax);
                        else if (Values.Count > 0 && Values[0] > Values[Values.Count - 1])
                            Values.Add(ScaleRangeMin);
                    }

                    // OK, we have a valid size of Positions and Values

                    // Now it is the time for checking holes in the given data
                    if ((Positions.Count < FramesCount - 1 && Values[0] <= Values[Values.Count - 1]) ||
                        (Values.Count > 1 && Values[0] == Values[Values.Count - 2] && Values[0] == 0))
                    {
                        int j = 1;
                        int p = 0;
                        // Skip the 0 element, that is the default MinValue
                        for (int i = 1; i < Positions.Count; i++)
                        {
                            // Found a hole
                            if (Positions[i] != p + 1)
                            {
                                // Iterate to the next valid data and fill the hole
                                for (j = p + 1; j < Positions[i]; j++)
                                {
                                    // Extrapolate into the hole
                                    Values[j] = MathHelper.Lerp((float)Values[p], (float)Values[Positions[i]], (float)j / (float)Positions[i]);
                                }
                            }
                            p = Positions[i];
                        }
                    }

                    // Don't need the MaxValue added before, remove it
                    Values.RemoveAt(Values.Count - 1);
                }
            }

            // MSTS ignores/overrides various settings by the following exceptional cases:
            switch (ControlType.CabViewControlType)
            {
                case CabViewControlType.Cp_Handle:
                    ControlStyle = CabViewControlStyle.Not_Sprung;
                    break;
                case CabViewControlType.Pantograph:
                case CabViewControlType.Pantograph2:
                case CabViewControlType.Orts_Pantograph3:
                case CabViewControlType.Orts_Pantograph4:
                    ControlStyle = CabViewControlStyle.OnOff;
                    break;
                case CabViewControlType.Horn:
                case CabViewControlType.Sanders:
                case CabViewControlType.Bell:
                case CabViewControlType.Reset:
                case CabViewControlType.Vacuum_Exhauster:
                    ControlStyle = CabViewControlStyle.While_Pressed;
                    break;
                case CabViewControlType.Direction:
                    if (Orientation == 0)
                        Direction = 1 - Direction;
                    break;
            }

            switch (discreteState)
            {
                case CabViewControlDiscreteState.TriState:
                    ScaleRangeMax = 2.0f; // So that LocomotiveViewerExtensions.GetWebControlValueList() returns right value to web server
                    break;
                default:
                    break;
            }

        } // End of Constructor

        public void ResetScaleRange(float min, float max)
        {
            ScaleRangeMin = min;
            ScaleRangeMax = max;
        }

        protected void ParseNewScreen(STFReader stf)
        {
            stf.MustMatch("(");
            var newScreen = new NewScreenData(stf.ReadString().ToLowerInvariant(), stf.ReadInt(-1));
            stf.SkipRestOfBlock();
            if (NewScreens == null)
                NewScreens = new List<NewScreenData>();
            NewScreens.Add(newScreen);
        }

    }
    #endregion

    #region Multistate Display Controls
    public class CabViewMultiStateDisplayControl : CabViewFramedControl
    {
#pragma warning disable CA1002 // Do not expose generic lists
        public List<float> Styles { get; } = new List<float>();
#pragma warning restore CA1002 // Do not expose generic lists

        internal CabViewMultiStateDisplayControl(STFReader stf, string basePath)
        {

            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basePath); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                new STFReader.TokenProcessor("disablediflowvoltagepowersupplyoff", ()=>{ ParseDisabledIfLowVoltagePowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("disabledifcabpowersupplyoff", ()=>{ ParseDisabledIfCabPowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("hideifdisabled", ()=>{ ParseHideIfDisabled(stf); }),
                new STFReader.TokenProcessor("valueifdisabled", ()=>{ ParseValueIfDisabled(stf); }),

                new STFReader.TokenProcessor("states", ()=>{
                    stf.MustMatchBlockStart();
                    FramesCount = stf.ReadInt(null);
                    FramesX = stf.ReadInt(null);
                    FramesY = stf.ReadInt(null);
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("state", ()=>{
                            stf.MustMatchBlockStart();
                            stf.ParseBlock( new STFReader.TokenProcessor[] {
                                new STFReader.TokenProcessor("style", ()=>{ Styles.Add(ParseNumStyle(stf)); }),
                                new STFReader.TokenProcessor("switchval", ()=>{ Values.Add(ParseSwitchVal(stf)); }),    // stf.ReadFloatBlock(STFReader.Units.None, null)); }),
                            });
                        }),
                    });
                    if (Values.Count > 0)
                        ScaleRangeMax = Values.Last();
                    for (int i = Values.Count; i < FramesCount; i++)    //fill missing values with dummies
                        Values.Add(-10000);
                }),
                new STFReader.TokenProcessor("ortsdisplay", () => { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", ()=>{ ParseCabViewpoint(stf); }),
            });
        }

        private protected static int ParseNumStyle(STFReader stf)
        {
            stf.MustMatchBlockStart();
            int style = stf.ReadInt(0);
            stf.SkipRestOfBlock();
            return style;
        }
    }
    #endregion

    #region Screen based controls
    public class CabViewScreenControl : CabViewControl
    {
        public Dictionary<string, string> CustomParameters { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public CabViewScreenControl()
        {
        }

        internal CabViewScreenControl(STFReader stf, string basepath)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basepath); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                new STFReader.TokenProcessor("parameters", ()=>{ ParseCustomParameters(stf); }),
                new STFReader.TokenProcessor("disablediflowvoltagepowersupplyoff", ()=>{ ParseDisabledIfLowVoltagePowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("disabledifcabpowersupplyoff", ()=>{ ParseDisabledIfCabPowerSupplyOff(stf); }),
                new STFReader.TokenProcessor("hideifdisabled", ()=>{ ParseHideIfDisabled(stf); }),
                new STFReader.TokenProcessor("valueifdisabled", ()=>{ ParseValueIfDisabled(stf); }),
                new STFReader.TokenProcessor("ortsdisplay", () => { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", ()=>{ ParseCabViewpoint(stf); }),
            });
        }
        private protected void ParseCustomParameters(STFReader stf)
        {
            stf.MustMatch("(");
            while (!stf.EndOfBlock())
            {
                CustomParameters[stf.ReadString()] = stf.ReadString();
            }
        }
    }

    public class CabViewAnimatedDisplayControl : CabViewFramedControl
    {
#pragma warning disable CA1002 // Do not expose generic lists
        public List<double> MSStyles { get; } = new List<double>();
#pragma warning restore CA1002 // Do not expose generic lists
        public float CycleTimeS { get; private set; }

        internal CabViewAnimatedDisplayControl(STFReader stf, string basepath)
        {

            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basepath); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                new STFReader.TokenProcessor("ortscycletime", ()=>{
                    CycleTimeS = stf.ReadFloatBlock(STFReader.Units.Time, null); }),
                new STFReader.TokenProcessor("states", ()=>{
                    stf.MustMatch("(");
                    FramesCount = stf.ReadInt(null);
                    FramesX = stf.ReadInt(null);
                    FramesY = stf.ReadInt(null);
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("state", ()=>{
                            stf.MustMatch("(");
                            stf.ParseBlock( new STFReader.TokenProcessor[] {
                                new STFReader.TokenProcessor("style", ()=>{ MSStyles.Add(ParseNumStyle(stf));
                                }),
                                new STFReader.TokenProcessor("switchval", ()=>{ Values.Add(stf.ReadFloatBlock(STFReader.Units.None, null))
                                ; }),
                        });}),
                    });
                    if (Values.Count > 0) ScaleRangeMax = Values.Last();
                    for (int i = Values.Count; i < FramesCount; i++)
                        Values.Add(-10000);
                }),
                new STFReader.TokenProcessor("ortsdisplay", () => { ParseDisplay(stf); }),
                new STFReader.TokenProcessor("ortsscreenpage", () => { ParseScreen(stf); }),
                new STFReader.TokenProcessor("ortscabviewpoint", () =>{ ParseCabViewpoint(stf); }),
            });
        }
        private protected static int ParseNumStyle(STFReader stf)
        {
            stf.MustMatch("(");
            int style = stf.ReadInt(0);
            stf.SkipRestOfBlock();
            return style;
        }
    }
    #endregion

    #region other controls
    public class CabViewSignalControl : CabViewDiscreteControl
    {
        internal CabViewSignalControl(STFReader inf, string basePath, CabViewControlDiscreteState discreteState)
            : base(inf, basePath, discreteState)
        {
            FramesCount = 8;
            FramesX = 4;
            FramesY = 2;

            ScaleRangeMin = 0;
            ScaleRangeMax = 1;

            Positions.Add(1);
            Values.Add(1);
        }
    }
    #endregion
}
