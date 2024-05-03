namespace Orts.Menu {
    partial class ResumeForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ResumeForm));
            gridSaves = new System.Windows.Forms.DataGridView();
            saveBindingSource = new System.Windows.Forms.BindingSource(components);
            buttonResume = new System.Windows.Forms.Button();
            buttonDelete = new System.Windows.Forms.Button();
            buttonUndelete = new System.Windows.Forms.Button();
            labelInvalidSaves = new System.Windows.Forms.Label();
            buttonDeleteInvalid = new System.Windows.Forms.Button();
            toolTip = new System.Windows.Forms.ToolTip(components);
            buttonImportExportSaves = new System.Windows.Forms.Button();
            groupBoxInvalid = new System.Windows.Forms.GroupBox();
            tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            buttonReplayFromPreviousSave = new System.Windows.Forms.Button();
            buttonReplayFromStart = new System.Windows.Forms.Button();
            checkBoxReplayPauseBeforeEnd = new System.Windows.Forms.CheckBox();
            label1 = new System.Windows.Forms.Label();
            numericReplayPauseBeforeEnd = new System.Windows.Forms.NumericUpDown();
            panelSaves = new System.Windows.Forms.Panel();
            panelScreenshot = new System.Windows.Forms.Panel();
            pictureBoxScreenshot = new System.Windows.Forms.PictureBox();
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            fileDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            realTimeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            pathNameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            gameTimeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            distanceDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            currentTileDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            validDataGridViewCheckBoxColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            DebriefEvaluation = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            Blank = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)gridSaves).BeginInit();
            ((System.ComponentModel.ISupportInitialize)saveBindingSource).BeginInit();
            groupBoxInvalid.SuspendLayout();
            tableLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericReplayPauseBeforeEnd).BeginInit();
            panelSaves.SuspendLayout();
            panelScreenshot.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxScreenshot).BeginInit();
            SuspendLayout();
            // 
            // gridSaves
            // 
            gridSaves.AllowUserToAddRows = false;
            gridSaves.AllowUserToDeleteRows = false;
            gridSaves.AutoGenerateColumns = false;
            gridSaves.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            gridSaves.BackgroundColor = System.Drawing.SystemColors.Window;
            gridSaves.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridSaves.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            gridSaves.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            gridSaves.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            gridSaves.ColumnHeadersHeight = 29;
            gridSaves.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridSaves.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { fileDataGridViewTextBoxColumn, realTimeDataGridViewTextBoxColumn, pathNameDataGridViewTextBoxColumn, gameTimeDataGridViewTextBoxColumn, distanceDataGridViewTextBoxColumn, currentTileDataGridViewTextBoxColumn, validDataGridViewCheckBoxColumn, DebriefEvaluation, Blank });
            gridSaves.DataSource = saveBindingSource;
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            gridSaves.DefaultCellStyle = dataGridViewCellStyle6;
            gridSaves.Dock = System.Windows.Forms.DockStyle.Fill;
            gridSaves.Location = new System.Drawing.Point(0, 0);
            gridSaves.Margin = new System.Windows.Forms.Padding(4);
            gridSaves.MultiSelect = false;
            gridSaves.Name = "gridSaves";
            gridSaves.ReadOnly = true;
            gridSaves.RowHeadersVisible = false;
            gridSaves.RowHeadersWidth = 51;
            gridSaves.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            gridSaves.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridSaves.Size = new System.Drawing.Size(499, 411);
            gridSaves.TabIndex = 0;
            gridSaves.SelectionChanged += GridSaves_SelectionChanged;
            gridSaves.DoubleClick += GridSaves_DoubleClick;
            // 
            // saveBindingSource
            // 
            saveBindingSource.DataSource = typeof(Models.Simplified.SavePoint);
            // 
            // buttonResume
            // 
            buttonResume.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            buttonResume.Location = new System.Drawing.Point(971, 528);
            buttonResume.Margin = new System.Windows.Forms.Padding(4);
            buttonResume.Name = "buttonResume";
            buttonResume.Size = new System.Drawing.Size(100, 28);
            buttonResume.TabIndex = 1;
            buttonResume.Text = "Resume";
            buttonResume.UseVisualStyleBackColor = true;
            buttonResume.Click += ButtonResume_Click;
            // 
            // buttonDelete
            // 
            buttonDelete.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            buttonDelete.Location = new System.Drawing.Point(405, 425);
            buttonDelete.Margin = new System.Windows.Forms.Padding(4);
            buttonDelete.Name = "buttonDelete";
            buttonDelete.Size = new System.Drawing.Size(100, 28);
            buttonDelete.TabIndex = 7;
            buttonDelete.Text = "Delete";
            toolTip.SetToolTip(buttonDelete, "Deletes the currently selected save or saves.");
            buttonDelete.UseVisualStyleBackColor = true;
            buttonDelete.Click += ButtonDelete_Click;
            // 
            // buttonUndelete
            // 
            buttonUndelete.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            buttonUndelete.Location = new System.Drawing.Point(405, 461);
            buttonUndelete.Margin = new System.Windows.Forms.Padding(4);
            buttonUndelete.Name = "buttonUndelete";
            buttonUndelete.Size = new System.Drawing.Size(100, 28);
            buttonUndelete.TabIndex = 8;
            buttonUndelete.Text = "Undelete";
            toolTip.SetToolTip(buttonUndelete, "Restores all saves deleted in this session.");
            buttonUndelete.UseVisualStyleBackColor = true;
            buttonUndelete.Click += ButtonUndelete_Click;
            // 
            // labelInvalidSaves
            // 
            labelInvalidSaves.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            labelInvalidSaves.Location = new System.Drawing.Point(8, 20);
            labelInvalidSaves.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            labelInvalidSaves.Name = "labelInvalidSaves";
            labelInvalidSaves.Size = new System.Drawing.Size(377, 76);
            labelInvalidSaves.TabIndex = 0;
            // 
            // buttonDeleteInvalid
            // 
            buttonDeleteInvalid.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            buttonDeleteInvalid.Location = new System.Drawing.Point(8, 99);
            buttonDeleteInvalid.Margin = new System.Windows.Forms.Padding(4);
            buttonDeleteInvalid.Name = "buttonDeleteInvalid";
            buttonDeleteInvalid.Size = new System.Drawing.Size(259, 28);
            buttonDeleteInvalid.TabIndex = 1;
            buttonDeleteInvalid.Text = "Delete all invalid saves";
            buttonDeleteInvalid.UseVisualStyleBackColor = true;
            buttonDeleteInvalid.Click += ButtonDeleteInvalid_Click;
            // 
            // buttonImportExportSaves
            // 
            buttonImportExportSaves.Location = new System.Drawing.Point(405, 497);
            buttonImportExportSaves.Margin = new System.Windows.Forms.Padding(4);
            buttonImportExportSaves.Name = "buttonImportExportSaves";
            tableLayoutPanel.SetRowSpan(buttonImportExportSaves, 2);
            buttonImportExportSaves.Size = new System.Drawing.Size(100, 60);
            buttonImportExportSaves.TabIndex = 9;
            buttonImportExportSaves.Text = "Import/ export";
            toolTip.SetToolTip(buttonImportExportSaves, "Restores all saves deleted in this session.");
            buttonImportExportSaves.UseVisualStyleBackColor = true;
            buttonImportExportSaves.Click += ButtonImportExportSaves_Click;
            // 
            // groupBoxInvalid
            // 
            groupBoxInvalid.Controls.Add(labelInvalidSaves);
            groupBoxInvalid.Controls.Add(buttonDeleteInvalid);
            groupBoxInvalid.Dock = System.Windows.Forms.DockStyle.Fill;
            groupBoxInvalid.Location = new System.Drawing.Point(4, 425);
            groupBoxInvalid.Margin = new System.Windows.Forms.Padding(4);
            groupBoxInvalid.Name = "groupBoxInvalid";
            groupBoxInvalid.Padding = new System.Windows.Forms.Padding(4);
            tableLayoutPanel.SetRowSpan(groupBoxInvalid, 4);
            groupBoxInvalid.Size = new System.Drawing.Size(393, 135);
            groupBoxInvalid.TabIndex = 10;
            groupBoxInvalid.TabStop = false;
            groupBoxInvalid.Text = "Invalid saves";
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tableLayoutPanel.ColumnCount = 5;
            tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 27F));
            tableLayoutPanel.Controls.Add(groupBoxInvalid, 0, 1);
            tableLayoutPanel.Controls.Add(buttonImportExportSaves, 1, 3);
            tableLayoutPanel.Controls.Add(buttonReplayFromPreviousSave, 2, 4);
            tableLayoutPanel.Controls.Add(buttonReplayFromStart, 3, 4);
            tableLayoutPanel.Controls.Add(buttonResume, 4, 4);
            tableLayoutPanel.Controls.Add(checkBoxReplayPauseBeforeEnd, 2, 2);
            tableLayoutPanel.Controls.Add(label1, 2, 3);
            tableLayoutPanel.Controls.Add(numericReplayPauseBeforeEnd, 3, 3);
            tableLayoutPanel.Controls.Add(buttonDelete, 1, 1);
            tableLayoutPanel.Controls.Add(buttonUndelete, 1, 2);
            tableLayoutPanel.Controls.Add(panelSaves, 0, 0);
            tableLayoutPanel.Controls.Add(panelScreenshot, 2, 0);
            tableLayoutPanel.Location = new System.Drawing.Point(12, 11);
            tableLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 5;
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel.Size = new System.Drawing.Size(1075, 564);
            tableLayoutPanel.TabIndex = 0;
            // 
            // buttonReplayFromPreviousSave
            // 
            buttonReplayFromPreviousSave.Location = new System.Drawing.Point(513, 528);
            buttonReplayFromPreviousSave.Margin = new System.Windows.Forms.Padding(4);
            buttonReplayFromPreviousSave.Name = "buttonReplayFromPreviousSave";
            buttonReplayFromPreviousSave.Size = new System.Drawing.Size(200, 28);
            buttonReplayFromPreviousSave.TabIndex = 2;
            buttonReplayFromPreviousSave.Text = "Replay from previous save";
            buttonReplayFromPreviousSave.UseVisualStyleBackColor = true;
            buttonReplayFromPreviousSave.Click += ButtonReplayFromPreviousSave_Click;
            // 
            // buttonReplayFromStart
            // 
            buttonReplayFromStart.Location = new System.Drawing.Point(721, 528);
            buttonReplayFromStart.Margin = new System.Windows.Forms.Padding(4);
            buttonReplayFromStart.Name = "buttonReplayFromStart";
            buttonReplayFromStart.Size = new System.Drawing.Size(200, 28);
            buttonReplayFromStart.TabIndex = 3;
            buttonReplayFromStart.Text = "Replay from start";
            buttonReplayFromStart.UseVisualStyleBackColor = true;
            buttonReplayFromStart.Click += ButtonReplayFromStart_Click;
            // 
            // checkBoxReplayPauseBeforeEnd
            // 
            checkBoxReplayPauseBeforeEnd.AutoSize = true;
            checkBoxReplayPauseBeforeEnd.Checked = true;
            checkBoxReplayPauseBeforeEnd.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBoxReplayPauseBeforeEnd.Location = new System.Drawing.Point(513, 461);
            checkBoxReplayPauseBeforeEnd.Margin = new System.Windows.Forms.Padding(4);
            checkBoxReplayPauseBeforeEnd.Name = "checkBoxReplayPauseBeforeEnd";
            checkBoxReplayPauseBeforeEnd.Size = new System.Drawing.Size(128, 19);
            checkBoxReplayPauseBeforeEnd.TabIndex = 4;
            checkBoxReplayPauseBeforeEnd.Text = "Pause replay at end";
            checkBoxReplayPauseBeforeEnd.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(566, 493);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(147, 31);
            label1.TabIndex = 6;
            label1.Text = "Pause seconds before end:";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numericReplayPauseBeforeEnd
            // 
            numericReplayPauseBeforeEnd.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            numericReplayPauseBeforeEnd.Location = new System.Drawing.Point(721, 497);
            numericReplayPauseBeforeEnd.Margin = new System.Windows.Forms.Padding(4);
            numericReplayPauseBeforeEnd.Maximum = new decimal(new int[] { 3600, 0, 0, 0 });
            numericReplayPauseBeforeEnd.Minimum = new decimal(new int[] { 3600, 0, 0, int.MinValue });
            numericReplayPauseBeforeEnd.Name = "numericReplayPauseBeforeEnd";
            numericReplayPauseBeforeEnd.Size = new System.Drawing.Size(69, 23);
            numericReplayPauseBeforeEnd.TabIndex = 5;
            numericReplayPauseBeforeEnd.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // panelSaves
            // 
            panelSaves.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            tableLayoutPanel.SetColumnSpan(panelSaves, 2);
            panelSaves.Controls.Add(gridSaves);
            panelSaves.Dock = System.Windows.Forms.DockStyle.Fill;
            panelSaves.Location = new System.Drawing.Point(4, 4);
            panelSaves.Margin = new System.Windows.Forms.Padding(4);
            panelSaves.Name = "panelSaves";
            panelSaves.Size = new System.Drawing.Size(501, 413);
            panelSaves.TabIndex = 11;
            // 
            // panelScreenshot
            // 
            panelScreenshot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            tableLayoutPanel.SetColumnSpan(panelScreenshot, 3);
            panelScreenshot.Controls.Add(pictureBoxScreenshot);
            panelScreenshot.Dock = System.Windows.Forms.DockStyle.Fill;
            panelScreenshot.Location = new System.Drawing.Point(513, 4);
            panelScreenshot.Margin = new System.Windows.Forms.Padding(4);
            panelScreenshot.Name = "panelScreenshot";
            panelScreenshot.Size = new System.Drawing.Size(558, 413);
            panelScreenshot.TabIndex = 12;
            // 
            // pictureBoxScreenshot
            // 
            pictureBoxScreenshot.Dock = System.Windows.Forms.DockStyle.Fill;
            pictureBoxScreenshot.Location = new System.Drawing.Point(0, 0);
            pictureBoxScreenshot.Margin = new System.Windows.Forms.Padding(0);
            pictureBoxScreenshot.Name = "pictureBoxScreenshot";
            pictureBoxScreenshot.Size = new System.Drawing.Size(556, 411);
            pictureBoxScreenshot.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pictureBoxScreenshot.TabIndex = 5;
            pictureBoxScreenshot.TabStop = false;
            pictureBoxScreenshot.Click += PictureBoxScreenshot_Click;
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // fileDataGridViewTextBoxColumn
            // 
            fileDataGridViewTextBoxColumn.DataPropertyName = "File";
            fileDataGridViewTextBoxColumn.HeaderText = "File";
            fileDataGridViewTextBoxColumn.MinimumWidth = 6;
            fileDataGridViewTextBoxColumn.Name = "fileDataGridViewTextBoxColumn";
            fileDataGridViewTextBoxColumn.ReadOnly = true;
            fileDataGridViewTextBoxColumn.Visible = false;
            fileDataGridViewTextBoxColumn.Width = 59;
            // 
            // realTimeDataGridViewTextBoxColumn
            // 
            realTimeDataGridViewTextBoxColumn.DataPropertyName = "RealTime";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.TopCenter;
            dataGridViewCellStyle2.Format = "g";
            dataGridViewCellStyle2.NullValue = null;
            realTimeDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            realTimeDataGridViewTextBoxColumn.HeaderText = "Saved At";
            realTimeDataGridViewTextBoxColumn.MinimumWidth = 6;
            realTimeDataGridViewTextBoxColumn.Name = "realTimeDataGridViewTextBoxColumn";
            realTimeDataGridViewTextBoxColumn.ReadOnly = true;
            realTimeDataGridViewTextBoxColumn.Width = 102;
            // 
            // pathNameDataGridViewTextBoxColumn
            // 
            pathNameDataGridViewTextBoxColumn.DataPropertyName = "PathName";
            pathNameDataGridViewTextBoxColumn.HeaderText = "Path";
            pathNameDataGridViewTextBoxColumn.MinimumWidth = 6;
            pathNameDataGridViewTextBoxColumn.Name = "pathNameDataGridViewTextBoxColumn";
            pathNameDataGridViewTextBoxColumn.ReadOnly = true;
            pathNameDataGridViewTextBoxColumn.Width = 66;
            // 
            // gameTimeDataGridViewTextBoxColumn
            // 
            gameTimeDataGridViewTextBoxColumn.DataPropertyName = "GameTime";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.TopCenter;
            dataGridViewCellStyle3.Format = "hh\\:mm\\:ss";
            dataGridViewCellStyle3.NullValue = null;
            gameTimeDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            gameTimeDataGridViewTextBoxColumn.HeaderText = "Time";
            gameTimeDataGridViewTextBoxColumn.MinimumWidth = 6;
            gameTimeDataGridViewTextBoxColumn.Name = "gameTimeDataGridViewTextBoxColumn";
            gameTimeDataGridViewTextBoxColumn.ReadOnly = true;
            gameTimeDataGridViewTextBoxColumn.Width = 78;
            // 
            // distanceDataGridViewTextBoxColumn
            // 
            distanceDataGridViewTextBoxColumn.DataPropertyName = "Distance";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.TopRight;
            distanceDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle4;
            distanceDataGridViewTextBoxColumn.HeaderText = "Distance";
            distanceDataGridViewTextBoxColumn.MinimumWidth = 6;
            distanceDataGridViewTextBoxColumn.Name = "distanceDataGridViewTextBoxColumn";
            distanceDataGridViewTextBoxColumn.ReadOnly = true;
            distanceDataGridViewTextBoxColumn.Width = 78;
            // 
            // currentTileDataGridViewTextBoxColumn
            // 
            currentTileDataGridViewTextBoxColumn.DataPropertyName = "CurrentTile";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.TopRight;
            currentTileDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle5;
            currentTileDataGridViewTextBoxColumn.HeaderText = "Tile";
            currentTileDataGridViewTextBoxColumn.MinimumWidth = 6;
            currentTileDataGridViewTextBoxColumn.Name = "currentTileDataGridViewTextBoxColumn";
            currentTileDataGridViewTextBoxColumn.ReadOnly = true;
            currentTileDataGridViewTextBoxColumn.Width = 102;
            // 
            // validDataGridViewCheckBoxColumn
            // 
            validDataGridViewCheckBoxColumn.DataPropertyName = "Valid";
            validDataGridViewCheckBoxColumn.HeaderText = "Valid";
            validDataGridViewCheckBoxColumn.MinimumWidth = 6;
            validDataGridViewCheckBoxColumn.Name = "validDataGridViewCheckBoxColumn";
            validDataGridViewCheckBoxColumn.ReadOnly = true;
            validDataGridViewCheckBoxColumn.ThreeState = true;
            validDataGridViewCheckBoxColumn.Width = 48;
            // 
            // DebriefEvaluation
            // 
            DebriefEvaluation.DataPropertyName = "DebriefEvaluation";
            DebriefEvaluation.HeaderText = "Eval";
            DebriefEvaluation.MinimumWidth = 6;
            DebriefEvaluation.Name = "DebriefEvaluation";
            DebriefEvaluation.ReadOnly = true;
            DebriefEvaluation.Width = 48;
            // 
            // Blank
            // 
            Blank.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            Blank.HeaderText = "";
            Blank.MinimumWidth = 6;
            Blank.Name = "Blank";
            Blank.ReadOnly = true;
            Blank.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // ResumeForm
            // 
            AcceptButton = buttonResume;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1099, 586);
            Controls.Add(tableLayoutPanel);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            MinimizeBox = false;
            Name = "ResumeForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Saved Games";
            FormClosing += ResumeForm_FormClosing;
            Shown += ResumeForm_Shown;
            ((System.ComponentModel.ISupportInitialize)gridSaves).EndInit();
            ((System.ComponentModel.ISupportInitialize)saveBindingSource).EndInit();
            groupBoxInvalid.ResumeLayout(false);
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericReplayPauseBeforeEnd).EndInit();
            panelSaves.ResumeLayout(false);
            panelScreenshot.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBoxScreenshot).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView gridSaves;
        private System.Windows.Forms.Button buttonResume;
        private System.Windows.Forms.Button buttonDelete;
        private System.Windows.Forms.Button buttonUndelete;
        private System.Windows.Forms.Label labelInvalidSaves;
        private System.Windows.Forms.Button buttonDeleteInvalid;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.BindingSource saveBindingSource;
        private System.Windows.Forms.GroupBox groupBoxInvalid;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Button buttonImportExportSaves;
        private System.Windows.Forms.PictureBox pictureBoxScreenshot;
        private System.Windows.Forms.NumericUpDown numericReplayPauseBeforeEnd;
        private System.Windows.Forms.Button buttonReplayFromPreviousSave;
        private System.Windows.Forms.Button buttonReplayFromStart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Panel panelSaves;
        private System.Windows.Forms.Panel panelScreenshot;
        private System.Windows.Forms.CheckBox checkBoxReplayPauseBeforeEnd;
        private System.Windows.Forms.DataGridViewTextBoxColumn fileDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn realTimeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn pathNameDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn gameTimeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distanceDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn currentTileDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn validDataGridViewCheckBoxColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn DebriefEvaluation;
        private System.Windows.Forms.DataGridViewTextBoxColumn Blank;
    }
}
