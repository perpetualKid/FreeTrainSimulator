// COPYRIGHT 2012, 2014 by the Open Rails project.
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using Orts.Common.Input;

using static FreeTrainSimulator.Common.Native.NativeMethods;

using Xna = Microsoft.Xna.Framework.Input;

namespace Orts.Menu
{
    /// <summary>
    /// A form used to edit keyboard input settings, in combination with <see cref="KeyInputControl"/>.
    /// </summary>
    /// <remarks>
    /// <para>This form is opened as a modal dialog by <see cref="KeyInputControl"/> and is not intended to be directly used by other code.</para>
    /// <para>The form hooks the keyboard input (using a low-level hook) and captures all input whilst focused. The captured input is translated in to the appropriate form for the keyboard input setting being modified (i.e. modifier only, or key + modifiers). Only scan codes are used, never virtual keys.</para>
    /// <para>The <see cref="DialogResult"/> indicates the user's response: <see cref="DialogResult.OK"/> for "accept", <see cref="DialogResult.Cancel"/> for "cancel" and <see cref="DialogResult.Ignore"/> for "reset to default".</para>
    /// </remarks>
    public partial class KeyInputEditControl : Form
    {
        private readonly UserCommandInput liveInput;
        private readonly bool isModifier;
        private int scanCode;
        private Xna.Keys virtualKey;
        private bool shift;
        private bool control;
        private bool alt;

        internal KeyInputEditControl(KeyInputControl control)
        {

            InitializeComponent();

            // Use a lambda here so we can capture the 'control' variable
            // only for as long as needed to set our location/size.
            Load += (object sender, EventArgs e) =>
            {
                Location = control.Parent.PointToScreen(control.Location);
                Size = control.Size;
                textBox.Focus();
                HookKeyboard();
            };

            FormClosed += (object sender, FormClosedEventArgs e) =>
            {
                UnhookKeyboard();
            };

            liveInput = control.UserInput;
            isModifier = liveInput.IsModifier;

#pragma warning disable IDE0008 // Use explicit type
            var input = UserCommandInput.DecomposeUniqueDescriptor(liveInput.UniqueDescriptor);
#pragma warning restore IDE0008 // Use explicit type
            shift = input.Shift;
            this.control = input.Control;
            alt = input.Alt;
            scanCode = input.ScanCode;
            virtualKey = (Xna.Keys)input.VirtualKey;

            UpdateText();
        }

        private void UpdateText()
        {
            liveInput.UniqueDescriptor = UserCommandInput.ComposeUniqueDescriptor(shift, control, alt, scanCode, virtualKey);
            textBox.Text = liveInput.ToString();
        }

        private KeyboardProcedure CurrentKeyboardProcedure;
        private bool CurrentShift, CurrentControl, CurrentAlt;

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (textBox.Focused)
            {
                if (nCode >= 0)
                {
                    Xna.Keys virtualKeyCode = (Xna.Keys)Marshal.ReadInt32(lParam);
                    int scanCode = (int)(Marshal.ReadInt64(lParam) >> 32);

                    switch ((int)wParam)
                    {
                        case WM_KEYDOWN:
                        case WM_SYSKEYDOWN:
                            // Print-screen needs an extended code, so fiddle things a bit.
                            if (virtualKeyCode == Xna.Keys.PrintScreen && scanCode == 0x37)
                                scanCode += 256;

                            // True if the virtual key code is for a modifier (shift, control, alt).
                            bool isModifier = false;
                            switch (scanCode)
                            {
                                case 0x2A:
                                case 0x36: CurrentShift = true; isModifier = true; break;
                                case 0x1D: CurrentControl = true; isModifier = true; break;
                                case 0x38: CurrentAlt = true; isModifier = true; break;
                                default: break;
                            }

                            if (!(this.isModifier ^ isModifier))
                            {
                                this.scanCode = this.isModifier ? 0 : scanCode;
                                virtualKey = Xna.Keys.None;
                                shift = CurrentShift;
                                control = CurrentControl;
                                alt = CurrentAlt;
                                UpdateText();
                            }

                            // Return 1 to disable further processing of this key.
                            return (IntPtr)1;
                        case WM_KEYUP:
                        case WM_SYSKEYUP:
                            switch (scanCode)
                            {
                                case 0x2A:
                                case 0x36: CurrentShift = false; break;
                                case 0x1D: CurrentControl = false; break;
                                case 0x38: CurrentAlt = false; break;
                                default: break;
                            }

                            // Return 1 to disable further processing of this key.
                            return (IntPtr)1;
                    }
                }
            }
            else
            {
                CurrentShift = CurrentControl = CurrentAlt = false;
            }
            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private static IntPtr keyboardHookId = IntPtr.Zero;

        public void HookKeyboard()
        {
            CurrentKeyboardProcedure = HookCallback;
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                using (ProcessModule currentModule = currentProcess.MainModule)
                {
                    Debug.Assert(keyboardHookId == IntPtr.Zero);
                    keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, CurrentKeyboardProcedure, GetModuleHandle(currentModule.ModuleName), 0);
                }
            }
        }

        private void UnhookKeyboard()
        {
            if (keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHookId);
                keyboardHookId = IntPtr.Zero;
                CurrentKeyboardProcedure = null;
            }
        }
    }
}
