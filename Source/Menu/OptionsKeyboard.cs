﻿using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Settings;
using Orts.Settings.Util;

namespace Orts.Menu
{
    public partial class OptionsForm : Form
    {

        private Panel InitializeKeyboardInputControls()
        {
            Panel panel = new Panel() { AutoScroll = true };
            panel.SuspendLayout();

            var columnWidth = (panelKeys.ClientSize.Width - 20) / 2;

            var tempLabel = new Label();
            var tempKIC = new KeyInputControl(settings.Input.Commands[(int)UserCommand.GameQuit], InputSettings.DefaultCommands[(int)UserCommand.GameQuit]);
            var rowTop = Math.Max(tempLabel.Margin.Top, tempKIC.Margin.Top);
            var rowHeight = tempKIC.Height;
            var rowSpacing = rowHeight + tempKIC.Margin.Vertical;

            var lastCategory = "";
            var i = 0;
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                var name = catalog.GetString(command.GetDescription());
                var category = ParseCategoryFrom(name);
                var descriptor = ParseDescriptorFrom(name);

                if (category != lastCategory)
                {
                    var catlabel = new Label
                    {
                        Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                        Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                        Text = category,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    catlabel.Font = new Font(catlabel.Font, FontStyle.Bold);
                    panel.Controls.Add(catlabel);

                    lastCategory = category;
                    ++i;
                }

                var label = new Label
                {
                    Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                    Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                    Text = descriptor,
                    TextAlign = ContentAlignment.MiddleRight
                };
                panel.Controls.Add(label);

                var keyInputControl = new KeyInputControl(settings.Input.Commands[(int)command], InputSettings.DefaultCommands[(int)command])
                {
                    Location = new Point(columnWidth + tempKIC.Margin.Left, rowTop + rowSpacing * i),
                    Size = new Size(columnWidth - tempKIC.Margin.Horizontal, rowHeight),
                    ReadOnly = true,
                    Tag = command
                };
                panel.Controls.Add(keyInputControl);
                ++i;
            }
            panel.ResumeLayout(true);
            tempLabel.Dispose();
            tempKIC.Dispose();
            return panel;
        }

        private void InitializeKeyboardSettings()
        {
            panelKeys.Controls.Clear();

            Panel controls = InitializeKeyboardInputControls();
            controls.Dock = DockStyle.Fill;
            panelKeys.Controls.Add(controls);
            string toolTip = catalog.GetString("Click to change this key");
            foreach (Control control in controls.Controls)
                if (control is RDButtonInputControl)
                    toolTip1.SetToolTip(control, toolTip);
        }

        private void ButtonDefaultKeys_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show(catalog.GetString("Remove all custom key assignments?"), RuntimeInfo.ProductName, MessageBoxButtons.YesNo))
            {
                settings.Input.Reset();
                InitializeKeyboardSettings();
            }
        }

        private void ButtonExport_Click(object sender, EventArgs e)
        {
            var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Open Rails Keyboard.txt");
            settings.Input.DumpToText(outputPath);
            MessageBox.Show(catalog.GetString("A listing of all keyboard commands and keys has been placed here:\n\n") + outputPath, RuntimeInfo.ProductName);
        }

        private void ButtonCheckKeys_Click(object sender, EventArgs e)
        {
            string errors = settings.Input.CheckForErrors();
            if (!string.IsNullOrEmpty(errors))
                MessageBox.Show(errors, Application.ProductName);
            else
                MessageBox.Show(catalog.GetString("No errors found."), RuntimeInfo.ProductName);
        }


    }
}
