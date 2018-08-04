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

The RunActivity program takes switches; one of these is -resume
The -resume switch can now take a SavePoint file name as a parameter. E.g.
    RunActivity.exe -resume "yard_two 2012-03-20 22.07.36"
or
    RunActivity.exe -resume "yard_two 2012-03-20 22.07.36.save"

If no parameter is provided, then RunActivity uses the most recent SavePoint.

New versions of Open Rails may be incompatible with Savepoints made by older versions. A mechanism is provided
here to reject Savepoints which are definitely incompatible and warn of Savepoints that may be incompatible. A SavePoint that
is marked as "may be incompatible" may not be resumed successfully by the RunActivity which will
stop and issue an error message.

Some problems remain (see <CJ comment> in the source code):
1. A screen-capture image is saved along with the SavePoint. The intention is that this image should be a thumbnail
   but I can't find how to code this successfully. In the meantime, the screen-capture image that is saved is full-size 
   but displayed as a thumbnail.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GNU.Gettext;
using GNU.Gettext.WinForms;
using MSTS;
using ORTS.Common;
using ORTS.Menu;
using ORTS.Settings;
using Path = System.IO.Path;

namespace ORTS
{
    public partial class ResumeForm : Form
    {
        private readonly UserSettings settings;
        private readonly Route route;
        private readonly Activity activity;
        private static List<Route> globalRoutes;
        private readonly TimetableInfo timeTable;
        private List<SavePoint> savePoints = new List<SavePoint>();

        private System.Threading.CancellationTokenSource ctsLoader;

        public class SavePoint
        {
            public string Name { get; private set; }
            public string File { get; private set; }
            public string PathName { get; private set; }
            public string RouteName { get; private set; }
            public TimeSpan GameTime { get; private set; }
            public DateTime RealTime { get; private set; }
            public string CurrentTile { get; private set; }
            public string Distance { get; private set; }
            public bool? Valid { get; private set; } // 3 possibilities: invalid, unknown validity, valid
            public string VersionOrBuild { get; private set; }
            public bool DbfEval { get; private set; } //Debrief Eval

            public static Task<List<SavePoint>> GetSavePoints(string directory, string prefix, string build,
                string routeName, int failedRestoreVersion, string warnings, System.Threading.CancellationToken token)
            {
                TaskCompletionSource<List<SavePoint>> tcs = new TaskCompletionSource<List<SavePoint>>();
                List<SavePoint> result = new List<SavePoint>();

                Parallel.ForEach(Directory.GetFiles(directory, prefix + "*.save"), (saveFile, state) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        state.Stop();
                    }
                    try
                    {
                        // SavePacks are all in the same folder and activities may have the same name 
                        // (e.g. Short Passenger Run shrtpass.act) but belong to a different route,
                        // so pick only the activities for the current route.
                        SavePoint save = new SavePoint(saveFile, build, failedRestoreVersion);
                        if (string.IsNullOrEmpty(routeName) || save.RouteName == routeName)
                        {
                            lock (result)
                            {
                                result.Add(save);
                            }
                        }
                        else    // In case you receive a SavePack where the activity is recognised but the route has been renamed.
                                // Checks the route is not in your list of routes.
                                // If so, add it with a warning.
                        {
                            if (!globalRoutes.Any(el => el.Name == save.RouteName))
                            {
                                lock (result)
                                {
                                    result.Add(save);
                                }
                                // SavePoint a warning to show later.
                                warnings += catalog.GetStringFmt("Warning: Save {0} found from a route with an unexpected name:\n{1}.\n\n", save.RealTime, save.RouteName);
                            }
                        }
                    }
                    catch { }
                });
                tcs.TrySetResult(result);
                return tcs.Task;
            }


            public SavePoint(string fileName, string currentBuild, int failedRestoreVersion)
            {
                File = fileName;
                Name = Path.GetFileNameWithoutExtension(fileName);
                using (BinaryReader inf = new BinaryReader(new FileStream(File, FileMode.Open, FileAccess.Read)))
                {
                    try
                    {
                        var version = inf.ReadString().Replace("\0", ""); // e.g. "0.9.0.1648" or "X1321" or "" (if compiled locally)
                        var build = inf.ReadString().Replace("\0", ""); // e.g. 0.0.5223.24629 (2014-04-20 13:40:58Z)
                        var versionOrBuild = version.Length > 0 ? version : build;
                        var valid = VersionInfo.GetValidity(version, build, failedRestoreVersion);

                        // Read in route/activity/path/player data.
                        var routeName = inf.ReadString(); // Route name
                        var pathName = inf.ReadString(); // Path name
                        var gameTime = new DateTime().AddSeconds(inf.ReadInt32()).TimeOfDay; // Game time
                        var realTime = DateTime.FromBinary(inf.ReadInt64()); // Real time
                        var currentTileX = inf.ReadSingle(); // Player TileX
                        var currentTileZ = inf.ReadSingle(); // Player TileZ
                        var currentTile = String.Format("{0:F1}, {1:F1}", currentTileX, currentTileZ);
                        var initialTileX = inf.ReadSingle(); // Initial TileX
                        var initialTileZ = inf.ReadSingle(); // Initial TileZ
                        if (currentTileX < short.MinValue || currentTileX > short.MaxValue || currentTileZ < short.MinValue || currentTileZ > short.MaxValue)
                            throw new InvalidDataException();
                        if (initialTileX < short.MinValue || initialTileX > short.MaxValue || initialTileZ < short.MinValue || initialTileZ > short.MaxValue)
                            throw new InvalidDataException();

                        // DistanceFromInitial using Pythagoras theorem.
                        var distance = String.Format("{0:F1}", Math.Sqrt(Math.Pow(currentTileX - initialTileX, 2) + Math.Pow(currentTileZ - initialTileZ, 2)) * 2048);

                        PathName = pathName;
                        RouteName = routeName.Trim();
                        GameTime = gameTime;
                        RealTime = realTime;
                        CurrentTile = currentTile;
                        Distance = distance;
                        Valid = valid;
                        VersionOrBuild = versionOrBuild;

                        //Debrief Eval
                        DbfEval = System.IO.File.Exists(fileName.Substring(0, fileName.Length - 5) + ".dbfeval");
                    }
                    catch { }
                }
            }
        }

        public string SelectedSaveFile { get; set; }
        public MainForm.UserAction SelectedAction { get; set; }

        private static GettextResourceManager catalog = new GettextResourceManager("Menu");

        public ResumeForm(UserSettings settings, Route route, MainForm.UserAction mainFormAction, Activity activity, TimetableInfo timetable,
            List<Route> mainRoutes)
        {
            globalRoutes = mainRoutes;
            SelectedAction = mainFormAction;
            InitializeComponent();  // Needed so that setting StartPosition = CenterParent is respected.

            Localizer.Localize(this, catalog);

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;

            this.settings = settings;
            this.route = route;
            this.activity = activity;
            this.timeTable = timetable;

            checkBoxReplayPauseBeforeEnd.Checked = settings.ReplayPauseBeforeEnd;
            numericReplayPauseBeforeEnd.Value = settings.ReplayPauseBeforeEndS;

            GridSaves_SelectionChanged(null, null);

            if (SelectedAction == MainForm.UserAction.SinglePlayerTimetableGame)
            {
                Text =String.Format("{0} - {1} - {2}", Text, route.Name, Path.GetFileNameWithoutExtension(timeTable.FileName));
                pathNameDataGridViewTextBoxColumn.Visible = true;
            }
            else
            {
                Text = String.Format("{0} - {1} - {2}", Text, route.Name, activity.FilePath != null ? activity.Name :
                    activity.Name == "+ " + catalog.GetString("Explore in Activity Mode") + " +" ? catalog.GetString("Explore in Activity Mode") : catalog.GetString("Explore Route"));
                pathNameDataGridViewTextBoxColumn.Visible = activity.FilePath == null;
            }
        }

        private async void ResumeForm_Shown(object sender, EventArgs e)
        {
            await LoadSavePointsAsync();
        }

        private void ResumeForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (null != ctsLoader && ! ctsLoader.IsCancellationRequested)
            {
                ctsLoader.Cancel();
                ctsLoader.Dispose();
            }
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
                ctsLoader = new System.Threading.CancellationTokenSource();
            }

            string warnings = string.Empty;

            string build = VersionInfo.Build.Contains(" ") ? VersionInfo.Build.Substring(VersionInfo.Build.IndexOf(" ") + 1) : null;
            var prefix = string.Empty;

            if (SelectedAction == MainForm.UserAction.SinglePlayerTimetableGame)
            {
                prefix = Path.GetFileName(route.Path) + " " + Path.GetFileNameWithoutExtension(timeTable.FileName);
            }
            else if (activity.FilePath != null)
            {
                prefix = Path.GetFileNameWithoutExtension(activity.FilePath);
            }
            else if (activity.Name == "- " + catalog.GetString("Explore Route") + " -")
            {
                prefix = Path.GetFileName(route.Path);
            }
            // Explore in activity mode
            else
            {
                prefix = "ea$" + Path.GetFileName(route.Path) + "$";
            }

            savePoints = (await Task.Run(()=>SavePoint.GetSavePoints(UserSettings.UserDataFolder, 
                prefix, build, route.Name, settings.YoungestFailedToRestore, warnings, ctsLoader.Token))).OrderBy(s => s.RealTime).Reverse().ToList();

            saveBindingSource.DataSource = savePoints;
            labelInvalidSaves.Text = catalog.GetString(
                "To prevent crashes and unexpected behaviour, Open Rails invalidates games saved from older versions if they fail to restore.\n") +
                catalog.GetStringFmt("{0} of {1} saves for this route are no longer valid.", savePoints.Count(s => (s.Valid == false)), savePoints.Count);
            GridSaves_SelectionChanged(null, null);
            // Show warning after the list has been updated as this is more useful.
            if (!string.IsNullOrEmpty(warnings))
                MessageBox.Show(warnings, Application.ProductName + " " + VersionInfo.VersionOrBuild);
        }

        private bool AcceptUseOfNonvalidSave(SavePoint save)
        {
            DialogResult reply = MessageBox.Show(catalog.GetStringFmt(
                "Restoring from a save made by version {1} of {0} may be incompatible with current version {2}. Please do not report any problems that may result.\n\nContinue?",
                Application.ProductName, save.VersionOrBuild, VersionInfo.VersionOrBuild),
                Application.ProductName + " " + VersionInfo.VersionOrBuild, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return reply == DialogResult.Yes;
        }
        private bool AcceptOfNonvalidDbfSetup(SavePoint save)
        {
            DialogResult reply = MessageBox.Show(catalog.GetStringFmt(
                   "The selected file contains Debrief Eval data.\nBut Debrief Evaluation checkbox (Main menu) is unchecked.\nYou cannot continue with the Evaluation on course.\n\nContinue?"),
                   Application.ProductName + " " + VersionInfo.VersionOrBuild, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            return reply == DialogResult.Yes;
        }

        private void ResumeSave()
        {
            var save = saveBindingSource.Current as SavePoint;

            if (null != save)
            {
                //Debrief Eval
                if (save.DbfEval && !settings.DebriefActivityEval)
                {
                    if (!AcceptOfNonvalidDbfSetup(save))
                        return;
                }

                if (save.Valid != false) // I.e. true or null. Check is for safety as buttons should be disabled if SavePoint is invalid.
                {
                    if (Found(save))
                    {
                        if (save.Valid == null)
                            if (!AcceptUseOfNonvalidSave(save))
                                return;

                        SelectedSaveFile = save.File;
                        SelectedAction = SelectedAction == MainForm.UserAction.SinglePlayerTimetableGame ?
                            MainForm.UserAction.SinglePlayerResumeTimetableGame : MainForm.UserAction.SingleplayerResumeSave;
                        DialogResult = DialogResult.OK;
                    }
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
                    var thumbFileName = Path.ChangeExtension(save.File, "png");
                    if (File.Exists(thumbFileName))
                        pictureBoxScreenshot.Image = new Bitmap(thumbFileName);

                    buttonDelete.Enabled = true;
                    buttonResume.Enabled = (save.Valid != false); // I.e. either true or null
                    var replayFileName = Path.ChangeExtension(save.File, "replay");
                    buttonReplayFromPreviousSave.Enabled = ((save.Valid != false) && File.Exists(replayFileName));
                    buttonReplayFromStart.Enabled = File.Exists(replayFileName); // can Replay From Start even if SavePoint is invalid.
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
            var selectedRows = gridSaves.SelectedRows;
            if (selectedRows.Count > 0)
            {
                gridSaves.ClearSelection();

                if (!Directory.Exists(UserSettings.DeletedSaveFolder))
                    Directory.CreateDirectory(UserSettings.DeletedSaveFolder);

                for (var i = 0; i < selectedRows.Count; i++)
                {
                    DeleteSavePoint(selectedRows[i].DataBoundItem as SavePoint);
                }
                await LoadSavePointsAsync();
            }
        }

        private void DeleteSavePoint(SavePoint savePoint)
        {
            if (null != savePoint)
            {
                foreach (string fileName in Directory.GetFiles(Path.GetDirectoryName(savePoint.File), savePoint.Name + ".*"))
                {
                    try
                    {
                        File.Move(fileName, Path.Combine(UserSettings.DeletedSaveFolder, Path.GetFileName(fileName)));
                    }
                    catch { }
                }
            }
        }

        private async void ButtonUndelete_Click(object sender, EventArgs e)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(UserSettings.DeletedSaveFolder))
                {
                    foreach (var filePath in Directory.GetFiles(UserSettings.DeletedSaveFolder))
                    {
                        try
                        {
                            File.Move(filePath, Path.Combine(UserSettings.UserDataFolder, Path.GetFileName(filePath)));
                        }
                        catch { }
                    }

                    Directory.Delete(UserSettings.DeletedSaveFolder);
                }
            });
            await LoadSavePointsAsync();
        }

        private async void ButtonDeleteInvalid_Click(object sender, EventArgs e)
        {
            gridSaves.ClearSelection();
            int deleted = 0;
            foreach(SavePoint savePoint in savePoints)
            {
                if(savePoint.Valid == false)
                {
                    DeleteSavePoint(savePoint);
                    deleted++;
                }
            }
            MessageBox.Show(catalog.GetStringFmt("{0} invalid saves have been deleted.", deleted), Application.ProductName + " " + VersionInfo.VersionOrBuild);
            await LoadSavePointsAsync();
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
            var save = saveBindingSource.Current as SavePoint;
            if (Found(save) )
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
            var save = saveBindingSource.Current as SavePoint;
            using (ImportExportSaveForm form = new ImportExportSaveForm(save))
            {
                form.ShowDialog();
            }
            await LoadSavePointsAsync();
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
        public bool Found(SavePoint save)
        {
            if (SelectedAction == MainForm.UserAction.SinglePlayerTimetableGame)
            {
                return true; // no additional actions required for timetable resume
            }
            else
            {
                try
                {
                    BinaryReader inf = new BinaryReader(new FileStream(save.File, FileMode.Open, FileAccess.Read));
                    var version = inf.ReadString();
                    var build = inf.ReadString();
                    var routeName = inf.ReadString();
                    var pathName = inf.ReadString();
                    var gameTime = inf.ReadInt32();
                    var realTime = inf.ReadInt64();
                    var currentTileX = inf.ReadSingle();
                    var currentTileZ = inf.ReadSingle();
                    var initialTileX = inf.ReadSingle();
                    var initialTileZ = inf.ReadSingle();
                    var tempInt = inf.ReadInt32();
                    var savedArgs = new string[tempInt];
                    for (var i = 0; i < savedArgs.Length; i++)
                        savedArgs[i] = inf.ReadString();

                    // Re-locate files if saved on another PC
                    var rewriteNeeded = false;
                    // savedArgs[0] contains Activity or Path filepath
                    var filePath = savedArgs[0];
                    if( !File.Exists(filePath) )
                    {
                        // Show the dialog and get result.
                        openFileDialog1.InitialDirectory = MSTSPath.Base();
                        openFileDialog1.FileName = Path.GetFileName(filePath);
                        openFileDialog1.Title = @"Find location for file " + filePath;
                        if( openFileDialog1.ShowDialog() != DialogResult.OK )
                            return false;
                        rewriteNeeded = true;
                        savedArgs[0] = openFileDialog1.FileName;
                    }
                    if( savedArgs.Length > 1 )  // Explore, not Activity
                    {
                        // savedArgs[1] contains Consist filepath
                        filePath = savedArgs[1];
                        if( !File.Exists(filePath) )
                        {
                            // Show the dialog and get result.
                            openFileDialog1.InitialDirectory = MSTSPath.Base();
                            openFileDialog1.FileName = Path.GetFileName(filePath);
                            openFileDialog1.Title = @"Find location for file " + filePath;
                            if( openFileDialog1.ShowDialog() != DialogResult.OK )
                                return false;
                            rewriteNeeded = true;
                            savedArgs[1] = openFileDialog1.FileName;
                        }
                    }
                    if( rewriteNeeded )
                    {
                        using( BinaryWriter outf = new BinaryWriter(new FileStream(save.File + ".tmp", FileMode.Create, FileAccess.Write)) )
                        {
                            // copy the start of the file
                            outf.Write(version);
                            outf.Write(build);
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
                            for( var i = 0; i < savedArgs.Length; i++ )
                                outf.Write(savedArgs[i]);
                            // copy the rest of the file
                            while( inf.BaseStream.Position < inf.BaseStream.Length )
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
                catch
                {
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
