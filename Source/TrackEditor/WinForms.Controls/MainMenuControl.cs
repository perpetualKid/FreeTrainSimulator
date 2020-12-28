using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Orts.Models.Simplified;

namespace Orts.TrackEditor.WinForms.Controls
{
    public partial class MainMenuControl : UserControl
    {
        private readonly GameWindow parent;

        public MainMenuControl(GameWindow game)
        {
            parent = game;
            InitializeComponent();
            MainMenuStrip.MenuActivate += MainMenuStrip_MenuActivate;
            MainMenuStrip.MenuDeactivate += MainMenuStrip_MenuDeactivate;
            menuItemFolder.DropDown.Closing += FolderDropDown_Closing;
        }

        private void MainMenuStrip_MenuDeactivate(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Deactivate");
            if (closingCancelled)
            {
                closingCancelled = false;
                System.Diagnostics.Debug.WriteLine("Skip");

            }
            else
            {
                parent.InputCaptured = false;
                System.Diagnostics.Debug.WriteLine("Release");
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
                ToolStripMenuItem routeItem = new ToolStripMenuItem(route.Name);
                menuItemRoutes.DropDownItems.Add(routeItem);
            }
            ResumeLayout();
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
                    PopulateRoutes(await parent.FindRoutes(folder).ConfigureAwait(true));
                    parent.DrawStatusMessage(folder.Name);
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
