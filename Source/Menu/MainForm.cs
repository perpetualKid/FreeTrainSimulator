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
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Online.Client;
using FreeTrainSimulator.Updater;

using GetText;
using GetText.WindowsForms;

using Orts.Settings;

namespace FreeTrainSimulator.Menu
{
    public partial class MainForm : Form
    {
        [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]{3,9}$")]//4-10 characters, digits or _, not starting with a digit
        private static partial Regex RegexUserName();

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

        private UserSettings settings;
        private CancellationTokenSource ctsProfileLoading;
        private CancellationTokenSource ctsFolderLoading;
        private CancellationTokenSource ctsRouteLoading;
        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly ResourceManager resources = new ResourceManager("FreeTrainSimulator.Menu.Properties.Resources", typeof(MainForm).Assembly);
        private UpdateManager updateManager;
        private readonly Image elevationIcon;
        private readonly string whatsNewLink;

        #region current selection to be passed a startup parameters
        private bool profileFromCurrent;
        internal ProfileModel SelectedProfile { get; private set; }
        internal ProfileSelectionsModel ProfileSelections { get; private set; }
        internal ProfileUserSettingsModel ProfileUserSettings { get; private set; }
        internal ContentModel ContentModel { get; private set; }
        internal string SelectedSaveFile { get; private set; }
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
            whatsNewLink = RuntimeInfo.WhatsNewLinkTemplate.Replace("gitcodeversion", VersionInfo.CodeVersion, StringComparison.OrdinalIgnoreCase);
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
                ctsProfileLoading?.Cancel();
                ctsProfileLoading?.Dispose();
                ctsFolderLoading?.Cancel();
                ctsFolderLoading?.Dispose();
                ctsRouteLoading?.Cancel();
                ctsRouteLoading?.Dispose();
                elevationIcon?.Dispose();
                updateManager?.Dispose();
            }
            base.Dispose(disposing);
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            ImmutableArray<string> options = Environment.GetCommandLineArgs().
                Where(a => a.StartsWith('-') || a.StartsWith('/')).Select(a => a[1..]).ToImmutableArray();

            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            FrozenSet<ProfileModel> profiles = await SelectedProfile.GetProfiles(ctsProfileLoading.Token).ConfigureAwait(true);
            SetupProfilesDropdown(profiles);

            ProfileModel currentProfile = await SelectedProfile.Current(ctsProfileLoading.Token).ConfigureAwait(true);

            settings = new UserSettings(options);
            updateManager = new UpdateManager(await SelectedProfile.AllProfileGetUpdateMode(ctsProfileLoading.Token).ConfigureAwait(true));

            await ProfileChanged(currentProfile).ConfigureAwait(true);

            Task updateTask = CheckForUpdateAsync();
            LoadToolsAndDocuments();

            UpdateEnabled();
            await updateTask.ConfigureAwait(false);
        }

        private IEnumerable<ToolStripItem> LoadTools()
        {
            return Directory.EnumerateFiles(Path.GetDirectoryName(RuntimeInfo.ApplicationFolder), "*.exe").
                Where(fileName => (!coreExecutables.Contains(Path.GetFileName(fileName), StringComparer.InvariantCultureIgnoreCase))).
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
                                Process.Start("cmd", $"/k \"{toolPath}\" {settings.MultiplayerPort}");
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
                    Union(Directory.Exists(Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.Name)) ?
                        Directory.EnumerateFiles(Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.Name)) : Array.Empty<string>()).
                    Union(Directory.Exists(Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)) ?
                        Directory.EnumerateFiles(Path.Combine(RuntimeInfo.DocumentationFolder, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)) : Array.Empty<string>()).
                    Where(fileName => documentFiles.Contains(Path.GetExtension(fileName), StringComparer.InvariantCultureIgnoreCase)).
                    Select(fileName =>
                    {
                        return new ToolStripMenuItem(Path.GetFileName(fileName), null, (object sender2, EventArgs e2) =>
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

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            await SaveOptions().ConfigureAwait(false);
            if (null != ctsProfileLoading && !ctsProfileLoading.IsCancellationRequested)
                await ctsProfileLoading.CancelAsync().ConfigureAwait(false);
            if (null != ctsFolderLoading && !ctsFolderLoading.IsCancellationRequested)
                await ctsFolderLoading.CancelAsync().ConfigureAwait(false);
            if (null != ctsRouteLoading && !ctsRouteLoading.IsCancellationRequested)
                await ctsRouteLoading.CancelAsync().ConfigureAwait(false);

            // Remove any deleted saves
            if (Directory.Exists(UserSettings.DeletedSaveFolder))
                Directory.Delete(UserSettings.DeletedSaveFolder, true);   // true removes all contents as well as folder
        }

        private async Task CheckForUpdateAsync()
        {
            string availableVersion = await updateManager.GetBestAvailableVersionString().ConfigureAwait(true);
            if (updateManager.LastCheckError != null)
            {
                toolStripButtonUpdate.Text = catalog.GetString("Update check failed");
                toolStripButtonUpdate.Enabled = false;
                toolStripButtonUpdate.Tag = null;
                toolStripButtonUpdate.Image = null;
            }
            else
            {
                if (!string.IsNullOrEmpty(availableVersion))
                {
                    toolStripButtonUpdate.Text = catalog.GetString($"Update to {UpdateManager.NormalizedPackageVersion(availableVersion)}");
                    toolStripButtonUpdate.Tag = availableVersion;
                    toolStripButtonUpdate.Enabled = true;
                    toolStripButtonUpdate.Image = updateManager.UpdaterNeedsElevation ? elevationIcon : null;
                }
                else
                {
                    toolStripButtonUpdate.Text = catalog.GetString($"No update available");
                    toolStripButtonUpdate.Enabled = false;
                }
            }
        }

        private void LoadLanguage()
        {
            if (InvokeRequired)
            {
                Invoke(LoadLanguage);
                return;
            }
            Localizer.Revert(this, store);
            CatalogManager.Reset();

            if (!string.IsNullOrEmpty(ProfileUserSettings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(ProfileUserSettings.Language);
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

            //Restore control content which may be dependent or overwritten from Localizer/Revert
            comboBoxStartTime.DataSourceFromList(Enumerable.Range(0, 24), (hour) => $"{hour:00}:00:00");
            comboBoxStartSeason.DataSourceFromEnum<SeasonType>();
            comboBoxStartWeather.DataSourceFromEnum<WeatherType>();
            comboBoxTimetableDay.DataSourceFromList(Enumerable.Range(0, 7), (day) => CultureInfo.CurrentUICulture.DateTimeFormat.DayNames[day]);

            toolStripDropDownButton1.Text = SelectedProfile?.Name;
            toolStripDropDownButton1.ToolTipText = SelectedProfile?.Name;
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
            if (!(sender as RadioButton).Checked)
                return;

            ActivityType FromSelection() => radioButtonModeTimetable.Checked
                    ? ActivityType.TimeTable
                    : (comboBoxActivity.SelectedValue as ActivityModelCore)?.ActivityType ?? ActivityType.Activity;

            ProfileSelections.ActivityType = FromSelection();

            if (ProfileSelections.ActivityType == ActivityType.TimeTable)
            {
                panelModeActivity.Visible = !(panelModeTimetable.Visible = true);
                SetupTimetableFromSelection();
            }
            else
            {
                panelModeActivity.Visible = !(panelModeTimetable.Visible = false);
                SetupActivityFromSelection();
            }

            UpdateEnabled();
            ShowDetails();
        }

        private void ComboBoxActivity_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ActivityChanged(comboBoxActivity.SelectedValue as ActivityModelCore);
            ShowDetails();
        }

        private void ComboBoxLocomotive_SelectionChangeCommitted(object sender, EventArgs e)
        {
            LocomotiveChanged((comboBoxLocomotive.SelectedItem as ComboBoxItem<IGrouping<string, WagonSetModel>>)?.Value.FirstOrDefault(), comboBoxLocomotive.SelectedIndex == 0);
            ShowDetails();
        }

        private void ComboBoxConsist_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ConsistChanged((comboBoxConsist.SelectedItem as ComboBoxItem<WagonSetModel>)?.Value);
            ShowDetails();
        }

        private void ComboBoxStartAt_SelectionChangeCommitted(object sender, EventArgs e)
        {
            PathChanged((comboBoxStartAt.SelectedItem as ComboBoxItem<IGrouping<string, PathModelCore>>)?.Value.FirstOrDefault());
            ShowDetails();
        }

        private void ComboBoxHeadTo_SelectionChangeCommitted(object sender, EventArgs e)
        {
            PathChanged((comboBoxHeadTo.SelectedItem as ComboBoxItem<PathModelCore>)?.Value);
            ShowDetails();
        }
        #endregion

        #region Environment
        private void ComboBoxStartTime_TextUpdated(object sender, EventArgs e)
        {
            if (TimeOnly.TryParse(comboBoxStartTime.Text, out TimeOnly startTime))
            {
                ProfileSelections.StartTime = startTime;
            }
        }

        private void ComboBoxStartTime_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (TimeOnly.TryParse(comboBoxStartTime.Text, out TimeOnly startTime))
            {
                ProfileSelections.StartTime = startTime;
            }
        }

        private void ComboBoxStartSeason_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ProfileSelections.Season = (SeasonType)comboBoxStartSeason.SelectedValue;
        }

        private void ComboBoxStartWeather_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ProfileSelections.Weather = (WeatherType)comboBoxStartWeather.SelectedValue;
        }
        #endregion

        #region Timetable environment
        private void ComboBoxTimetableDay_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ProfileSelections.TimetableDay = (DayOfWeek)comboBoxTimetableDay.SelectedIndex;
        }

        private void ComboBoxTimetableWeatherFile_SelectionChangeCommitted(object sender, EventArgs e)
        {
            TimetableWeatherChanged((comboBoxTimetableWeatherFile.SelectedItem as ComboBoxItem<WeatherModelCore>)?.Value);
        }

        private void ComboBoxTimetableSet_SelectionChangeCommitted(object sender, EventArgs e)
        {
            TimetableSetChanged((comboBoxTimetableSet.SelectedItem as ComboBoxItem<TimetableModel>)?.Value);
            ShowDetails();
        }

        private void ComboBoxTimetable_SelectionChangeCommitted(object sender, EventArgs e)
        {
            TimetableChanged((comboBoxTimetable.SelectedValue as IGrouping<string, TimetableTrainModel>));
            ShowDetails();
        }

        private void ComboBoxTimetableTrain_SelectionChangeCommitted(object sender, EventArgs e)
        {
            TimetableTrainChanged((comboBoxTimetableTrain.SelectedValue as TimetableTrainModel));
            ShowDetails();
        }

        #endregion

        #region Multiplayer
        private void TextBoxMPUser_TextChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
        }

        private static bool CheckUserName(string text)
        {
            Match match = RegexUserName().Match(text);
            if (!match.Success)
            {
                MessageBox.Show(CatalogManager.Catalog.GetString("User name must be 4-10 characters (chars, digits, _) long, cannot contain space, ', \" or - and must not start with a digit."), RuntimeInfo.ProductName);
                return false;
            }
            return true;
        }

        #endregion

        #region Misc. buttons and options
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
            using (TestingForm form = new TestingForm(ContentModel, RuntimeInfo.ActivityRunnerExecutable))
            {
                _ = form.ShowDialog(this);
            }
        }

        private async void ButtonOptions_Click(object sender, EventArgs e)
        {
            await SaveOptions().ConfigureAwait(false);
            await ShowOptionsForm(ContentModel.ContentFolders.Count == 0).ConfigureAwait(true);
        }

        private async Task ShowOptionsForm(bool initialSetup)
        {
            if (InvokeRequired)
            {
                Invoke(ShowOptionsForm, initialSetup);
                return;
            }
            using (OptionsForm form = new OptionsForm(ProfileUserSettings, settings, updateManager, initialSetup, ContentModel))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    ModelConverterProgress progressForm = null;
                    try
                    {
                        ProfileModel currentProfile = SelectedProfile;
                        SelectedProfile = null;
                        progressForm = new ModelConverterProgress();
                        {
                            progressForm.Show(this);
                            Enabled = false;
                            ContentModel = await form.ContentModel.Setup(progressForm, CancellationToken.None).ConfigureAwait(true);
                            await ProfileChanged(currentProfile).ConfigureAwait(true);
                            await Task.Delay(1200).ConfigureAwait(true);
                            progressForm.Close();
                        }
                    }
                    finally
                    {
                        progressForm?.Dispose();
                        Enabled = true;
                        BringToFront();
                    }
                }
            }
        }

        private async void ButtonStart_Click(object sender, EventArgs e)
        {
            if (radioButtonModeActivity.Checked)
            {
                ProfileSelections.GamePlayAction = GamePlayAction.SingleplayerNewGame;
                await SaveOptions().ConfigureAwait(false);
                if (ProfileSelections.ActivityType is ActivityType.Activity or ActivityType.Explorer or ActivityType.ExploreActivity)
                    DialogResult = DialogResult.OK;
            }
            else if (radioButtonModeTimetable.Checked)
            {
                ProfileSelections.GamePlayAction = GamePlayAction.SinglePlayerTimetableGame;
                await SaveOptions().ConfigureAwait(false);
                if (ProfileSelections.ActivityType == ActivityType.TimeTable)
                    DialogResult = DialogResult.OK;
            }
        }

        private async void ButtonResume_Click(object sender, EventArgs e)
        {
            await OpenResumeForm(false).ConfigureAwait(false);
        }

        private async void ButtonResumeMP_Click(object sender, EventArgs e)
        {
            await OpenResumeForm(true).ConfigureAwait(false);
        }

        private async Task OpenResumeForm(bool multiplayer)
        {
            if (radioButtonModeTimetable.Checked)
            {
                ProfileSelections.GamePlayAction = GamePlayAction.SinglePlayerTimetableGame;
            }
            else if (!multiplayer)
            {
                ProfileSelections.GamePlayAction = GamePlayAction.SingleplayerNewGame;
            }
            else
            {
                ProfileSelections.GamePlayAction = GamePlayAction.MultiplayerClient;
            }

            // if timetable mode but no timetable selected - no action
            if (ProfileSelections.GamePlayAction == GamePlayAction.SinglePlayerTimetableGame && (ProfileSelections.TimetableTrain == null || multiplayer))
            {
                return;
            }

            using (ResumeForm form = new ResumeForm(settings, ProfileSelections))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    ProfileSelections.GamePlayAction = form.SelectedAction;
                    await SaveOptions().ConfigureAwait(true);
                    SelectedSaveFile = form.SelectedSaveFile;
                    DialogResult = DialogResult.OK;
                }
            }
        }

        private async void ButtonStartMP_Click(object sender, EventArgs e)
        {
            if (!CheckUserName(textBoxMPUser.Text))
                return;
            ProfileSelections.GamePlayAction = GamePlayAction.MultiplayerClient;
            await SaveOptions().ConfigureAwait(true);
            DialogResult = DialogResult.OK;
        }

        private async void ButtonConnectivityTest_Click(object sender, EventArgs e)
        {
            string[] mpHost = textBoxMPHost.Text.Split(':');
            settings.MultiplayerHost = mpHost[0];
            settings.MultiplayerPort = mpHost.Length > 1 && int.TryParse(mpHost[1], out int port) ? port : (int)settings.GetDefaultValue("Multiplayer_Port");

            ConnectivityClient client = new ConnectivityClient(settings.MultiplayerHost, settings.MultiplayerPort, CancellationToken.None, true);
            bool result = await client.Ping().ConfigureAwait(true);
            MessageBox.Show($"Connectivity test {(result ? "succeeded" : "failed")}!", "Multiplayer Connection", MessageBoxButtons.OK, result ? MessageBoxIcon.Information : MessageBoxIcon.Exclamation);
        }

        #endregion

        #region Options
        private void LoadOptions()
        {
            if (InvokeRequired)
            {
                Invoke(LoadOptions);
                return;
            }
            checkBoxWarnings.Checked = settings.Logging;

            textBoxMPUser.Text = settings.MultiplayerUser;
            textBoxMPHost.Text = settings.MultiplayerHost + ":" + settings.MultiplayerPort;
        }

        private async Task SaveOptions()
        {
            settings.Logging = checkBoxWarnings.Checked;
            settings.MultiplayerUser = textBoxMPUser.Text;

            string[] mpHost = textBoxMPHost.Text.Split(':');
            settings.MultiplayerHost = mpHost[0];
            if (mpHost.Length > 1 && int.TryParse(mpHost[1], out int port))
            {
                settings.MultiplayerPort = port;
            }
            else
            {
                settings.MultiplayerPort = (int)settings.GetDefaultValue("MultiplayerPort");
            }
            settings.Save();

            ProfileSelections = await SelectedProfile.UpdateSettingsModel(ProfileSelections, CancellationToken.None).ConfigureAwait(false);
            ProfileUserSettings = await SelectedProfile.UpdateSettingsModel(ProfileUserSettings, CancellationToken.None).ConfigureAwait(false);
            await SelectedProfile.UpdateCurrent(CancellationToken.None).ConfigureAwait(!false);
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

            bool explorerActivity = ProfileSelections != null && (ProfileSelections.ActivityType is ActivityType.ExploreActivity or ActivityType.Explorer);

            comboBoxFolder.Enabled = comboBoxFolder.Items.Count > 0;
            comboBoxRoute.Enabled = comboBoxRoute.Items.Count > 0;
            comboBoxActivity.Enabled = comboBoxActivity.Items.Count > 0;
            comboBoxLocomotive.Enabled = comboBoxLocomotive.Items.Count > 0 && explorerActivity;
            comboBoxConsist.Enabled = comboBoxConsist.Items.Count > 0 && explorerActivity;
            comboBoxStartAt.Enabled = comboBoxStartAt.Items.Count > 0 && explorerActivity;
            comboBoxHeadTo.Enabled = comboBoxHeadTo.Items.Count > 0 && explorerActivity;
            comboBoxStartTime.Enabled = comboBoxStartSeason.Enabled = comboBoxStartWeather.Enabled = explorerActivity || ProfileSelections?.ActivityType == ActivityType.TimeTable;
            comboBoxTimetableSet.Enabled = comboBoxTimetable.Enabled = comboBoxTimetableSet.Items.Count > 0;
            comboBoxTimetableTrain.Enabled = comboBoxTimetable.Items.Count > 0;
            comboBoxTimetableWeatherFile.Enabled = comboBoxTimetableWeatherFile.Items.Count > 0;
            //Avoid to Start with a non valid Activity/Locomotive/Consist.
            buttonResume.Enabled = buttonStart.Enabled = radioButtonModeActivity.Checked &&
                comboBoxActivity.Text.Length > 0 && comboBoxActivity.Text[0] != '<' && comboBoxLocomotive.Text.Length > 0 && comboBoxLocomotive.Text[0] != '<' ?
                ProfileSelections?.ActivityType is ActivityType.Activity or ActivityType.Explorer or ActivityType.ExploreActivity :
                ProfileSelections?.TimetableTrain != null;
            buttonConnectivityTest.Enabled = buttonStartMP.Enabled = buttonStart.Enabled && !string.IsNullOrEmpty(textBoxMPUser.Text) && !string.IsNullOrEmpty(textBoxMPHost.Text);
        }
        #endregion

        #region profile selections
        private void SetupProfilesDropdown(FrozenSet<ProfileModel> profiles)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupProfilesDropdown, profiles);
                return;
            }
            toolStripDropDownButton1.DropDownItems.Clear();

            foreach (ProfileModel profile in profiles.OrderBy(p => p.Name))
            {
                ToolStripMenuItem profileItem = new ToolStripMenuItem(profile.Name)
                {
                    Tag = profile,
                };
                profileItem.Click += ProfileItem_Click;
                toolStripDropDownButton1.DropDownItems.Add(profileItem);
            }
        }

        private async void ProfileItem_Click(object sender, EventArgs e)
        {
            await SaveOptions().ConfigureAwait(false);
            await ProfileChanged((sender as ToolStripMenuItem).Tag as ProfileModel).ConfigureAwait(false);
        }

        private void UpdateProfilesDropdown(ProfileModel profileModel)
        {
            if (profileModel == null)
                return;

            if (InvokeRequired)
            {
                Invoke(UpdateProfilesDropdown, profileModel);
                return;
            }
            foreach (ToolStripMenuItem toolStripMenuItem in toolStripDropDownButton1.DropDownItems.OfType<ToolStripMenuItem>())
            {
                if (toolStripMenuItem.Tag as ProfileModel == profileModel)
                {
                    toolStripMenuItem.Checked = true;
                    toolStripDropDownButton1.Text = profileModel.Name;
                }
                else
                    toolStripMenuItem.Checked = false;
            }
        }

        #endregion

        #region Activity dropdown selections
        private void SetupFoldersDropdown(FrozenSet<FolderModel> contentFolders)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupFoldersDropdown, contentFolders);
                return;
            }
            comboBoxFolder.EnableComboBoxItemDataSource(contentFolders.OrderBy(f => f.Name).Select(f => new ComboBoxItem<FolderModel>(f.Name, f)));

            if (ContentModel.ContentFolders.Count > 0)
            {
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
        }

        private void SetupActivitiesDropdown(FrozenSet<ActivityModelCore> activities)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupActivitiesDropdown, activities);
                return;
            }

            comboBoxActivity.EnableComboBoxItemDataSource(activities.OrderBy(a => a.Name).Select(a => new ComboBoxItem<ActivityModelCore>(a.Name, a)));
        }

        private void SetupLocomotivesDropdown(FrozenSet<WagonSetModel> consists)
        {
            if (InvokeRequired)
            {
                _ = Invoke(SetupLocomotivesDropdown, consists);
                return;
            }

            comboBoxLocomotive.EnableComboBoxItemDataSource(consists.Where(c => c.Locomotive != null).OrderBy(c => c.Name).GroupBy(c => c.Locomotive.Name).
                Concat(consists.Where(c => c.Locomotive != null).GroupBy(c => consists.Any().Name)).
                OrderBy(g => g.Key).
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
                Select(g => new ComboBoxItem<IGrouping<string, PathModelCore>>($"{g.Key} ({g.Count()} " + catalog.GetPluralString("train path", "train paths", g.Count()) + ")", g)));
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

        #endregion

        #region Timetable dropwon selections
        private void SetupTimetableSetDropdown(FrozenSet<TimetableModel> timetables)
        {
            if (InvokeRequired)
            {
                Invoke(SetupTimetableSetDropdown, timetables);
                return;
            }

            comboBoxTimetableSet.EnableComboBoxItemDataSource(timetables.OrderBy(t => t.Name).Select(t => new ComboBoxItem<TimetableModel>($"{t.Name}", t)));
        }

        private void SetupTimetableDropdown()
        {
            if (InvokeRequired)
            {
                Invoke(SetupTimetableDropdown);
                return;
            }

            comboBoxTimetable.EnableComboBoxItemDataSource((comboBoxTimetableSet.SelectedValue as TimetableModel)?.TimetableTrains.GroupBy(t => t.Group)?.OrderBy(g => g.Key).
                Select(g => new ComboBoxItem<IGrouping<string, TimetableTrainModel>>($"{g.Key} ({g.Count()} train services)", g)));
        }

        private void SetupTimetableTrainsDropdown()
        {
            if (InvokeRequired)
            {
                Invoke(SetupTimetableTrainsDropdown);
                return;
            }

            comboBoxTimetableTrain.EnableComboBoxItemDataSource((comboBoxTimetable.SelectedValue as IGrouping<string, TimetableTrainModel>)?.OrderBy(t => t.StartTime).ThenBy(t => t.Name).
                Select(t => new ComboBoxItem<TimetableTrainModel>($"{t.StartTime} {t.Name}", t)));
        }

        private void SetupTimetableWeatherDropdown(FrozenSet<WeatherModelCore> weatherModels)
        {
            if (InvokeRequired)
            {
                Invoke(SetupTimetableWeatherDropdown, weatherModels);
                return;
            }

            comboBoxTimetableWeatherFile.EnableComboBoxItemDataSource(weatherModels.OrderBy(w => w.Name).Select(w => new ComboBoxItem<WeatherModelCore>(w.Name, w)));
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
            {
                AddDetailToShow(catalog.GetString("Route: {0}", routeModel.Name), routeModel.Description);

                if (ProfileSelections.ActivityType != ActivityType.TimeTable)
                {
                    if (comboBoxConsist.SelectedValue is WagonSetModel wagonSetModel && wagonSetModel.Locomotive != null)
                    {
                        AddDetailToShow(catalog.GetString("Locomotive: {0}", wagonSetModel.Locomotive.Name), wagonSetModel.Locomotive.Description);
                    }
                    if ((comboBoxActivity.SelectedValue is ActivityModelCore activityModel && activityModel.ActivityType == ActivityType.Activity))
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
                else
                {
                    TimetableModel timetableModel;
                    TimetableTrainModel timetableTrainModel;
                    if ((timetableModel = ProfileSelections.SelectedTimetable()) != null)
                    {
                        if (!string.IsNullOrEmpty(ProfileSelections.TimetableName))
                            AddDetailToShow(catalog.GetString($"Timetable: {ProfileSelections.TimetableName}"), timetableModel.Name);
                    }
                    if ((timetableTrainModel = ProfileSelections.SelectedTimetableTrain()) != null)
                    {
                        if (string.IsNullOrEmpty(timetableTrainModel.Briefing))
                            AddDetailToShow(catalog.GetString("Train: {0}", timetableTrainModel.Name), catalog.GetString("Start time: {0}", timetableTrainModel.StartTime));
                        else
                            AddDetailToShow(catalog.GetString("Train: {0}", timetableTrainModel.Name), catalog.GetString("Start time: {0}", timetableTrainModel.StartTime) + $"\n{timetableTrainModel.Briefing}");

                        WagonSetModel wagonSetModel = routeModel.Parent.GetWagonSets().GetById(timetableTrainModel.WagonSet);
                        if (null != wagonSetModel)
                        {
                            if (timetableTrainModel.WagonSetReverse)
                                wagonSetModel = wagonSetModel with { Reverse = true };
                            AddDetailToShow(catalog.GetString("Consist: {0}", wagonSetModel.Name), string.Empty);
                            if (wagonSetModel.Locomotive != null && wagonSetModel.Locomotive.Description != null)
                            {
                                AddDetailToShow(catalog.GetString("Locomotive: {0}", wagonSetModel.Locomotive.Name), wagonSetModel.Locomotive.Description);
                            }
                        }
                        PathModelCore pathModel = routeModel.GetPaths().GetById(timetableTrainModel.Path);
                        if (pathModel != null)
                        {
                            AddDetailToShow(catalog.GetString("Path: {0}", pathModel.Name), string.Join("\n", catalog.GetString($"Start at: {pathModel.Start}"), catalog.GetString($"Heading to: {pathModel.End}")));
                        }
                    }
                }
            }

            FlowDetails();
        }

        #region Details Panel
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

        private void ToolStripLabelNews_Click(object sender, EventArgs e)
        {
            SystemInfo.OpenBrowser(whatsNewLink);
        }

        private async void ToolStripButtonUpdate_Click(object sender, EventArgs e)
        {
            if (updateManager.LastCheckError != null)
            {
                MessageBox.Show(catalog.GetString($"The update check failed due to an error:\n\n{updateManager.LastCheckError.Message} {updateManager.LastCheckError.InnerException?.Message}"), RuntimeInfo.ProductName);
                return;
            }

            try
            {
                await updateManager.RunUpdateProcess(toolStripButtonUpdate.Tag as string).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                MessageBox.Show(catalog.GetString($"The update failed due to an error:\n\n{exception.Message} {exception.InnerException?.Message}"), RuntimeInfo.ProductName);
                return;
                throw;
            }
        }

        private void ToolStripButtonLogFolder_Click(object sender, EventArgs e)
        {
            SystemInfo.OpenFolder(ProfileUserSettings.LogFilePath);
        }

        #region setup profiles
        private async void ToolStripButtonProfileAdd_Click(object sender, EventArgs e)
        {
            profileFromCurrent = false;
            await AddNewProfile().ConfigureAwait(false);
        }

        private async void ToolStripButtonProfileCopy_Click(object sender, EventArgs e)
        {
            profileFromCurrent = true;
            await AddNewProfile().ConfigureAwait(false);
        }

        private async Task AddNewProfile()
        {
            textInputControlProfileName.Bounds = toolStripDropDownButton1.Bounds;
            textInputControlProfileName.Visible = true;
            textInputControlProfileName.Focus();
            await SaveOptions().ConfigureAwait(false);
        }

        private void TextInputControlProfileName_OnCancel(object sender, EventArgs e)
        {
            textInputControlProfileName.Visible = false;
        }

        private async void TextInputControlProfileName_OnAccept(object sender, EventArgs e)
        {
            textInputControlProfileName.Visible = false;
            ProfileModel currentProfile = new ProfileModel(textInputControlProfileName.Text);
            textInputControlProfileName.ResetText();
            FrozenSet<ProfileModel> profiles = await currentProfile.Create(CancellationToken.None).ConfigureAwait(false);
            SetupProfilesDropdown(profiles);
            if (profileFromCurrent)
            {
                ProfileSelectionsModel profileSelections = ProfileSelections with
                {
                    Name = currentProfile.Name,
                    Id = currentProfile.Id,
                };
                ProfileUserSettingsModel profileUserSettings = ProfileUserSettings with
                {
                    Name = currentProfile.Name,
                    Id = currentProfile.Id,
                };
                await Task.WhenAll(
                    currentProfile.UpdateSettingsModel(profileSelections, ctsProfileLoading.Token),
                    currentProfile.UpdateSettingsModel(profileUserSettings, ctsProfileLoading.Token)
                    ).ConfigureAwait(false);
            }
            await ProfileChanged(currentProfile).ConfigureAwait(false);
        }

        private void TextInputControlProfileName_Leave(object sender, EventArgs e)
        {
            textInputControlProfileName.Visible = false;
        }

        private async void ToolStripButtonProfileDelete_Click(object sender, EventArgs e)
        {
            FrozenSet<ProfileModel> profiles = await SelectedProfile.Delete(CancellationToken.None).ConfigureAwait(false);
            SelectedProfile = null;
            SetupProfilesDropdown(profiles);
            ProfileModel currentProfile = await SelectedProfile.Current(ctsProfileLoading.Token).ConfigureAwait(false);
            await ProfileChanged(currentProfile).ConfigureAwait(false);
        }
        #endregion
    }
}
