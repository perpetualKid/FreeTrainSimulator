using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Orts.Common;
using Orts.Models.Simplified;
using Orts.View.Track;

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

            backgroundColorComboBoxMenuItem.DisplayXnaColors(game.Settings.TrackViewer.ColorBackground, ColorPreference.Background);
            backgroundColorComboBoxMenuItem.SelectedIndexChanged += BackgroundColorComboBoxMenuItem_SelectedIndexChanged;
        }

        private void BackgroundColorComboBoxMenuItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ToolStripComboBox comboBox)
            {
                parent.UpdateColorPreference((ColorPreference)comboBox.Tag, comboBox.SelectedItem as string);
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

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        internal void PopulateRoutes(IEnumerable<Route> routes)
        {
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
        }

        private async void RouteItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripDropDownItem menuItem && menuItem.Tag is Route route)
            {
                parent.StatusMessage = route.Name;
                parent.ContentArea = null;

                TrackData trackData = new TrackData(route.Path);

                bool? useMetricUnits = (parent.Settings.MeasurementUnit == MeasurementUnit.Metric || parent.Settings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
                if (parent.Settings.MeasurementUnit == MeasurementUnit.Route)
                    useMetricUnits = null;

                await trackData.LoadTrackData(useMetricUnits).ConfigureAwait(false);

                TrackContent content = new TrackContent(trackData.TrackDB, trackData.TrackSections, trackData.SignalConfig, trackData.UseMetricUnits);
                await content.Initialize().ConfigureAwait(false);
                parent.ContentArea = new ContentArea(parent, content);
                parent.StatusMessage = null;

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

        bool closingCancelled;
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
    }
}
