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
using System.Drawing;
using System.Windows.Forms;
using ORTS.Common.Input;
using ORTS.Settings;

namespace ORTS
{
    /// <summary>
    /// A control for viewing and altering keyboard input settings, in combination with <see cref="KeyInputEditControl"/>.
    /// </summary>
    /// <remarks>
    /// <para>This control will modify the <see cref="UserCommandInput"/> it is given (but not the default input).</para>
    /// <para>The control displays the currently assigtned keyboard shortcut and highlights the text if it is not the default. Clicking on the text invokes the editing behaviour via <see cref="KeyInputEditControl"/>.</para>
    /// </remarks>
    public partial class RDButtonInputControl : UserControl
    {
        public byte UserButton { get; private set; }
        public byte DefaultButton { get; private set; }

        private static RailDriverBase instance;
        private static byte[] readBuffer;
        private static byte[] buttonData = new byte[8];

        private static bool edit;

        public RDButtonInputControl(byte userButton, byte defaultButton, RailDriverBase instance)
        {
            InitializeComponent();
            if (RDButtonInputControl.instance == null)
                RDButtonInputControl.instance = instance;
            if (null == readBuffer)
                readBuffer = instance.NewReadBuffer;
            UserButton = userButton;
            DefaultButton = defaultButton;

            UpdateText();
        }

        private void UpdateText()
        {
            textBox.Text = UserButton == byte.MaxValue ? string.Empty: UserButton.ToString();
            if (UserButton == DefaultButton)
            {
                textBox.BackColor = SystemColors.Window;
                textBox.ForeColor = SystemColors.WindowText;
            }
            else
            {
                textBox.BackColor = SystemColors.Highlight;
                textBox.ForeColor = SystemColors.HighlightText;
            }
        }

        private void EnableEditButtons(bool enable)
        {
            edit = enable;
            buttonCancel.Visible = enable;
            buttonOK.Visible = enable;
            buttonDefault.Visible = enable;
        }

        private void TextBox_Enter(object sender, EventArgs e)
        {
            EnableEditButtons(true);
            if (0 == instance.BlockingReadCurrentData(ref readBuffer, 2000))
            {
                sbyte data = ValidateButtonIndex();
                if (data > -1)
                {
                    textBox.Text = ((byte)data).ToString();
                }
            }
        }

        private sbyte ValidateButtonIndex()
        {
            if (readBuffer.Length > 0)
            {
                Buffer.BlockCopy(readBuffer, 8, buttonData, 0, 6);
                ulong buttons = BitConverter.ToUInt64(buttonData, 0);
                //check that only 1 button is pressed
                if ((buttons & (buttons - 1)) == 0)
                {
                    // loop which bit is set
                    for (sbyte i = 0; i < 48; i++)
                    {
                        buttons = buttons >> 1;
                        if (buttons == 0)
                            return i;
                    }
                }
            }
            return -1;
        }

        private void TextBox_Leave(object sender, EventArgs e)
        {
            if (edit)
                textBox.Focus();
        }

        private void ButtonOK_Click(object sender, EventArgs e)
        {
            if (byte.TryParse(textBox.Text, out byte result))
                UserButton = result;
            ButtonClick();
        }
        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            ButtonClick();
        }

        private void ButtonDefault_Click(object sender, EventArgs e)
        {
            UserButton = DefaultButton;
            ButtonClick();
        }

        private void ButtonClick()
        {
            Parent.Focus();
            EnableEditButtons(false);
            UpdateText();
        }
    }
}
