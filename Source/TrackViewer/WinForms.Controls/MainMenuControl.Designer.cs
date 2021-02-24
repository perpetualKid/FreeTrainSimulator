
namespace Orts.TrackViewer.WinForms.Controls
{
    partial class MainMenuControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.MainMenuStrip = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuItemFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemRoutes = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.menuItemQuit = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.trackItemsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.trackSegmentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.endNodesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.junctionNodesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.crossverNodesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.preferencesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadAtStartupMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.backgroundColorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.backgroundColorComboBoxMenuItem = new System.Windows.Forms.ToolStripComboBox();
            this.railTrackColorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.railTrackColorComboBoxMenuItem = new System.Windows.Forms.ToolStripComboBox();
            this.roadTrackColorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.roadTrackColorComboBoxMenuItem = new System.Windows.Forms.ToolStripComboBox();
            this.selectLanguageMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.languageSelectionComboBoxMenuItem = new System.Windows.Forms.ToolStripComboBox();
            this.MainMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainMenuStrip
            // 
            this.MainMenuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.MainMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.preferencesToolStripMenuItem});
            this.MainMenuStrip.Location = new System.Drawing.Point(0, 0);
            this.MainMenuStrip.Name = "MainMenuStrip";
            this.MainMenuStrip.Size = new System.Drawing.Size(1132, 30);
            this.MainMenuStrip.TabIndex = 0;
            this.MainMenuStrip.Text = "MenuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.toolStripSeparator1,
            this.menuItemFolder,
            this.menuItemRoutes,
            this.toolStripSeparator2,
            this.menuItemQuit});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(46, 26);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(221, 26);
            this.openToolStripMenuItem.Text = "Open";
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(221, 26);
            this.saveToolStripMenuItem.Text = "Save";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(218, 6);
            // 
            // menuItemFolder
            // 
            this.menuItemFolder.Name = "menuItemFolder";
            this.menuItemFolder.Size = new System.Drawing.Size(221, 26);
            this.menuItemFolder.Text = "Select Route Folder";
            // 
            // menuItemRoutes
            // 
            this.menuItemRoutes.Name = "menuItemRoutes";
            this.menuItemRoutes.Size = new System.Drawing.Size(221, 26);
            this.menuItemRoutes.Text = "Select Route";
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(218, 6);
            // 
            // menuItemQuit
            // 
            this.menuItemQuit.Name = "menuItemQuit";
            this.menuItemQuit.Size = new System.Drawing.Size(221, 26);
            this.menuItemQuit.Text = "Quit (Q)";
            this.menuItemQuit.Click += new System.EventHandler(this.MenuItemQuit_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.trackItemsToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(55, 26);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // trackItemsToolStripMenuItem
            // 
            this.trackItemsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.trackSegmentsToolStripMenuItem,
            this.endNodesToolStripMenuItem,
            this.junctionNodesToolStripMenuItem,
            this.crossverNodesToolStripMenuItem});
            this.trackItemsToolStripMenuItem.Name = "trackItemsToolStripMenuItem";
            this.trackItemsToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.trackItemsToolStripMenuItem.Text = "Track Items";
            // 
            // trackSegmentsToolStripMenuItem
            // 
            this.trackSegmentsToolStripMenuItem.Name = "trackSegmentsToolStripMenuItem";
            this.trackSegmentsToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.trackSegmentsToolStripMenuItem.Text = "Track Segments";
            // 
            // endNodesToolStripMenuItem
            // 
            this.endNodesToolStripMenuItem.Name = "endNodesToolStripMenuItem";
            this.endNodesToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.endNodesToolStripMenuItem.Text = "End Nodes";
            // 
            // junctionNodesToolStripMenuItem
            // 
            this.junctionNodesToolStripMenuItem.Name = "junctionNodesToolStripMenuItem";
            this.junctionNodesToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.junctionNodesToolStripMenuItem.Text = "Junction Nodes";
            // 
            // crossverNodesToolStripMenuItem
            // 
            this.crossverNodesToolStripMenuItem.Name = "crossverNodesToolStripMenuItem";
            this.crossverNodesToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.crossverNodesToolStripMenuItem.Text = "Crossver Nodes";
            // 
            // preferencesToolStripMenuItem
            // 
            this.preferencesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadAtStartupMenuItem,
            this.selectLanguageMenuItem,
            this.toolStripSeparator3,
            this.backgroundColorToolStripMenuItem,
            this.railTrackColorToolStripMenuItem,
            this.roadTrackColorToolStripMenuItem});
            this.preferencesToolStripMenuItem.Name = "preferencesToolStripMenuItem";
            this.preferencesToolStripMenuItem.Size = new System.Drawing.Size(99, 26);
            this.preferencesToolStripMenuItem.Text = "Preferences";
            // 
            // loadAtStartupMenuItem
            // 
            this.loadAtStartupMenuItem.CheckOnClick = true;
            this.loadAtStartupMenuItem.Name = "loadAtStartupMenuItem";
            this.loadAtStartupMenuItem.Size = new System.Drawing.Size(224, 26);
            this.loadAtStartupMenuItem.Text = "Load Route on Start";
            this.loadAtStartupMenuItem.Click += new System.EventHandler(this.LoadAtStartupMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(221, 6);
            // 
            // backgroundColorToolStripMenuItem
            // 
            this.backgroundColorToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.backgroundColorComboBoxMenuItem});
            this.backgroundColorToolStripMenuItem.Name = "backgroundColorToolStripMenuItem";
            this.backgroundColorToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.backgroundColorToolStripMenuItem.Text = "Background Color";
            // 
            // backgroundColorComboBoxMenuItem
            // 
            this.backgroundColorComboBoxMenuItem.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.backgroundColorComboBoxMenuItem.MaxDropDownItems = 24;
            this.backgroundColorComboBoxMenuItem.Name = "backgroundColorComboBoxMenuItem";
            this.backgroundColorComboBoxMenuItem.Size = new System.Drawing.Size(224, 28);
            // 
            // railTrackColorToolStripMenuItem
            // 
            this.railTrackColorToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.railTrackColorComboBoxMenuItem});
            this.railTrackColorToolStripMenuItem.Name = "railTrackColorToolStripMenuItem";
            this.railTrackColorToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.railTrackColorToolStripMenuItem.Text = "Rail Track Color";
            // 
            // railTrackColorComboBoxMenuItem
            // 
            this.railTrackColorComboBoxMenuItem.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.railTrackColorComboBoxMenuItem.MaxDropDownItems = 24;
            this.railTrackColorComboBoxMenuItem.Name = "railTrackColorComboBoxMenuItem";
            this.railTrackColorComboBoxMenuItem.Size = new System.Drawing.Size(224, 28);
            // 
            // roadTrackColorToolStripMenuItem
            // 
            this.roadTrackColorToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.roadTrackColorComboBoxMenuItem});
            this.roadTrackColorToolStripMenuItem.Name = "roadTrackColorToolStripMenuItem";
            this.roadTrackColorToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.roadTrackColorToolStripMenuItem.Text = "Road Track Color";
            // 
            // roadTrackColorComboBoxMenuItem
            // 
            this.roadTrackColorComboBoxMenuItem.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.roadTrackColorComboBoxMenuItem.MaxDropDownItems = 24;
            this.roadTrackColorComboBoxMenuItem.Name = "roadTrackColorComboBoxMenuItem";
            this.roadTrackColorComboBoxMenuItem.Size = new System.Drawing.Size(224, 28);
            // 
            // selectLanguageMenuItem
            // 
            this.selectLanguageMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.languageSelectionComboBoxMenuItem});
            this.selectLanguageMenuItem.Name = "selectLanguageMenuItem";
            this.selectLanguageMenuItem.Size = new System.Drawing.Size(224, 26);
            this.selectLanguageMenuItem.Text = "Select Language";
            // 
            // languageSelectionComboBoxMenuItem
            // 
            this.languageSelectionComboBoxMenuItem.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.languageSelectionComboBoxMenuItem.Name = "languageSelectionComboBoxMenuItem";
            this.languageSelectionComboBoxMenuItem.Size = new System.Drawing.Size(224, 28);
            // 
            // MainMenuControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.MainMenuStrip);
            this.Name = "MainMenuControl";
            this.Size = new System.Drawing.Size(1132, 273);
            this.MainMenuStrip.ResumeLayout(false);
            this.MainMenuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip MainMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem menuItemRoutes;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem menuItemFolder;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem menuItemQuit;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem trackItemsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem trackSegmentsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem endNodesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem junctionNodesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem crossverNodesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem preferencesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem backgroundColorToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox backgroundColorComboBoxMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadAtStartupMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem railTrackColorToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox railTrackColorComboBoxMenuItem;
        private System.Windows.Forms.ToolStripMenuItem roadTrackColorToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox roadTrackColorComboBoxMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectLanguageMenuItem;
        private System.Windows.Forms.ToolStripComboBox languageSelectionComboBoxMenuItem;
    }
}
