using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;
using Orts.Common.Native;

namespace Orts.Common.Input
{
    public static class ScanCodeKeyUtils
    {
        public static Keys GetScanCodeKeys(int scanCode)
        {
            var sc = scanCode;
            if (scanCode >= 0x0100)
                sc = 0xE100 | (scanCode & 0x7F);
            else if (scanCode >= 0x0080)
                sc = 0xE000 | (scanCode & 0x7F);
            return (Keys)NativeMethods.MapVirtualKey(sc, NativeMethods.MapVirtualKeyType.ScanToVirtualEx);
        }

        public static string GetScanCodeKeyName(int scanCode)
        {
            var xnaName = Enum.GetName(typeof(Keys), GetScanCodeKeys(scanCode));
            var keyName = new String('\0', 32);
            var keyNameLength = NativeMethods.GetKeyNameText(scanCode << 16, keyName, keyName.Length);
            keyName = keyName.Substring(0, keyNameLength);

            if (keyName.Length > 0)
            {
                // Pick the XNA key name because:
                //   Pause (0x11D) is mapped to "Right Control".
                //   GetKeyNameText prefers "NUM 9" to "PAGE UP".
                if (!String.IsNullOrEmpty(xnaName) && ((scanCode == 0x11D) || keyName.StartsWith("NUM ", StringComparison.OrdinalIgnoreCase) || keyName.StartsWith(xnaName, StringComparison.OrdinalIgnoreCase) || xnaName.StartsWith(keyName, StringComparison.OrdinalIgnoreCase)))
                    return xnaName;

                return keyName;
            }

            // If we failed to convert the scan code to a name, show the scan code for debugging.
            return String.Format(" [sc=0x{0:X2}]", scanCode);
        }

    }
    /// <summary>
    /// Represents a single user-triggerable keyboard input command.
    /// </summary>
    public abstract class UserCommandInput
    {
        public abstract string PersistentDescriptor { get; set; }

        public virtual bool IsModifier { get { return false; } }

        public abstract bool IsKeyDown(KeyboardState keyboardState);

        public abstract IEnumerable<string> GetUniqueInputs();

        public override string ToString()
        {
            return "";
        }
    }

    /// <summary>
    /// Stores a specific combination of keyboard modifiers for comparison with a <see cref="KeyboardState"/>.
    /// </summary>
    public class UserCommandModifierInput : UserCommandInput
    {
        public bool Shift { get; private set; }
        public bool Control { get; private set; }
        public bool Alt { get; private set; }

        protected UserCommandModifierInput(bool shift, bool control, bool alt)
        {
            Shift = shift;
            Control = control;
            Alt = alt;
        }

        public UserCommandModifierInput(KeyModifiers modifiers)
        : this((modifiers & KeyModifiers.Shift) != 0, (modifiers & KeyModifiers.Control) != 0, (modifiers & KeyModifiers.Alt) != 0)
        {
        }

        protected static bool IsModifiersMatching(KeyboardState keyboardState, bool shift, bool control, bool alt)
        {
            return (!shift || keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift)) &&
                (!control || keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)) &&
                (!alt || keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt));
        }

        public override string PersistentDescriptor
        {
            get
            {
                return String.Format("0,0,{0},{1},{2}", Shift ? 1 : 0, Control ? 1 : 0, Alt ? 1 : 0);
            }
            set
            {
                var parts = value.Split(',');
                if (parts.Length >= 5)
                {
                    Shift = parts[2] != "0";
                    Control = parts[3] != "0";
                    Alt = parts[4] != "0";
                }
            }
        }

        public override bool IsModifier { get { return true; } }

        public override bool IsKeyDown(KeyboardState keyboardState)
        {
            return IsModifiersMatching(keyboardState, Shift, Control, Alt);
        }

        public override IEnumerable<string> GetUniqueInputs()
        {
            var key = new StringBuilder();
            if (Shift) key = key.Append("Shift+");
            if (Control) key = key.Append("Control+");
            if (Alt) key = key.Append("Alt+");
            if (key.Length > 0) key.Length -= 1;
            return new[] { key.ToString() };
        }

        public override string ToString()
        {
            var key = new StringBuilder();
            if (Shift) key = key.Append("Shift + ");
            if (Control) key = key.Append("Control + ");
            if (Alt) key = key.Append("Alt + ");
            if (key.Length > 0) key.Length -= 3;
            return key.ToString();
        }
    }

    /// <summary>
    /// Stores a key and specific combination of keyboard modifiers for comparison with a <see cref="KeyboardState"/>.
    /// </summary>
    public class UserCommandKeyInput : UserCommandInput
    {
        public int ScanCode { get; private set; }
        public Keys VirtualKey { get; private set; }
        public bool Shift { get; private set; }
        public bool Control { get; private set; }
        public bool Alt { get; private set; }

        protected UserCommandKeyInput(int scanCode, Keys virtualKey, bool shift, bool control, bool alt)
        {
            Debug.Assert((scanCode >= 1 && scanCode <= 127) || (virtualKey != Keys.None), "Scan code for keyboard input is outside the allowed range of 1-127.");
            ScanCode = scanCode;
            VirtualKey = virtualKey;
            Shift = shift;
            Control = control;
            Alt = alt;
        }

        public UserCommandKeyInput(int scancode)
        : this(scancode, KeyModifiers.None)
        {
        }

        public UserCommandKeyInput(Keys virtualKey)
        : this(virtualKey, KeyModifiers.None)
        {
        }

        public UserCommandKeyInput(int scancode, KeyModifiers modifiers)
        : this(scancode, Keys.None, (modifiers & KeyModifiers.Shift) != 0, (modifiers & KeyModifiers.Control) != 0, (modifiers & KeyModifiers.Alt) != 0)
        {
        }

        public UserCommandKeyInput(Keys virtualKey, KeyModifiers modifiers)
        : this(0, virtualKey, (modifiers & KeyModifiers.Shift) != 0, (modifiers & KeyModifiers.Control) != 0, (modifiers & KeyModifiers.Alt) != 0)
        {
        }

        protected Keys Key
        {
            get
            {
                return VirtualKey == Keys.None ? ScanCodeKeyUtils.GetScanCodeKeys(ScanCode) : VirtualKey;
            }
        }

        protected static bool IsKeyMatching(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key);
        }

        protected static bool IsModifiersMatching(KeyboardState keyboardState, bool shift, bool control, bool alt)
        {
            return ((keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift)) == shift) &&
                ((keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)) == control) &&
                ((keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt)) == alt);
        }

        public override string PersistentDescriptor
        {
            get
            {
                return String.Format("{0},{1},{2},{3},{4}", ScanCode, (int)VirtualKey, Shift ? 1 : 0, Control ? 1 : 0, Alt ? 1 : 0);
            }
            set
            {
                var parts = value.Split(',');
                if (parts.Length >= 5)
                {
                    ScanCode = int.Parse(parts[0]);
                    VirtualKey = (Keys)int.Parse(parts[1]);
                    Shift = parts[2] != "0";
                    Control = parts[3] != "0";
                    Alt = parts[4] != "0";
                }
            }
        }

        public override bool IsKeyDown(KeyboardState keyboardState)
        {
            return IsKeyMatching(keyboardState, Key) && IsModifiersMatching(keyboardState, Shift, Control, Alt);
        }

        public override IEnumerable<string> GetUniqueInputs()
        {
            var key = new StringBuilder();
            if (Shift) key = key.Append("Shift+");
            if (Control) key = key.Append("Control+");
            if (Alt) key = key.Append("Alt+");
            if (VirtualKey == Keys.None)
                key.AppendFormat("0x{0:X2}", ScanCode);
            else
                key.Append(VirtualKey);
            return new[] { key.ToString() };
        }

        public override string ToString()
        {
            var key = new StringBuilder();
            if (Shift) key.Append("Shift + ");
            if (Control) key.Append("Control + ");
            if (Alt) key.Append("Alt + ");
            if (VirtualKey == Keys.None)
                key.Append(ScanCodeKeyUtils.GetScanCodeKeyName(ScanCode));
            else
                key.Append(VirtualKey);
            return key.ToString();
        }
    }

    /// <summary>
    /// Stores a key, specific combination of keyboard modifiers and a set of keyboard modifiers to ignore for comparison with a <see cref="KeyboardState"/>.
    /// </summary>
    public class UserCommandModifiableKeyInput : UserCommandKeyInput
    {
        public bool IgnoreShift { get; private set; }
        public bool IgnoreControl { get; private set; }
        public bool IgnoreAlt { get; private set; }

        UserCommandModifierInput[] Combine;

        UserCommandModifiableKeyInput(int scanCode, Keys virtualKey, KeyModifiers modifiers, IEnumerable<UserCommandInput> combine)
            : base(scanCode, virtualKey, (modifiers & KeyModifiers.Shift) != 0, (modifiers & KeyModifiers.Control) != 0, (modifiers & KeyModifiers.Alt) != 0)
        {
            Combine = combine.Cast<UserCommandModifierInput>().ToArray();
            SynchronizeCombine();
        }

        public UserCommandModifiableKeyInput(int scanCode, KeyModifiers modifiers, params UserCommandInput[] combine)
            : this(scanCode, Keys.None, modifiers, combine)
        {
        }

        public UserCommandModifiableKeyInput(int scanCode, params UserCommandInput[] combine)
            : this(scanCode, KeyModifiers.None, combine)
        {
        }

        public override string PersistentDescriptor
        {
            get
            {
                return String.Format("{0},{1},{2},{3}", base.PersistentDescriptor, IgnoreShift ? 1 : 0, IgnoreControl ? 1 : 0, IgnoreAlt ? 1 : 0);
            }
            set
            {
                base.PersistentDescriptor = value;
                var parts = value.Split(',');
                if (parts.Length >= 8)
                {
                    IgnoreShift = parts[5] != "0";
                    IgnoreControl = parts[6] != "0";
                    IgnoreAlt = parts[7] != "0";
                }
            }
        }

        public override bool IsKeyDown(KeyboardState keyboardState)
        {
            var shiftState = IgnoreShift ? keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift) : Shift;
            var controlState = IgnoreControl ? keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl) : Control;
            var altState = IgnoreAlt ? keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt) : Alt;
            return IsKeyMatching(keyboardState, Key) && IsModifiersMatching(keyboardState, shiftState, controlState, altState);
        }

        public override IEnumerable<string> GetUniqueInputs()
        {
            IEnumerable<string> inputs = new[] { Key.ToString() };

            // This must result in the output being Shift+Control+Alt+key.

            if (IgnoreAlt)
                inputs = inputs.SelectMany(i => new[] { i, "Alt+" + i });
            else if (Alt)
                inputs = inputs.Select(i => "Alt+" + i);

            if (IgnoreControl)
                inputs = inputs.SelectMany(i => new[] { i, "Control+" + i });
            else if (Control)
                inputs = inputs.Select(i => "Control+" + i);

            if (IgnoreShift)
                inputs = inputs.SelectMany(i => new[] { i, "Shift+" + i });
            else if (Shift)
                inputs = inputs.Select(i => "Shift+" + i);

            return inputs;
        }

        public override string ToString()
        {
            var key = new StringBuilder(base.ToString());
            if (IgnoreShift) key.Append(" (+ Shift)");
            if (IgnoreControl) key.Append(" (+ Control)");
            if (IgnoreAlt) key.Append(" (+ Alt)");
            return key.ToString();
        }

        public void SynchronizeCombine()
        {
            IgnoreShift = Combine.Any(c => c.Shift);
            IgnoreControl = Combine.Any(c => c.Control);
            IgnoreAlt = Combine.Any(c => c.Alt);
        }
    }
}
