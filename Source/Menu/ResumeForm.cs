// COPYRIGHT 2012, 2013, 2014 by the Open Rails project.
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

/*
This form adds the ability to save the state of the simulator (a SavePoint) multiple times.
Savepoints are made to the folder Program.UserDataFolder (e.g.
    C:\Users\Chris\AppData\Roaming\Open Rails\ 
and take the form  <activity file name> <date> <time>.save. E.g.
    yard_two 2012-03-20 22.07.36.save

As Savepoints for all routes are saved in the same folder and activity file names might be common to several routes, 
the date and time elements ensure that the SavePoint file names are unique.

If the player is not running an activity but exploring a route, the filename takes the form  
<route folder name> <date> <time>.save. E.g.
    USA2 2012-03-20 22.07.36.save

The ActivityRunner program takes switches; one of these is -resume
The -resume switch can now take a SavePoint file name as a parameter. E.g.
    ActivityRunner.exe -resume "yard_two 2012-03-20 22.07.36"
or
    ActivityRunner.exe -resume "yard_two 2012-03-20 22.07.36.save"

If no parameter is provided, then ActivityRunner uses the most recent SavePoint.

New versions of Open Rails may be incompatible with Savepoints made by older versions. A mechanism is provided
here to reject Savepoints which are definitely incompatible and warn of Savepoints that may be incompatible. A SavePoint that
is marked as "may be incompatible" may not be resumed successfully by the ActivityRunner which will
stop and issue an error message.

Some problems remain (see comments in the code):
1. A screen-capture image is saved along with the SavePoint. The intention is that this image should be a thumbnail
   but I can't find how to code this successfully. In the meantime, the screen-capture image that is saved is full-size 
   but displayed as a thumbnail.
*/

using System;
using System.Collections.Frozen;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using GetText;
using GetText.WindowsForms;

namespace FreeTrainSimulator.Menu
{
    public partial class ResumeForm : Form
    {
        private readonly ProfileUserSettingsModel userSettings;
        private readonly ProfileSelectionsModel profileSelectionsModel;
        private readonly RouteModelCore route;
        private readonly ActivityModelCore activity;
        private readonly TimetableModel timeTable;
        private FrozenSet<SavePointModel> savePoints;
        private CancellationTokenSource ctsLoader;
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

        public string SelectedSaveFile { get; private set; }
        public GamePlayAction SelectedAction { get; private set; }
        private readonly bool multiplayer;

        private readonly Catalog catalog;

        internal ResumeForm(ProfileUserSettingsModel userSettings, ProfileSelectionsModel profileSelections)
        {
            catalog = CatalogManager.Catalog;
            InitializeComponent();  // Needed so that setting StartPosition = CenterParent is respected.
            Localizer.Localize(this, catalog);

            this.userSettings = userSettings;
            this.profileSelectionsModel = profileSelections;
            this.route = profileSelections.SelectedRoute();
            this.activity = profileSelections.SelectedActivity();
            this.timeTable = profileSelections.SelectedTimetable();

            checkBoxReplayPauseBeforeEnd.Checked = userSettings.ReplayPause;
            numericReplayPauseBeforeEnd.Value = userSettings.ReplayPauseDuration;

            GridSaves_SelectionChanged(null, null);

            Text += profileSelections.ActivityType switch
            {
                ActivityType.Explorer => $" - {route.Name} - {catalog.GetString("Explore Route")}",
                ActivityType.ExploreActivity => $" - {route.Name} - {catalog.GetString("Explore in Activity Mode")}",
                ActivityType.Activity => $" - {route.Name} - {activity.Name}",
                ActivityType.TimeTable => $" - {route.Name} - {timeTable.Name}",
                _ => throw new NotImplementedException(),
            };

            multiplayer = profileSelections.GamePlayAction == GamePlayAction.MultiplayerClientGame;
            if (multiplayer)
                Text += $" - {catalog.GetString("Multiplayer")} ";
        }

        private async void ResumeForm_Shown(object sender, EventArgs e)
        {
            await LoadSavePointsAsync().ConfigureAwait(true);
        }

        private void ResumeForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (null != ctsLoader && !ctsLoader.IsCancellationRequested)
            {
                ctsLoader.Cancel();
                ctsLoader.Dispose();
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                semaphoreSlim?.Dispose();
                components?.Dispose();
                ctsLoader?.Dispose();
            }
            base.Dispose(disposing);
        }


        private async Task LoadSavePointsAsync()
        {
            ctsLoader = await ctsLoader.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            StringBuilder warnings = new StringBuilder();
            string prefix = string.Empty;

            prefix = profileSelectionsModel.ActivityType switch
            {
                ActivityType.Explorer => Path.GetFileName(route.SourceFolder()),
                ActivityType.ExploreActivity => $"ea${Path.GetFileName(route.SourceFolder())}$",
                ActivityType.Activity => Path.GetFileNameWithoutExtension(activity.SourceFile()),
                ActivityType.TimeTable => $"{Path.GetFileName(route.SourceFolder())} {Path.GetFileNameWithoutExtension(timeTable.SourceFile())}",
                _ => throw new NotImplementedException(),
            };

            FrozenSet<RouteModelCore> globalRoutes = await route.Parent.GetRoutes(ctsLoader.Token).ConfigureAwait(false);

            savePoints = await route.RefreshSavePoints(prefix, ctsLoader.Token).ConfigureAwait(true);
            savePoints = savePoints.Where(s => (!s.MultiplayerGame ^ multiplayer)).
                // SavePacks are all in the same folder and activities may have the same name 
                // (e.g. Short Passenger Run shrtpass.act) but belong to a different route,
                // so pick only the activities for the current route.
                Where(s => string.Equals(s.Route, route.Id, StringComparison.OrdinalIgnoreCase)).
                // In case you receive a SavePack where the activity is recognised but the route has been renamed.
                // Checks the route is not in your list of routes.
                // If so, add it with a warning.
                Where(s => globalRoutes.Any(route => string.Equals(s.Route, route.Id, StringComparison.OrdinalIgnoreCase))).
                OrderByDescending(s => s.RealTime).
                ToFrozenSet();
            saveBindingSource.DataSource = savePoints;

            GridSaves_SelectionChanged(null, null);
            // Show warning after the list has been updated as this is more useful.

            if (savePoints.Count == 0)
                gridSaves.Rows.Clear();

            int invalidCount = 0;
            foreach (var item in savePoints)
            {
                if (item.ValidState == false)
                {
                    warnings?.Append(catalog.GetString($"Error: File '{item.Name}' is invalid or corrupted.\n"));
                    invalidCount++;
                }
            }

            if (invalidCount > 0)
            {
                labelInvalidSaves.Text = catalog.GetString(
                     "To prevent crashes and unexpected behaviour, saved states from older versions may be invalid and fail to restore.\n") +
                     catalog.GetString("{0} of {1} saves for this route can not be validated.", invalidCount, savePoints.Count);
                MessageBox.Show(warnings.ToString(), $"{RuntimeInfo.ProductName} {VersionInfo.Version}");
            }
        }

        private bool AcceptUseOfNonvalidSave(SavePointModel savePoint)
        {
            DialogResult reply = MessageBox.Show(catalog.GetString(
                $"Restoring from a save made by version {savePoint.Version} of {RuntimeInfo.ProductName} may be incompatible with current version {VersionInfo.Version}.\n\nPlease do not report any problems that may result.\n\nContinue?"),
                $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return reply == DialogResult.Yes;
        }

        private void ResumeSave()
        {
            if (saveBindingSource.Current is SavePointModel savePoint)
            {
                if (savePoint.ValidState != false)// && Found(save)) // I.e. true or null. Check is for safety as buttons should be disabled if SavePoint is invalid.
                {
                    if (savePoint.ValidState == null)
                        if (!AcceptUseOfNonvalidSave(savePoint))
                            return;

                    SelectedSaveFile = savePoint.SourceFile();
                    SelectedAction = GamePlayAction.SingleplayerResume;
                    DialogResult = DialogResult.OK;
                }
            }
        }

        private void GridSaves_SelectionChanged(object sender, EventArgs e)
        {
            // Clean up old thumbnail.
            if (pictureBoxScreenshot.Image != null)
            {
                pictureBoxScreenshot.Image.Dispose();
                pictureBoxScreenshot.Image = null;
            }

            // Load new thumbnail.
            if (gridSaves.SelectedRows.Count > 0)
            {
                if (saveBindingSource.Current is SavePointModel savePoint)
                {
                    string thumbFileName = Path.ChangeExtension(savePoint.SourceFile(), "png");
                    if (File.Exists(thumbFileName))
                        pictureBoxScreenshot.Image = new Bitmap(thumbFileName);

                    buttonDelete.Enabled = true;
                    buttonResume.Enabled = (savePoint.ValidState != false); // I.e. either true or null
                    string replayFileName = Path.ChangeExtension(savePoint.SourceFile(), "replay");
                    buttonReplayFromPreviousSave.Enabled = (savePoint.ValidState != false) && File.Exists(replayFileName) && !multiplayer;
                    buttonReplayFromStart.Enabled = File.Exists(replayFileName) && !multiplayer; // can Replay From Start even if SavePoint is invalid.
                }
                else
                {
                    buttonDelete.Enabled = buttonResume.Enabled = buttonReplayFromStart.Enabled = buttonReplayFromPreviousSave.Enabled = false;
                }
            }
            else
            {
                buttonDelete.Enabled = buttonResume.Enabled = buttonReplayFromStart.Enabled = buttonReplayFromPreviousSave.Enabled = false;
            }

            buttonDeleteInvalid.Enabled = true; // Always enabled because there may be Saves to be deleted for other activities not just this one.
            buttonUndelete.Enabled = Directory.Exists(RuntimeInfo.DeletedSaveFolder) && Directory.GetFiles(RuntimeInfo.DeletedSaveFolder).Length > 0;
        }

        private void GridSaves_DoubleClick(object sender, EventArgs e)
        {
            ResumeSave();
        }

        private void PictureBoxScreenshot_Click(object sender, EventArgs e)
        {
            ResumeSave();
        }

        private void ButtonResume_Click(object sender, EventArgs e)
        {
            ResumeSave();
        }

        private async void ButtonDelete_Click(object sender, EventArgs e)
        {
            DataGridViewSelectedRowCollection selectedRows = gridSaves.SelectedRows;
            if (selectedRows.Count > 0)
            {
                gridSaves.ClearSelection();

                for (int i = 0; i < selectedRows.Count; i++)
                {
                    DeleteSavePoint(selectedRows[i].DataBoundItem as SavePointModel);
                }
                await LoadSavePointsAsync().ConfigureAwait(true);
            }
        }

        private static void DeleteSavePoint(SavePointModel savePoint)
        {
            if (null != savePoint)
            {
                if (!Directory.Exists(RuntimeInfo.DeletedSaveFolder))
                    Directory.CreateDirectory(RuntimeInfo.DeletedSaveFolder);

                foreach (string fileName in Directory.EnumerateFiles(Path.GetDirectoryName(savePoint.SourceFile()), savePoint.Name + ".*"))
                {
                    try
                    {
                        File.Move(fileName, Path.Combine(RuntimeInfo.DeletedSaveFolder, Path.GetFileName(fileName)));
                    }
                    catch (Exception ex) when (ex is IOException || ex is FileNotFoundException || ex is UnauthorizedAccessException)
                    { }
                }
            }
        }

        private async void ButtonUndelete_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(RuntimeInfo.DeletedSaveFolder))
            {
                foreach (string filePath in Directory.EnumerateFiles(RuntimeInfo.DeletedSaveFolder))
                {
                    try
                    {
                        File.Move(filePath, Path.Combine(RuntimeInfo.UserDataFolder, Path.GetFileName(filePath)));
                    }
                    catch (Exception ex) when (ex is IOException || ex is FileNotFoundException || ex is UnauthorizedAccessException)
                    { }
                }
                Directory.Delete(RuntimeInfo.DeletedSaveFolder);
            }
            await LoadSavePointsAsync().ConfigureAwait(true);
        }

        private async void ButtonDeleteInvalid_Click(object sender, EventArgs e)
        {
            gridSaves.ClearSelection();
            int deleted = 0;
            foreach (SavePointModel savePoint in savePoints)
            {
                if (savePoint.ValidState == false)
                {
                    DeleteSavePoint(savePoint);
                    deleted++;
                }
            }
            MessageBox.Show(catalog.GetString($"{deleted} invalid saves have been deleted."), $"{RuntimeInfo.ProductName} {VersionInfo.Version}");
            await LoadSavePointsAsync().ConfigureAwait(true);
        }

        private void ButtonReplayFromStart_Click(object sender, EventArgs e)
        {
            SelectedAction = GamePlayAction.SingleplayerReplay;
            InitiateReplay(true);
        }

        private void ButtonReplayFromPreviousSave_Click(object sender, EventArgs e)
        {
            SelectedAction = GamePlayAction.SingleplayerReplayFromSave;
            InitiateReplay(false);
        }

        private void InitiateReplay(bool fromStart)
        {
            SavePointModel savePoint = saveBindingSource.Current as SavePointModel;
            //            if (Found(save))
            {
                if (fromStart && (savePoint.ValidState == null))
                    if (!AcceptUseOfNonvalidSave(savePoint))
                        return;

                SelectedSaveFile = savePoint.SourceFile();
                userSettings.ReplayPause = checkBoxReplayPauseBeforeEnd.Checked;
                userSettings.ReplayPauseDuration = (int)numericReplayPauseBeforeEnd.Value;
                DialogResult = DialogResult.OK; // Anything but DialogResult.Cancel
            }
        }

        private async void ButtonImportExportSaves_Click(object sender, EventArgs e)
        {
            SavePointModel savePoint = saveBindingSource.Current as SavePointModel;
            using (ImportExportSaveForm form = new ImportExportSaveForm(savePoint, catalog))
            {
                form.ShowDialog();
            }
            await LoadSavePointsAsync().ConfigureAwait(true);
        }

        /*
        /// <summary>
        /// Saves may come from other, foreign installations (i.e. not this PC). 
        /// They can be replayed or resumed on this PC but they will contain activity / path / consist filenames
        /// and these may be inappropriate for this PC, typically having a different path.
        /// This method tries to use the paths in the SavePoint if they exist on the current PC. 
        /// If not, it prompts the user to locate a matching file from those on the current PC.
        /// 
        /// The save file is then modified to contain filename(s) from the current PC instead.
        /// </summary>
        // TODO 20240502 Refactor for new savestate format
        private bool Found(SavePoint save)
        {
            if (SelectedAction == GamePlayAction.SinglePlayerTimetableGame)
            {
                return true; // no additional actions required for timetable resume
            }
            else
            {
                string[] savedArgs = Array.Empty<string>();
                try
                {
                    using (BinaryReader inf = new BinaryReader(new FileStream(save.File, FileMode.Open, FileAccess.Read)))
                    {
                        string version = inf.ReadString();
                        string routeName = inf.ReadString();
                        bool isMultiPlayer = false;
                        if (routeName == "$Multipl$")
                        {
                            isMultiPlayer = true;
                            routeName = inf.ReadString(); // Route name
                        }
                        string pathName = inf.ReadString();
                        int gameTime = inf.ReadInt32();
                        long realTime = inf.ReadInt64();
                        float currentTileX = inf.ReadSingle();
                        float currentTileZ = inf.ReadSingle();
                        float initialTileX = inf.ReadSingle();
                        float initialTileZ = inf.ReadSingle();
                        int tempInt = inf.ReadInt32();
                        savedArgs = new string[tempInt];
                        for (int i = 0; i < savedArgs.Length; i++)
                            savedArgs[i] = inf.ReadString();

                        // Re-locate files if saved on another PC
                        bool rewriteNeeded = false;
                        // savedArgs[0] contains Activity or Path filepath
                        string filePath = savedArgs[0];
                        if (!File.Exists(filePath))
                        {
                            // Show the dialog and get result.
                            openFileDialog1.InitialDirectory = FolderStructure.Current.Folder;
                            openFileDialog1.FileName = Path.GetFileName(filePath);
                            openFileDialog1.Title = catalog.GetString($"Find location for file {filePath}");
                            if (openFileDialog1.ShowDialog() != DialogResult.OK)
                                return false;
                            rewriteNeeded = true;
                            savedArgs[0] = openFileDialog1.FileName;
                        }
                        if (savedArgs.Length > 1)  // Explore, not Activity
                        {
                            // savedArgs[1] contains Consist filepath
                            filePath = savedArgs[1];
                            if (!File.Exists(filePath))
                            {
                                // Show the dialog and get result.
                                openFileDialog1.InitialDirectory = FolderStructure.Current.Folder;
                                openFileDialog1.FileName = Path.GetFileName(filePath);
                                openFileDialog1.Title = catalog.GetString($"Find location for file {filePath}");
                                if (openFileDialog1.ShowDialog() != DialogResult.OK)
                                    return false;
                                rewriteNeeded = true;
                                savedArgs[1] = openFileDialog1.FileName;
                            }
                        }
                        if (rewriteNeeded)
                        {
                            using (BinaryWriter outf = new BinaryWriter(new FileStream(save.File + ".tmp", FileMode.Create, FileAccess.Write)))
                            {
                                // copy the start of the file
                                outf.Write(version);
                                if (isMultiPlayer)
                                    outf.Write("$Multipl$");
                                outf.Write(routeName);
                                outf.Write(pathName);
                                outf.Write(gameTime);
                                outf.Write(realTime);
                                outf.Write(currentTileX);
                                outf.Write(currentTileZ);
                                outf.Write(initialTileX);
                                outf.Write(initialTileZ);
                                outf.Write(savedArgs.Length);
                                // copy the pars which may have changed
                                for (int i = 0; i < savedArgs.Length; i++)
                                    outf.Write(savedArgs[i]);
                                // copy the rest of the file
                                while (inf.BaseStream.Position < inf.BaseStream.Length)
                                {
                                    outf.Write(inf.ReadByte());
                                }
                            }
                            inf.Close();
                            File.Replace(save.File + ".tmp", save.File, null);
                        }
                        else
                        {
                            inf.Close();
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is FileNotFoundException)
                {
                    MessageBox.Show(catalog.GetString($"Could not change file location from {save.File} to {savedArgs.FirstOrDefault()}."));
                }
            }
            return true;
        }
        */

        private void GridSaves_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (!gridSaves.IsCurrentRowDirty)
                throw e.Exception;
        }
    }
}
