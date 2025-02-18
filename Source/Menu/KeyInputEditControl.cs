﻿// COPYRIGHT 2012, 2014 by the Open Rails project.
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

using FreeTrainSimulator.Common.Input;

using static FreeTrainSimulator.Common.Native.NativeMethods;

using Xna = Microsoft.Xna.Framework.Input;

namespace FreeTrainSimulator.Menu
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

        private KeyboardProcedure currentKeyboardProcedure;
        private bool currentShift, currentControl, currentAlt;

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

            (shift, this.control, alt, scanCode, int virtualKeyInt) = UserCommandInput.DecomposeUniqueDescriptor(liveInput.UniqueDescriptor);
            virtualKey = (Xna.Keys)virtualKeyInt;

            UpdateText();
        }

        private void UpdateText()
        {
            liveInput.UniqueDescriptor = UserCommandInput.ComposeUniqueDescriptor(shift, control, alt, scanCode, virtualKey);
            textBox.Text = liveInput.ToString();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (textBox.Focused)
            {
                if (nCode >= 0)
                {
                    Xna.Keys virtualKeyCode = (Xna.Keys)Marshal.ReadInt32(lParam);
                    int scanCode = (int)(Marshal.ReadInt64(lParam) >> 32);

                    switch (wParam)
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
                                case 0x36:
                                    currentShift = true;
                                    isModifier = true;
                                    break;
                                case 0x1D:
                                    currentControl = true;
                                    isModifier = true;
                                    break;
                                case 0x38:
                                    currentAlt = true;
                                    isModifier = true;
                                    break;
                                default:
                                    break;
                            }

                            if (!(this.isModifier ^ isModifier))
                            {
                                this.scanCode = this.isModifier ? 0 : scanCode;
                                virtualKey = Xna.Keys.None;
                                shift = currentShift;
                                control = currentControl;
                                alt = currentAlt;
                                UpdateText();
                            }

                            // Return 1 to disable further processing of this key.
                            return (IntPtr)1;
                        case WM_KEYUP:
                        case WM_SYSKEYUP:
                            switch (scanCode)
                            {
                                case 0x2A:
                                case 0x36:
                                    currentShift = false;
                                    break;
                                case 0x1D:
                                    currentControl = false;
                                    break;
                                case 0x38:
                                    currentAlt = false;
                                    break;
                                default:
                                    break;
                            }

                            // Return 1 to disable further processing of this key.
                            return (IntPtr)1;
                    }
                }
            }
            else
            {
                currentShift = currentControl = currentAlt = false;
            }
            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private static IntPtr keyboardHookId = IntPtr.Zero;
        private static ProcessModule mainModule;

        public void HookKeyboard()
        {
            currentKeyboardProcedure = HookCallback;
            mainModule ??= Process.GetCurrentProcess().MainModule;
            Debug.Assert(keyboardHookId == IntPtr.Zero);
            keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, currentKeyboardProcedure, GetModuleHandle(mainModule.ModuleName), 0);
        }

        private void UnhookKeyboard()
        {
            if (keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHookId);
                keyboardHookId = IntPtr.Zero;
                currentKeyboardProcedure = null;
            }
        }
    }
}
