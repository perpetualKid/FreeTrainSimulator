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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using GetText;
using GetText.WindowsForms;

using Orts.Common;
using Orts.Common.Info;
using Orts.Formats.Msts;
using Orts.Models.Simplified;
using Orts.Settings;

using Path = System.IO.Path;

namespace Orts.Menu
{
    public partial class ResumeForm : Form
    {
        private readonly UserSettings settings;
        private readonly Route route;
        private readonly Activity activity;
        private readonly IEnumerable<Route> globalRoutes;
        private readonly TimetableInfo timeTable;
        private List<SavePoint> savePoints = new List<SavePoint>();
        private CancellationTokenSource ctsLoader;

        public string SelectedSaveFile { get; private set; }
        public MainForm.UserAction SelectedAction { get; private set; }
        private readonly bool multiplayer;

        private readonly ICatalog catalog;

        internal ResumeForm(UserSettings settings, Route route, MainForm.UserAction mainFormAction, Activity activity, TimetableInfo timeTable, IEnumerable<Route> mainRoutes)
        {
            catalog = CatalogManager.Catalog;
            globalRoutes = mainRoutes;
            SelectedAction = mainFormAction;
            multiplayer = SelectedAction == MainForm.UserAction.MultiplayerClient;
            InitializeComponent();  // Needed so that setting StartPosition = CenterParent is respected.

            Localizer.Localize(this, catalog);

            this.settings = settings;
            this.route = route;
            this.activity = activity;
            this.timeTable = timeTable;

            checkBoxReplayPauseBeforeEnd.Checked = settings.ReplayPauseBeforeEnd;
            numericReplayPauseBeforeEnd.Value = settings.ReplayPauseBeforeEndS;

            GridSaves_SelectionChanged(null, null);

            if (SelectedAction == MainForm.UserAction.SinglePlayerTimetableGame)
            {
                Text += $" - {route.Name} - {Path.GetFileNameWithoutExtension(timeTable.FileName)}";
                pathNameDataGridViewTextBoxColumn.Visible = true;
            }
            else
            {
                Text += $" - {route.Name} - {(activity.GetType() == typeof(Activity) ? activity.Name : activity is ExploreThroughActivity ? catalog.GetString("Explore in Activity Mode") : catalog.GetString("Explore Route"))}";
                pathNameDataGridViewTextBoxColumn.Visible = activity.FilePath == null;
            }

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
                components?.Dispose();
                ctsLoader?.Dispose();
            }
            base.Dispose(disposing);
        }


        private async Task LoadSavePointsAsync()
        {
            lock (savePoints)
            {
                if (ctsLoader != null && !ctsLoader.IsCancellationRequested)
                {
                    ctsLoader.Cancel();
                    ctsLoader.Dispose();
                }
                ctsLoader = new CancellationTokenSource();
            }

            StringBuilder warnings = new StringBuilder();
            string prefix = string.Empty;

            if (SelectedAction == MainForm.UserAction.SinglePlayerTimetableGame)
            {
                prefix = $"{Path.GetFileName(route.Path)} {Path.GetFileNameWithoutExtension(timeTable.FileName)}";
            }
            else if (activity.FilePath != null)
            {
                prefix = Path.GetFileNameWithoutExtension(activity.FilePath);
            }
            else if (activity.Name == $"- {catalog.GetString("Explore Route")} -")
            {
                prefix = Path.GetFileName(route.Path);
            }
            // Explore in activity mode
            else
            {
                prefix = $"ea${Path.GetFileName(route.Path)}$";
            }

            savePoints = (await SavePoint.GetSavePoints(RuntimeInfo.UserDataFolder,
                prefix, route.Name, warnings, multiplayer, globalRoutes, ctsLoader.Token).ConfigureAwait(true)).
                OrderByDescending(s => s.Valid).ThenByDescending(s => s.RealTime).ToList();

            saveBindingSource.DataSource = savePoints;
            labelInvalidSaves.Text = catalog.GetString(
                "To prevent crashes and unexpected behaviour, Open Rails invalidates games saved from older versions if they fail to restore.\n") +
                catalog.GetString("{0} of {1} saves for this route are no longer valid.", savePoints.Count(s => (s.Valid == false)), savePoints.Count);
            GridSaves_SelectionChanged(null, null);
            // Show warning after the list has been updated as this is more useful.
            if (warnings.Length > 0)
                MessageBox.Show(warnings.ToString(), $"{RuntimeInfo.ProductName} {VersionInfo.Version}");
        }

        private bool AcceptUseOfNonvalidSave(SavePoint save)
        {
            DialogResult reply = MessageBox.Show(catalog.GetString(
                $"Restoring from a save made by version {save.ProgramVersion} of {RuntimeInfo.ProductName} may be incompatible with current version {VersionInfo.Version}.\n\nPlease do not report any problems that may result.\n\nContinue?"),
                $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return reply == DialogResult.Yes;
        }

        private void ResumeSave()
        {
            SavePoint save = saveBindingSource.Current as SavePoint;

            if (null != save)
            {
                if (save.Valid != false && Found(save)) // I.e. true or null. Check is for safety as buttons should be disabled if SavePoint is invalid.
                {
                    if (save.Valid == null)
                        if (!AcceptUseOfNonvalidSave(save))
                            return;

                    SelectedSaveFile = save.File;
                    MainForm.UserAction selectedAction = SelectedAction;
                    switch (SelectedAction)
                    {
                        case MainForm.UserAction.SinglePlayerTimetableGame:
                            selectedAction = MainForm.UserAction.SinglePlayerResumeTimetableGame;
                            break;
                        case MainForm.UserAction.SingleplayerNewGame:
                            selectedAction = MainForm.UserAction.SingleplayerResumeSave;
                            break;
                        case MainForm.UserAction.MultiplayerClient:
                            selectedAction = MainForm.UserAction.MultiplayerClientResumeSave;
                            break;
                    }
                    SelectedAction = selectedAction;
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
                if (saveBindingSource.Current is SavePoint save)
                {
                    string thumbFileName = Path.ChangeExtension(save.File, "png");
                    if (File.Exists(thumbFileName))
                        pictureBoxScreenshot.Image = new Bitmap(thumbFileName);

                    buttonDelete.Enabled = true;
                    buttonResume.Enabled = (save.Valid != false); // I.e. either true or null
                    string replayFileName = Path.ChangeExtension(save.File, "replay");
                    buttonReplayFromPreviousSave.Enabled = (save.Valid != false) && File.Exists(replayFileName) && !multiplayer;
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
            buttonUndelete.Enabled = Directory.Exists(UserSettings.DeletedSaveFolder) && Directory.GetFiles(UserSettings.DeletedSaveFolder).Length > 0;
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
                    DeleteSavePoint(selectedRows[i].DataBoundItem as SavePoint);
                }
                await LoadSavePointsAsync().ConfigureAwait(true);
            }
        }

        private static void DeleteSavePoint(SavePoint savePoint)
        {
            if (null != savePoint)
            {
                if (!Directory.Exists(UserSettings.DeletedSaveFolder))
                    Directory.CreateDirectory(UserSettings.DeletedSaveFolder);

                foreach (string fileName in Directory.EnumerateFiles(Path.GetDirectoryName(savePoint.File), savePoint.Name + ".*"))
                {
                    try
                    {
                        File.Move(fileName, Path.Combine(UserSettings.DeletedSaveFolder, Path.GetFileName(fileName)));
                    }
                    catch (Exception ex) when (ex is IOException || ex is FileNotFoundException || ex is UnauthorizedAccessException)
                    { }
                }
            }
        }

        private async void ButtonUndelete_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(UserSettings.DeletedSaveFolder))
            {
                foreach (string filePath in Directory.EnumerateFiles(UserSettings.DeletedSaveFolder))
                {
                    try
                    {
                        File.Move(filePath, Path.Combine(RuntimeInfo.UserDataFolder, Path.GetFileName(filePath)));
                    }
                    catch (Exception ex) when (ex is IOException || ex is FileNotFoundException || ex is UnauthorizedAccessException)
                    { }
                }
                Directory.Delete(UserSettings.DeletedSaveFolder);
            }
            await LoadSavePointsAsync().ConfigureAwait(true);
        }

        private async void ButtonDeleteInvalid_Click(object sender, EventArgs e)
        {
            gridSaves.ClearSelection();
            int deleted = 0;
            foreach (SavePoint savePoint in savePoints)
            {
                if (savePoint.Valid == false)
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
            SelectedAction = MainForm.UserAction.SingleplayerReplaySave;
            InitiateReplay(true);
        }

        private void ButtonReplayFromPreviousSave_Click(object sender, EventArgs e)
        {
            SelectedAction = MainForm.UserAction.SingleplayerReplaySaveFromSave;
            InitiateReplay(false);
        }

        private void InitiateReplay(bool fromStart)
        {
            SavePoint save = saveBindingSource.Current as SavePoint;
            if (Found(save))
            {
                if (fromStart && (save.Valid == null))
                    if (!AcceptUseOfNonvalidSave(save))
                        return;

                SelectedSaveFile = save.File;
                settings.ReplayPauseBeforeEnd = checkBoxReplayPauseBeforeEnd.Checked;
                settings.ReplayPauseBeforeEndS = (int)numericReplayPauseBeforeEnd.Value;
                DialogResult = DialogResult.OK; // Anything but DialogResult.Cancel
            }
        }

        private async void ButtonImportExportSaves_Click(object sender, EventArgs e)
        {
            SavePoint save = saveBindingSource.Current as SavePoint;
            using (ImportExportSaveForm form = new ImportExportSaveForm(save, catalog))
            {
                form.ShowDialog();
            }
            await LoadSavePointsAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Saves may come from other, foreign installations (i.e. not this PC). 
        /// They can be replayed or resumed on this PC but they will contain activity / path / consist filenames
        /// and these may be inappropriate for this PC, typically having a different path.
        /// This method tries to use the paths in the SavePoint if they exist on the current PC. 
        /// If not, it prompts the user to locate a matching file from those on the current PC.
        /// 
        /// The save file is then modified to contain filename(s) from the current PC instead.
        /// </summary>
        private bool Found(SavePoint save)
        {
            if (SelectedAction == MainForm.UserAction.SinglePlayerTimetableGame)
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

        private void GridSaves_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (!gridSaves.IsCurrentRowDirty)
                throw e.Exception;
        }

    }
}
