﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace MSTS
{

	// TODO - this is an incomplete parse of the cvf file.
	public class CVFFile
	{
        public List<Vector3> Locations = new List<Vector3>();   // Head locations for front, left and right views
        public List<Vector3> Directions = new List<Vector3>();  // Head directions for each view
        public List<string> TwoDViews = new List<string>();     // 2D CAB Views - by GeorgeS
        public List<string> NightViews = new List<string>();    // Night CAB Views - by GeorgeS
        public List<string> LightViews = new List<string>();    // Light CAB Views - by GeorgeS
        public CabViewControls CabViewControls = null;     // Controls in CAB - by GeorgeS

        public CVFFile(string filePath, string basePath)
		{
            using (STFReader stf = new STFReader(filePath, false))
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tr_cabviewfile", ()=>{ stf.MustMatch("("); stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("position", ()=>{ Locations.Add(stf.ReadVector3Block(STFReader.UNITS.None, new Vector3())); }),
                        new STFReader.TokenProcessor("direction", ()=>{ Directions.Add(stf.ReadVector3Block(STFReader.UNITS.None, new Vector3())); }),
                        new STFReader.TokenProcessor("cabviewfile", ()=>{
                            var fileName = stf.ReadStringBlock(null);
                            var path = Path.Combine(basePath, Path.GetDirectoryName(fileName));
                            var name = Path.GetFileName(fileName);

                            // Use *Frnt1024.ace if avalible
                            string s = name;
                            string[] nameParts = s.Split('.');
                            string name1024 = nameParts[0] + "1024." + nameParts[1];
                            var tstFileName1024 = Path.Combine(path, name1024);
                            if (File.Exists(tstFileName1024))
                                name = name1024;

                            TwoDViews.Add(Path.Combine(path, name));
                            NightViews.Add(Path.Combine(path, Path.Combine("NIGHT", name)));
                            LightViews.Add(Path.Combine(path, Path.Combine("CABLIGHT", name)));
                        }),
                        new STFReader.TokenProcessor("cabviewcontrols", ()=>{ CabViewControls = new CabViewControls(stf, basePath); }),
                    });}),
                });
		}

	} // class CVFFile

    public enum CABViewControlTypes
    {
        NONE,
        SPEEDOMETER,
        MAIN_RES,
        EQ_RES,
        BRAKE_CYL,
        BRAKE_PIPE,
        LINE_VOLTAGE,
        AMMETER,
        AMMETER_ABS,
        LOAD_METER,
        THROTTLE,
        PANTOGRAPH,
        TRAIN_BRAKE,
        FRICTION_BRAKE,
        ENGINE_BRAKE,
        DYNAMIC_BRAKE,
        DYNAMIC_BRAKE_DISPLAY,
        SANDERS,
        WIPERS,
        HORN,
        BELL,
        FRONT_HLIGHT,
        DIRECTION,
        ASPECT_DISPLAY,
        THROTTLE_DISPLAY,
        CPH_DISPLAY,
        PANTO_DISPLAY,
        DIRECTION_DISPLAY,
        CP_HANDLE,
        PANTOGRAPH2,
        CLOCK,
        SANDING,
        ALERTER_DISPLAY,
        TRACTION_BRAKING,
        ACCELEROMETER,
        WHEELSLIP,
        FRICTION_BRAKING,
        PENALTY_APP,
        EMERGENCY_BRAKE,
        RESET,
        CAB_RADIO,
        OVERSPEED,
        SPEEDLIM_DISPLAY,
        FUEL_GAUGE,
        WHISTLE,
        REGULATOR,
        CYL_COCKS,
        BLOWER,
        STEAM_INJ1,
        STEAM_INJ2,
        DAMPERS_FRONT,
        DAMPERS_BACK,
        WATER_INJECTOR1,
        WATER_INJECTOR2,
        SMALL_EJECTOR,
        STEAM_PR,
        STEAMCHEST_PR,
        TENDER_WATER,
        BOILER_WATER,
        REVERSER_PLATE,
        STEAMHEAT_PRESSURE,
        FIREBOX,
        RPM,
        FIREHOLE,
        CUTOFF,
        VACUUM_RESERVOIR_PRESSURE
    }

    public enum CABViewControlStyles
    {
        NONE,
        NEEDLE,
        POINTER,
        SOLID,
        LIQUID,
        SPRUNG,
        NOT_SPRUNG,
        WHILE_PRESSED,
        PRESSED,
        ONOFF, 
        _24HOUR, 
        _12HOUR
    }

    public enum CABViewControlUnits
    {
        NONE,
        BAR,
        PSI,
        KILOPASCALS,
        KGS_PER_SQUARE_CM,
        AMPS,
        VOLTS,
        KILOVOLTS,

        KM_PER_HOUR,
        MILES_PER_HOUR, 
        METRESµSECµSEC,
        METRES_SEC_SEC,
        KMµHOURµHOUR,
        KM_HOUR_HOUR,
        KMµHOURµSEC,
        KM_HOUR_SEC,
        METRESµSECµHOUR,
        METRES_SEC_HOUR,
        MILES_HOUR_MIN,
        MILES_HOUR_HOUR,

        NEWTONS, 
        KILO_NEWTONS,
        KILO_LBS,
        METRES_PER_SEC,
        LITRES,
        GALLONS,
        INCHES_OF_MERCURY,
        MILI_AMPS,
        RPM
    }

    public class CabViewControls : List<CabViewControl>
    {
        public CabViewControls(STFReader stf, string basepath)
        {
            stf.MustMatch("(");
            int count = stf.ReadInt(STFReader.UNITS.None, null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("dial", ()=>{ Add(new CVCDial(stf, basepath)); }),
                new STFReader.TokenProcessor("gauge", ()=>{ Add(new CVCGauge(stf, basepath)); }),
                new STFReader.TokenProcessor("lever", ()=>{ Add(new CVCDiscrete(stf, basepath)); }),
                new STFReader.TokenProcessor("twostate", ()=>{ Add(new CVCDiscrete(stf, basepath)); }),
                new STFReader.TokenProcessor("tristate", ()=>{ Add(new CVCDiscrete(stf, basepath)); }),
                new STFReader.TokenProcessor("multistatedisplay", ()=>{ Add(new CVCMultiStateDisplay(stf, basepath)); }),
                new STFReader.TokenProcessor("cabsignaldisplay", ()=>{ Add(new CVCSignal(stf, basepath)); }), 
                new STFReader.TokenProcessor("digital", ()=>{ Add(new CVCDigital(stf, basepath)); }), 
                new STFReader.TokenProcessor("combinedcontrol", ()=>{ Add(new CVCDiscrete(stf, basepath)); }),
                new STFReader.TokenProcessor("firebox", ()=>{ Add(new CVCDiscrete(stf, basepath)); }), 
                new STFReader.TokenProcessor("digitalclock", ()=>{ Add(new CVCDigitalClock(stf, basepath)); })
            });
            //TODO Uncomment when parsed all type
            /*
            if (count != this.Count) STFException.ReportWarning(inf, "CabViewControl count mismatch");
            */
        }
    }
    
    #region CabViewControl
    public class CabViewControl
    {
        public double PositionX = 0;
        public double PositionY = 0;
        public double Width = 0;
        public double Height = 0;

        public double MinValue = 0;
        public double MaxValue = 0;
        public double OldValue = 0;
        public string ACEFile = "";

        public CABViewControlTypes ControlType = CABViewControlTypes.NONE;
        public CABViewControlStyles ControlStyle = CABViewControlStyles.NONE;
        public CABViewControlUnits Units = CABViewControlUnits.NONE;

        protected void ParseType(STFReader stf)
        {
            stf.MustMatch("(");
            try
            {
                ControlType = (CABViewControlTypes)Enum.Parse(typeof(CABViewControlTypes), stf.ReadString());
            }
            catch(ArgumentException)
            {
                stf.StepBackOneItem();
                STFException.TraceInformation(stf, "Skipped unknown ControlType " + stf.ReadString());
                ControlType = CABViewControlTypes.NONE;
            }
            //stf.ReadItem(); // Skip repeated Class Type 
            stf.SkipRestOfBlock();
        }
        protected void ParsePosition(STFReader stf)
        {
            stf.MustMatch("(");
            PositionX = stf.ReadDouble( STFReader.UNITS.None, null );
            PositionY = stf.ReadDouble( STFReader.UNITS.None, null );
            Width = stf.ReadDouble( STFReader.UNITS.None, null );
            Height = stf.ReadDouble( STFReader.UNITS.None, null );

            // Handling middle values
            while (!stf.EndOfBlock())
            {
                STFException.TraceWarning(stf, "Ignored additional positional parameters");
                Width = Height;
                Height = stf.ReadInt(STFReader.UNITS.None, null);
            }
        }
        protected void ParseScaleRange(STFReader stf)
        {
            stf.MustMatch("(");
            MinValue = stf.ReadDouble(STFReader.UNITS.None, null);
            MaxValue = stf.ReadDouble(STFReader.UNITS.None, null);
            stf.SkipRestOfBlock();
        }
        protected void ParseGraphic(STFReader stf, string basepath)
        {
            ACEFile = Path.Combine(basepath, stf.ReadStringBlock(null));
        }
        protected void ParseStyle(STFReader stf)
        {
            stf.MustMatch("(");
            try
            {
                string sStyle = stf.ReadString();
                int checkNumeric = 0;
                if(int.TryParse(sStyle.Substring(0, 1), out checkNumeric) == true)
                {
                    sStyle = sStyle.Insert(0, "_");
                }
                ControlStyle = (CABViewControlStyles)Enum.Parse(typeof(CABViewControlStyles), sStyle);
            }
            catch (ArgumentException)
            {
                stf.StepBackOneItem();
                STFException.TraceInformation(stf, "Skipped unknown ControlStyle " + stf.ReadString());
                ControlStyle = CABViewControlStyles.NONE;
            }
            stf.SkipRestOfBlock();
        }
        protected void ParseUnits(STFReader stf)
        {
            stf.MustMatch("(");
            try
            {
                string sUnits = stf.ReadItem();
                // sUnits = sUnits.Replace('/', '?');
                sUnits = sUnits.Replace('/', '_');
                Units = (CABViewControlUnits)Enum.Parse(typeof(CABViewControlUnits), sUnits);
            }
            catch (ArgumentException)
            {
                stf.StepBackOneItem();
                STFException.TraceInformation(stf, "Skipped unknown ControlStyle " + stf.ReadItem());
                Units = CABViewControlUnits.NONE;
            }
            stf.SkipRestOfBlock();
        }
        // Used by subclasses CVCGauge and CVCDigital
        protected virtual color ParseControlColor( STFReader stf )
        {
            stf.MustMatch("(");
            color colour = new color { A = 1, R = stf.ReadInt(STFReader.UNITS.None, 0) / 255f, G = stf.ReadInt(STFReader.UNITS.None, 0) / 255f, B = stf.ReadInt(STFReader.UNITS.None, 0) / 255f };
            stf.SkipRestOfBlock();
            return colour;
        }
    }
    #endregion

    #region Dial controls
    public class CVCDial : CabViewControl
    {
        public float FromDegree = 0;
        public float ToDegree = 0;
        public float Center = 0;
        public int Direction = 0;
        
        public CVCDial(STFReader stf, string basepath)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basepath); }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),

                new STFReader.TokenProcessor("pivot", ()=>{ Center = stf.ReadFloatBlock(STFReader.UNITS.None, null); }),
                new STFReader.TokenProcessor("dirincrease", ()=>{ Direction = stf.ReadIntBlock(STFReader.UNITS.None, null); }),
                new STFReader.TokenProcessor("scalepos", ()=>{
                    stf.MustMatch("(");
                    FromDegree = stf.ReadFloat(STFReader.UNITS.None, null);
                    ToDegree = stf.ReadFloat(STFReader.UNITS.None, null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }
    #endregion

    #region Gauges
    public class CVCGauge : CabViewControl
    {
        public Rectangle Area = new Rectangle();
        public int ZeroPos = 0;
        public int Orientation = 0;
        public int Direction = 0;
        public color PositiveColor { get; set; }
        public color NegativeColor { get; set; }
        public color DecreaseColor { get; set; }

        public CVCGauge(STFReader stf, string basepath)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basepath); }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),

                new STFReader.TokenProcessor("zeropos", ()=>{ ZeroPos = stf.ReadIntBlock(STFReader.UNITS.None, null); }),
                new STFReader.TokenProcessor("orientation", ()=>{ Orientation = stf.ReadIntBlock(STFReader.UNITS.None, null); }),
                new STFReader.TokenProcessor("dirincrease", ()=>{ Direction = stf.ReadIntBlock(STFReader.UNITS.None, null); }),
                new STFReader.TokenProcessor("area", ()=>{ 
                    stf.MustMatch("(");
                    int x = stf.ReadInt(STFReader.UNITS.None, null);
                    int y = stf.ReadInt(STFReader.UNITS.None, null);
                    int width = stf.ReadInt(STFReader.UNITS.None, null);
                    int height = stf.ReadInt(STFReader.UNITS.None, null);
                    Area = new Rectangle(x, y, width, height);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("positivecolour", ()=>{ 
                    stf.MustMatch("(");
                    stf.ReadInt(STFReader.UNITS.None, 0);
                    if(stf.EndOfBlock() == false)
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ PositiveColor = ParseControlColor(stf); }) });
                    }
                }),
                new STFReader.TokenProcessor("negativecolour", ()=>{
                    stf.MustMatch("(");
                    stf.ReadInt(STFReader.UNITS.None, 0);
                    if(stf.EndOfBlock() == false)
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ NegativeColor = ParseControlColor(stf); }) });
                    }
                }),
                new STFReader.TokenProcessor("decreasecolour", ()=>{
                    stf.MustMatch("(");
                    stf.ReadInt(STFReader.UNITS.None, 0);
                    if(stf.EndOfBlock() == false)
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ DecreaseColor = ParseControlColor(stf); }) });
                    }
                })
            });
        }
    }
    #endregion

    #region Digital controls
    public class CVCDigital : CabViewControl
    {
        public int LeadingZeros { get; set; }
        public double Accuracy { get; set; }
        public double AccuracySwitch { get; set; }
        public int Justification { get; set; }
        public color PositiveColor { get; set; }
        public color NegativeColor { get; set; }
        public color DecreaseColor { get; set; }

        public CVCDigital()
        {
        }

        public CVCDigital(STFReader stf, string basepath)
        {
            // Set white as the default positive colour for digital displays
            color white = new color();
            white.R = 255f;
            white.G = 255f;
            white.B = 255f;
            PositiveColor = white;
            
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basepath); }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),
                new STFReader.TokenProcessor("leadingzeros", ()=>{ ParseLeadingZeros(stf); }),
                new STFReader.TokenProcessor("accuracy", ()=>{ ParseAccuracy(stf); }), 
                new STFReader.TokenProcessor("accuracyswitch", ()=>{ ParseAccuracySwitch(stf); }), 
                new STFReader.TokenProcessor("justification", ()=>{ ParseJustification(stf); }),
                new STFReader.TokenProcessor("positivecolour", ()=>{ 
                    stf.MustMatch("(");
                    stf.ReadInt(STFReader.UNITS.None, 0);
                    if(stf.EndOfBlock() == false)
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ PositiveColor = ParseControlColor(stf); }) });
                    }
                }),
                new STFReader.TokenProcessor("negativecolour", ()=>{
                    stf.MustMatch("(");
                    stf.ReadInt(STFReader.UNITS.None, 0);
                    if(stf.EndOfBlock() == false)
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ NegativeColor = ParseControlColor(stf); }) });
                    }
                }),
                new STFReader.TokenProcessor("decreasecolour", ()=>{
                    stf.MustMatch("(");
                    stf.ReadInt(STFReader.UNITS.None, 0);
                    if(stf.EndOfBlock() == false)
                    {
                        stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("controlcolour", ()=>{ DecreaseColor = ParseControlColor(stf); }) });
                    }
                })
            });
        }

        protected virtual void ParseLeadingZeros(STFReader stf)
        {
            stf.MustMatch("(");
            LeadingZeros = stf.ReadInt(STFReader.UNITS.None, 0);
            stf.SkipRestOfBlock();
        }

        protected virtual void ParseAccuracy(STFReader stf)
        {
            stf.MustMatch("(");
            Accuracy = stf.ReadDouble(STFReader.UNITS.None, 0);
            stf.SkipRestOfBlock();
        }

        protected virtual void ParseAccuracySwitch(STFReader stf)
        {
            stf.MustMatch("(");
            AccuracySwitch = stf.ReadDouble(STFReader.UNITS.None, 0);
            stf.SkipRestOfBlock();
        }

        protected virtual void ParseJustification(STFReader stf)
        {
            stf.MustMatch("(");
            Justification = stf.ReadInt(STFReader.UNITS.None, 3);
            stf.SkipRestOfBlock();
        }
    }

    public class CVCDigitalClock : CVCDigital
    {

        public CVCDigitalClock(STFReader stf, string basepath)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("accuracy", ()=>{ ParseAccuracy(stf); }), 
                new STFReader.TokenProcessor("controlcolour", ()=>{ PositiveColor = ParseControlColor(stf); })
            });
        }

        
    }
    #endregion

    #region Frames controls
    public abstract class CVCWithFrames : CabViewControl
    {
        private List<double> values = new List<double>();

        public int FramesCount { get; set; }
        public int FramesX { get; set; }
        public int FramesY { get; set; }

        public List<double> Values 
        {
            get
            {
                return values;
            }
        }
    }

    public class CVCDiscrete : CVCWithFrames
    {
        public List<int> Positions = new List<int>();

        private int _ValuesRead = 0;

        public CVCDiscrete(STFReader stf, string basepath)
        {
//            try
            {
                stf.MustMatch("(");
                stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                    new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                    new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                    new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basepath); }),
                    new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                    new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),

                    new STFReader.TokenProcessor("numframes", ()=>{
                        stf.MustMatch("(");
                        FramesCount = stf.ReadInt(STFReader.UNITS.None, null);
                        FramesX = stf.ReadInt(STFReader.UNITS.None, null);
                        FramesY = stf.ReadInt(STFReader.UNITS.None, null);
                        stf.SkipRestOfBlock();
                    }),
                    // <CJComment> Would like to revise this, as it is difficult to follow and debug.
                    // Can't do that until interaction of ScaleRange, NumFrames, NumPositions and NumValues is more fully specified.
                    // What is needed is samples of data that must be accommodated.
                    // Some decisions appear unwise but they might be a pragmatic solution to a real problem. </CJComment>
                    //
                    // Code accommodates:
                    // - NumValues before NumPositions or the other way round.
                    // - More NumValues than NumPositions and the other way round - perhaps unwise.
                    // - The count of NumFrames, NumValues and NumPositions is ignored - perhaps unwise.
                    // - Abbreviated definitions so that values at intermediate unspecified positions can be omitted.
                    //   Strangely, these values are set to 0 and worked out later when drawing.
                    // Max and min NumValues which don't match the ScaleRange are ignored - perhaps unwise.
                    new STFReader.TokenProcessor("numpositions", ()=>{
                        stf.MustMatch("(");
                        // If Positions are not filled before by Values
                        bool shouldFill = (Positions.Count == 0);
                        stf.ReadInt(STFReader.UNITS.None, null); // Number of Positions - Ignore it

                        var minPosition = 0;
                        var positionsRead = 0;
                        while (!stf.EndOfBlock())
                        {
                            int p = stf.ReadInt(STFReader.UNITS.None, null);

                            minPosition = positionsRead == 0 ? p : Math.Min(minPosition, p);  // used to get correct offset
                            positionsRead++;

                            // If Positions are not filled before by Values
                            if (shouldFill) Positions.Add(p);
                        }
                        
                            // If positions do not start at 0, add offset to shift them all so they do.
                            // An example of this is RENFE 400 (from http://www.trensim.com/lib/msts/index.php?act=view&id=186)
                            // which has a COMBINED_CONTROL with:
                            //   NumPositions ( 21 -11 -10 -9 -8 -7 -6 -5 -4 -3 -2 -1 0 1 2 3 4 5 6 7 8 9 )
                            // Also handles definitions with position in reverse order, e.g.
                            //   NumPositions ( 5 8 7 2 1 0 )
                            positionsRead++;

                        for (int iPos = 0; iPos <= Positions.Count - 1; iPos++)
                        {
                            Positions[iPos] -= minPosition;
                        }

                        // This is a hack for SLI locomotives which have the positions listed as "1056964608 0 0 0 ...".
                        if (Positions.Any(p => p > 0xFFFF))
                        {
                            STFException.TraceInformation(stf, "Renumbering cab control positions from zero due to value > 0xFFFF");
                            for (var i = 0; i < Positions.Count; i++)
                                Positions[i] = i;
                        }
                    }),
                    new STFReader.TokenProcessor("numvalues", ()=>{
                        stf.MustMatch("(");
                        stf.ReadDouble(STFReader.UNITS.None, null); // Number of Values - ignore it
                        while (!stf.EndOfBlock())
                        {
                            double v = stf.ReadDouble(STFReader.UNITS.None, null);
                            // If the Positions are less than expected add new Position(s)
                            while (Positions.Count <= _ValuesRead)
                            {
                                Positions.Add(_ValuesRead);
                            }
                            // Avoid later repositioning, put every value to its Position
                            // But before resize Values if needed
                            while (Values.Count <= Positions[_ValuesRead])
                            {
                                Values.Add(0);
                            }
                            // Avoid later repositioning, put every value to its Position
                            Values[Positions[_ValuesRead]] = v;
                            _ValuesRead++;
                        }
                    }),
                });

                // If no ACE, just don't need any fixup
                // Because Values are tied to the image Frame to be shown
                if (string.IsNullOrEmpty(ACEFile)) return;

                // Now, we have an ACE.

                // If read any Values, or the control requires Values to control
                //     The twostate, tristate, signal displays are not in these
                // Need check the Values collection for validity
                if (_ValuesRead > 0 || ControlStyle == CABViewControlStyles.SPRUNG || ControlStyle == CABViewControlStyles.NOT_SPRUNG)
                {
                    // Check max number of Frames
                    if (FramesCount == 0)
                    {
                        // Check valid Frame information
                        if (FramesX == 0 || FramesY == 0)
                        {
                            // Give up, it won't work
                            // Because later we won't know how to display frames from that
                            Trace.TraceWarning("Invalid Frames information given for ACE {0} in {1}", ACEFile, stf.FileName);
                            ACEFile = "";
                            return;
                        }

                        // Valid frames info, set FramesCount
                        FramesCount = FramesX * FramesY;
                    }

                    // Now we have an ACE and Frames for it.

                    // Fixup Positions and Values collections first

                    // If the read Positions and Values are not match
                    // Or we didn't read Values but have Frames to draw
                    // Do not test if FramesCount equals Values count, we trust in the creator -
                    //     maybe did not want to display all Frames
                    // (If there are more Values than Frames it will checked at draw time)
                    // Need to fix the whole Values
                    if (Positions.Count != _ValuesRead || (FramesCount > 0 && Values.Count == 0))
                    {
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
                        Values[0] = MinValue;

                        Values.Add(MaxValue);
                    }
                    // The Positions, Values are correct
                    else
                    {
                        // Check if read Values at all
                        if (Values.Count > 0)
                            // Set Min for sure
                            Values[0] = MinValue;
                        else
                            Values.Add(MinValue);

                        // Fill empty Values
                        for (int i = Values.Count; i < FramesCount; i++)
                            Values.Add(0);

                        // Add the maximums to the end, the Value will be removed
                        // We use Positions only here
                        Values.Add(MaxValue);
                        Positions.Add(FramesCount);
                    }

                    // OK, we have a valid size of Positions and Values

                    // Now it is the time for checking holes in the given data
                    if (Positions.Count < FramesCount - 1)
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
                    Values.RemoveAt(FramesCount);
                }
            }
//            catch (Exception error)
//            {
//                if (error is STFException) // Parsing error, so pass it on
//                    throw;
//                else                       // Unexpected error, so provide a hint
//                    throw new STFException(stf, "Problem with NumPositions/NumValues/NumFrames/ScaleRange");
//            } // End of Need check the Values collection for validity
        } // End of Constructor
    }
    #endregion

    #region Multistate Display Controls
    public class CVCMultiStateDisplay : CVCWithFrames
    {

        public CVCMultiStateDisplay(STFReader stf, string basepath)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("type", ()=>{ ParseType(stf); }),
                new STFReader.TokenProcessor("position", ()=>{ ParsePosition(stf);  }),
                new STFReader.TokenProcessor("scalerange", ()=>{ ParseScaleRange(stf); }),
                new STFReader.TokenProcessor("graphic", ()=>{ ParseGraphic(stf, basepath); }),
                new STFReader.TokenProcessor("style", ()=>{ ParseStyle(stf); }),
                new STFReader.TokenProcessor("units", ()=>{ ParseUnits(stf); }),

                new STFReader.TokenProcessor("states", ()=>{
                    stf.MustMatch("(");
                    FramesCount = stf.ReadInt(STFReader.UNITS.None, null);
                    FramesX = stf.ReadInt(STFReader.UNITS.None, null);
                    FramesY = stf.ReadInt(STFReader.UNITS.None, null);
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("state", ()=>{ stf.MustMatch("("); stf.ParseBlock( new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("switchval", ()=>{ Values.Add(stf.ReadDoubleBlock(STFReader.UNITS.None, null)); }),
                        });}),
                    });
                    if (Values.Count > 0) MaxValue = Values.Last();
                    for (int i = Values.Count; i < FramesCount; i++)
                        Values.Add(-10000);
                }),
            });
        }
    }
    #endregion

    #region other controls
    public class CVCSignal : CVCDiscrete
    {
        public CVCSignal(STFReader inf, string basepath)
            : base(inf, basepath)
        {
            FramesCount = 8;
            FramesX = 4;
            FramesY = 2;

            MinValue = 0;
            MaxValue = 1;

            Positions.Add(1);
            Values.Add(1);
        }
    }
    #endregion
} // namespace MSTS

