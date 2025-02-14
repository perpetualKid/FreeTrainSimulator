using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Toolbox.WinForms.Controls
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

            restoreLastViewMenuItem.Checked = game.ToolboxSettings.RestoreLastView;
            SetupColorComboBoxMenuItem(backgroundColorComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.Background], ColorSetting.Background);
            SetupColorComboBoxMenuItem(railTrackColorComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.RailTrack], ColorSetting.RailTrack);
            SetupColorComboBoxMenuItem(railEndColorComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.RailTrackEnd], ColorSetting.RailTrackEnd);
            SetupColorComboBoxMenuItem(railJunctionColorComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.RailTrackJunction], ColorSetting.RailTrackJunction);
            SetupColorComboBoxMenuItem(railCrossingColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.RailTrackCrossing], ColorSetting.RailTrackCrossing);
            SetupColorComboBoxMenuItem(railLevelCrossingColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.RoadLevelCrossing], ColorSetting.RailLevelCrossing);

            SetupColorComboBoxMenuItem(roadTrackColorComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.RoadTrack], ColorSetting.RoadTrack);
            SetupColorComboBoxMenuItem(roadTrackEndColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.RoadTrackEnd], ColorSetting.RoadTrackEnd);

            SetupColorComboBoxMenuItem(pathTrackColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.PathTrack], ColorSetting.PathTrack);

            SetupColorComboBoxMenuItem(stationColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.StationItem], ColorSetting.StationItem);
            SetupColorComboBoxMenuItem(platformColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.PlatformItem], ColorSetting.PlatformItem);
            SetupColorComboBoxMenuItem(sidingColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.SidingItem], ColorSetting.SidingItem);
            SetupColorComboBoxMenuItem(speedpostColorToolStripComboBoxMenuItem, game.ToolboxSettings.ColorSettings[ColorSetting.SpeedPostItem], ColorSetting.SpeedPostItem);

            SetupVisibilityMenuItem(trackSegmentsVisibleToolStripMenuItem, MapContentType.Tracks);
            SetupVisibilityMenuItem(trackEndNodesVisibleToolStripMenuItem, MapContentType.EndNodes);
            SetupVisibilityMenuItem(trackJunctionNodesVisibleToolStripMenuItem, MapContentType.JunctionNodes);
            SetupVisibilityMenuItem(trackCrossverNodesVisibleToolStripMenuItem, MapContentType.Crossovers);
            SetupVisibilityMenuItem(trackLevelCrossingsVisibleToolStripMenuItem, MapContentType.LevelCrossings);

            SetupVisibilityMenuItem(roadSegmentsVisibleToolStripMenuItem, MapContentType.Roads);
            SetupVisibilityMenuItem(roadEndNodesVisibleToolStripMenuItem, MapContentType.RoadEndNodes);
            SetupVisibilityMenuItem(roadLevelCrossingsVisibleToolStripMenuItem, MapContentType.RoadCrossings);
            SetupVisibilityMenuItem(roadCarSpawnersVisibleToolStripMenuItem, MapContentType.CarSpawners);

            SetupVisibilityMenuItem(primarySignalsVisibleToolStripMenuItem, MapContentType.Signals);
            SetupVisibilityMenuItem(otherSignalsVisibleToolStripMenuItem, MapContentType.OtherSignals);
            SetupVisibilityMenuItem(platformsVisibleToolStripMenuItem, MapContentType.Platforms);
            SetupVisibilityMenuItem(platformNamesVisibleToolStripMenuItem, MapContentType.PlatformNames);
            SetupVisibilityMenuItem(stationNamesVisibleToolStripMenuItem, MapContentType.StationNames);
            SetupVisibilityMenuItem(sidingsVisibleToolStripMenuItem, MapContentType.Sidings);
            SetupVisibilityMenuItem(sidingNamesVisibleToolStripMenuItem, MapContentType.SidingNames);
            SetupVisibilityMenuItem(speedpostsVisibleToolStripMenuItem, MapContentType.SpeedPosts);
            SetupVisibilityMenuItem(milepostsVisibleToolStripMenuItem, MapContentType.MilePosts);
            SetupVisibilityMenuItem(hazardsVisibleToolStripMenuItem, MapContentType.Hazards);
            SetupVisibilityMenuItem(pickupsVisibleToolStripMenuItem, MapContentType.Pickups);
            SetupVisibilityMenuItem(soundRegionsVisibleToolStripMenuItem, MapContentType.SoundRegions);

            SetupVisibilityMenuItem(tileGridVisibleToolStripMenuItem, MapContentType.Grid);

            SetupVisibilityMenuItem(pathsVisisbleToolStripMenuItem, MapContentType.Paths);

            LoadLanguage(languageSelectionComboBoxMenuItem.ComboBox);
            languageSelectionComboBoxMenuItem.SelectedIndexChanged += LanguageSelectionComboBoxMenuItem_SelectedIndexChanged;
        }

        private void SetupColorComboBoxMenuItem(ToolStripComboBox menuItem, string selectColor, ColorSetting setting)
        {
            menuItem.DisplayXnaColors(selectColor, setting);
            menuItem.SelectedIndexChanged += BackgroundColorComboBoxMenuItem_SelectedIndexChanged;
        }

        private void SetupVisibilityMenuItem(ToolStripMenuItem menuItem, MapContentType setting)
        {
            menuItem.Tag = setting;
            menuItem.Checked = parent.ToolboxSettings.ViewSettings[setting];
            menuItem.Click += VisibilitySettingToolStripMenuItem_Click;
            if (menuItem.OwnerItem is ToolStripMenuItem parentItem)
                SetupVisibilityParentMenuItem(parentItem);
        }

        private static void SetupVisibilityParentMenuItem(ToolStripMenuItem menuItem)
        {
            foreach (ToolStripMenuItem item in menuItem.DropDownItems)
            {
                if (!item.Checked)
                {
                    menuItem.Checked = false;
                    return;
                }
            }
            menuItem.Checked = true;
        }

        private void VisibilitySettingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem)
            {
                parent.UpdateItemVisibilityPreference((MapContentType)menuItem.Tag, menuItem.Checked);
                if (menuItem.OwnerItem is ToolStripMenuItem parentItem)
                    SetupVisibilityParentMenuItem(parentItem);
            }

        }

        private void LanguageSelectionComboBoxMenuItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            object item = languageSelectionComboBoxMenuItem.SelectedItem;
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
            {
                foreach (string path in Directory.EnumerateDirectories(RuntimeInfo.LocalesFolder))
                {
                    if (Directory.EnumerateFiles(path, "Toolbox.mo").Any())
                    {
                        try
                        {
                            string languageCode = Path.GetFileName(path);
                            CultureInfo.GetCultureInfo(languageCode);
                            languageCodes.Add(languageCode);
                        }
                        catch (CultureNotFoundException) { }
                    }
                }
            }
            // Turn the list of codes in to a list of code + name pairs for
            // displaying in the dropdown list.
            languageCodes.Add(string.Empty);
            languageCodes.Sort();
            combobox.BindingContext = BindingContext;
            combobox.DataSourceFromList(languageCodes, (language) => string.IsNullOrEmpty(language) ? "System" : CultureInfo.GetCultureInfo(language).NativeName);
            combobox.SelectedValue = parent.ToolboxUserSettings.Language ?? string.Empty;
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
            if (ModifierKeys.HasFlag(Keys.Alt))
                MainMenuStrip.Enabled = false;
            else
                parent.InputCaptured = true;
        }

        internal void PopulateRoutes(ImmutableArray<RouteModelHeader> routes)
        {
            if (InvokeRequired)
            {
                Invoke(PopulateRoutes, routes);
                return;
            }
            SuspendLayout();
            menuItemRoutes.DropDownItems.Clear();
            foreach (RouteModelHeader route in routes.OrderBy(r => r.Name))
            {
                ToolStripMenuItem routeItem = new ToolStripMenuItem(route.Name)
                {
                    Tag = route,
                };
                routeItem.Click += RouteItem_Click;
                menuItemRoutes.DropDownItems.Add(routeItem);
            }
            ResumeLayout();
        }

        private async void RouteItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is RouteModelHeader route)
            {
                if (menuItem.Checked)
                {
                    menuItem.Checked = false;
                    parent.UnloadRoute();
                }
                else
                {
                    UncheckOtherMenuItems(menuItem);
                    await parent.LoadRoute(route).ConfigureAwait(false);
                }
            }
        }

        internal void PreSelectRoute(string routeName)
        {
            if (InvokeRequired)
            {
                Invoke(PreSelectRoute, routeName);
                return;
            }
            foreach (object dropdownItem in menuItemRoutes.DropDownItems)
            {
                if (dropdownItem is ToolStripMenuItem menuItem && menuItem.Text == routeName)
                {
                    UncheckOtherMenuItems(menuItem);
                    break;
                }
            }
        }

        internal void PopulateContentFolders(ImmutableArray<FolderModel> folders)
        {
            if (InvokeRequired)
            {
                Invoke(PopulateContentFolders, folders);
                return;
            }
            SuspendLayout();
            menuItemFolder.DropDownItems.Clear();
            foreach (FolderModel folder in folders.OrderBy(f => f.Name))
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
                UncheckOtherMenuItems(folderItem);
                if (folderItem.Tag is FolderModel folder)
                {
                    parent.UnloadRoute();
                    PopulateRoutes(await parent.FindRoutes(folder).ConfigureAwait(true));
                }
            }
        }

        internal FolderModel SelectContentFolder(string folderName)
        {
            foreach (ToolStripMenuItem item in menuItemFolder.DropDownItems)
            {
                if (item.Text.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    FolderItem_Click(item, EventArgs.Empty);
                    return item.Tag as FolderModel;
                }
            }
            return null;
        }

        private static void UncheckOtherMenuItems(ToolStripMenuItem selectedMenuItem)
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
            parent.PrepareExitApplication();
        }

        private void LoadAtStartupMenuItem_Click(object sender, EventArgs e)
        {
            parent.ToolboxSettings.RestoreLastView = restoreLastViewMenuItem.Checked;
        }

        private void VisibilitySettingParentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem)
            {
                foreach (ToolStripMenuItem item in menuItem.DropDownItems)
                {
                    item.Checked = menuItem.Checked;
                    parent.UpdateItemVisibilityPreference((MapContentType)item.Tag, item.Checked);
                }

            }
        }

        private void DocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StringBuilder documentation = new StringBuilder();
            documentation.AppendLine(parent.Catalog.GetString($"Documentation for {RuntimeInfo.ApplicationName} is available online at:"));
            documentation.AppendLine(RuntimeInfo.WikiLink.ToString());
            documentation.AppendLine();
            documentation.AppendLine(parent.Catalog.GetString("Do you want to visit the website now?"));
            documentation.AppendLine(parent.Catalog.GetString("This will open the page in standard web browser."));
            documentation.AppendLine();
            DialogResult result = MessageBox.Show(documentation.ToString(), $"{RuntimeInfo.ApplicationName}", MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
            {
                SystemInfo.OpenBrowser(RuntimeInfo.WikiLink);
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            parent.ShowAboutWindow();
        }

        private void TakeScreenshotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            parent.PrintScreen();
        }

        private void MainMenuStrip_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Menu)
                MainMenuStrip.Enabled = true;
        }

        #region Path Methods

        internal void PopulatePaths(ImmutableArray<PathModelHeader> paths)
        {
            if (InvokeRequired)
            {
                Invoke(PopulatePaths, paths);
                return;
            }
            List<ToolStripMenuItem> menuItems = new List<ToolStripMenuItem>();
            foreach (PathModelHeader path in paths.OrderBy(p => p.Name))
            {
                ToolStripMenuItem pathItem = new ToolStripMenuItem(path.Name)
                {
                    Tag = path,
                };
                pathItem.Click += LoadPathToolStripMenuItem_Click;
                menuItems.Add(pathItem);
            }
            SuspendLayout();
            loadPathToolStripMenuItem.DropDownItems.Clear();
            loadPathToolStripMenuItem.DropDownItems.AddRange(menuItems.ToArray());
            ResumeLayout();
        }

        internal void ClearPathMenu()
        {
            loadPathToolStripMenuItem.DropDownItems.Clear();
        }

        private void LoadPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is PathModelHeader path)
            {
                if (menuItem.Checked)
                {
                    parent.UnloadPath();
                    menuItem.Checked = false;
                }
                else
                {
                    if (parent.LoadPath(path))
                        UncheckOtherMenuItems(menuItem);
                    else
                        MessageBox.Show("Invalid Path");
                }
            }
        }

        internal void PreSelectPath(PathModelHeader path)
        {
            if (path == null)
            {
                foreach (ToolStripMenuItem dropdownItem in loadPathToolStripMenuItem.DropDownItems)
                {
                    dropdownItem.Checked = false;
                }
            }
            else
            {
                foreach (object dropdownItem in loadPathToolStripMenuItem.DropDownItems)
                {
                    if (dropdownItem is ToolStripMenuItem menuItem && string.Equals((menuItem.Tag as PathModelHeader)?.Id, path.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        UncheckOtherMenuItems(menuItem);
                        break;
                    }
                }
            }
        }

        private void EnableEditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            parent.EditPath();
        }

        #endregion
    }
}
