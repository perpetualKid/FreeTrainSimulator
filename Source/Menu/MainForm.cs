// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GNU.Gettext;
using GNU.Gettext.WinForms;
using Orts.Formats.OR;
using ORTS.Common;
using ORTS.Menu;
using ORTS.Settings;
using ORTS.Updater;
using Path = ORTS.Menu.Path;

namespace ORTS
{
    public partial class MainForm : Form
    {
        public enum UserAction
        {
            SingleplayerNewGame,
            SingleplayerResumeSave,
            SingleplayerReplaySave,
            SingleplayerReplaySaveFromSave,
            MultiplayerServer,
            MultiplayerClient,
            SinglePlayerTimetableGame,
            SinglePlayerResumeTimetableGame,
            MultiplayerServerResumeSave,
            MultiplayerClientResumeSave
        }

        private bool initialized;
        private UserSettings settings;
        private List<Folder> folders = new List<Folder>();
        private List<Route> routes = new List<Route>();
        private List<Activity> activities = new List<Activity>();
        private List<Consist> consists = new List<Consist>();
        private List<Path> paths = new List<Path>();
        private List<TimetableInfo> timetableSets = new List<TimetableInfo>();

        private System.Threading.CancellationTokenSource ctsRouteLoading;
        private System.Threading.CancellationTokenSource ctsActivityLoading;
        private System.Threading.CancellationTokenSource ctsConsistLoading;
        private System.Threading.CancellationTokenSource ctsPathLoading;
        private System.Threading.CancellationTokenSource ctsTimeTableLoading;

        private readonly ResourceManager Resources = new ResourceManager("ORTS.Properties.Resources", typeof(MainForm).Assembly);
        private UpdateManager UpdateManager;
        private readonly Image ElevationIcon;

        internal string RunActivityProgram
        {
            get
            {
                return System.IO.Path.Combine(Application.StartupPath, "RunActivity.exe"); ;
            }
        }
        
        // Base items
        public Folder SelectedFolder { get { return (Folder)comboBoxFolder.SelectedItem; } }
        public Route SelectedRoute { get { return (Route)comboBoxRoute.SelectedItem; } }

        // Activity mode items
        public Activity SelectedActivity { get { return (Activity)comboBoxActivity.SelectedItem; } }
        public Consist SelectedConsist { get { return (Consist)comboBoxConsist.SelectedItem; } }
        public Path SelectedPath { get { return (Path)comboBoxHeadTo.SelectedItem; } }
        public string SelectedStartTime { get { return comboBoxStartTime.Text; } }

        // Timetable mode items
        public TimetableInfo SelectedTimetableSet { get { return (TimetableInfo)comboBoxTimetableSet.SelectedItem; } }
        public TimetableFileLite SelectedTimetable { get { return (TimetableFileLite)comboBoxTimetable.SelectedItem; } }
        public TimetableFileLite.TrainInformation SelectedTimetableTrain { get { return (TimetableFileLite.TrainInformation)comboBoxTimetableTrain.SelectedItem; } }
        public int SelectedTimetableDay { get { return initialized ? (comboBoxTimetableDay.SelectedItem as KeyedComboBoxItem).Key : 0; } }
        public Consist SelectedTimetableConsist;
        public Path SelectedTimetablePath;

        // Shared items
        public int SelectedStartSeason { get { return initialized ? (radioButtonModeActivity.Checked ? (comboBoxStartSeason.SelectedItem as KeyedComboBoxItem).Key : (comboBoxTimetableSeason.SelectedItem as KeyedComboBoxItem).Key) : 0; } }
        public int SelectedStartWeather { get { return initialized ? (radioButtonModeActivity.Checked ? (comboBoxStartWeather.SelectedItem as KeyedComboBoxItem).Key : (comboBoxTimetableWeather.SelectedItem as KeyedComboBoxItem).Key) : 0; } }

        public string SelectedSaveFile { get; set; }
        public UserAction SelectedAction { get; set; }

        private GettextResourceManager catalog = new GettextResourceManager("Menu");

        #region Main Form
        public MainForm()
        {
            InitializeComponent();

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;

            // Set title to show revision or build info.
            Text = String.Format(VersionInfo.Version.Length > 0 ? "{0} {1}" : "{0} build {2}", Application.ProductName, VersionInfo.Version, VersionInfo.Build);
#if DEBUG
            Text += " (debug)";
#endif
            panelModeTimetable.Location = panelModeActivity.Location;
            UpdateEnabled();
            ElevationIcon = new Icon(SystemIcons.Shield, SystemInformation.SmallIconSize).ToBitmap();
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            this.Suspend();
            var options = Environment.GetCommandLineArgs().Where(a => (a.StartsWith("-") || a.StartsWith("/"))).Select(a => a.Substring(1));
            settings = new UserSettings(options);

            List<Task> initTasks = new List<Task>
            {
                InitializeUpdateManager(),
                LoadToolsAndDocuments()
            };

            LoadOptions();
            LoadLanguage();

            if (!initialized)
            {
                var seasons = new[] {
                    new KeyedComboBoxItem(0, catalog.GetString("Spring")),
                    new KeyedComboBoxItem(1, catalog.GetString("Summer")),
                    new KeyedComboBoxItem(2, catalog.GetString("Autumn")),
                    new KeyedComboBoxItem(3, catalog.GetString("Winter")),
                };
                var weathers = new[] {
                    new KeyedComboBoxItem(0, catalog.GetString("Clear")),
                    new KeyedComboBoxItem(1, catalog.GetString("Snow")),
                    new KeyedComboBoxItem(2, catalog.GetString("Rain")),
                };
                var difficulties = new[] {
                    catalog.GetString("Easy"),
                    catalog.GetString("Medium"),
                    catalog.GetString("Hard"),
                    "",
                };
                var days = new[] {
                    new KeyedComboBoxItem(0, catalog.GetString("Monday")),
                    new KeyedComboBoxItem(1, catalog.GetString("Tuesday")),
                    new KeyedComboBoxItem(2, catalog.GetString("Wednesday")),
                    new KeyedComboBoxItem(3, catalog.GetString("Thursday")),
                    new KeyedComboBoxItem(4, catalog.GetString("Friday")),
                    new KeyedComboBoxItem(5, catalog.GetString("Saturday")),
                    new KeyedComboBoxItem(6, catalog.GetString("Sunday")),
                };

                comboBoxStartSeason.Items.AddRange(seasons);
                comboBoxStartWeather.Items.AddRange(weathers);
                comboBoxDifficulty.Items.AddRange(difficulties);

                comboBoxTimetableSeason.Items.AddRange(seasons);
                comboBoxTimetableWeather.Items.AddRange(weathers);
                comboBoxTimetableDay.Items.AddRange(days);

                initTasks.Add(LoadFolderListAsync());

                await Task.WhenAll(initTasks);
                initialized = true;
            }

            ShowEnvironment();
            ShowTimetableEnvironment();
            ShowDetails();

            this.Resume();
        }

        private async Task InitializeUpdateManager()
        {
            await Task.Run(() =>
            {
                UpdateManager = new UpdateManager(System.IO.Path.GetDirectoryName(Application.ExecutablePath), Application.ProductName, VersionInfo.VersionOrBuild);
            });
            await CheckForUpdateAsync();

        }

        private Task<List<ToolStripItem>> LoadTools()
        {
            TaskCompletionSource<List<ToolStripItem>> tcs = new TaskCompletionSource<List<ToolStripItem>>();
            List<ToolStripItem> result = new List<ToolStripItem>();

            var coreExecutables = new[] {
                    "OpenRails.exe",
                    "Menu.exe",
                    "RunActivity.exe",
                    "RunActivityLAA.exe",
                    "Updater.exe",
                };
            Parallel.ForEach(Directory.GetFiles(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "*.exe"), (fileName) =>
            {
                // Don't show any of the core parts of the application.
                if (coreExecutables.Contains(System.IO.Path.GetFileName(fileName)))
                    return;

                FileVersionInfo toolInfo = FileVersionInfo.GetVersionInfo(fileName);

                // Skip any executable that isn't part of this product (e.g. Visual Studio hosting files).
                if (toolInfo.ProductName != Application.ProductName)
                    return;

                // Remove the product name from the tool's name and localise.
                string toolName = catalog.GetString(toolInfo.FileDescription.Replace(Application.ProductName, "").Trim());

                lock (result)
                {
                    // Create menu item to execute tool.
                    result.Add(new ToolStripMenuItem(toolName, null, (Object sender2, EventArgs e2) =>
                    {
                        string toolPath = (sender2 as ToolStripItem).Tag as string;
                        bool toolIsConsole = false;
                        using (var reader = new BinaryReader(File.OpenRead(toolPath)))
                        {
                            toolIsConsole = GetImageSubsystem(reader) == ImageSubsystem.WindowsConsole;
                        }
                        if (toolIsConsole)
                            Process.Start("cmd", "/k \"" + toolPath + "\"");
                        else
                            Process.Start(toolPath);
                    })
                    { Tag = fileName });
                }
            });
            tcs.TrySetResult(result);
            return tcs.Task;
        }

        private Task<List<ToolStripItem>> LoadDocuments()
        {
            TaskCompletionSource<List<ToolStripItem>> tcs = new TaskCompletionSource<List<ToolStripItem>>();
            List<ToolStripItem> result = new List<ToolStripItem>();

            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Documentation");
            if (Directory.Exists(path))
            {
                Parallel.ForEach(Directory.GetFiles(path), (fileName) =>
                {
                    // These are the following formats that can be selected.
                    if (fileName.EndsWith(".pdf") || fileName.EndsWith(".doc") || fileName.EndsWith(".docx") || fileName.EndsWith(".pptx") || fileName.EndsWith(".txt"))
                    {
                        lock (result)
                        {
                            result.Add(new ToolStripMenuItem(System.IO.Path.GetFileName(fileName), null, (Object sender2, EventArgs e2) =>
                            {
                                var docPath = (sender2 as ToolStripItem).Tag as string;
                                Process.Start(docPath);
                            })
                            { Tag = fileName });
                        }
                    }
                });
            }
            tcs.TrySetResult(result);
            return tcs.Task;
        }

        private async Task LoadToolsAndDocuments()
        {
            await Task.WhenAll(
                Task.Run(() => LoadTools()).ContinueWith((tools) =>
                {
                    // Add all the tools in alphabetical order.
                    contextMenuStripTools.Items.AddRange((from tool in tools.Result
                                                          orderby tool.Text
                                                          select tool).ToArray());

                }),
                // Just like above, buttonDocuments is a button that is treated like a menu.  The result is a button that acts like a combobox.
                // Populate buttonDocuments.
                Task.Run(() => LoadDocuments()).ContinueWith((documents) =>
                {
                    // Add all the tools in alphabetical order.
                    contextMenuStripDocuments.Items.AddRange((from doc in documents.Result
                                                              orderby doc.Text
                                                              select doc).ToArray());

                }));
            // Documents button will be disabled if Documentation folder is not present.
            buttonDocuments.Enabled = contextMenuStripDocuments.Items.Count > 0;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveOptions();
            if (null != ctsRouteLoading && !ctsRouteLoading.IsCancellationRequested)
                ctsRouteLoading.Cancel();
            if (null != ctsActivityLoading && !ctsActivityLoading.IsCancellationRequested)
                ctsActivityLoading.Cancel();
            if (null != ctsConsistLoading && !ctsConsistLoading.IsCancellationRequested)
                ctsConsistLoading.Cancel();
            if (null != ctsPathLoading && !ctsPathLoading.IsCancellationRequested)
                ctsPathLoading.Cancel();
            if (null != ctsTimeTableLoading && !ctsPathLoading.IsCancellationRequested)
                ctsTimeTableLoading.Cancel();

            // Remove any deleted saves
            if (Directory.Exists(UserSettings.DeletedSaveFolder))
                Directory.Delete(UserSettings.DeletedSaveFolder, true);   // true removes all contents as well as folder

            // Tidy up after versions which used SAVE.BIN
            string file = UserSettings.UserDataFolder + @"\SAVE.BIN";
            if (File.Exists(file))
                File.Delete(file);
        }

        private async Task CheckForUpdateAsync()
        {
            if (string.IsNullOrEmpty(UpdateManager.ChannelName))
            {
                linkLabelChangeLog.Visible = false;
                linkLabelUpdate.Visible = false;
                return;
            }
            // This is known directly from the chosen channel so doesn't need to wait for the update check itself.
            linkLabelChangeLog.Visible = !string.IsNullOrEmpty(UpdateManager.ChangeLogLink);

            await Task.Run(() => UpdateManager.CheckForUpdateAsync());

            if (UpdateManager.LastCheckError != null)
                linkLabelUpdate.Text = catalog.GetString("Update check failed");
            else if (UpdateManager.LastUpdate != null && UpdateManager.LastUpdate.Version != VersionInfo.Version)
                linkLabelUpdate.Text = catalog.GetStringFmt("Update to {0}", UpdateManager.LastUpdate.Version);
            else
                linkLabelUpdate.Text = "";
            linkLabelUpdate.Enabled = true;
            linkLabelUpdate.Visible = linkLabelUpdate.Text.Length > 0;
            // Update link's elevation icon and size/position.
            if (UpdateManager.LastCheckError == null && UpdateManager.LastUpdate?.Version != VersionInfo.Version && UpdateManager.UpdaterNeedsElevation)
                linkLabelUpdate.Image = ElevationIcon;
            else
                linkLabelUpdate.Image = null;
            linkLabelUpdate.AutoSize = true;
            linkLabelUpdate.Left = panelDetails.Right - linkLabelUpdate.Width - ElevationIcon.Width;
            linkLabelUpdate.AutoSize = false;
            linkLabelUpdate.Width = panelDetails.Right - linkLabelUpdate.Left;
        }

        private void LoadLanguage()
        {
            if (!string.IsNullOrEmpty(settings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(settings.Language);
                }
                catch
                {
                }
            }

            Localizer.Localize(this, catalog);
        }

        private void RestartMenu()
        {
            Process.Start(Application.ExecutablePath);
            Close();
        }
        #endregion

        #region Folders
        private async void ComboBoxFolder_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                await Task.WhenAll(LoadRouteListAsync(), LoadLocomotiveListAsync());
                ShowDetails();
            }
            catch (System.Threading.Tasks.TaskCanceledException) { }
        }
        #endregion

        #region Routes
        private async void ComboBoxRoute_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                await Task.WhenAll(
                    LoadActivityListAsync(),
                    LoadStartAtListAsync(),
                    LoadTimetableSetListAsync());
                ShowDetails();
            }
            catch (System.Threading.Tasks.TaskCanceledException) { }
        }
        #endregion

        #region Mode
        private void RadioButtonMode_CheckedChanged(object sender, EventArgs e)
        {
            panelModeActivity.Visible = radioButtonModeActivity.Checked;
            panelModeTimetable.Visible = radioButtonModeTimetable.Checked;
            UpdateEnabled();
            ShowDetails();
        }
        #endregion

        #region Activities
        private void ComboBoxActivity_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowLocomotiveList();
            ShowConsistList();
            ShowStartAtList();
            ShowEnvironment();
            ShowDetails();
            //Debrief Activity Eval
            //0 = "- Explore route -"
            //1 = "+ Explore in Activity mode +"
            if (comboBoxActivity.SelectedIndex < 2)
            { checkDebriefActivityEval.Checked = false; checkDebriefActivityEval.Enabled = false; }
            else
            { checkDebriefActivityEval.Enabled = true; }
        }
        #endregion

        #region Locomotives
        private void ComboBoxLocomotive_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowConsistList();
            ShowDetails();
        }
        #endregion

        #region Consists
        private void ComboBoxConsist_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
            ShowDetails();
        }
        #endregion

        #region Starting from
        private void comboBoxStartAt_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowHeadToList();
        }
        #endregion

        #region Heading to
        private void ComboBoxHeadTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
            ShowDetails();
        }
        #endregion

        #region Environment
        private void ComboBoxStartTime_TextChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
        }

        private void ComboBoxStartSeason_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
        }

        private void ComboBoxStartWeather_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
        }
        #endregion

        #region Timetable Sets
        private void ComboBoxTimetableSet_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
            ShowTimetableList();
            ShowDetails();
        }
        #endregion

        #region Timetables
        private void ComboBoxTimetable_selectedIndexChanged(object sender, EventArgs e)
        {
            ShowTimetableTrainList();
            ShowDetails();
        }
        #endregion

        #region Timetable Trains
        private void ComboBoxTimetableTrain_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedTrain = comboBoxTimetableTrain.SelectedItem as TimetableFileLite.TrainInformation;
            if (null != selectedTrain)
            {
                SelectedTimetableConsist = Consist.GetConsist(SelectedFolder, selectedTrain.LeadingConsist, selectedTrain.ReverseConsist);
                SelectedTimetablePath = Path.GetPath(SelectedRoute, selectedTrain.Path, false);
                ShowDetails();
            }
        }
        #endregion

        #region Timetable environment
        private void ComboBoxTimetableDay_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
        }

        private void ComboBoxTimetableSeason_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
        }

        private void ComboBoxTimetableWeather_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
        }
        #endregion

        #region Multiplayer
        private void TextBoxMPUser_TextChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
        }

        private bool CheckUserName(string text)
        {
            string tmp = text;
            if (tmp.Length < 4 || tmp.Length > 10 || tmp.Contains("\"") || tmp.Contains("\'") || tmp.Contains(" ") || tmp.Contains("-") || Char.IsDigit(tmp, 0))
            {
                MessageBox.Show(catalog.GetString("User name must be 4-10 characters long, cannot contain space, ', \" or - and must not start with a digit."), Application.ProductName);
                return false;
            }
            return true;
        }

        #endregion

        #region Misc. buttons and options
        private async void LinkLabelUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (UpdateManager.LastCheckError != null)
            {
                MessageBox.Show(catalog.GetStringFmt("The update check failed due to an error:\n\n{0}", UpdateManager.LastCheckError), Application.ProductName);
                return;
            }

            await UpdateManager.RunUpdateProcess();

            if (UpdateManager.LastUpdateError != null)
            {
                MessageBox.Show(catalog.GetStringFmt("The update failed due to an error:\n\n{0}", UpdateManager.LastUpdateError), Application.ProductName);
            }
        }

        private void LinkLabelChangeLog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(UpdateManager.ChangeLogLink);
        }

        private void ButtonTools_Click(object sender, EventArgs e)
        {
            contextMenuStripTools.Show(buttonTools, new Point(0, buttonTools.ClientSize.Height), ToolStripDropDownDirection.Default);
        }

        private void ButtonDocuments_Click(object sender, EventArgs e)
        {
            contextMenuStripDocuments.Show(buttonDocuments, new Point(0, buttonDocuments.ClientSize.Height), ToolStripDropDownDirection.Default);
        }

        private void TestingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new TestingForm(settings, this.RunActivityProgram))
            {
                form.ShowDialog(this);
            }
        }

        private async void ButtonOptions_Click(object sender, EventArgs e)
        {
            SaveOptions();

            using (var form = new OptionsForm(settings, UpdateManager, false))
            {
                switch (form.ShowDialog(this))
                {
                    case DialogResult.OK:
                        await Task.WhenAll(LoadFolderListAsync(), CheckForUpdateAsync());
                        break;
                    case DialogResult.Retry:
                        RestartMenu();
                        break;
                }
            }
        }

        private void ButtonStart_Click(object sender, EventArgs e)
        {
            SaveOptions();

            if (radioButtonModeActivity.Checked)
            {
                SelectedAction = UserAction.SingleplayerNewGame;
                if (SelectedActivity != null)
                    DialogResult = DialogResult.OK;
            }
            else
            {
                SelectedAction = UserAction.SinglePlayerTimetableGame;
                if (SelectedTimetableTrain != null)
                    DialogResult = DialogResult.OK;
            }
        }

        private void ButtonResume_Click(object sender, EventArgs e)
        {
            OpenResumeForm(false);
        }

        void buttonResumeMP_Click(object sender, EventArgs e)
        {
            OpenResumeForm(true);
        }

        void OpenResumeForm (bool multiplayer)
        {
            if (radioButtonModeTimetable.Checked)
            {
                SelectedAction = UserAction.SinglePlayerTimetableGame;
            }
            else if (!multiplayer)
            {
                SelectedAction = UserAction.SingleplayerNewGame;
            }
            else if (radioButtonMPClient.Checked)
            {
                SelectedAction = UserAction.MultiplayerClient;
            }
            else
                SelectedAction = UserAction.MultiplayerServer;

            // if timetable mode but no timetable selected - no action
            if (SelectedAction == UserAction.SinglePlayerTimetableGame && (SelectedTimetableSet == null || multiplayer))
            {
                return;
            }

            using (var form = new ResumeForm(settings, SelectedRoute, SelectedAction, SelectedActivity, SelectedTimetableSet, this.routes))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    SaveOptions();
                    SelectedSaveFile = form.SelectedSaveFile;
                    SelectedAction = form.SelectedAction;
                    DialogResult = DialogResult.OK;
                }
            }
        }

        void buttonStartMP_Click(object sender, EventArgs e)
        {
            if (CheckUserName(textBoxMPUser.Text) == false) return;
            SaveOptions();
            SelectedAction = radioButtonMPClient.Checked? UserAction.MultiplayerClient : UserAction.MultiplayerServer;
            DialogResult = DialogResult.OK;
        }

        #endregion

        #region Options
        private void LoadOptions()
        {
            checkBoxWarnings.Checked = settings.Logging;
            checkBoxWindowed.Checked = !settings.FullScreen;
            //Debrief activity evaluation
            checkDebriefActivityEval.Checked = settings.DebriefActivityEval;
            //TO DO: Debrief TTactivity evaluation
            //checkDebriefTTActivityEval.Checked = Settings.DebriefTTActivityEval;

            textBoxMPUser.Text = settings.Multiplayer_User;
            textBoxMPHost.Text = settings.Multiplayer_Host + ":" + settings.Multiplayer_Port;
        }

        private void SaveOptions()
        {
            settings.Logging = checkBoxWarnings.Checked;
            settings.FullScreen = !checkBoxWindowed.Checked;
            settings.Multiplayer_User = textBoxMPUser.Text;
            //Debrief activity evaluation
            settings.DebriefActivityEval = checkDebriefActivityEval.Checked;
            //TO DO: Debrief TTactivity evaluation
            //Settings.DebriefTTActivityEval = checkDebriefTTActivityEval.Checked;

            var mpHost = textBoxMPHost.Text.Split(':');
            settings.Multiplayer_Host = mpHost[0];
            if (mpHost.Length > 1)
            {
                var port = settings.Multiplayer_Port;
                if (int.TryParse(mpHost[1], out port))
                    settings.Multiplayer_Port = port;
            }
            else
            {
                settings.Multiplayer_Port = (int)settings.GetDefaultValue("Multiplayer_Port");
            }
            settings.Menu_Selection = new[] {
                // Base items
                SelectedFolder?.Path ?? string.Empty,
                SelectedRoute?.Path ?? string.Empty,
                // Activity mode items / Explore mode items
                radioButtonModeActivity.Checked ? SelectedActivity?.FilePath ?? string.Empty : SelectedTimetableSet?.FileName ?? string.Empty,
                radioButtonModeActivity.Checked ?
                    SelectedActivity is ExploreActivity && (comboBoxLocomotive.SelectedItem as Locomotive)?.FilePath != null ? (comboBoxLocomotive.SelectedItem as Locomotive).FilePath : string.Empty :
                    SelectedTimetable?.Description ?? string.Empty,
                radioButtonModeActivity.Checked ?
                    SelectedActivity is ExploreActivity && SelectedConsist != null ? SelectedConsist.FilePath : string.Empty :
                    SelectedTimetableTrain?.Column.ToString() ?? string.Empty,
                radioButtonModeActivity.Checked ?
                    SelectedActivity is ExploreActivity && SelectedPath != null ? SelectedPath.FilePath : string.Empty : SelectedTimetableDay.ToString(),
                radioButtonModeActivity.Checked ?
                    SelectedActivity is ExploreActivity ? SelectedStartTime : string.Empty : string.Empty,
                // Shared items
                radioButtonModeActivity.Checked ?
                    SelectedActivity is ExploreActivity ? SelectedStartSeason.ToString() : string.Empty : SelectedStartSeason.ToString(),
                radioButtonModeActivity.Checked ?
                    SelectedActivity is ExploreActivity ? SelectedStartWeather.ToString() : string.Empty : SelectedStartWeather.ToString(),
            };
            settings.Save();
        }
        #endregion

        #region Enabled state
        private void UpdateEnabled()
        {
            comboBoxFolder.Enabled = comboBoxFolder.Items.Count > 0;
            comboBoxRoute.Enabled = comboBoxRoute.Items.Count > 0;
            comboBoxActivity.Enabled = comboBoxActivity.Items.Count > 0;
            comboBoxLocomotive.Enabled = comboBoxLocomotive.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxConsist.Enabled = comboBoxConsist.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxStartAt.Enabled = comboBoxStartAt.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxHeadTo.Enabled = comboBoxHeadTo.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxStartTime.Enabled = comboBoxStartSeason.Enabled = comboBoxStartWeather.Enabled = SelectedActivity is ExploreActivity;
            comboBoxStartTime.DropDownStyle = SelectedActivity is ExploreActivity ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList;
            comboBoxTimetable.Enabled = comboBoxTimetableSet.Items.Count > 0;
            comboBoxTimetableTrain.Enabled = comboBoxTimetable.Items.Count > 0;
            //Avoid to Start with a non valid Activity/Locomotive/Consist.
            buttonResume.Enabled = buttonStart.Enabled = radioButtonModeActivity.Checked && !comboBoxActivity.Text.StartsWith("<") && !comboBoxLocomotive.Text.StartsWith("<") ?
                SelectedActivity != null && (!(SelectedActivity is ExploreActivity) || (comboBoxConsist.Items.Count > 0 && comboBoxHeadTo.Items.Count > 0)) :
                SelectedTimetableTrain != null;
            buttonResumeMP.Enabled = buttonStartMP.Enabled = buttonStart.Enabled && !String.IsNullOrEmpty(textBoxMPUser.Text) && !String.IsNullOrEmpty(textBoxMPHost.Text);
        }
        #endregion

        #region Folder list
        private async Task LoadFolderListAsync()
        {
            folders.Clear();
            ShowFolderList();

            folders = (await Task.Run (() => Folder.GetFolders(settings))).OrderBy(f => f.Name).ToList();

            ShowFolderList();
            if (folders.Count > 0)
                comboBoxFolder.Focus();

            if (!initialized && folders.Count == 0)
            {
                using (var form = new OptionsForm(settings, UpdateManager, true))
                {
                    switch (form.ShowDialog(this))
                    {
                        case DialogResult.OK:
                            await LoadFolderListAsync();
                            break;
                        case DialogResult.Retry:
                            RestartMenu();
                            break;
                    }
                }
            }
        }

        private void ShowFolderList()
        {
            try
            {
                comboBoxFolder.BeginUpdate();
                comboBoxFolder.Items.Clear();
                comboBoxFolder.Items.AddRange(folders.ToArray());
            }
            finally
            {
                comboBoxFolder.EndUpdate();
            }
            UpdateFromMenuSelection<Folder>(comboBoxFolder, UserSettings.Menu_SelectionIndex.Folder, f => f.Path);
            UpdateEnabled();
        }
        #endregion

        #region Route list
        private async Task LoadRouteListAsync()
        {
            lock (routes)
            {
                if (ctsRouteLoading != null && !ctsRouteLoading.IsCancellationRequested)
                    ctsRouteLoading.Cancel();
                ctsRouteLoading = ResetCancellationTokenSource(ctsRouteLoading);
            }
            routes.Clear();
            activities.Clear();
            paths.Clear();

            //cleanout existing data
            ShowRouteList();
            ShowActivityList();
            ShowStartAtList();
            ShowHeadToList();

            Folder selectedFolder = SelectedFolder;
            routes = (await Task.Run(() => Route.GetRoutes(selectedFolder, ctsRouteLoading.Token))).OrderBy(r => r.Name).ToList();
            ShowRouteList();
        }

        private void ShowRouteList()
        {
            try
            {
                comboBoxRoute.BeginUpdate();
                comboBoxRoute.Items.Clear();
                comboBoxRoute.Items.AddRange(routes.ToArray());
            }
            finally
            {
                comboBoxRoute.EndUpdate();
            }
            UpdateFromMenuSelection<Route>(comboBoxRoute, UserSettings.Menu_SelectionIndex.Route, r => r.Path);
            if (settings.Menu_Selection.Length > (int)UserSettings.Menu_SelectionIndex.Activity)
            {
                string path = settings.Menu_Selection[(int)UserSettings.Menu_SelectionIndex.Activity]; // Activity or Timetable
                string extension = System.IO.Path.GetExtension(path).ToLower();
                if (extension == ".act")
                    radioButtonModeActivity.Checked = true;
                else if (extension == ".timetable_or")
                    radioButtonModeTimetable.Checked = true;
            }
            UpdateEnabled();
        }
        #endregion

        #region Activity list
        private async Task LoadActivityListAsync()
        {
            lock (activities)
            {
                if (ctsActivityLoading != null && !ctsActivityLoading.IsCancellationRequested)
                    ctsActivityLoading.Cancel();
                ctsActivityLoading = ResetCancellationTokenSource(ctsActivityLoading);
            }
            activities.Clear();
            ShowActivityList();

            Folder selectedFolder = SelectedFolder;
            Route selectedRoute = SelectedRoute;
            activities = (await Task.Run(() => Activity.GetActivities(selectedFolder, selectedRoute, ctsActivityLoading.Token))).OrderBy(a => a.Name).ToList();
            ShowActivityList();
        }

        private void ShowActivityList()
        {
            try
            {
                comboBoxActivity.BeginUpdate();
                comboBoxActivity.Items.Clear();
                comboBoxActivity.Items.AddRange(activities.ToArray());
            }
            finally
            {
                comboBoxActivity.EndUpdate();
            }
            UpdateFromMenuSelection<Activity>(comboBoxActivity, UserSettings.Menu_SelectionIndex.Activity, a => a.FilePath);
            UpdateEnabled();
        }

        private void UpdateExploreActivity()
        {
            (SelectedActivity as ExploreActivity)?.UpdateActivity(SelectedStartTime, (Orts.Formats.Msts.SeasonType)SelectedStartSeason, (Orts.Formats.Msts.WeatherType)SelectedStartWeather, SelectedConsist, SelectedPath);
        }
        #endregion

        #region Consist lists
        private async Task LoadLocomotiveListAsync()
        {
            lock (consists)
            {
                if (ctsConsistLoading != null && !ctsConsistLoading.IsCancellationRequested)
                    ctsConsistLoading.Cancel();
                ctsConsistLoading = ResetCancellationTokenSource(ctsConsistLoading);
            }

            consists.Clear();
            ShowLocomotiveList();
            ShowConsistList();

            Folder selectedFolder = SelectedFolder;
            consists = (await Task.Run(() => Consist.GetConsists(selectedFolder, ctsConsistLoading.Token))).OrderBy(c => c.Name).ToList();
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
                ShowLocomotiveList();
        }

        private void ShowLocomotiveList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                try
                {
                    comboBoxLocomotive.BeginUpdate();
                    comboBoxLocomotive.Items.Clear();
                    comboBoxLocomotive.Items.Add(Locomotive.GetLocomotive(null));
                    comboBoxLocomotive.Items.AddRange(consists.Where(c => c.Locomotive != null).Select(c => c.Locomotive).Distinct().OrderBy(l => l.Name).ToArray());
                    if (comboBoxLocomotive.Items.Count == 1)
                        comboBoxLocomotive.Items.Clear();
                }
                finally
                {
                    comboBoxLocomotive.EndUpdate();
                }
                UpdateFromMenuSelection<Locomotive>(comboBoxLocomotive, UserSettings.Menu_SelectionIndex.Locomotive, l => l.FilePath);
            }
            else
            {
                try
                {
                    comboBoxLocomotive.BeginUpdate();
                    comboBoxConsist.BeginUpdate();
                    var consist = SelectedActivity.Consist;
                    comboBoxLocomotive.Items.Clear();
                    comboBoxLocomotive.Items.Add(consist.Locomotive);
                    comboBoxLocomotive.SelectedIndex = 0;
                    comboBoxConsist.Items.Clear();
                    comboBoxConsist.Items.Add(consist);
                    comboBoxConsist.SelectedIndex = 0;
                }
                finally
                {
                    comboBoxLocomotive.EndUpdate();
                    comboBoxConsist.EndUpdate();
                }
            }
            UpdateEnabled();
        }

        private void ShowConsistList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                try
                {
                    comboBoxConsist.BeginUpdate();
                    comboBoxConsist.Items.Clear();
                    comboBoxConsist.Items.AddRange(consists.Where(c => comboBoxLocomotive.SelectedItem.Equals(c.Locomotive)).OrderBy(c => c.Name).ToArray());
                }
                finally
                {
                    comboBoxConsist.EndUpdate();
                }
                UpdateFromMenuSelection<Consist>(comboBoxConsist, UserSettings.Menu_SelectionIndex.Consist, c => c.FilePath);
            }
            UpdateEnabled();
        }
        #endregion

        #region Path lists
        private async Task LoadStartAtListAsync()
        {
            lock (paths)
            {
                if (ctsPathLoading != null && !ctsPathLoading.IsCancellationRequested)
                    ctsPathLoading.Cancel();
                ctsPathLoading = ResetCancellationTokenSource(ctsPathLoading);
            }

            paths.Clear();
            ShowStartAtList();
            ShowHeadToList();

            var selectedRoute = SelectedRoute;
            paths = (await Task.Run(() => Path.GetPaths(selectedRoute, false, ctsPathLoading.Token))).OrderBy(a => a.ToString()).ToList();

            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
                ShowStartAtList();
        }

        private void ShowStartAtList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                try
                {
                    comboBoxStartAt.BeginUpdate();
                    comboBoxStartAt.Items.Clear();
                    comboBoxStartAt.Items.AddRange(paths.Select(p => p.Start).Distinct().OrderBy(s => s.ToString()).ToArray());
                }
                finally
                {
                    comboBoxStartAt.EndUpdate();
                }
                // Because this list is unique names, we have to do some extra work to select it.
                if (settings.Menu_Selection.Length >= (int)UserSettings.Menu_SelectionIndex.Path)
                {
                    string pathFilePath = settings.Menu_Selection[(int)UserSettings.Menu_SelectionIndex.Path];
                    Path path = paths.FirstOrDefault(p => p.FilePath == pathFilePath);
                    if (path != null)
                        SelectComboBoxItem<string>(comboBoxStartAt, s => s == path.Start);
                    else if (comboBoxStartAt.Items.Count > 0)
                        comboBoxStartAt.SelectedIndex = 0;
                }
            }
            else
            {
                try
                {
                    comboBoxStartAt.BeginUpdate();
                    comboBoxHeadTo.BeginUpdate();
                    Path path = SelectedActivity.Path;
                    comboBoxStartAt.Items.Clear();
                    comboBoxStartAt.Items.Add(path.Start);
                    comboBoxHeadTo.Items.Clear();
                    comboBoxHeadTo.Items.Add(path);
                }
                finally
                {
                    comboBoxStartAt.EndUpdate();
                    comboBoxHeadTo.EndUpdate();
                }
                comboBoxStartAt.SelectedIndex = 0;
                comboBoxHeadTo.SelectedIndex = 0;
            }
            UpdateEnabled();
        }

        private void ShowHeadToList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                try
                {
                    comboBoxHeadTo.BeginUpdate();
                    comboBoxHeadTo.Items.Clear();
                    comboBoxHeadTo.Items.AddRange(paths.Where(p => p.Start == (string)comboBoxStartAt.SelectedItem).ToArray());
                }
                finally
                {
                    comboBoxHeadTo.EndUpdate();
                }
                UpdateFromMenuSelection<Path>(comboBoxHeadTo, UserSettings.Menu_SelectionIndex.Path, c => c.FilePath);
            }
            UpdateEnabled();
        }
        #endregion

        #region Environment
        private void ShowEnvironment()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                try
                {
                    comboBoxStartTime.BeginUpdate();
                    comboBoxDuration.BeginUpdate();
                    comboBoxStartTime.Items.Clear();
                    foreach (var hour in Enumerable.Range(0, 24))
                        comboBoxStartTime.Items.Add(String.Format("{0}:00", hour));
                    comboBoxDuration.Items.Clear();
                    comboBoxDuration.Items.Add("");
                }
                finally
                {
                    comboBoxStartTime.EndUpdate();
                    comboBoxDuration.EndUpdate();
                }

                UpdateFromMenuSelection(comboBoxStartTime, UserSettings.Menu_SelectionIndex.Time, "12:00");
                UpdateFromMenuSelection(comboBoxStartSeason, UserSettings.Menu_SelectionIndex.Season, s => s.Key.ToString(), new KeyedComboBoxItem(1, ""));
                UpdateFromMenuSelection(comboBoxStartWeather, UserSettings.Menu_SelectionIndex.Weather, w => w.Key.ToString(), new KeyedComboBoxItem(0, ""));
                comboBoxDifficulty.SelectedIndex = 3;
                comboBoxDuration.SelectedIndex = 0;
            }
            else
            {
                try
                {
                    comboBoxStartTime.BeginUpdate();
                    comboBoxDuration.BeginUpdate();

                    comboBoxStartTime.Items.Clear();
                    comboBoxStartTime.Items.Add(SelectedActivity.StartTime.FormattedStartTime());
                    comboBoxDuration.Items.Clear();
                    comboBoxDuration.Items.Add(SelectedActivity.Duration.FormattedDurationTime());
                }
                finally
                {
                    comboBoxStartTime.EndUpdate();
                    comboBoxDuration.EndUpdate();
                }
                comboBoxStartTime.SelectedIndex = 0;
                comboBoxStartSeason.SelectedIndex = (int)SelectedActivity.Season;
                comboBoxStartWeather.SelectedIndex = (int)SelectedActivity.Weather;
                comboBoxDifficulty.SelectedIndex = (int)SelectedActivity.Difficulty;
                comboBoxDuration.SelectedIndex = 0;
            }
        }
        #endregion

        #region Timetable Set list
        private async Task LoadTimetableSetListAsync()
        {
            lock (timetableSets)
            {
                if (ctsTimeTableLoading != null && !ctsTimeTableLoading.IsCancellationRequested)
                    ctsTimeTableLoading.Cancel();
                ctsTimeTableLoading = ResetCancellationTokenSource(ctsTimeTableLoading);
            }

            timetableSets.Clear();
            ShowTimetableSetList();

            var selectedFolder = SelectedFolder;
            var selectedRoute = SelectedRoute;
            timetableSets = (await Task.Run(() => TimetableInfo.GetTimetableInfo(selectedFolder, selectedRoute, ctsTimeTableLoading.Token))).OrderBy(tt => tt.Description).ToList();
            ShowTimetableSetList();
        }

        private void ShowTimetableSetList()
        {
            try
            {
                comboBoxTimetableSet.BeginUpdate();
                comboBoxTimetableSet.Items.Clear();
                comboBoxTimetableSet.Items.AddRange(timetableSets.ToArray());
            }
            finally
            {
                comboBoxTimetableSet.EndUpdate();
            }
            UpdateFromMenuSelection<TimetableInfo>(comboBoxTimetableSet, UserSettings.Menu_SelectionIndex.TimetableSet, t => t.FileName);
            UpdateEnabled();
        }

        private void UpdateTimetableSet()
        {
            if (SelectedTimetableSet != null)
            {
                SelectedTimetableSet.Day = SelectedTimetableDay;
                SelectedTimetableSet.Season = SelectedStartSeason;
                SelectedTimetableSet.Weather = SelectedStartWeather;
            }
        }
        #endregion

        #region Timetable list
        private void ShowTimetableList()
        {
            if (null != SelectedTimetableSet)
            {
                try
                {
                    comboBoxTimetable.BeginUpdate();
                    comboBoxTimetable.Items.Clear();
                    comboBoxTimetable.Items.AddRange(SelectedTimetableSet.ORTTList.ToArray());
                }
                finally
                {
                    comboBoxTimetable.EndUpdate();
                }
                UpdateFromMenuSelection<TimetableFileLite>(comboBoxTimetable, UserSettings.Menu_SelectionIndex.Timetable, t => t.Description);
            }
            else
                comboBoxTimetable.Items.Clear();

            UpdateEnabled();
        }
        #endregion

        #region Timetable Train list
                private void ShowTimetableTrainList()
                {
                    if (null != SelectedTimetableSet)
                    {
                        try
                        {
                            comboBoxTimetableTrain.BeginUpdate();
                            comboBoxTimetableTrain.Items.Clear();

                            var trains = SelectedTimetableSet.ORTTList[comboBoxTimetable.SelectedIndex].Trains;
                            trains.Sort();
                            comboBoxTimetableTrain.Items.AddRange(trains.ToArray());
                        }
                        finally
                        {
                            comboBoxTimetableTrain.EndUpdate();
                        }
                        UpdateFromMenuSelection<TimetableFileLite.TrainInformation>(comboBoxTimetableTrain, UserSettings.Menu_SelectionIndex.Train, t => t.Column.ToString());
                    }
                    else
                        comboBoxTimetableTrain.Items.Clear();

                    UpdateEnabled();
                }
        #endregion

        #region Timetable environment
        private void ShowTimetableEnvironment()
        {
            UpdateFromMenuSelection(comboBoxTimetableDay, UserSettings.Menu_SelectionIndex.Day, d => d.Key.ToString(), new KeyedComboBoxItem(0, string.Empty));
            UpdateFromMenuSelection(comboBoxTimetableSeason, UserSettings.Menu_SelectionIndex.Season, s => s.Key.ToString(), new KeyedComboBoxItem(1, string.Empty));
            UpdateFromMenuSelection(comboBoxTimetableWeather, UserSettings.Menu_SelectionIndex.Weather, w => w.Key.ToString(), new KeyedComboBoxItem(0, string.Empty));
        }
        #endregion

        #region Details
        private void ShowDetails()
        {
            try
            {
                this.Suspend();
                ClearDetails();
                if (SelectedRoute != null && SelectedRoute.Description != null)
                    ShowDetail(catalog.GetStringFmt("Route: {0}", SelectedRoute.Name), SelectedRoute.Description.Split('\n'));

                if (radioButtonModeActivity.Checked)
                {
                    if (SelectedConsist != null && SelectedConsist.Locomotive != null && SelectedConsist.Locomotive.Description != null)
                    {
                        ShowDetail(catalog.GetStringFmt("Locomotive: {0}", SelectedConsist.Locomotive.Name), SelectedConsist.Locomotive.Description.Split('\n'));
                    }
                    if (SelectedActivity != null && SelectedActivity.Description != null)
                    {
                        ShowDetail(catalog.GetStringFmt("Activity: {0}", SelectedActivity.Name), SelectedActivity.Description.Split('\n'));
                        ShowDetail(catalog.GetString("Activity Briefing"), SelectedActivity.Briefing.Split('\n'));
                    }
                    else if (SelectedPath != null)
                    {
                        ShowDetail(catalog.GetStringFmt("Path: {0}", SelectedPath.Name), new[] {
                        catalog.GetStringFmt("Starting at: {0}", SelectedPath.Start),
                        catalog.GetStringFmt("Heading to: {0}", SelectedPath.End)
                    });
                    }
                }
                if (radioButtonModeTimetable.Checked)
                {
                    if (SelectedTimetableSet != null)
                    {
                        ShowDetail(catalog.GetStringFmt("Timetable set: {0}", SelectedTimetableSet), new string[0]);
                    }
                    if (SelectedTimetable != null)
                    {
                        ShowDetail(catalog.GetStringFmt("Timetable: {0}", SelectedTimetable), new string[0]);
                    }
                    if (SelectedTimetableTrain != null)
                    {
                        ShowDetail(catalog.GetStringFmt("Train: {0}", SelectedTimetableTrain), SelectedTimetableTrain.ToInfo());
                        if (SelectedTimetableConsist != null)
                        {
                            ShowDetail(catalog.GetStringFmt("Consist: {0}", SelectedTimetableConsist.Name), new string[0]);
                            if (SelectedTimetableConsist.Locomotive != null && SelectedTimetableConsist.Locomotive.Description != null)
                            {
                                ShowDetail(catalog.GetStringFmt("Locomotive: {0}", SelectedTimetableConsist.Locomotive.Name), SelectedTimetableConsist.Locomotive.Description.Split('\n'));
                            }
                        }
                        if (SelectedTimetablePath != null)
                        {
                            ShowDetail(catalog.GetStringFmt("Path: {0}", SelectedTimetablePath.Name), SelectedTimetablePath.ToInfo());
                        }
                    }
                }

                FlowDetails();
            }
            finally
            {
                this.Resume();
            }
        }

        private List<Detail> Details = new List<Detail>();

        private class Detail
        {
            public readonly Control Title;
            public readonly Control Expander;
            public readonly Control Summary;
            public readonly Control Description;
            public bool Expanded;
            public Detail(Control title, Control expander, Control summary, Control lines)
            {
                Title = title;
                Expander = expander;
                Summary = summary;
                Description = lines;
                Expanded = false;
            }
        }

        private void ClearDetails()
        {
            Details.Clear();
            while (panelDetails.Controls.Count > 0)
                panelDetails.Controls.RemoveAt(0);
        }

        private void ShowDetail(string title, string[] lines)
        {
            var titleControl = new Label { Margin = new Padding(2), Text = title, UseMnemonic = false, Font = new Font(panelDetails.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            panelDetails.Controls.Add(titleControl);
            titleControl.Left = titleControl.Margin.Left;
            titleControl.Width = panelDetails.ClientSize.Width - titleControl.Margin.Horizontal - titleControl.PreferredHeight;
            titleControl.Height = titleControl.PreferredHeight;
            titleControl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            var expanderControl = new Button { Margin = new Padding(0), Text = "", FlatStyle = FlatStyle.Flat };
            panelDetails.Controls.Add(expanderControl);
            expanderControl.Left = panelDetails.ClientSize.Width - titleControl.Height - titleControl.Margin.Right;
            expanderControl.Width = expanderControl.Height = titleControl.Height;
            expanderControl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            expanderControl.FlatAppearance.BorderSize = 0;
            expanderControl.BackgroundImageLayout = ImageLayout.Center;

            var summaryControl = new Label { Margin = new Padding(2), Text = String.Join("\n", lines), AutoSize = false, UseMnemonic = false, UseCompatibleTextRendering = false };
            panelDetails.Controls.Add(summaryControl);
            summaryControl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            summaryControl.Left = summaryControl.Margin.Left;
            summaryControl.Width = panelDetails.ClientSize.Width - summaryControl.Margin.Horizontal;
            summaryControl.Height = TextRenderer.MeasureText("1\n2\n3\n4\n5", summaryControl.Font).Height;

            // Find out where we need to cut the text to make the summary 5 lines long. Uses a binaty search to find the cut point.
            var size = MeasureText(summaryControl.Text, summaryControl);
            if (size > summaryControl.Height)
            {
                var index = (float)summaryControl.Text.Length;
                var indexChunk = (float)summaryControl.Text.Length / 2;
                while (indexChunk > 0.5f || size > summaryControl.Height)
                {
                    if (size > summaryControl.Height)
                        index -= indexChunk;
                    else
                        index += indexChunk;
                    if (indexChunk > 0.5f)
                        indexChunk /= 2;
                    size = MeasureText(summaryControl.Text.Substring(0, (int)index) + "...", summaryControl);
                }
                summaryControl.Text = summaryControl.Text.Substring(0, (int)index) + "...";
            }

            var descriptionControl = new Label { Margin = new Padding(2), Text = String.Join("\n", lines), AutoSize = false, UseMnemonic = false, UseCompatibleTextRendering = false };
            panelDetails.Controls.Add(descriptionControl);
            descriptionControl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            descriptionControl.Left = descriptionControl.Margin.Left;
            descriptionControl.Width = panelDetails.ClientSize.Width - descriptionControl.Margin.Horizontal;
            descriptionControl.Height = MeasureText(descriptionControl.Text, descriptionControl);

            // Enable the expander only if the full description is longer than the summary. Otherwise, disable the expander.
            expanderControl.Enabled = descriptionControl.Height > summaryControl.Height;
            if (expanderControl.Enabled)
            {
                expanderControl.BackgroundImage = (Image)Resources.GetObject("ExpanderClosed");
                expanderControl.Tag = Details.Count;
                expanderControl.Click += new EventHandler(ExpanderControl_Click);
            }
            else
            {
                expanderControl.BackgroundImage = (Image)Resources.GetObject("ExpanderClosedDisabled");
            }
            Details.Add(new Detail(titleControl, expanderControl, summaryControl, descriptionControl));
        }

        private static int MeasureText(string text, Label summaryControl)
        {
            return TextRenderer.MeasureText(text, summaryControl.Font, summaryControl.ClientSize, TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        }

        private void ExpanderControl_Click(object sender, EventArgs e)
        {
            try
            {
                this.Suspend();
                var index = (int)(sender as Control).Tag;
                Details[index].Expanded = !Details[index].Expanded;
                Details[index].Expander.BackgroundImage = (Image)Resources.GetObject(Details[index].Expanded ? "ExpanderOpen" : "ExpanderClosed");
                FlowDetails();
            }
            finally
            {
                this.Resume();
            }
        }

        private void FlowDetails()
        {
                var scrollPosition = panelDetails.AutoScrollPosition.Y;
                panelDetails.AutoScrollPosition = Point.Empty;
                panelDetails.AutoScrollMinSize = new Size(0, panelDetails.ClientSize.Height + 1);

                var top = 0;
                foreach (var detail in Details)
                {
                    top += detail.Title.Margin.Top;
                    detail.Title.Top = detail.Expander.Top = top;
                    top += detail.Title.Height + detail.Title.Margin.Bottom + detail.Description.Margin.Top;
                    detail.Summary.Top = detail.Description.Top = top;
                    detail.Summary.Visible = !detail.Expanded && detail.Expander.Enabled;
                    detail.Description.Visible = !detail.Summary.Visible;
                    if (detail.Description.Visible)
                        top += detail.Description.Height + detail.Description.Margin.Bottom;
                    else
                        top += detail.Summary.Height + detail.Summary.Margin.Bottom;
                }

                if (panelDetails.AutoScrollMinSize.Height < top)
                    panelDetails.AutoScrollMinSize = new Size(0, top);
                panelDetails.AutoScrollPosition = new Point(0, -scrollPosition);
        }
        #endregion

        #region Utility functions
        private void UpdateFromMenuSelection<T>(ComboBox comboBox, UserSettings.Menu_SelectionIndex index, T defaultValue)
        {
            UpdateFromMenuSelection<T>(comboBox, index, _ => _.ToString(), defaultValue);
        }

        private void UpdateFromMenuSelection<T>(ComboBox comboBox, UserSettings.Menu_SelectionIndex index, Func<T, string> map)
        {
            UpdateFromMenuSelection<T>(comboBox, index, map, default(T));
        }

        private void UpdateFromMenuSelection<T>(ComboBox comboBox, UserSettings.Menu_SelectionIndex index, Func<T, string> map, T defaultValue)
        {
            if (settings.Menu_Selection.Length > (int)index && settings.Menu_Selection[(int)index] != "")
            {
                if (comboBox.DropDownStyle == ComboBoxStyle.DropDown)
                    comboBox.Text = settings.Menu_Selection[(int)index];
                else
                    SelectComboBoxItem<T>(comboBox, item => map(item) == settings.Menu_Selection[(int)index]);
            }
            else
            {
                if (comboBox.DropDownStyle == ComboBoxStyle.DropDown)
                    comboBox.Text = map(defaultValue);
                else if (defaultValue != null)
                    SelectComboBoxItem<T>(comboBox, item => map(item) == map(defaultValue));
                else if (comboBox.Items.Count > 0)
                    comboBox.SelectedIndex = 0;
            }
        }

        private void SelectComboBoxItem<T>(ComboBox comboBox, Func<T, bool> predicate)
        {
            if (comboBox.Items.Count == 0)
                return;

            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is T && predicate((T)comboBox.Items[i]))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
            comboBox.SelectedIndex = 0;
        }

        private class KeyedComboBoxItem
        {
            public readonly int Key;
            public readonly string Value;

            public override string ToString()
            {
                return Value;
            }

            public KeyedComboBoxItem(int key, string value)
            {
                Key = key;
                Value = value;
            }
        }
        #endregion

        #region Executable utils
        private enum ImageSubsystem
        {
            Unknown = 0,
            Native = 1,
            WindowsGui = 2,
            WindowsConsole = 3,
        }

        private static ImageSubsystem GetImageSubsystem(BinaryReader stream)
        {
            try
            {
                var baseOffset = stream.BaseStream.Position;

                // WORD IMAGE_DOS_HEADER.e_magic = 0x4D5A (MZ)
                stream.BaseStream.Seek(baseOffset + 0, SeekOrigin.Begin);
                var dosMagic = stream.ReadUInt16();
                if (dosMagic != 0x5A4D)
                    return ImageSubsystem.Unknown;

                // LONG IMAGE_DOS_HEADER.e_lfanew
                stream.BaseStream.Seek(baseOffset + 60, SeekOrigin.Begin);
                var ntHeaderOffset = stream.ReadUInt32();
                if (ntHeaderOffset == 0)
                    return ImageSubsystem.Unknown;

                // DWORD IMAGE_NT_HEADERS.Signature = 0x00004550 (PE..)
                stream.BaseStream.Seek(baseOffset + ntHeaderOffset, SeekOrigin.Begin);
                var ntMagic = stream.ReadUInt32();
                if (ntMagic != 0x00004550)
                    return ImageSubsystem.Unknown;

                // WORD IMAGE_OPTIONAL_HEADER.Magic = 0x010A (32bit header) or 0x020B (64bit header)
                stream.BaseStream.Seek(baseOffset + ntHeaderOffset + 24, SeekOrigin.Begin);
                var optionalMagic = stream.ReadUInt16();
                if (optionalMagic != 0x010B && optionalMagic != 0x020B)
                    return ImageSubsystem.Unknown;

                // WORD IMAGE_OPTIONAL_HEADER.Subsystem
                // Note: There might need to be an adjustment for ImageBase being ULONGLONG in the 64bit header though this doesn't actually seem to be true.
                stream.BaseStream.Seek(baseOffset + ntHeaderOffset + 92, SeekOrigin.Begin);
                var peSubsystem = stream.ReadUInt16();

                return (ImageSubsystem)peSubsystem;
            }
            catch (EndOfStreamException)
            {
                return ImageSubsystem.Unknown;
            }
        }
        #endregion

        private static System.Threading.CancellationTokenSource ResetCancellationTokenSource(System.Threading.CancellationTokenSource cts)
        {
            if (cts != null)
            {
                cts.Dispose();
            }
            // Create a new cancellation token source so that can cancel all the tokens again 
            return new System.Threading.CancellationTokenSource();
        }

        private void ComboBoxTimetable_EnabledChanged(object sender, EventArgs e)
        {
            //Debrief Eval TTActivity.
            if (!comboBoxTimetable.Enabled)
            {
                //comboBoxTimetable.Enabled == false then we erase comboBoxTimetable and comboBoxTimetableTrain data.
                if (comboBoxTimetable.Items.Count > 0)
                {
                    comboBoxTimetable.Items.Clear();
                    comboBoxTimetableTrain.Items.Clear();
                    buttonStart.Enabled = false;
                }
            }
            //TO DO: Debrief Eval TTActivity
        }
    }
    internal static class Win32
    {
        /// <summary>
        /// Lock or relase the window for updating.
        /// </summary>
        [DllImport("user32")]
        public static extern int LockWindowUpdate(IntPtr hwnd);

        public static void Suspend(this Control control)
        {
            LockWindowUpdate(control.Handle);
        }

        public static void Resume(this Control control)
        {
            LockWindowUpdate(IntPtr.Zero);
        }
    }
}
