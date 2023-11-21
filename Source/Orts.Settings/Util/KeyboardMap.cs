using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;

namespace Orts.Settings.Util
{
    /// <summary>
    /// Extension class for Input settings to dump keyboard mappings to screen or file
    /// </summary>
    public static class KeyboardMap
    {
        private static readonly int[] excludedFunctionKeys = new[] { 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44, 0x57, 0x58 };

        // Keyboard scancodes are basically constant; some keyboards have extra buttons (e.g. UK ones tend to have an
        // extra button next to Left Shift) or move one or two around (e.g. UK ones tend to move 0x2B down one row)
        // but generally this layout is right. Numeric keypad omitted as most keys are just duplicates of the main
        // keys (in two sets, based on Num Lock) and we don't use them. Scancodes are in hex.
        //
        // Break/Pause (0x11D) is handled specially and doesn't use the expect 0x45 scancode.
        //
        public static readonly string[] KeyboardLayout = new[] {
            "[01 ]   [3B ][3C ][3D ][3E ]   [3F ][40 ][41 ][42 ]   [43 ][44 ][57 ][58 ]   [37 ][46 ][11D]",
            "                                                                                            ",
            "[29 ][02 ][03 ][04 ][05 ][06 ][07 ][08 ][09 ][0A ][0B ][0C ][0D ][0E     ]   [52 ][47 ][49 ]",
            "[0F   ][10 ][11 ][12 ][13 ][14 ][15 ][16 ][17 ][18 ][19 ][1A ][1B ][2B   ]   [53 ][4F ][51 ]",
            "[3A     ][1E ][1F ][20 ][21 ][22 ][23 ][24 ][25 ][26 ][27 ][28 ][1C      ]                  ",
            "[2A       ][2C ][2D ][2E ][2F ][30 ][31 ][32 ][33 ][34 ][35 ][36         ]        [48 ]     ",
            "[1D   ][    ][38  ][39                          ][    ][    ][    ][1D   ]   [4B ][50 ][4D ]",
        };

        public static int MapWidth => KeyboardLayout[0].Length;

        // These should be placed in order of priority - the first found match is used.
        private static readonly List<KeyValuePair<string, Color>> prefixesToColors = new List<KeyValuePair<string, Color>>()
        {
            new KeyValuePair<string, Color>("ControlReverser", Color.DarkGreen),
            new KeyValuePair<string, Color>("ControlThrottle", Color.DarkGreen),
            new KeyValuePair<string, Color>("ControlTrainBrake", Color.DarkRed),
            new KeyValuePair<string, Color>("ControlEngineBrake", Color.DarkRed),
            new KeyValuePair<string, Color>("ControlBrakemanBrake", Color.DarkRed),
            new KeyValuePair<string, Color>("ControlDynamicBrake", Color.DarkRed),
            new KeyValuePair<string, Color>("ControlBrakeHose", Color.DarkRed),
            new KeyValuePair<string, Color>("ControlEmergency", Color.DarkRed),
            new KeyValuePair<string, Color>("ControlBailOff", Color.DarkRed),
            new KeyValuePair<string, Color>("ControlInitializeBrakes", Color.DarkRed),
            new KeyValuePair<string, Color>("Control", Color.DarkBlue),
            new KeyValuePair<string, Color>("Camera", Color.Orange),
            new KeyValuePair<string, Color>("Display", Color.DarkGoldenrod),
            //new KeyValuePair<string, Color>("Game", Color.Blue),
            new KeyValuePair<string, Color>("", Color.Gray),
        };

        public static void DrawKeyboardMap(Action<Rectangle, int, string> drawKey)
        {
            for (int y = 0; y < KeyboardLayout.Length; y++)
            {
                string keyboardLine = KeyboardLayout[y];

                int x = keyboardLine.IndexOf('[', StringComparison.Ordinal);
                while (x != -1)
                {
                    int x2 = keyboardLine.IndexOf("]", x, StringComparison.Ordinal);

                    ReadOnlySpan<char> scanCodeString = keyboardLine.AsSpan()[(x + 1)..(x + 4)].Trim();
                    if (!int.TryParse(scanCodeString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int keyScanCode))
                        keyScanCode = 0;

                    string keyName = ScanCodeKeyUtils.GetScanCodeKeyName(keyScanCode);
                    // Only allow F-keys to show >1 character names. The rest we'll remove for now.
                    if ((keyName.Length > 1) && !excludedFunctionKeys.Contains(keyScanCode))
                        keyName = "";

                    drawKey?.Invoke(new Rectangle(x, y, x2 - x + 1, 1), keyScanCode, keyName);

                    x = keyboardLine.IndexOf('[', x2);
                }
            }
        }

        public static Color GetScanCodeColor(IEnumerable<UserCommand> commands)
        {
            IEnumerable<string> commandNames = commands.Select(c => c.ToString());
            foreach (KeyValuePair<string, Color> prefixToColor in prefixesToColors)
                foreach (string commandName in commandNames)
                    if (commandName.StartsWith(prefixToColor.Key, StringComparison.OrdinalIgnoreCase))
                        return prefixToColor.Value;

            return Color.Transparent;
        }

        public static void DumpToText(this InputSettings input, string filePath)
        {
            ArgumentNullException.ThrowIfNull(input);

            using (StreamWriter writer = new StreamWriter(File.OpenWrite(filePath)))
            {
                writer.WriteLine("{0,-40}{1,-40}{2}", "Command", "Key", "Unique Inputs");
                writer.WriteLine(new string('=', 40 * 3));
                foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                    writer.WriteLine("{0,-40}{1,-40}{2}", command.GetLocalizedDescription(), input.UserCommands[command], string.Join(", ", input.UserCommands[command].GetUniqueInputs().OrderBy(s => s).ToArray()));
            }
        }

        public static void DumpToGraphic(this InputSettings input, string filePath)
        {
            ArgumentNullException.ThrowIfNull(input);

            int keyWidth = 50;
            int keyHeight = 4 * keyWidth;
            int keySpacing = 5;
            System.Drawing.Font keyFontLabel = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, keyHeight * 0.33f, System.Drawing.GraphicsUnit.Pixel);
            System.Drawing.Font keyFontCommand = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, keyHeight * 0.22f, System.Drawing.GraphicsUnit.Pixel);
            System.Drawing.Bitmap keyboardLayoutBitmap = new System.Drawing.Bitmap(KeyboardLayout[0].Length * keyWidth, KeyboardLayout.Length * keyHeight);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(keyboardLayoutBitmap))
            {
                DrawKeyboardMap((keyBox, keyScanCode, keyName) =>
                {
                    IEnumerable<UserCommand> keyCommands = GetScanCodeCommands(keyScanCode, input.UserCommands);
                    string keyCommandNames = string.Join("\n", keyCommands.Select(c => string.Join(" ", c.GetLocalizedDescription().Split(' ').Skip(1))));

                    Color keyColor = GetScanCodeColor(keyCommands);
                    System.Drawing.Brush keyTextColor = System.Drawing.Brushes.Black;
                    if (keyColor == Color.Transparent)
                    {
                        keyColor = Color.White;
                    }
                    else
                    {
                        keyColor.R += (byte)((255 - keyColor.R) * 2 / 3);
                        keyColor.G += (byte)((255 - keyColor.G) * 2 / 3);
                        keyColor.B += (byte)((255 - keyColor.B) * 2 / 3);
                    }

                    Scale(ref keyBox, keyWidth, keyHeight);
                    keyBox.Inflate(-keySpacing, -keySpacing);

                    g.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb((int)keyColor.PackedValue)), keyBox.Left, keyBox.Top, keyBox.Width, keyBox.Height);
                    g.DrawRectangle(System.Drawing.Pens.Black, keyBox.Left, keyBox.Top, keyBox.Width, keyBox.Height);
                    g.DrawString(keyName, keyFontLabel, keyTextColor, keyBox.Right - g.MeasureString(keyName, keyFontLabel).Width + keySpacing, keyBox.Top - 3 * keySpacing);
                    g.DrawString(keyCommandNames, keyFontCommand, keyTextColor, keyBox.Left, keyBox.Bottom - keyCommands.Count() * keyFontCommand.Height);
                });
            }
            keyboardLayoutBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            keyFontLabel.Dispose();
            keyFontCommand.Dispose();
            keyboardLayoutBitmap.Dispose();
        }

        public static void Scale(ref Rectangle rectangle, int scaleX, int scaleY)
        {
            rectangle.X *= scaleX;
            rectangle.Y *= scaleY;
            rectangle.Width *= scaleX;
            rectangle.Height *= scaleY;
        }

        public static string GetPrettyUniqueInput(string uniqueInput)
        {
            if (string.IsNullOrEmpty(uniqueInput))
                return string.Empty;

            string[] parts = uniqueInput.Split('+');
            if (parts[^1].StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(parts[^1].AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int key))
                    parts[^1] = ScanCodeKeyUtils.GetScanCodeKeyName(key);
            }
            return string.Join(" + ", parts);
        }

        public static IEnumerable<UserCommand> GetScanCodeCommands(int scanCode, EnumArray<UserCommandInput, UserCommand> commands)
        {
            return EnumExtension.GetValues<UserCommand>().Where(uc => ((commands[uc] as UserCommandKeyInput)?.ScanCode == scanCode));
        }
    }
}
