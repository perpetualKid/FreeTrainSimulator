using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ORTS.Common;
using ORTS.Settings;

namespace ORTS
{
    public partial class OptionsForm : Form
    {

        private Task<Panel> InitializeKeyboardInputControls()
        {
            TaskCompletionSource<Panel> tcs = new TaskCompletionSource<Panel>();
            Panel panel = new Panel() { AutoScroll = true };
            panel.SuspendLayout();

            var columnWidth = (panelKeys.ClientSize.Width - 20) / 2;

            var tempLabel = new Label();
            var tempKIC = new KeyInputControl(Settings.Input.Commands[(int)UserCommand.GameQuit], InputSettings.DefaultCommands[(int)UserCommand.GameQuit]);
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

                var keyInputControl = new KeyInputControl(Settings.Input.Commands[(int)command], InputSettings.DefaultCommands[(int)command])
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
            tcs.SetResult(panel);
            return tcs.Task;
        }

        private async Task InitializeKeyboardSettingsAsync()
        {
            panelKeys.Controls.Clear();

            Panel controls = await Task.Run(InitializeKeyboardInputControls);
            controls.Dock = DockStyle.Fill;
            panelKeys.Controls.Add(controls);
            foreach (Control control in controls.Controls)
                if (control is RDButtonInputControl)
                    toolTip1.SetToolTip(control, catalog.GetString("Click to change this key"));
        }

        private async void ButtonDefaultKeys_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show(catalog.GetString("Remove all custom key assignments?"), Application.ProductName, MessageBoxButtons.YesNo))
            {
                Settings.Input.Reset();
                await InitializeKeyboardSettingsAsync();
            }
        }

        private void ButtonExport_Click(object sender, EventArgs e)
        {
            var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Open Rails Keyboard.txt");
            Settings.Input.DumpToText(outputPath);
            MessageBox.Show(catalog.GetString("A listing of all keyboard commands and keys has been placed here:\n\n") + outputPath, Application.ProductName);
        }

        private void ButtonCheckKeys_Click(object sender, EventArgs e)
        {
            string errors = Settings.Input.CheckForErrors();
            if (!string.IsNullOrEmpty(errors))
                MessageBox.Show(errors, Application.ProductName);
            else
                MessageBox.Show(catalog.GetString("No errors found."), Application.ProductName);
        }


    }
}
