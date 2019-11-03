using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts.Parsers;
using Tests.Orts.Shared;

namespace Tests.Orts.Formats.Msts.Parsers
{
    [TestClass]
    public class StfReaderIntegrationTests
    {
        [TestMethod]
        public void EncodingAscii()
        {
            var reader = new STFReader(new MemoryStream(Encoding.ASCII.GetBytes("TheBlock()")), "", Encoding.ASCII, false);
            reader.MustMatch("TheBlock");
        }

        [TestMethod]
        public void EncodingUtf16()
        {
            var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false);
            reader.MustMatch("TheBlock");
        }

        [TestMethod]
        public void EmptyFile()
        {
            AssertWarnings.Expected(); // many warnings will result!
            using (var reader = new STFReader(new MemoryStream(Encoding.ASCII.GetBytes("")), "EmptyFile.stf", Encoding.ASCII, false))
            {
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual("EmptyFile.stf", reader.FileName);
                Assert.AreEqual(1, reader.LineNumber);
                Assert.AreEqual(null, reader.SimisSignature);
                // Note, the Debug.Assert() in reader.Tree is already captured by AssertWarnings.Expected.
                // For the rest, we do not care which exception is being thrown.
                Assert.ThrowsException<AssertFailedException>(() => reader.Tree);

                // All of the following will execute successfully at EOF..., although they might give warnings.
                reader.MustMatch("ANYTHING GOES");
                reader.ParseBlock(new STFReader.TokenProcessor[0]);
                reader.ParseFile(new STFReader.TokenProcessor[0]);
                Assert.AreEqual(-1, reader.PeekPastWhitespace());
                Assert.AreEqual(false, reader.ReadBoolBlock(false));
                Assert.AreEqual(0, reader.ReadDouble(null));
                Assert.AreEqual(0, reader.ReadDoubleBlock(null));
                Assert.AreEqual(0, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual(0, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual(0U, reader.ReadHex(null));
                Assert.AreEqual(0U, reader.ReadHexBlock(null));
                Assert.AreEqual(0, reader.ReadInt(null));
                Assert.AreEqual(0, reader.ReadIntBlock(null));
                Assert.AreEqual("", reader.ReadItem());
                Assert.AreEqual("", reader.ReadString());
                Assert.AreEqual(null, reader.ReadStringBlock(null));
                Assert.AreEqual(0U, reader.ReadUInt(null));
                Assert.AreEqual(0U, reader.ReadUIntBlock(null));
                Assert.AreEqual(Vector3.Zero, reader.ReadVector3Block(STFReader.Units.None, Vector3.Zero));
                Assert.AreEqual(Vector4.Zero, reader.ReadVector4Block(STFReader.Units.None, Vector4.Zero));
            }
        }

        [TestMethod]
        public void EmptyBlock()
        {
            AssertWarnings.Expected();
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "EmptyBlock.stf", Encoding.Unicode, false))
            {
                Assert.IsFalse(reader.Eof, "STFReader.Eof");
                Assert.IsFalse(reader.EOF(), "STFReader.EOF()");
                Assert.IsFalse(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual("EmptyBlock.stf", reader.FileName);
                Assert.AreEqual(1, reader.LineNumber);
                Assert.AreEqual(null, reader.SimisSignature);
                Assert.ThrowsException<STFException>(() => reader.MustMatch("Something Else"));
                // We can't rewind the STFReader and it has advanced forward now. :(
            }
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false))
            {
                reader.MustMatch("TheBlock");
                Assert.IsFalse(reader.Eof, "STFReader.Eof");
                Assert.IsFalse(reader.EOF(), "STFReader.EOF()");
                Assert.IsFalse(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
                reader.VerifyStartOfBlock(); // Same as reader.MustMatch("(");
                Assert.IsFalse(reader.Eof, "STFReader.Eof");
                Assert.IsFalse(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
                reader.MustMatch(")");
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false))
            {
                var called = 0;
                var not_called = 0;
                reader.ParseBlock(new[] {
                    new STFReader.TokenProcessor("theblock", () => { called++; }),
                    new STFReader.TokenProcessor("TheBlock", () => { not_called++; })
                });
                Assert.IsTrue(called == 1, "TokenProcessor for theblock must be called exactly once: called = " + called);
                Assert.IsTrue(not_called == 0, "TokenProcessor for TheBlock must not be called: not_called = " + not_called);
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false))
            {
                reader.MustMatch("TheBlock");
                reader.SkipBlock();
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void NumericFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("1.123456789 1e9 1.1e9 2.123456 2e9 2.1e9 00ABCDEF 123456 -123456 234567")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual(1.123456789, reader.ReadDouble(null));
                Assert.AreEqual(1e9, reader.ReadDouble(null));
                Assert.AreEqual(1.1e9, reader.ReadDouble(null));
                Assert.AreEqual(2.123456f, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual(2e9, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual(2.1e9f, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual((uint)0xABCDEF, reader.ReadHex(null));
                Assert.AreEqual(123456, reader.ReadInt(null));
                Assert.AreEqual(-123456, reader.ReadInt(null));
                Assert.AreEqual(234567U, reader.ReadUInt(null));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void StringFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("Item1 \"Item2\" \"Item\" + \"3\" String1 \"String2\" \"String\" + \"3\"")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual("Item1", reader.ReadItem());
                Assert.AreEqual("Item2", reader.ReadItem());
                Assert.AreEqual("Item3", reader.ReadItem());
                Assert.AreEqual("String1", reader.ReadString());
                Assert.AreEqual("String2", reader.ReadString());
                Assert.AreEqual("String3", reader.ReadString());
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void BlockNumericFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("(true ignored) (false ignored) (1.123456789 ignored) (1e9 ignored) (1.1e9 ignored) (2.123456 ignored) (2e9 ignored) (2.1e9 ignored) (00ABCDEF ignored) (123456 ignored) (-123456 ignored) (234567 ignored)")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual(true, reader.ReadBoolBlock(false));
                Assert.AreEqual(false, reader.ReadBoolBlock(true));
                Assert.AreEqual(1.123456789, reader.ReadDoubleBlock(null));
                Assert.AreEqual(1e9, reader.ReadDoubleBlock(null));
                Assert.AreEqual(1.1e9, reader.ReadDoubleBlock(null));
                Assert.AreEqual(2.123456f, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual(2e9, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual(2.1e9f, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual((uint)0xABCDEF, reader.ReadHexBlock(null));
                Assert.AreEqual(123456, reader.ReadIntBlock(null));
                Assert.AreEqual(-123456, reader.ReadIntBlock(null));
                Assert.AreEqual(234567U, reader.ReadUIntBlock(null));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void BlockStringFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("(String1 ignored) (\"String2\" ignored) (\"String\" + \"3\" ignored)")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual("String1", reader.ReadStringBlock(null));
                Assert.AreEqual("String2", reader.ReadStringBlock(null));
                Assert.AreEqual("String3", reader.ReadStringBlock(null));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void BlockVectorFormats()
        {
            Vector2 vector2 = Vector2.Zero;
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("(1.1 1.2 ignored) (1.1 1.2 1.3 ignored) (1.1 1.2 1.3 1.4 ignored)")), "", Encoding.Unicode, false))
            {
                reader.ReadVector2Block(STFReader.Units.None, ref vector2);
                Assert.AreEqual(new Vector2(1.1f, 1.2f), vector2);
                Assert.AreEqual(new Vector3(1.1f, 1.2f, 1.3f), reader.ReadVector3Block(STFReader.Units.None, Vector3.Zero));
                Assert.AreEqual(new Vector4(1.1f, 1.2f, 1.3f, 1.4f), reader.ReadVector4Block(STFReader.Units.None, Vector4.Zero));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void Units()
        {
            AssertWarnings.NotExpected();
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("1.1 1.2 1.3km 1.4 1.5km")), "", Encoding.Unicode, false))
            {
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1.10000f, reader.ReadFloat(STFReader.Units.None, null)));
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1.20000f, reader.ReadFloat(STFReader.Units.Distance, null)));
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1300.00000f, reader.ReadFloat(STFReader.Units.Distance, null)));
                float result4 = 0;
                AssertWarnings.Matching("", () =>
                {
                    result4 = reader.ReadFloat(STFReader.Units.Distance | STFReader.Units.Compulsory, null);
                });
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1.40000f, result4));
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1500.00000f, reader.ReadFloat(STFReader.Units.Distance | STFReader.Units.Compulsory, null)));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
            }
        }

        /* All conversion factors have been sourced from:
        *   - https://en.wikipedia.org/wiki/Conversion_of_units
        *   - Windows 8.1 Calculator utility
        * 
        * In any case where there is disagreement or only 1 source available, the
        * chosen source is specified. In all other cases, all sources exist and agree.
        * 
        *********************************************************************
        * DO NOT CHANGE ANY OF THESE WITHOUT CONSULTING OTHER TEAM MEMBERS! *
        *********************************************************************/

        const double BarToPascal = 100000;
        const double CelsiusToKelvin = 273.15;
        const double DayToSecond = 86400;
        const double FahrenheitToKelvinA = 459.67;
        const double FahrenheitToKelvinB = 5 / 9;
        const double FeetToMetre = 0.3048;
        const double GallonUSToCubicMetre = 0.003785411784; // (fluid; Wine)
        const double HorsepowerToWatt = 745.69987158227022; // (imperial mechanical hoursepower)
        const double HourToSecond = 3600;
        const double InchOfMercuryToPascal = 3386.389; // (conventional)
        const double InchToMetre = 0.0254;
        const double KilometrePerHourToMetrePerSecond = 1 / 3.6;
        const double LitreToCubicMetre = 0.001;
        const double MilePerHourToMetrePerSecond = 0.44704;
        const double MileToMetre = 1609.344;
        const double MinuteToSecond = 60;
        const double PascalToPSI = 0.0001450377438972831; // Temporary while pressure values are returned in PSI instead of Pascal.
        const double PoundForceToNewton = 4.4482216152605; // Conversion_of_units
        const double PoundToKG = 0.453592926; //0.45359237;
        const double PSIToPascal = 6894.757;
        const double TonLongToKG = 1016.0469088;
        const double TonneToKG = 1000;
        const double TonShortToKG = 907.18474;

        static void UnitConversionTest(string input, double output, STFReader.Units unit)
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes(input)), "", Encoding.Unicode, false))
            {
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(output, reader.ReadFloat(unit, null)));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
            }
        }

        /* Base unit categories available in MSTS:
         *     (Constants)   pi
         *     Current       a ka ma
         *     Energy        j nm
         *     Force         n kn lbf
         *     Length        mm cm m km " in ' ft mil
         *     Mass          g kg t tn ton lb lbs
         *     Power         kw hp
         *     Pressure      pascal mbar bar psi
         *     Rotation      deg rad
         *     Temperature   k c f
         *     Time          us ms s min h d
         *     Velocity      kmh mph
         *     Voltage       v kv mv
         *     Volume        l gal
         */

        /* Base units for current available in MSTS:
         *     a         Amp
         *     ka        Kilo-amp
         *     ma        Mega-amp
         */

        [TestMethod]
        public void UnitConversionBaseMstsCurrent()
        {
            UnitConversionTest("1.2a", 1.2, STFReader.Units.Current);
            // TODO not implemented yet: UnitConversionTest("1.2ka", 1.2 * 1000, STFReader.Units.Current);
            // TODO not implemented yet: UnitConversionTest("1.2ma", 1.2 * 1000 * 1000, STFReader.Units.Current);
        }

        /* Base units for energy available in MSTS:
 *     j         Joule
 *     nm        Newton meter
 */
        [TestMethod]
        public void UnitConversionBaseMstsEnergy()
        {
            // TODO not implemented yet: UnitConversionTest("1.2j", 1.2, STFReader.Units.Energy);
            // TODO not implemented yet: UnitConversionTest("1.2nm", 1.2, STFReader.Units.Energy);
        }

        /* Base units for force available in MSTS:
         *     n         Newton
         *     kn        Kilo-newton
         *     lbf       Pound-force
         */
        [TestMethod]
        public void UnitConversionBaseMstsForce()
        {
            UnitConversionTest("1.2n", 1.2, STFReader.Units.Force);
            UnitConversionTest("1.2kn", 1.2 * 1000, STFReader.Units.Force);
            UnitConversionTest("1.2lbf", 1.2 * PoundForceToNewton, STFReader.Units.Force);
        }

        /* Base units for length available in MSTS:
         *     mm        Millimeter
         *     cm        Centimeter
         *     m         Meter
         *     km        Kilometer
         *     "         Inch
         *     in        Inch
         *     '         Foot
         *     ft        Foot
         *     mil       Mile
         */
        [TestMethod]
        public void UnitConversionBaseMstsLength()
        {
            UnitConversionTest("1.2mm", 1.2 / 1000, STFReader.Units.Distance);
            UnitConversionTest("1.2cm", 1.2 / 100, STFReader.Units.Distance);
            UnitConversionTest("1.2m", 1.2, STFReader.Units.Distance);
            UnitConversionTest("1.2km", 1.2 * 1000, STFReader.Units.Distance);
            // TODO not implemented yet: UnitConversionTest("1.2\"", 1.2 * InchToMetre, STFReader.Units.Distance);
            UnitConversionTest("1.2in", 1.2 * InchToMetre, STFReader.Units.Distance);
            // TODO not implemented yet: UnitConversionTest("1.2\'", 1.2 * FeetToMetre, STFReader.Units.Distance);
            UnitConversionTest("1.2ft", 1.2 * FeetToMetre, STFReader.Units.Distance);
            // TODO not implemented yet: UnitConversionTest("1.2mil", 1.2 * MileToMetre, STFReader.Units.Distance);
        }

        /* Base units for mass available in MSTS:
         *     g         Gram
         *     kg        Kilogram
         *     t         Tonne
         *     tn        Short/US ton
         *     ton       Long/UK ton
         *     lb        Pound
         *     lbs       Pound
         */
        [TestMethod]
        public void UnitConversionBaseMstsMass()
        {
            // TODO not implemented yet: UnitConversionTest("1.2g", 1.2 / 1000, STFReader.Units.Mass);
            UnitConversionTest("1.2kg", 1.2, STFReader.Units.Mass);
            UnitConversionTest("1.2t", 1.2 * TonneToKG, STFReader.Units.Mass);
            // TODO not implemented yet: UnitConversionTest("1.2tn", 1.2 * TonShortToKG, STFReader.Units.Mass);
            // TODO not implemented yet: UnitConversionTest("1.2ton", 1.2 * TonLongToKG, STFReader.Units.Mass);
            UnitConversionTest("1.2lb", 1.2 * PoundToKG, STFReader.Units.Mass);
            // TODO not implemented yet: UnitConversionTest("1.2lbs", 1.2 * PoundToKG, STFReader.Units.Mass);
        }

        /* Base units for power available in MSTS:
         *     kw        Kilowatt
         *     hp        Horsepower
         */
        [TestMethod]
        public void UnitConversionBaseMstsPower()
        {
            UnitConversionTest("1.2kw", 1.2 * 1000, STFReader.Units.Power);
            UnitConversionTest("1.2hp", 1.2 * HorsepowerToWatt, STFReader.Units.Power);
        }

        /* Base units for pressure available in MSTS:
         *     pascal    Pascal
         *     mbar      Millibar
         *     bar       Bar
         *     psi       psi
         */
        [TestMethod]
        public void UnitConversionBaseMstsPressure()
        {
            // TODO not implemented yet: UnitConversionTest("1.2pascal", 1.2, STFReader.Units.PressureDefaultInHg);
            // TODO not implemented yet: UnitConversionTest("1.2pascal", 1.2, STFReader.Units.PressureDefaultPSI);
            // TODO not implemented yet: UnitConversionTest("1.2mbar", 1.2 / 1000 * BarToPascal, STFReader.Units.PressureDefaultInHg);
            // TODO not implemented yet: UnitConversionTest("1.2mbar", 1.2 / 1000 * BarToPascal, STFReader.Units.PressureDefaultPSI);
            // TODO not using SI units: UnitConversionTest("1.2bar", 1.2 * BarToPascal, STFReader.Units.PressureDefaultInHg);
            // TODO not using SI units: UnitConversionTest("1.2bar", 1.2 * BarToPascal, STFReader.Units.PressureDefaultPSI);
            // TODO not using SI units: UnitConversionTest("1.2psi", 1.2 * PSIToPascal, STFReader.Units.PressureDefaultInHg);
            // TODO not using SI units: UnitConversionTest("1.2psi", 1.2 * PSIToPascal, STFReader.Units.PressureDefaultPSI);
        }

        /* Base units for rotation available in MSTS:
         *     deg       Degree
         *     rad       Radian
         */
        [TestMethod]
        public void UnitConversionBaseMstsRotation()
        {
            // TODO not implemented yet: UnitConversionTest("1.2deg", 1.2, STFReader.Units.Rotation);
            // TODO not implemented yet: UnitConversionTest("1.2rad", 1.2 * Math.PI / 180, STFReader.Units.Rotation);
        }

        /* Base units for temperature available in MSTS:
         *     k         Kelvin
         *     c         Celsius
         *     f         Fahrenheit
         */
        [TestMethod]
        public void UnitConversionBaseMstsTemperature()
        {
            // TODO not implemented yet: UnitConversionTest("1.2k", 1.2, STFReader.Units.Temperature);
            // TODO not implemented yet: UnitConversionTest("1.2c", 1.2 + CelsiusToKelvin, STFReader.Units.Temperature);
            // TODO not implemented yet: UnitConversionTest("1.2f", (1.2 + FahrenheitToKelvinA) * FahrenheitToKelvinB, STFReader.Units.Temperature);
        }

        /* Base units for time available in MSTS:
         *     us        Microsecond
         *     ms        Millisecond
         *     s         Second
         *     min       Minute
         *     h         Hour
         *     d         Day
         */
        [TestMethod]
        public void UnitConversionBaseMstsTime()
        {
            // TODO not implemented yet: UnitConversionTest("1.2us", 1.2 / 1000000, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2us", 1.2 / 1000000, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2us", 1.2 / 1000000, STFReader.Units.TimeDefaultH);
            // TODO not implemented yet: UnitConversionTest("1.2ms", 1.2 / 1000, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2ms", 1.2 / 1000, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2ms", 1.2 / 1000, STFReader.Units.TimeDefaultH);
            UnitConversionTest("1.2s", 1.2, STFReader.Units.Time);
            UnitConversionTest("1.2s", 1.2, STFReader.Units.TimeDefaultM);
            UnitConversionTest("1.2s", 1.2, STFReader.Units.TimeDefaultH);
            // TODO not implemented yet: UnitConversionTest("1.2min", 1.2 * MinuteToSecond, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2min", 1.2 * MinuteToSecond, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2min", 1.2 * MinuteToSecond, STFReader.Units.TimeDefaultH);
            UnitConversionTest("1.2h", 1.2 * HourToSecond, STFReader.Units.Time);
            UnitConversionTest("1.2h", 1.2 * HourToSecond, STFReader.Units.TimeDefaultM);
            UnitConversionTest("1.2h", 1.2 * HourToSecond, STFReader.Units.TimeDefaultH);
            // TODO not implemented yet: UnitConversionTest("1.2d", 1.2 * DayToSecond, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2d", 1.2 * DayToSecond, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2d", 1.2 * DayToSecond, STFReader.Units.TimeDefaultH);
        }

        /* Base units for velocity available in MSTS:
         *     kmh       Kilometers/hour
         *     mph       Miles/hour
         */
        [TestMethod]
        public void UnitConversionBaseMstsVelocity()
        {
            UnitConversionTest("1.2kmh", 1.2 * KilometrePerHourToMetrePerSecond, STFReader.Units.Speed);
            UnitConversionTest("1.2kmh", 1.2 * KilometrePerHourToMetrePerSecond, STFReader.Units.SpeedDefaultMPH);
            UnitConversionTest("1.2mph", 1.2 * MilePerHourToMetrePerSecond, STFReader.Units.Speed);
            UnitConversionTest("1.2mph", 1.2 * MilePerHourToMetrePerSecond, STFReader.Units.SpeedDefaultMPH);
        }

        /* Base units for voltage available in MSTS:
         *     v         Volt
         *     kv        Kilovolt
         *     mv        Megavolt
         */
        [TestMethod]
        public void UnitConversionBaseMstsVoltage()
        {
            UnitConversionTest("1.2v", 1.2, STFReader.Units.Voltage);
            UnitConversionTest("1.2kv", 1.2 * 1000, STFReader.Units.Voltage);
            // TODO not implemented yet: UnitConversionTest("1.2mv", 1.2 * 1000 * 1000, STFReader.Units.Voltage);
        }

        /* Base units for volume available in MSTS:
         *     l         Liter
         *     gal       Gallon (US)
         */
        [TestMethod]
        public void UnitConversionBaseMstsVolume()
        {
            // TODO not using SI units: UnitConversionTest("1.2l", 1.2 * LitreToCubicMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2l", 1.2 * LitreToCubicMetre, STFReader.Units.VolumeDefaultFT3);
            // TODO not using SI units: UnitConversionTest("1.2gal", 1.2 * GallonUSToCubicMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2gal", 1.2 * GallonUSToCubicMetre, STFReader.Units.VolumeDefaultFT3);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsArea()
        {
            // TODO not implemented yet: UnitConversionTest("1.2mm^2", 1.2 / 1000 / 1000, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2cm^2", 1.2 / 100 / 100, STFReader.Units.AreaDefaultFT2);
            UnitConversionTest("1.2m^2", 1.2, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2km^2", 1.2 * 1000 * 1000, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2\"^2", 1.2 * InchToMetre * InchToMetre, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2in^2", 1.2 * InchToMetre * InchToMetre, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2\'^2", 1.2 * FeetToMetre * FeetToMetre, STFReader.Units.AreaDefaultFT2);
            UnitConversionTest("1.2ft^2", 1.2 * FeetToMetre * FeetToMetre, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2mil^2", 1.2 * MileToMetre * MileToMetre, STFReader.Units.AreaDefaultFT2);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsDamping()
        {
            UnitConversionTest("1.2n/m/s", 1.2, STFReader.Units.Resistance);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsMassRate()
        {
            // TODO not using SI units: UnitConversionTest("1.2lb/h", 1.2 * PoundToKG / HourToSecond, STFReader.Units.MassRateDefaultLBpH);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsStiffness()
        {
            UnitConversionTest("1.2n/m", 1.2, STFReader.Units.Stiffness);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsVelocity()
        {
            UnitConversionTest("1.2m/s", 1.2, STFReader.Units.Speed);
            UnitConversionTest("1.2m/s", 1.2, STFReader.Units.SpeedDefaultMPH);
            UnitConversionTest("1.2km/h", 1.2 * 1000 / HourToSecond, STFReader.Units.Speed);
            UnitConversionTest("1.2km/h", 1.2 * 1000 / HourToSecond, STFReader.Units.SpeedDefaultMPH);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsVolume()
        {
            // TODO not implemented yet: UnitConversionTest("1.2mm^3", 1.2 * 1000 / 1000 / 1000, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2cm^3", 1.2 / 100 / 100 / 100, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2m^3", 1.2, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2km^3", 1.2 * 1000 * 1000 * 1000, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2\"^3", 1.2 * InchToMetre * InchToMetre * InchToMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2in^3", 1.2 * InchToMetre * InchToMetre * InchToMetre, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2\'^3", 1.2 * FeetToMetre * FeetToMetre * FeetToMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2ft^3", 1.2 * FeetToMetre * FeetToMetre * FeetToMetre, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2mil^3", 1.2 * MileToMetre * MileToMetre * MileToMetre, STFReader.Units.Volume);
        }

        /* Default unit for pressure in MSTS is UNKNOWN.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsPressure()
        {
            //UnitConversionTest("1.2", UNKNOWN, STFReader.Units.Pressure);
            UnitConversionTest("1.2", 1.2 * InchOfMercuryToPascal * PascalToPSI, STFReader.Units.PressureDefaultInHg);
            UnitConversionTest("1.2", 1.2, STFReader.Units.PressureDefaultPSI);
        }

        /* Default unit for time in MSTS is seconds.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsTime()
        {
            UnitConversionTest("1.2", 1.2, STFReader.Units.Time);
            UnitConversionTest("1.2", 1.2 * MinuteToSecond, STFReader.Units.TimeDefaultM);
            UnitConversionTest("1.2", 1.2 * HourToSecond, STFReader.Units.TimeDefaultH);
        }

        /* Default unit for velocity in MSTS is UNKNOWN.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsVelocity()
        {
            //UnitConversionTest("1.2", UNKNOWN, STFReader.Units.Speed);
            UnitConversionTest("1.2", 1.2 * MilePerHourToMetrePerSecond, STFReader.Units.SpeedDefaultMPH);
        }

        /* Default unit for volume in MSTS is UNKNOWN.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsVolume()
        {
            //UnitConversionTest("1.2", UNKNOWN, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2", 1.2 * FeetToMetre * FeetToMetre * FeetToMetre, STFReader.Units.VolumeDefaultFT3);
        }

        /* The following units are currently accepted by Open Rails but have no meaning in MSTS:
         *     amps
         *     btu/lb
         *     degc
         *     degf
         *     gals
         *     g-uk
         *     g-us
         *     hz
         *     inhg
         *     inhg/s
         *     kj/kg
         *     kmph
         *     kpa
         *     kpa/s
         *     kph
         *     ns/m
         *     rpm
         *     rps
         *     t-uk
         *     t-us
         *     w
         */

        [TestMethod]
        public void ParentheticalCommentsCanGoAnywhere()
        {
            // Also testing for bugs 1274713, 1221696, 1377393
            var part1 =
                "Wagon(\n" +
                "    Lights(\n" +
                "        Light( 1 )\n" +
                "";
            var part2 =
                "        Light( 2 )\n" +
                "";
            var part3 =
                "    )\n" +
                "    Sound( test.sms )\n" +
                ")";
            var middles = new[] {
                "        #(Comment)\n",
                "        # (Comment)\n",
                "        Skip( ** comment ** ) \n",
                "        Skip ( ** comment ** ) \n"
            };
            foreach (var middle in middles)
                ParenthicalCommentSingle(part1 + middle + part2 + part3);
            //foreach (var middle in middles)
            //    ParenthicalCommentSingle(part1 + part2 + middle + part3);
        }

        private void ParenthicalCommentSingle(string inputString)
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes(inputString)), "", Encoding.Unicode, true))
            {
                reader.ReadItem();
                Assert.AreEqual("wagon", reader.Tree.ToLower());
                reader.MustMatch("(");

                reader.ReadItem();
                Assert.AreEqual("wagon(lights", reader.Tree.ToLower());
                reader.MustMatch("(");

                reader.ReadItem();
                Assert.AreEqual("wagon(lights(light", reader.Tree.ToLower());
                reader.MustMatch("(");
                Assert.AreEqual(1, reader.ReadInt(null));
                reader.SkipRestOfBlock();
                Assert.AreEqual("wagon(lights()", reader.Tree.ToLower());

                reader.ReadItem();
                Assert.AreEqual("wagon(lights(light", reader.Tree.ToLower());
                reader.MustMatch("(");
                Assert.AreEqual(2, reader.ReadInt(null));
                reader.SkipRestOfBlock();
                Assert.AreEqual("wagon(lights()", reader.Tree.ToLower());

                reader.ReadItem();
                reader.ReadItem();
                Assert.AreEqual("wagon(sound", reader.Tree.ToLower());
                Assert.AreEqual("test.sms", reader.ReadStringBlock(""));
                reader.SkipRestOfBlock();

                Assert.IsTrue(reader.Eof, "STFReader.Eof");
            }
        }

    }

    [TestClass]
    public class StfExceptionTests
    {
        [TestMethod]
        public void ConstructorTest()
        {
            var reader = Create.Reader("sometoken");
            //Assert there is no exception thrown
        }

        /// <summary>
        /// Test that we can correctly assert a throw of an exception
        /// </summary>
        [TestMethod]
        public void BeThrowable()
        {
            string filename = "somefile";
            string message = "some message";
            AssertStfException.Throws(new STFException(new STFReader(new MemoryStream(), filename, Encoding.ASCII, true), message), message);
        }

    }

    #region Test utilities
    class Create
    {
        public static STFReader Reader(string source)
        {
            return Reader(source, "some.stf", false);
        }

        public static STFReader Reader(string source, string fileName)
        {
            return Reader(source, fileName, false);
        }

        public static STFReader Reader(string source, bool useTree)
        {
            return Reader(source, "some.stf", useTree);
        }

        public static STFReader Reader(string source, string fileName, bool useTree)
        {
            return new STFReader(new MemoryStream(Encoding.ASCII.GetBytes(source)), fileName, Encoding.ASCII, useTree);
        }
    }

    struct TokenTester
    {
        public string InputString;
        public string[] ExpectedTokens;
        public int[] ExpectedLineNumbers;

        public TokenTester(string input, string[] output)
        {
            InputString = input;
            ExpectedTokens = output;
            ExpectedLineNumbers = new int[0];
        }

        public TokenTester(string input, int[] lineNumbers)
        {
            InputString = input;
            ExpectedTokens = new string[0];
            ExpectedLineNumbers = lineNumbers;
        }
    }
    #endregion

    /// <summary>
    /// Class to help assert not only the type of exception being thrown, but also the message being generated
    /// </summary>
    static class AssertStfException
    {
        /// <summary>
        /// Run the testcode, make sure an exception is called and test the exception
        /// </summary>
        /// <param name="testCode">Code that will be executed</param>
        /// <param name="pattern">The pattern that the exception message should match</param>
        public static void Throws(STFException exception, string pattern)
        {
            Assert.IsNotNull(exception);
            Assert.IsTrue(Regex.IsMatch(exception.Message, pattern), exception.Message + " does not match pattern: " + pattern);
        }
    }

    #region Common test utilities
    class StfTokenReaderCommon
    {
        //#region Value itself
        //public static void OnNoValueWarnAndReturnDefault<T, nullableT>
        //    (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = ")";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Cannot parse|expecting.*found.*[)]", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(niceDefault, result);
        //    Assert.Equal(")", reader.ReadItem());

        //    reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("expecting.*found.*[)]", () => { result = codeDoingReading(reader, someDefault); });
        //    Assert.Equal(resultDefault, result);
        //    Assert.Equal(")", reader.ReadItem());
        //}

        //public static void ReturnValue<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    ReturnValue<T>(testValues, inputValues, codeDoingReading);
        //}

        //public static void ReturnValue<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string inputString = String.Join(" ", inputValues);
        //    var reader = Create.Reader(inputString);
        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        //public static void WithCommaReturnValue<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    WithCommaReturnValue(testValues, inputValues, codeDoingReading);
        //}

        //public static void WithCommaReturnValue<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string inputString = String.Join(" ", inputValues);
        //    var reader = Create.Reader(inputString);
        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        //public static void ForEmptyStringReturnNiceDefault<T, nullableT>
        //    (T niceDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] tokenValues = new string[] { "", "" };
        //    string emptyQuotedString = "\"\"";
        //    string inputString = emptyQuotedString + " " + emptyQuotedString;
        //    var reader = Create.Reader(inputString);

        //    T result = codeDoingReading(reader, default(nullableT));
        //    Assert.Equal(niceDefault, result);

        //    result = codeDoingReading(reader, someDefault);
        //    Assert.Equal(niceDefault, result);
        //}

        //public static void ForNonNumbersReturnNiceDefaultAndWarn<T, nullableT>
        //    (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] testValues = new string[] { "noint", "sometoken", "(" };
        //    string inputString = String.Join(" ", testValues);

        //    var reader = Create.Reader(inputString);
        //    foreach (string testValue in testValues)
        //    {
        //        T result = default(T);
        //        AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, default(nullableT)); });
        //        Assert.Equal(niceDefault, result);
        //    }

        //    reader = Create.Reader(inputString);
        //    foreach (string testValue in testValues)
        //    {
        //        T result = default(T);
        //        AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, someDefault); });
        //        Assert.Equal(resultDefault, result);

        //    }
        //}
        //#endregion

        #region Value in blocks
        public static void OnEofWarnAndReturnDefault<T, nullableT>
            (T resultDefault, ref nullableT someValue, ReadValueCodeByRef<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader(string.Empty);
//            AssertWarnings.Matching("Unexpected end of file", () => codeDoingReading(reader, ref someValue));
            //            AssertWarnings.Matching("Unexpected end of file", () => codeDoingReading(reader, ref someValue) );
            //Assert.Equal(default(T), result);
            //AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, ref someDefault); });
            Assert.AreEqual(resultDefault, someValue);
        }

        //public static void OnEofWarnAndReturnDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = "";
        //    var reader = Create.Reader(inputString);
        //    T result = default(T);
        //    AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(default(T), result);
        //    AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, someDefault); });
        //    Assert.Equal(resultDefault, result);
        //}

        //public static void ForNoOpenWarnAndReturnDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = "noblock";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(default(T), result);

        //    reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, someDefault); });
        //    Assert.Equal(resultDefault, result);
        //}

        //public static void OnBlockEndReturnDefaultOrWarn<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    OnBlockEndReturnNiceDefaultAndWarn(resultDefault, someDefault, codeDoingReading);
        //    OnBlockEndReturnGivenDefault(resultDefault, someDefault, codeDoingReading);
        //}

        //public static void OnBlockEndReturnNiceDefaultAndWarn<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = ")";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(default(T), result);
        //}

        //public static void OnBlockEndReturnGivenDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = ")";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    result = codeDoingReading(reader, someDefault);
        //    Assert.Equal(resultDefault, result);
        //    Assert.Equal(")", reader.ReadItem());
        //}

        //public static void OnEmptyBlockEndReturnGivenDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = "()a";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    result = codeDoingReading(reader, someDefault);
        //    Assert.Equal(resultDefault, result);
        //    Assert.Equal("a", reader.ReadItem());
        //}

        //public static void ReturnValueInBlock<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    ReturnValueInBlock<T>(testValues, inputValues, codeDoingReading);
        //}

        //public static void ReturnValueInBlock<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] tokenValues = inputValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0})", value)).ToArray();
        //    string inputString = String.Join(" ", tokenValues);
        //    var reader = Create.Reader(inputString);

        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        //public static void ReturnValueInBlockAndSkipRestOfBlock<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    ReturnValueInBlockAndSkipRestOfBlock<T>(testValues, inputValues, codeDoingReading);
        //}

        //public static void ReturnValueInBlockAndSkipRestOfBlock<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] tokenValues = inputValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0} dummy_token(nested_token))", value)
        //        ).ToArray();
        //    string inputString = String.Join(" ", tokenValues);
        //    var reader = Create.Reader(inputString);

        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        #endregion

        public delegate T ReadValueCode<T, nullableT>(STFReader reader, nullableT defaultValue);
        public delegate T ReadValueCode<T>(STFReader reader);
        public delegate void ReadValueCodeByRef<T, nullableT>(STFReader reader, ref nullableT defaultResult);

    }
    #endregion

    #region Vector2
    [TestClass]
    public class ReadingVector2BlockTests
    {
        static readonly Vector2 SOMEDEFAULT = new Vector2(1.1f, 1.2f);
        static readonly Vector2[] SOMEDEFAULTS = new Vector2[] { new Vector2(1.3f, 1.5f), new Vector2(-2f, 1e6f) };
        static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 ignore", "-2 1000000" };

        [TestMethod]
        public void OnEofWarnAndReturnDefault()
        {
            Vector2 defaultResult = SOMEDEFAULT;
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<Vector2, Vector2>
                (SOMEDEFAULT, ref defaultResult, (STFReader reader, ref Vector2 x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
        }

        //[Fact]
        //public static void ForNoOpenReturnDefaultAndWarn()
        //{
        //    StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<Vector2, Vector2>
        //        (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
        //}

        //[Fact]
        //public static void OnBlockEndReturnDefaultWhenGiven()
        //{
        //    StfTokenReaderCommon.OnBlockEndReturnGivenDefault<Vector2, Vector2>
        //        (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
        //}

        //[Fact]
        //public static void ReturnValueInBlock()
        //{
        //    Vector2 zero = Vector2.Zero;
        //    StfTokenReaderCommon.ReturnValueInBlock<Vector2>
        //        (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector2Block(STFReader.Units.None, ref zero));
        //}

        //[Fact]
        //public static void ReturnValueInBlockAndSkipRestOfBlock()
        //{
        //    Vector2 zero = Vector2.Zero;
        //    StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<Vector2>
        //        (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector2Block(STFReader.Units.None, ref zero));
        //}
    }
    #endregion

}
