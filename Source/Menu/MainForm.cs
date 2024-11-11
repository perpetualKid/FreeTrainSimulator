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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Shim;
using FreeTrainSimulator.Models.Simplified;
using FreeTrainSimulator.Online.Client;
using FreeTrainSimulator.Updater;

using GetText;
using GetText.WindowsForms;

using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Settings;

namespace Orts.Menu
{
    public partial class MainForm : Form
    {
        [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]{3,9}$")]//4-10 characters, digits or _, not starting with a digit
        private static partial Regex RegexUserName();

        public enum UserAction
        {
            SingleplayerNewGame,
            SingleplayerResumeSave,
            SingleplayerReplaySave,
            SingleplayerReplaySaveFromSave,
            MultiplayerClient,
            SinglePlayerTimetableGame,
            SinglePlayerResumeTimetableGame,
            MultiplayerServerResumeSave,
            MultiplayerClientResumeSave
        }

        private static readonly string[] coreExecutables = new[] {
                    "FreeTrainSimulator.exe",
                    "Menu.exe",
                    "ActivityRunner.exe",
                    "Updater.exe",
                };

        private static readonly string[] documentFiles = new[]
        {
            ".pdf", ".doc", ".docx", ".pptx", ".txt"
        };

        private bool initialized;
        private UserSettings settings;
        private IEnumerable<TimetableInfo> timetableSets = Array.Empty<TimetableInfo>();
        private IEnumerable<WeatherFileInfo> timetableWeatherFileSet = Array.Empty<WeatherFileInfo>();
        private CancellationTokenSource ctsModelLoading;
        private CancellationTokenSource ctsTimeTableLoading;
        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly ResourceManager resources = new ResourceManager("Orts.Menu.Properties.Resources", typeof(MainForm).Assembly);
        private UpdateManager updateManager;
        private readonly Image elevationIcon;
        private int detailUpdater;

        #region current selection to be passed a startup parameters
        internal ProfileModel SelectedProfile { get; private set; }
        // Base items
        internal FolderModel SelectedFolder { get; private set; }
        internal RouteModelCore SelectedRoute { get; private set; }

        // Activity mode items
        internal ActivityModelCore SelectedActivity { get; private set; }
        internal WagonSetModel SelectedConsist { get; private set; }
        internal PathModelCore SelectedPath { get; private set; }

        // Timetable mode items
        internal TimetableInfo SelectedTimetableSet => (TimetableInfo)comboBoxTimetableSet.SelectedItem;
        internal TimetableFile SelectedTimetable => (TimetableFile)comboBoxTimetable.SelectedItem;
        internal TrainInformation SelectedTimetableTrain => (TrainInformation)comboBoxTimetableTrain.SelectedItem;
        internal int SelectedTimetableDay => initialized ? (comboBoxTimetableDay.SelectedItem as ComboBoxItem<int>).Value : 0;
        internal WeatherFileInfo SelectedWeatherFile => (WeatherFileInfo)comboBoxTimetableWeatherFile.SelectedItem;
        internal Consist SelectedTimetableConsist { get; private set; }
        internal PathModelCore SelectedTimetablePath { get; private set; }

        internal string SelectedSaveFile { get; private set; }
        internal UserAction SelectedAction { get; private set; }
        #endregion

        private Catalog catalog;
        private readonly ObjectPropertiesStore store = new ObjectPropertiesStore();

        #region Main Form
        public MainForm()
        {
            InitializeComponent();

            // Set title to show revision or build info.
            Text = $"{RuntimeInfo.ProductName} {VersionInfo.Version}";
#if DEBUG
            Text += " (debug)";
#endif
            panelModeTimetable.Location = panelModeActivity.Location;
            UpdateEnabled();
            using (Icon icon = new Icon(SystemIcons.Shield, SystemInformation.SmallIconSize))
                elevationIcon = icon.ToBitmap();

            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                ctsModelLoading?.Cancel();
                ctsModelLoading?.Dispose();
                ctsTimeTableLoading?.Cancel();
                ctsTimeTableLoading?.Dispose();
                elevationIcon?.Dispose();
                updateManager?.Dispose();
            }
            base.Dispose(disposing);
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            ImmutableArray<string> options = Environment.GetCommandLineArgs().
                Where(a => a.StartsWith('-') || a.StartsWith('/')).Select(a => a[1..]).ToImmutableArray();
            settings = new UserSettings(options);

            updateManager = new UpdateManager(settings);

            Task profileTask = ProfileChanged();

            linkLabelUpdate.Visible = false;
            LoadLanguage();
            LoadOptions();
            Task updateTask = Task.CompletedTask;

            if (!initialized)
            {
                updateTask = CheckForUpdateAsync();
                LoadToolsAndDocuments();

                comboBoxStartTime.DataSourceFromList(Enumerable.Range(0, 24), (hour) => $"{hour:00}:00:00");
                comboBoxStartSeason.DataSourceFromEnum<SeasonType>();
                comboBoxStartWeather.DataSourceFromEnum<WeatherType>();
                comboBoxTimetableSeason.DataSourceFromEnum<SeasonType>();
                comboBoxTimetableWeather.DataSourceFromEnum<WeatherType>();
                comboBoxTimetableDay.DataSourceFromList(Enumerable.Range(0, 7), (day) => CultureInfo.CurrentUICulture.DateTimeFormat.DayNames[day]);
            }

            ShowTimetableEnvironment();

            await Task.WhenAll(profileTask, updateTask).ConfigureAwait(true);
            initialized = true;

            UpdateEnabled();
        }

        private IEnumerable<ToolStripItem> LoadTools()
        {
            return Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(RuntimeInfo.ApplicationFolder), "*.exe").
                Where(fileName => (!coreExecutables.Contains(System.IO.Path.GetFileName(fileName), StringComparer.InvariantCultureIgnoreCase))).
                Select(fileName =>
                {
                    FileVersionInfo toolInfo = FileVersionInfo.GetVersionInfo(fileName);
                    // Skip any executable that isn't part of this product (e.g. Visual Studio hosting files).
                    if (toolInfo.ProductName != RuntimeInfo.ProductName)
                        return null;
                    // Remove the product name from the tool's name
                    string toolName = string.Join(" ", toolInfo.Comments.Split(' ').Except(RuntimeInfo.ProductName.Split(' ')));
                    return new ToolStripMenuItem(toolName, null, (object sender2, EventArgs e2) =>
                    {
                        string toolPath = (sender2 as ToolStripItem).Tag as string;
                        bool toolIsConsole = false;
                        using (BinaryReader reader = new BinaryReader(File.OpenRead(toolPath)))
                        {
                            toolIsConsole = GetImageSubsystem(reader) == ImageSubsystem.WindowsConsole;
                        }
                        if (toolIsConsole)
                        {
                            if (toolName.Equals("MultiPlayer Hub", StringComparison.OrdinalIgnoreCase))
                                Process.Start("cmd", $"/k \"{toolPath}\" {settings.Multiplayer_Port}");
                            else
                                Process.Start("cmd", $"/k \"{toolPath}\"");
                        }
                        else
                            Process.Start(toolPath);
                    }
                    )
                    { Tag = fileName };
                }).Where(t => t != null);
        }

        private static IEnumerable<ToolStripItem> LoadDocuments()
        {
            return Directory.Exists(RuntimeInfo.DocumentationFolder)
                ? Directory.EnumerateFiles(RuntimeInfo.DocumentationFolder).
                    Union(Directory.Exists(System.IO.Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.Name)) ?
                        Directory.EnumerateFiles(System.IO.Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.Name)) : Array.Empty<string>()).
                    Union(Directory.Exists(System.IO.Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)) ?
                        Directory.EnumerateFiles(System.IO.Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)) : Array.Empty<string>()).
                    Where(fileName => documentFiles.Contains(System.IO.Path.GetExtension(fileName), StringComparer.InvariantCultureIgnoreCase)).
                    Select(fileName =>
                    {
                        return new ToolStripMenuItem(System.IO.Path.GetFileName(fileName), null, (object sender2, EventArgs e2) =>
                        {
                            string docPath = (sender2 as ToolStripItem).Tag as string;
                            Process.Start(new ProcessStartInfo { FileName = docPath, UseShellExecute = true });
                        })
                        { Tag = fileName };
                    }).Where(d => d != null)
                : Enumerable.Empty<ToolStripItem>();
        }

        private void LoadToolsAndDocuments()
        {
            contextMenuStripTools.Items.Clear();
            contextMenuStripDocuments.Items.Clear();
            contextMenuStripTools.Items.Add(testingToolStripMenuItem);
            contextMenuStripTools.Items.AddRange(LoadTools().OrderBy(tool => tool.Text).ToArray());
            contextMenuStripDocuments.Items.AddRange(LoadDocuments().OrderBy(doc => doc.Text).ToArray());
            // Documents button will be disabled if Documentation folder is not present.
            buttonDocuments.Enabled = contextMenuStripDocuments.Items.Count > 0;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveOptions();
            if (null != ctsModelLoading && !ctsModelLoading.IsCancellationRequested)
                ctsModelLoading.Cancel();

            // Remove any deleted saves
            if (Directory.Exists(UserSettings.DeletedSaveFolder))
                Directory.Delete(UserSettings.DeletedSaveFolder, true);   // true removes all contents as well as folder
        }

        private async Task CheckForUpdateAsync()
        {
            string availableVersion = await updateManager.GetBestAvailableVersionString(false).ConfigureAwait(true);
            if (updateManager.LastCheckError != null)
            {
                linkLabelUpdate.Text = catalog.GetString("Update check failed");
                linkLabelUpdate.Visible = true;
                linkLabelUpdate.Tag = null;
            }
            else
            {
                if (!string.IsNullOrEmpty(availableVersion))
                {
                    linkLabelUpdate.Text = catalog.GetString($"Update to {UpdateManager.NormalizedPackageVersion(availableVersion)}");
                    linkLabelUpdate.Tag = availableVersion;
                    linkLabelUpdate.Visible = true;
                    linkLabelUpdate.Image = updateManager.UpdaterNeedsElevation ? elevationIcon : null;
                    linkLabelUpdate.AutoSize = true;
                    linkLabelUpdate.Left = panelDetails.Right - linkLabelUpdate.Width - elevationIcon.Width;
                    linkLabelUpdate.AutoSize = false;
                    linkLabelUpdate.Width = panelDetails.Right - linkLabelUpdate.Left;
                }
                else
                {
                    linkLabelUpdate.Visible = false;
                }
            }
        }

        private void LoadLanguage()
        {
            Localizer.Revert(this, store);
            CatalogManager.Reset();

            if (!string.IsNullOrEmpty(settings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(settings.Language);
                }
                catch (CultureNotFoundException exception)
                {
                    Trace.WriteLine(exception.Message);
                }
            }
            else
            {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InstalledUICulture;
            }
            catalog = CatalogManager.Catalog;
            Localizer.Localize(this, catalog, store);
        }
        #endregion

        #region selection updates
        private async void ComboBoxFolder_SelectionChangeCommitted(object sender, EventArgs e)
        {
            await FolderChanged(comboBoxFolder.SelectedValue as FolderModel).ConfigureAwait(true);
        }

        private async void ComboBoxRoute_SelectionChangeCommitted(object sender, EventArgs e)
        {
            await RouteChanged(comboBoxRoute.SelectedValue as RouteModelCore).ConfigureAwait(true);
        }

        private void RadioButtonMode_CheckedChanged(object sender, EventArgs e)
        {
            ActivityType FromSelection() => radioButtonModeTimetable.Checked
                    ? ActivityType.TimeTable
                    : radioButtonModeActivity.Checked ? ActivityType.Activity : ActivityType.None;

            currentSelections = currentSelections with { ActivityType = FromSelection() };
            panelModeActivity.Visible = !(panelModeTimetable.Visible = currentSelections.ActivityType == ActivityType.TimeTable);
            UpdateEnabled();
            ShowDetails();
        }

        private void ComboBoxActivity_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ActivityChanged(comboBoxActivity.SelectedValue as ActivityModelCore);
        }

        private void ComboBoxLocomotive_SelectionChangeCommitted(object sender, EventArgs e)
        {
            LocomotiveChanged((comboBoxLocomotive.SelectedItem as ComboBoxItem<IGrouping<string, WagonSetModel>>)?.Value.FirstOrDefault());
        }

        private void ComboBoxConsist_SelectionChangeCommitted(object sender, EventArgs e)
        {
            LocomotiveChanged((comboBoxConsist.SelectedItem as ComboBoxItem<WagonSetModel>)?.Value);
        }

        private void ComboBoxStartAt_SelectionChangeCommitted(object sender, EventArgs e)
        {
            PathChanged((comboBoxStartAt.SelectedItem as ComboBoxItem<IGrouping<string, PathModelCore>>)?.Value.FirstOrDefault());
        }

        private void ComboBoxHeadTo_SelectionChangeCommitted(object sender, EventArgs e)
        {
            PathChanged((comboBoxHeadTo.SelectedItem as ComboBoxItem<PathModelCore>)?.Value);
        }
        #endregion

        #region Environment
        private void ComboBoxStartTime_TextUpdated(object sender, EventArgs e)
        {
            if (TimeOnly.TryParse(comboBoxStartTime.Text, out TimeOnly startTime))
            {
                currentSelections = currentSelections with
                {
                    StartTime = startTime,
                };
            }
        }

        private void ComboBoxStartTime_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (TimeOnly.TryParse(comboBoxStartTime.Text, out TimeOnly startTime))
            {
                currentSelections = currentSelections with
                {
                    StartTime = startTime,
                };
            }
        }


        private void ComboBoxStartSeason_SelectionChangeCommitted(object sender, EventArgs e)
        {
            currentSelections = currentSelections with { Season = ((SeasonType)comboBoxStartSeason.SelectedValue) };
        }

        private void ComboBoxStartWeather_SelectionChangeCommitted(object sender, EventArgs e)
        {
            currentSelections = currentSelections with { Weather = ((WeatherType)comboBoxStartWeather.SelectedValue) };
        }
        #endregion

        #region Timetable Sets
        private void ComboBoxTimetableSet_SelectedIndexChanged(object sender, EventArgs e)
        {
            int updater = Interlocked.CompareExchange(ref detailUpdater, 1, 0);
            UpdateTimetableSet();
            ShowTimetableList();
            if (updater == 0)
            {
                ShowDetails();
                detailUpdater = 0;
            }
        }
        #endregion

        #region Timetables
        private void ComboBoxTimetable_SelectedIndexChanged(object sender, EventArgs e)
        {
            int updater = Interlocked.CompareExchange(ref detailUpdater, 1, 0);
            ShowTimetableTrainList();
            if (updater == 0)
            {
                ShowDetails();
                detailUpdater = 0;
            }
        }
        #endregion

        #region Timetable Trains
        private async void ComboBoxTimetableTrain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxTimetableTrain.SelectedItem is TrainInformation selectedTrain)
            {
                int updater = Interlocked.CompareExchange(ref detailUpdater, 1, 0);
                SelectedTimetableConsist = Consist.GetConsist(SelectedFolder.MstsContentFolder(), selectedTrain.LeadingConsist, selectedTrain.ReverseConsist);

                PathModelCore pathModel = string.IsNullOrEmpty(selectedTrain.Path) ? null : await SelectedRoute.PathModel(selectedTrain.Path, CancellationToken.None).ConfigureAwait(false);
                SelectedTimetablePath = pathModel == null || !pathModel.PlayerPath ? null : pathModel;

                if (updater == 0)
                {
                    ShowDetails();
                    detailUpdater = 0;
                }
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

        private void ComboBoxTimetableWeatherFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableWeatherSet();
        }
        #endregion

        #region Multiplayer
        private void TextBoxMPUser_TextChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
        }

        private bool CheckUserName(string text)
        {
            Match match = RegexUserName().Match(text);
            if (!match.Success)
            {
                MessageBox.Show(catalog.GetString("User name must be 4-10 characters (chars, digits, _) long, cannot contain space, ', \" or - and must not start with a digit."), RuntimeInfo.ProductName);
                return false;
            }
            return true;
        }

        #endregion

        #region Misc. buttons and options
        private async void LinkLabelUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (updateManager.LastCheckError != null)
            {
                MessageBox.Show(catalog.GetString($"The update check failed due to an error:\n\n{updateManager.LastCheckError.Message} {updateManager.LastCheckError.InnerException?.Message}"), RuntimeInfo.ProductName);
                return;
            }

            try
            {
                await updateManager.RunUpdateProcess(linkLabelUpdate.Tag as string).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                MessageBox.Show(catalog.GetString($"The update failed due to an error:\n\n{exception.Message} {exception.InnerException?.Message}"), RuntimeInfo.ProductName);
                return;
                throw;
            }
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
            using (TestingForm form = new TestingForm(SelectedProfile, RuntimeInfo.ActivityRunnerExecutable))
            {
                _ = form.ShowDialog(this);
            }
        }

        private async void ButtonOptions_Click(object sender, EventArgs e)
        {
            SaveOptions();
            await ShowOptionsForm(false).ConfigureAwait(true);
        }

        private async ValueTask ShowOptionsForm(bool initialSetup)
        {
            if (InvokeRequired)
            {
                Invoke(ShowOptionsForm, initialSetup);
                return;
            }
            using (OptionsForm form = new OptionsForm(settings, updateManager, initialSetup))
            {
                switch (form.ShowDialog(this))
                {
                    case DialogResult.OK:
                        await SelectedProfile.Setup(settings.FolderSettings.Folders.Select(folder => (folder.Key, folder.Value)), CancellationToken.None).ConfigureAwait(true);
                        await ProfileChanged().ConfigureAwait(true);
                        break;
                    case DialogResult.Retry: //Language has changed
                        LoadLanguage();
                        LoadToolsAndDocuments();
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

        private void ButtonResumeMP_Click(object sender, EventArgs e)
        {
            OpenResumeForm(true);
        }

        private void OpenResumeForm(bool multiplayer)
        {
            if (radioButtonModeTimetable.Checked)
            {
                SelectedAction = UserAction.SinglePlayerTimetableGame;
            }
            else if (!multiplayer)
            {
                SelectedAction = UserAction.SingleplayerNewGame;
            }
            else
            {
                SelectedAction = UserAction.MultiplayerClient;
            }

            // if timetable mode but no timetable selected - no action
            if (SelectedAction == UserAction.SinglePlayerTimetableGame && (SelectedTimetableSet == null || multiplayer))
            {
                return;
            }

            using (ResumeForm form = new ResumeForm(settings, SelectedRoute, SelectedAction, SelectedActivity, SelectedTimetableSet, SelectedFolder.GetRoutes(CancellationToken.None).Result))
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

        private void ButtonStartMP_Click(object sender, EventArgs e)
        {
            if (!CheckUserName(textBoxMPUser.Text))
                return;
            SaveOptions();
            SelectedAction = UserAction.MultiplayerClient;
            DialogResult = DialogResult.OK;
        }

        private async void ButtonConnectivityTest_Click(object sender, EventArgs e)
        {
            string[] mpHost = textBoxMPHost.Text.Split(':');
            settings.Multiplayer_Host = mpHost[0];
            settings.Multiplayer_Port = mpHost.Length > 1 && int.TryParse(mpHost[1], out int port) ? port : (int)settings.GetDefaultValue("Multiplayer_Port");

            ConnectivityClient client = new ConnectivityClient(settings.Multiplayer_Host, settings.Multiplayer_Port, CancellationToken.None, true);
            bool result = await client.Ping().ConfigureAwait(true);
            MessageBox.Show($"Connectivity test {(result ? "succeeded" : "failed")}!", "Multiplayer Connection", MessageBoxButtons.OK, result ? MessageBoxIcon.Information : MessageBoxIcon.Exclamation);
        }

        #endregion

        #region Options
        private void LoadOptions()
        {
            checkBoxWarnings.Checked = settings.Logging;

            textBoxMPUser.Text = settings.Multiplayer_User;
            textBoxMPHost.Text = settings.Multiplayer_Host + ":" + settings.Multiplayer_Port;
        }

        private void SaveOptions()
        {
            settings.Logging = checkBoxWarnings.Checked;
            settings.Multiplayer_User = textBoxMPUser.Text;

            string[] mpHost = textBoxMPHost.Text.Split(':');
            settings.Multiplayer_Host = mpHost[0];
            if (mpHost.Length > 1 && int.TryParse(mpHost[1], out int port))
            {
                settings.Multiplayer_Port = port;
            }
            else
            {
                settings.Multiplayer_Port = (int)settings.GetDefaultValue("Multiplayer_Port");
            }
            // Activity mode items / Explore mode items
            //settings.MenuSelection[MenuSelection.Activity] = SelectedActivity?.FilePath ?? SelectedActivity?.Name ?? string.Empty;
            //settings.MenuSelection[MenuSelection.Locomotive] = SelectedActivity is ExploreActivity && (comboBoxLocomotive.SelectedItem as Locomotive)?.FilePath != null ? (comboBoxLocomotive.SelectedItem as Locomotive).FilePath : string.Empty;
            //settings.MenuSelection[MenuSelection.Consist] = SelectedActivity is ExploreActivity && SelectedConsist != null ? SelectedConsist.FilePath : string.Empty;
            // Timetable mode
            settings.MenuSelection[MenuSelection.TimetableSet] = SelectedTimetableSet?.FileName ?? string.Empty;
            settings.MenuSelection[MenuSelection.Timetable] = SelectedTimetable?.Description ?? string.Empty;
            settings.MenuSelection[MenuSelection.Train] = SelectedTimetableTrain?.Column.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            settings.MenuSelection[MenuSelection.Day] = SelectedTimetableDay.ToString(CultureInfo.InvariantCulture);

            settings.Save();

            _ = UpdateSelections();
        }

        private async ValueTask UpdateSelections()
        {
            currentSelections = await SelectedProfile.UpdateSelectionsModel(currentSelections, CancellationToken.None).ConfigureAwait(false);
        }
        #endregion

        #region Enabled state
        private void UpdateEnabled()
        {
            if (InvokeRequired)
            {
                Invoke(UpdateEnabled);
                return;
            }

            bool explorerActivity = currentSelections != null && (currentSelections.ActivityType is ActivityType.ExploreActivity or ActivityType.Explorer);

            comboBoxFolder.Enabled = comboBoxFolder.Items.Count > 0;
            comboBoxRoute.Enabled = comboBoxRoute.Items.Count > 0;
            comboBoxActivity.Enabled = comboBoxActivity.Items.Count > 0;
            comboBoxLocomotive.Enabled = comboBoxLocomotive.Items.Count > 0 && explorerActivity;
            comboBoxConsist.Enabled = comboBoxConsist.Items.Count > 0 && explorerActivity;
            comboBoxStartAt.Enabled = comboBoxStartAt.Items.Count > 0 && explorerActivity;
            comboBoxHeadTo.Enabled = comboBoxHeadTo.Items.Count > 0 && explorerActivity;
            comboBoxStartTime.Enabled = comboBoxStartSeason.Enabled = comboBoxStartWeather.Enabled = explorerActivity;
            comboBoxTimetable.Enabled = comboBoxTimetableSet.Items.Count > 0;
            comboBoxTimetableTrain.Enabled = comboBoxTimetable.Items.Count > 0;
            comboBoxTimetableWeatherFile.Enabled = comboBoxTimetableWeatherFile.Items.Count > 0;
            //Avoid to Start with a non valid Activity/Locomotive/Consist.
            buttonResume.Enabled = buttonStart.Enabled = radioButtonModeActivity.Checked &&
                comboBoxActivity.Text.Length > 0 && comboBoxActivity.Text[0] != '<' && comboBoxLocomotive.Text.Length > 0 && comboBoxLocomotive.Text[0] != '<' ?
                SelectedActivity != null && (!(explorerActivity) || (comboBoxConsist.Items.Count > 0 && comboBoxHeadTo.Items.Count > 0)) :
                SelectedTimetableTrain != null;
            buttonConnectivityTest.Enabled = buttonStartMP.Enabled = buttonStart.Enabled && !string.IsNullOrEmpty(textBoxMPUser.Text) && !string.IsNullOrEmpty(textBoxMPHost.Text);
        }
        #endregion

        #region populate dropdown boxes
        private void SetupFoldersDropdown(FrozenSet<FolderModel> contentFolders)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupFoldersDropdown, contentFolders);
                return;
            }
            comboBoxFolder.EnableComboBoxItemDataSource(contentFolders.OrderBy(f => f.Name).Select(f => new ComboBoxItem<FolderModel>(f.Name, f)));

            if (SelectedProfile.ContentFolders.Count > 0)
            {
                UpdateEnabled();
                _ = comboBoxFolder.Focus();
            }
        }

        private void SetupRoutesDropdown(FrozenSet<RouteModelCore> routeModels)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupRoutesDropdown, routeModels);
                return;
            }

            comboBoxRoute.EnableComboBoxItemDataSource(routeModels.OrderBy(r => r.Name).Select(r => new ComboBoxItem<RouteModelCore>(r.Name, r)));
            UpdateEnabled();
        }

        private void SetupActivitiesDropdown(FrozenSet<ActivityModelCore> activities)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupActivitiesDropdown, activities);
                return;
            }

            comboBoxActivity.EnableComboBoxItemDataSource(activities.OrderBy(a => a.Name).Select(a => new ComboBoxItem<ActivityModelCore>(a.Name, a)));
            UpdateEnabled();
        }

        private void SetupLocomotivesDropdown(FrozenSet<WagonSetModel> consists)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupLocomotivesDropdown, consists);
                return;
            }

            comboBoxLocomotive.EnableComboBoxItemDataSource(consists.Where(c => c.Locomotive != null).GroupBy(c => c.Locomotive.Name).OrderBy(g => g.Key).
                Select(g => new ComboBoxItem<IGrouping<string, WagonSetModel>>($"{g.Key} ({g.Count()} " + catalog.GetPluralString("train set", "train sets", g.Count()) + ")", g)));
        }

        private void SetupConsistsDropdown()
        {
            if (InvokeRequired)
            {
                Invoke(SetupConsistsDropdown);
                return;
            }
            comboBoxConsist.EnableComboBoxItemDataSource((comboBoxLocomotive.SelectedValue as IGrouping<string, WagonSetModel>)?.OrderBy(w => w.Name).Select(w => new ComboBoxItem<WagonSetModel>(w.Name, w)));
        }

        private void SetupPathStartDropdown(FrozenSet<PathModelCore> pathModels)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupPathStartDropdown, pathModels);
                return;
            }

            comboBoxStartAt.EnableComboBoxItemDataSource(pathModels.Where(p => p.PlayerPath).GroupBy(p => p.Start).OrderBy(g => g.Key).
                Select(g => new ComboBoxItem<IGrouping<string, PathModelCore>>($"{g.Key} {g.Count()} " + catalog.GetPluralString("train path", "train paths", g.Count()) + ")", g)));
        }

        private void SetupPathEndDropdown()
        {
            if (InvokeRequired)
            {
                Invoke(SetupPathEndDropdown);
                return;
            }

            comboBoxHeadTo.EnableComboBoxItemDataSource((comboBoxStartAt.SelectedValue as IGrouping<string, PathModelCore>)?.OrderBy(p => p.Name).Select(p => new ComboBoxItem<PathModelCore>($"{p.End} ({p.Name})", p)));
        }

        private void SetupActivityFromSelection(ProfileSelectionsModel profileSelections)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupActivityFromSelection, profileSelections);
                return;
            }

            bool exploreActivity = profileSelections != null && (profileSelections.ActivityType is ActivityType.ExploreActivity or ActivityType.Explorer);
            bool activity = exploreActivity || (profileSelections?.ActivityType is ActivityType.Activity);
            radioButtonModeTimetable.Checked = !(radioButtonModeActivity.Checked = activity);

            // values
            _ = comboBoxStartSeason.SetComboBoxItem((ComboBoxItem<SeasonType> cbi) => cbi.Value == profileSelections.Season);
            _ = comboBoxStartWeather.SetComboBoxItem((ComboBoxItem<WeatherType> cbi) => cbi.Value == profileSelections.Weather);

            comboBoxStartTime.Text = $"{profileSelections.StartTime:HH\\:mm\\:ss}";
            comboBoxStartTime.Tag = profileSelections.StartTime;

            if (activity)
            {
                _ = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Name, profileSelections.ActivityName, StringComparison.OrdinalIgnoreCase));
            }
            else if (exploreActivity)
            {
                _ = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => activityItem.ActivityType == profileSelections.ActivityType);
            }

            _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Any(w => string.Equals(w.Id, profileSelections.WagonSetName, StringComparison.OrdinalIgnoreCase)));
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((ComboBoxItem<WagonSetModel> cbi) => string.Equals(cbi.Value.Id, profileSelections.WagonSetName, StringComparison.OrdinalIgnoreCase));

            _ = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Where(p => p.Id == profileSelections.PathName).Any());
            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((ComboBoxItem<PathModelCore> cbi) => string.Equals(profileSelections.PathName, cbi.Value.Id, StringComparison.OrdinalIgnoreCase));

            //enabled
            UpdateEnabled();

            ShowDetails();

        }

        #endregion

        #region Timetable Set list
        private async Task LoadTimetableSetListAsync()
        {
            ctsTimeTableLoading = await ctsTimeTableLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
            ShowTimetableSetList();

            FolderModel selectedFolder = SelectedFolder;
            RouteModelCore selectedRoute = SelectedRoute;
            try
            {
                timetableSets = (await TimetableInfo.GetTimetableInfo(selectedRoute.MstsRouteFolder(), ctsTimeTableLoading.Token).ConfigureAwait(true)).OrderBy(tt => tt.Description);
                timetableWeatherFileSet = (await WeatherFileInfo.GetTimetableWeatherFiles(selectedRoute.MstsRouteFolder(), ctsTimeTableLoading.Token).ConfigureAwait(true)).OrderBy(a => a.ToString());
            }
            catch (TaskCanceledException)
            {
                timetableSets = Array.Empty<TimetableInfo>();
                timetableWeatherFileSet = Array.Empty<WeatherFileInfo>();
            }
            ShowTimetableSetList();
            ShowTimetableWeatherSet();
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
            UpdateFromMenuSelection(comboBoxTimetableSet, FreeTrainSimulator.Common.MenuSelection.TimetableSet, (TimetableInfo t) => t.FileName);
            UpdateEnabled();
        }

        private void UpdateTimetableSet()
        {
            if (SelectedTimetableSet != null)
            {
                SelectedTimetableSet.Day = SelectedTimetableDay;
                SelectedTimetableSet.Season = currentSelections.Season;
                SelectedTimetableSet.Weather = currentSelections.Weather;
            }
        }

        private void ShowTimetableWeatherSet()
        {
            comboBoxTimetableWeatherFile.Items.Clear();
            foreach (WeatherFileInfo weatherFile in timetableWeatherFileSet)
            {
                comboBoxTimetableWeatherFile.Items.Add(weatherFile);
                UpdateEnabled();
            }
        }

        private void UpdateTimetableWeatherSet()
        {
            SelectedTimetableSet.WeatherFile = SelectedWeatherFile.FullName;
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
                    comboBoxTimetable.Items.AddRange(SelectedTimetableSet.TimeTables.ToArray());
                }
                finally
                {
                    comboBoxTimetable.EndUpdate();
                }
                UpdateFromMenuSelection(comboBoxTimetable, FreeTrainSimulator.Common.MenuSelection.Timetable, (TimetableFile t) => t.Description);
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

                    List<TrainInformation> trains = SelectedTimetableSet.TimeTables[comboBoxTimetable.SelectedIndex].Trains;
                    trains.Sort();
                    comboBoxTimetableTrain.Items.AddRange(trains.ToArray());
                }
                finally
                {
                    comboBoxTimetableTrain.EndUpdate();
                }
                UpdateFromMenuSelection(comboBoxTimetableTrain, FreeTrainSimulator.Common.MenuSelection.Train, (TrainInformation t) => t.Column.ToString(CultureInfo.InvariantCulture));
            }
            else
                comboBoxTimetableTrain.Items.Clear();

            UpdateEnabled();
        }
        #endregion

        #region Timetable environment
        private void ShowTimetableEnvironment()
        {
            UpdateFromMenuSelectionComboBoxItem(comboBoxTimetableDay, FreeTrainSimulator.Common.MenuSelection.Day, 0);
            UpdateFromMenuSelectionComboBoxItem(comboBoxTimetableSeason, FreeTrainSimulator.Common.MenuSelection.Season, 1);
            UpdateFromMenuSelectionComboBoxItem(comboBoxTimetableWeather, FreeTrainSimulator.Common.MenuSelection.Weather, 0);
        }
        #endregion

        #region Details
        private void ShowDetails()
        {
            if (InvokeRequired)
            {
                Invoke(ShowDetails);
                return;
            }

            ClearDetails();
            if (comboBoxRoute.SelectedValue is RouteModelCore routeModel)
                AddDetailToShow(catalog.GetString("Route: {0}", routeModel.Name), routeModel.Description);

            if (currentSelections.ActivityType != ActivityType.TimeTable)
            {
                if (comboBoxConsist.SelectedValue is WagonSetModel wagonSetModel && wagonSetModel.Locomotive != null)
                {
                    AddDetailToShow(catalog.GetString("Locomotive: {0}", wagonSetModel.Locomotive.Name), wagonSetModel.Locomotive.Description);
                }
                if ((comboBoxActivity.SelectedValue is ActivityModelCore activityModel))
                {
                    AddDetailToShow(catalog.GetString($"Activity: {activityModel.Name}"), activityModel.Description);
                    AddDetailToShow(catalog.GetString("Duration:"), $"{activityModel.Duration}");
                    AddDetailToShow(catalog.GetString("Difficulty:"), $"{activityModel.Difficulty}");
                    AddDetailToShow(catalog.GetString("Activity Briefing"), activityModel.Briefing);
                }
                else if ((comboBoxHeadTo.SelectedValue is PathModelCore pathModel))
                {
                    AddDetailToShow(catalog.GetString("Path: {0}", pathModel.Name),
                        string.Join("\n", catalog.GetString("Starting at: {0}", pathModel.Start),
                    catalog.GetString("Heading to: {0}", pathModel.End)));
                }
            }
            if (radioButtonModeTimetable.Checked)
            {
                if (SelectedTimetableSet != null)
                {
                    AddDetailToShow(catalog.GetString("Timetable set: {0}", SelectedTimetableSet), string.Empty);
                    // Description not shown as no description is available for a timetable set.
                }

                if (SelectedTimetable != null)
                {
                    AddDetailToShow(catalog.GetString("Timetable: {0}", SelectedTimetable), SelectedTimetable.Briefing);
                }
                if (SelectedTimetableTrain != null)
                {
                    if (string.IsNullOrEmpty(SelectedTimetableTrain.Briefing))
                        AddDetailToShow(catalog.GetString("Train: {0}", SelectedTimetableTrain), catalog.GetString("Start time: {0}", SelectedTimetableTrain.StartTimeCleaned));
                    else
                        AddDetailToShow(catalog.GetString("Train: {0}", SelectedTimetableTrain), catalog.GetString("Start time: {0}", SelectedTimetableTrain.StartTimeCleaned) + $"\n{SelectedTimetableTrain.Briefing}");

                    if (SelectedTimetableConsist != null)
                    {
                        AddDetailToShow(catalog.GetString("Consist: {0}", SelectedTimetableConsist.Name), string.Empty);
                        if (SelectedTimetableConsist.Locomotive != null && SelectedTimetableConsist.Locomotive.Description != null)
                        {
                            AddDetailToShow(catalog.GetString("Locomotive: {0}", SelectedTimetableConsist.Locomotive.Name), SelectedTimetableConsist.Locomotive.Description);
                        }
                    }
                    if (SelectedTimetablePath != null)
                    {
                        AddDetailToShow(catalog.GetString("Path: {0}", SelectedTimetablePath.Name), string.Join("\n", catalog.GetString($"Start at: {SelectedTimetablePath.Start}"), catalog.GetString($"Heading to: {SelectedTimetablePath.End}")));
                    }
                }
            }

            FlowDetails();
        }

        private readonly List<Detail> details = new List<Detail>();

        private sealed class Detail
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
            details.Clear();
            panelDetails.Controls.Clear();
        }

        private void AddDetailToShow(string title, string text)
        {
            panelDetails.SuspendLayout();
            Label titleControl = new Label { Margin = new Padding(2), Text = title, UseMnemonic = false, Font = new Font(panelDetails.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            titleControl.Left = titleControl.Margin.Left;
            titleControl.Width = panelDetails.ClientSize.Width - titleControl.Margin.Horizontal - titleControl.PreferredHeight;
            titleControl.Height = titleControl.PreferredHeight;
            titleControl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            panelDetails.Controls.Add(titleControl);

            Button expanderControl = new Button()
            {
                Margin = new Padding(0),
                Text = "",
                FlatStyle = FlatStyle.Flat,
                Left = panelDetails.ClientSize.Width - titleControl.Height - titleControl.Margin.Right
            };
            expanderControl.Width = expanderControl.Height = titleControl.Height;
            expanderControl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            expanderControl.FlatAppearance.BorderSize = 0;
            expanderControl.BackgroundImageLayout = ImageLayout.Center;
            panelDetails.Controls.Add(expanderControl);

            Label summaryControl = new Label()
            {
                Margin = new Padding(2),
                Text = text,
                AutoSize = false,
                UseMnemonic = false,
                UseCompatibleTextRendering = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            summaryControl.Left = summaryControl.Margin.Left;
            summaryControl.Width = panelDetails.ClientSize.Width - summaryControl.Margin.Horizontal;
            summaryControl.Height = MeasureTextHeigth("1\n2\n3\n4\n5", panelDetails.Font, summaryControl.ClientSize);
            panelDetails.Controls.Add(summaryControl);

            // Find out where we need to cut the text to make the summary 5 lines long. Uses a binary search to find the cut point.
            int size = MeasureTextHeigth(text, panelDetails.Font, summaryControl.ClientSize);
            int height = size;
            if (size > summaryControl.Height)
            {
                StringBuilder builder = new StringBuilder(text);
                float index = summaryControl.Text.Length;
                float indexChunk = index;
                while (indexChunk > 0.5f || size > summaryControl.Height)
                {
                    if (indexChunk > 0.5f)
                        indexChunk /= 2;
                    if (size > summaryControl.Height)
                        index -= indexChunk;
                    else
                        index += indexChunk;
                    size = MeasureTextHeigth(builder.ToString(0, (int)index) + "...", panelDetails.Font, summaryControl.ClientSize);
                }
                for (int i = 0; i < 3; i++)
                    builder[(int)index++] = '.';
                summaryControl.Text = builder.ToString(0, (int)index);
            }

            Label descriptionControl = new Label()
            {
                Margin = new Padding(2),
                Text = text,
                AutoSize = false,
                UseMnemonic = false,
                UseCompatibleTextRendering = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            descriptionControl.Left = descriptionControl.Margin.Left;
            descriptionControl.Width = panelDetails.ClientSize.Width - descriptionControl.Margin.Horizontal;
            descriptionControl.Height = height;
            panelDetails.Controls.Add(descriptionControl);

            // Enable the expander only if the full description is longer than the summary. Otherwise, disable the expander.
            expanderControl.Enabled = descriptionControl.Height > summaryControl.Height;
            if (expanderControl.Enabled)
            {
                expanderControl.BackgroundImage = (Image)resources.GetObject("ExpanderClosed", CultureInfo.InvariantCulture);
                expanderControl.Tag = details.Count;
                expanderControl.Click += new EventHandler(ExpanderControl_Click);
            }
            else
            {
                expanderControl.BackgroundImage = (Image)resources.GetObject("ExpanderClosedDisabled", CultureInfo.InvariantCulture);
            }
            details.Add(new Detail(titleControl, expanderControl, summaryControl, descriptionControl));
            panelDetails.ResumeLayout();
        }

        private static int MeasureTextHeigth(string text, Font font, Size clientSize)
        {
            return TextRenderer.MeasureText(text, font, clientSize, TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        }

        private void ExpanderControl_Click(object sender, EventArgs e)
        {
            int index = (int)(sender as Control).Tag;
            details[index].Expanded = !details[index].Expanded;
            details[index].Expander.BackgroundImage = (Image)resources.GetObject(details[index].Expanded ? "ExpanderOpen" : "ExpanderClosed", CultureInfo.InvariantCulture);
            FlowDetails();
        }

        private void FlowDetails()
        {
            int scrollPosition = panelDetails.AutoScrollPosition.Y;
            panelDetails.AutoScrollPosition = Point.Empty;
            panelDetails.AutoScrollMinSize = new Size(0, panelDetails.ClientSize.Height + 1);

            int top = 0;
            foreach (Detail detail in details)
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
        private void UpdateFromMenuSelection<T>(ComboBox comboBox, MenuSelection menuSelection, Func<T, string> map)
        {
            UpdateFromMenuSelection(comboBox, menuSelection, map, default);
        }

        private void UpdateFromMenuSelectionComboBoxItem<T>(ComboBox comboBox, MenuSelection menuSelection, T defaultValue)
        {
            UpdateFromMenuSelection(comboBox, menuSelection, (item => item.Value.ToString()), new ComboBoxItem<T>(string.Empty, defaultValue));
        }

        private void UpdateFromMenuSelection<T>(ComboBox comboBox, MenuSelection menuSelection, Func<T, string> map, T defaultValue)
        {
            if (!string.IsNullOrEmpty(settings.MenuSelection[menuSelection]))
            {
                if (comboBox.DropDownStyle == ComboBoxStyle.DropDown)
                    comboBox.Text = settings.MenuSelection[menuSelection];
                else
                    comboBox.SetComboBoxItem((T item) => string.Equals(map(item), settings.MenuSelection[menuSelection], StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                if (comboBox.DropDownStyle == ComboBoxStyle.DropDown)
                    comboBox.Text = map(defaultValue);
                else if (defaultValue != null)
                    comboBox.SetComboBoxItem((T item) => map(item) == map(defaultValue));
                else if (comboBox.Items.Count > 0)
                    comboBox.SelectedIndex = 0;
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
                long baseOffset = stream.BaseStream.Position;

                // WORD IMAGE_DOS_HEADER.e_magic = 0x4D5A (MZ)
                stream.BaseStream.Seek(baseOffset + 0, SeekOrigin.Begin);
                ushort dosMagic = stream.ReadUInt16();
                if (dosMagic != 0x5A4D)
                    return ImageSubsystem.Unknown;

                // LONG IMAGE_DOS_HEADER.e_lfanew
                stream.BaseStream.Seek(baseOffset + 60, SeekOrigin.Begin);
                uint ntHeaderOffset = stream.ReadUInt32();
                if (ntHeaderOffset == 0)
                    return ImageSubsystem.Unknown;

                // DWORD IMAGE_NT_HEADERS.Signature = 0x00004550 (PE..)
                stream.BaseStream.Seek(baseOffset + ntHeaderOffset, SeekOrigin.Begin);
                uint ntMagic = stream.ReadUInt32();
                if (ntMagic != 0x00004550)
                    return ImageSubsystem.Unknown;

                // WORD IMAGE_OPTIONAL_HEADER.Magic = 0x010A (32bit header) or 0x020B (64bit header)
                stream.BaseStream.Seek(baseOffset + ntHeaderOffset + 24, SeekOrigin.Begin);
                ushort optionalMagic = stream.ReadUInt16();
                if (optionalMagic != 0x010B && optionalMagic != 0x020B)
                    return ImageSubsystem.Unknown;

                // WORD IMAGE_OPTIONAL_HEADER.Subsystem
                // Note: There might need to be an adjustment for ImageBase being ULONGLONG in the 64bit header though this doesn't actually seem to be true.
                stream.BaseStream.Seek(baseOffset + ntHeaderOffset + 92, SeekOrigin.Begin);
                ushort peSubsystem = stream.ReadUInt16();

                return (ImageSubsystem)peSubsystem;
            }
            catch (EndOfStreamException)
            {
                return ImageSubsystem.Unknown;
            }
        }
        #endregion

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
}
