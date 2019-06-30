// COPYRIGHT 2012 by the Open Rails project.
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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using GNU.Gettext;
using GNU.Gettext.WinForms;
using Orts.Settings;

namespace Orts.Menu
{
    public partial class ImportExportSaveForm : Form
    {
        private readonly ResumeForm.SavePoint savePoint;
        private const string SavePackFileExtension = "ORSavePack";  // Includes "OR" in the extension as this may be emailed, downloaded and mixed in with non-OR files.

        private GettextResourceManager catalog = new GettextResourceManager("Menu");

        public ImportExportSaveForm(ResumeForm.SavePoint save)
        {
            InitializeComponent();  // Needed so that setting StartPosition = CenterParent is respected.

            Localizer.Localize(this, catalog);

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;

            savePoint = save;
			if (!Directory.Exists(UserSettings.SavePackFolder))
                Directory.CreateDirectory(UserSettings.SavePackFolder);
            UpdateFileList(null);
            bExport.Enabled = (savePoint != null);
            ofdImportSave.Filter = Application.ProductName + catalog.GetString("Save Packs") + " (*." + SavePackFileExtension + ")|*." + SavePackFileExtension + "|" + catalog.GetString("All files") + " (*.*)|*";
        }

        #region Event handlers
        private void BImportSave_Click_1(object sender, EventArgs e)
        {
            // Show the dialog and get result.
			ofdImportSave.InitialDirectory = UserSettings.SavePackFolder;
            if (ofdImportSave.ShowDialog() == DialogResult.OK)
            {
				ExtractFilesFromZip(ofdImportSave.FileName, UserSettings.UserDataFolder);
                UpdateFileList(catalog.GetStringFmt("Save Pack '{0}' imported successfully.", Path.GetFileNameWithoutExtension(ofdImportSave.FileName)));
            }
        }

        private void BExport_Click(object sender, EventArgs e)
        {
            // Create a Zip-compatible file/compressed folder containing:
            // all files with the same stem (i.e. *.save, *.png, *.replay, *.txt)
            // Copy files to new package in folder save_packs
            string fullZipFilePath = Path.Combine(UserSettings.SavePackFolder, savePoint.Name + "." + SavePackFileExtension);
            AddFileToZip(fullZipFilePath, Directory.GetFiles(Path.GetDirectoryName(savePoint.File), savePoint.Name + ".*"));
            UpdateFileList(catalog.GetStringFmt("Save Pack '{0}' exported successfully.", Path.GetFileNameWithoutExtension(savePoint.File)));
        }

        private void BViewSavePacksFolder_Click(object sender, EventArgs e)
        {
            var processStart = new ProcessStartInfo();
            var winDir = Environment.GetEnvironmentVariable("windir");
            processStart.FileName = winDir + @"\explorer.exe";
            processStart.Arguments = $"\"{UserSettings.SavePackFolder}\""; // Opens the SavePoint Packs folder
            if (savePoint != null)
            {
                string targetFile = Path.GetFileNameWithoutExtension(savePoint.File) + "." + SavePackFileExtension;
				var fullZipFilePath = Path.Combine(UserSettings.SavePackFolder, targetFile);
                if (File.Exists(fullZipFilePath))
                {
                    processStart.Arguments = $"/select,\"{fullZipFilePath}\""; // Opens the SavePoint Packs folder and selects the exported SavePack
                }
            }
            Process.Start(processStart);
        }
        #endregion

        private void UpdateFileList(string message)
        {
			string[] files = Directory.GetFiles(UserSettings.SavePackFolder, "*." + SavePackFileExtension);
            textBoxSavePacks.Text = String.IsNullOrEmpty(message) ? "" : message + "\r\n";
            textBoxSavePacks.Text += catalog.GetPluralStringFmt("Save Pack folder contains {0} save pack:", "Save Pack folder contains {0} save packs:", files.Length);
            foreach (var s in files)
                textBoxSavePacks.Text += "\r\n    " + Path.GetFileNameWithoutExtension(s);
        }

        private static void AddFileToZip(string zipFilename, string[] filesToAdd)
        {
            using (FileStream fileStream = new FileStream(zipFilename, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (ZipArchive zipFile = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    foreach (string file in filesToAdd)
                        zipFile.CreateEntryFromFile(file, Path.GetFileName(file));
                }
            }
        }

        private static void ExtractFilesFromZip(string zipFilename, string path)
        {
            using (FileStream fileStream = new FileStream(zipFilename, FileMode.Open, FileAccess.Read))
            {
                using (ZipArchive zipFile = new ZipArchive(fileStream))
                {
                    foreach (ZipArchiveEntry entry in zipFile.Entries)
                    {
                        entry.ExtractToFile(Path.Combine(path, entry.Name), true);
                    }
                }
            }
        }
    }
}
