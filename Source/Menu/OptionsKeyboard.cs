using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Menu
{
    public partial class OptionsForm : Form
    {
        private Panel InitializeKeyboardInputControls()
        {
            ProfileKeyboardSettingsModel defaultKeyboardSettings = new ProfileKeyboardSettingsModel();
            Panel panel = new Panel() { AutoScroll = true };
            panel.SuspendLayout();

            int columnWidth = (panelKeys.ClientSize.Width - 20) / 2;

            Label tempLabel = new Label();
            KeyInputControl tempKIC = new KeyInputControl(userSettings.KeyboardSettings.UserCommands[UserCommand.GameQuit], defaultKeyboardSettings.UserCommands[UserCommand.GameQuit]);
            int rowTop = Math.Max(tempLabel.Margin.Top, tempKIC.Margin.Top);
            int rowHeight = tempKIC.Height;
            int rowSpacing = rowHeight + tempKIC.Margin.Vertical;

            string previousCategory = "";
            int i = 0;
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                string name = command.GetLocalizedDescription();
                string category, description;
                int index = name.IndexOf(' ', StringComparison.OrdinalIgnoreCase);
                if (index == -1)
                {
                    category = string.Empty;
                    description = name;
                }
                else
                {
                    category = name[..index];
                    description = name[(index + 1)..];
                }

                if (category != previousCategory)
                {
                    Label catlabel = new Label
                    {
                        Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                        Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                        Text = category,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    catlabel.Font = new Font(catlabel.Font, FontStyle.Bold);
                    panel.Controls.Add(catlabel);

                    previousCategory = category;
                    ++i;
                }

                Label label = new Label
                {
                    Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                    Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                    Text = description,
                    TextAlign = ContentAlignment.MiddleRight
                };
                panel.Controls.Add(label);

                KeyInputControl keyInputControl = new KeyInputControl(userSettings.KeyboardSettings.UserCommands[command], defaultKeyboardSettings.UserCommands[command])
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

        private async Task InitializeKeyboardSettings()
        {
            panelKeys.Controls.Clear();

            userSettings.KeyboardSettings ??= await userSettings.Parent.LoadSettingsModel<ProfileKeyboardSettingsModel>(CancellationToken.None).ConfigureAwait(false);

            Panel controls = InitializeKeyboardInputControls();
            controls.Dock = DockStyle.Fill;
            panelKeys.Controls.Add(controls);
            string toolTip = catalog.GetString("Click to change this key");
            foreach (Control control in controls.Controls)
                if (control is KeyInputControl)
                    toolTip1.SetToolTip(control, toolTip);
        }

        private async void ButtonDefaultKeys_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show(catalog.GetString("Remove all custom key assignments?"), RuntimeInfo.ProductName, MessageBoxButtons.YesNo))
            {
                userSettings.KeyboardSettings = new ProfileKeyboardSettingsModel();
                await InitializeKeyboardSettings().ConfigureAwait(false);
            }
        }

        private void ButtonExport_Click(object sender, EventArgs e)
        {
            string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Open Rails Keyboard.txt");
            userSettings.KeyboardSettings.UserCommands.DumpToText(outputPath);
            MessageBox.Show(catalog.GetString("A listing of all keyboard commands and keys has been placed here:\n\n") + outputPath, RuntimeInfo.ProductName);
        }

        private void ButtonCheckKeys_Click(object sender, EventArgs e)
        {
            string errors = userSettings.KeyboardSettings.UserCommands.CheckForErrors(ProfileKeyboardSettingsModel.Default.UserCommands);
            if (!string.IsNullOrEmpty(errors))
                MessageBox.Show(errors, RuntimeInfo.ProductName);
            else
                MessageBox.Show(catalog.GetString("No errors found."), RuntimeInfo.ProductName);
        }


    }
}
