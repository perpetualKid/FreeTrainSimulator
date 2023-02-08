namespace Orts.Menu
{
    partial class ImportExportSaveForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing ) {
            if( disposing && (components != null) ) {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.bViewSavePacksFolder = new System.Windows.Forms.Button();
            this.bExport = new System.Windows.Forms.Button();
            this.bImportSave = new System.Windows.Forms.Button();
            this.ofdImportSave = new System.Windows.Forms.OpenFileDialog();
            this.textBoxSavePacks = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // bViewSavePacksFolder
            // 
            this.bViewSavePacksFolder.Location = new System.Drawing.Point(432, 15);
            this.bViewSavePacksFolder.Margin = new System.Windows.Forms.Padding(4);
            this.bViewSavePacksFolder.Name = "bViewSavePacksFolder";
            this.bViewSavePacksFolder.Size = new System.Drawing.Size(200, 28);
            this.bViewSavePacksFolder.TabIndex = 1;
            this.bViewSavePacksFolder.Text = "Open Save Packs folder";
            this.bViewSavePacksFolder.UseVisualStyleBackColor = true;
            this.bViewSavePacksFolder.Click += new System.EventHandler(this.ButtonViewSavePacksFolder_Click);
            // 
            // bExport
            // 
            this.bExport.Location = new System.Drawing.Point(224, 15);
            this.bExport.Margin = new System.Windows.Forms.Padding(4);
            this.bExport.Name = "bExport";
            this.bExport.Size = new System.Drawing.Size(200, 28);
            this.bExport.TabIndex = 0;
            this.bExport.Text = "Export to Save Pack";
            this.bExport.UseVisualStyleBackColor = true;
            this.bExport.Click += new System.EventHandler(this.BExport_Click);
            // 
            // bImportSave
            // 
            this.bImportSave.Location = new System.Drawing.Point(16, 15);
            this.bImportSave.Margin = new System.Windows.Forms.Padding(4);
            this.bImportSave.Name = "bImportSave";
            this.bImportSave.Size = new System.Drawing.Size(200, 28);
            this.bImportSave.TabIndex = 0;
            this.bImportSave.Text = "Import Save Pack";
            this.bImportSave.UseVisualStyleBackColor = true;
            this.bImportSave.Click += new System.EventHandler(this.ButtonImportSave_Click);
            // 
            // ofdImportSave
            // 
            this.ofdImportSave.Title = "Import a saved game from a Save Pack";
            // 
            // textBoxSavePacks
            // 
            this.textBoxSavePacks.BackColor = System.Drawing.SystemColors.Window;
            this.textBoxSavePacks.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxSavePacks.ForeColor = System.Drawing.SystemColors.WindowText;
            this.textBoxSavePacks.Location = new System.Drawing.Point(16, 50);
            this.textBoxSavePacks.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxSavePacks.Multiline = true;
            this.textBoxSavePacks.Name = "textBoxSavePacks";
            this.textBoxSavePacks.ReadOnly = true;
            this.textBoxSavePacks.Size = new System.Drawing.Size(615, 269);
            this.textBoxSavePacks.TabIndex = 4;
            // 
            // ImportExportSaveForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(648, 335);
            this.Controls.Add(this.textBoxSavePacks);
            this.Controls.Add(this.bViewSavePacksFolder);
            this.Controls.Add(this.bImportSave);
            this.Controls.Add(this.bExport);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportExportSaveForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import and export saved games";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bViewSavePacksFolder;
        private System.Windows.Forms.Button bExport;
        private System.Windows.Forms.OpenFileDialog ofdImportSave;
        private System.Windows.Forms.Button bImportSave;
        private System.Windows.Forms.TextBox textBoxSavePacks;
    }
}
