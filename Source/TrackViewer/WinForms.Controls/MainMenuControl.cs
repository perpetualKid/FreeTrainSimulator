using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Orts.Common.Info;
using Orts.Models.Simplified;
using Orts.View;

namespace Orts.TrackViewer.WinForms.Controls
{
    public partial class MainMenuControl : UserControl
    {
        private readonly GameWindow parent;

        internal MainMenuControl(GameWindow game)
        {
            parent = game;
            InitializeComponent();
            MainMenuStrip.MenuActivate += MainMenuStrip_MenuActivate;
            MainMenuStrip.MenuDeactivate += MainMenuStrip_MenuDeactivate;
            menuItemFolder.DropDown.Closing += FolderDropDown_Closing;

            loadAtStartupMenuItem.Checked = game.Settings.TrackViewer.LoadRouteOnStart;
            backgroundColorComboBoxMenuItem.DisplayXnaColors(game.Settings.TrackViewer.ColorBackground, ColorSetting.Background);
            backgroundColorComboBoxMenuItem.SelectedIndexChanged += BackgroundColorComboBoxMenuItem_SelectedIndexChanged;
            railTrackColorComboBoxMenuItem.DisplayXnaColors(game.Settings.TrackViewer.ColorRailTrack, ColorSetting.RailTrack);
            railTrackColorComboBoxMenuItem.SelectedIndexChanged += BackgroundColorComboBoxMenuItem_SelectedIndexChanged;
            roadTrackColorComboBoxMenuItem.DisplayXnaColors(game.Settings.TrackViewer.ColorRoadTrack, ColorSetting.RoadTrack);
            roadTrackColorComboBoxMenuItem.SelectedIndexChanged += BackgroundColorComboBoxMenuItem_SelectedIndexChanged;

            LoadLanguage(languageSelectionComboBoxMenuItem.ComboBox);
            languageSelectionComboBoxMenuItem.SelectedIndexChanged += LanguageSelectionComboBoxMenuItem_SelectedIndexChanged;
        }

        private void LanguageSelectionComboBoxMenuItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = languageSelectionComboBoxMenuItem.SelectedItem;
            languageSelectionComboBoxMenuItem.SelectedIndexChanged -= LanguageSelectionComboBoxMenuItem_SelectedIndexChanged;
            parent.UpdateLanguagePreference(languageSelectionComboBoxMenuItem.ComboBox.SelectedValue as string);
            languageSelectionComboBoxMenuItem.SelectedItem = item;
            languageSelectionComboBoxMenuItem.SelectedIndexChanged += LanguageSelectionComboBoxMenuItem_SelectedIndexChanged;
        }

        private void LoadLanguage(ComboBox combobox)
        {
            // Collect all the available language codes by searching for
            // localisation files, but always include English (base language).
            List<string> languageCodes = new List<string> { "en" };
            if (Directory.Exists(RuntimeInfo.LocalesFolder))
                foreach (string path in Directory.EnumerateDirectories(RuntimeInfo.LocalesFolder))
                    if (Directory.EnumerateFiles(path, "TrackViewer.mo").Any())
                    {
                        try
                        {
                            string languageCode = System.IO.Path.GetFileName(path);
                            CultureInfo.GetCultureInfo(languageCode);
                            languageCodes.Add(languageCode);
                        }
                        catch (CultureNotFoundException) { }
                    }
            // Turn the list of codes in to a list of code + name pairs for
            // displaying in the dropdown list.
            languageCodes.Add(string.Empty);
            languageCodes.Sort();
            //combobox.Items.AddRange(languageCodes.ToArray());
            combobox.BindingContext = this.BindingContext;
            combobox.DataSourceFromList(languageCodes, (language) => string.IsNullOrEmpty(language) ? "System" : CultureInfo.GetCultureInfo(language).NativeName);
            combobox.SelectedValue = parent.Settings.Language;
            if (combobox.SelectedValue == null)
                combobox.SelectedIndex = 0;
        }

        private void BackgroundColorComboBoxMenuItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ToolStripComboBox comboBox)
            {
                parent.UpdateColorPreference((ColorSetting)comboBox.Tag, comboBox.SelectedItem as string);
            }
        }

        private void MainMenuStrip_MenuDeactivate(object sender, EventArgs e)
        {
            if (closingCancelled)
            {
                closingCancelled = false;

            }
            else
            {
                parent.InputCaptured = false;
            }
        }

        private void MainMenuStrip_MenuActivate(object sender, EventArgs e)
        {
            parent.InputCaptured = true;
        }

        internal void PopulateRoutes(IEnumerable<Route> routes)
        {
            Invoke((MethodInvoker)delegate {
                SuspendLayout();
                menuItemRoutes.DropDownItems.Clear();
                foreach (Route route in routes)
                {
                    ToolStripMenuItem routeItem = new ToolStripMenuItem(route.Name)
                    {
                        Tag = route,
                    };
                    routeItem.Click += RouteItem_Click;
                    menuItemRoutes.DropDownItems.Add(routeItem);
                }
                ResumeLayout();
            });
        }

        private async void RouteItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripDropDownItem menuItem && menuItem.Tag is Route route)
            {
                await parent.LoadRoute(route).ConfigureAwait(false);
            }
        }

        internal void PopulateContentFolders(IEnumerable<Folder> folders)
        {
            SuspendLayout();
            menuItemFolder.DropDownItems.Clear();
            foreach (Folder folder in folders)
            {
                ToolStripMenuItem folderItem = new ToolStripMenuItem(folder.Name)
                {
                    Tag = folder,
                };
                folderItem.Click += FolderItem_Click;
                menuItemFolder.DropDownItems.Add(folderItem);
            }
            ResumeLayout();
        }

        private bool closingCancelled;
        private void FolderDropDown_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            //if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            //{
            //    e.Cancel = true;
            //    parent.InputCaptured = true;
            //    closingCancelled = true;
            //}
        }

        private async void FolderItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem folderItem)
            {
                UncheckOtherFolderMenuItems(folderItem);
                if (folderItem.Tag is Folder folder)
                {
                    parent.ContentArea = null;
                    PopulateRoutes(await parent.FindRoutes(folder).ConfigureAwait(true));
                }
            }
        }

        internal Folder SelectContentFolder(string folderName)
        {
            foreach (ToolStripMenuItem item in menuItemFolder.DropDownItems)
            {
                if (item.Text.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    FolderItem_Click(item, EventArgs.Empty);
                    return item.Tag as Folder;
                }
            }
            return null;
        }

        private static void UncheckOtherFolderMenuItems(ToolStripMenuItem selectedMenuItem)
        {
            selectedMenuItem.Checked = true;

            foreach (ToolStripMenuItem toolStripMenuItem in selectedMenuItem.Owner.Items.OfType<ToolStripMenuItem>())
            {
                if (toolStripMenuItem == selectedMenuItem)
                    continue;
                toolStripMenuItem.Checked = false;
            }
        }

        private void MenuItemQuit_Click(object sender, EventArgs e)
        {
            parent.CloseWindow();
        }

        private void LoadAtStartupMenuItem_Click(object sender, EventArgs e)
        {
            parent.Settings.TrackViewer.LoadRouteOnStart = loadAtStartupMenuItem.Checked;
        }
    }
}
