namespace Orts.Menu {
    partial class TestingForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TestingForm));
            this.buttonTestAll = new System.Windows.Forms.Button();
            this.buttonTest = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSummary = new System.Windows.Forms.Button();
            this.gridTestActivities = new System.Windows.Forms.DataGridView();
            this.toTestDataGridViewCheckBoxColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.activityFilePathDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.defaultSortDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.routeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.activityDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.testedDataGridViewCheckBoxColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.passedDataGridViewCheckBoxColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.errorsDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.loadDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.fpsDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.testBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.buttonDetails = new System.Windows.Forms.Button();
            this.checkBoxOverride = new System.Windows.Forms.CheckBox();
            this.buttonNoSort = new System.Windows.Forms.Button();
            this.panelTests = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.gridTestActivities)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.testBindingSource)).BeginInit();
            this.panelTests.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonTestAll
            // 
            this.buttonTestAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonTestAll.Location = new System.Drawing.Point(16, 543);
            this.buttonTestAll.Margin = new System.Windows.Forms.Padding(4);
            this.buttonTestAll.Name = "buttonTestAll";
            this.buttonTestAll.Size = new System.Drawing.Size(100, 28);
            this.buttonTestAll.TabIndex = 1;
            this.buttonTestAll.Text = "Test all";
            this.buttonTestAll.UseVisualStyleBackColor = true;
            this.buttonTestAll.Click += new System.EventHandler(this.ButtonTestAll_Click);
            // 
            // buttonTest
            // 
            this.buttonTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonTest.Location = new System.Drawing.Point(124, 543);
            this.buttonTest.Margin = new System.Windows.Forms.Padding(4);
            this.buttonTest.Name = "buttonTest";
            this.buttonTest.Size = new System.Drawing.Size(100, 28);
            this.buttonTest.TabIndex = 2;
            this.buttonTest.Text = "Test";
            this.buttonTest.UseVisualStyleBackColor = true;
            this.buttonTest.Click += new System.EventHandler(this.ButtonTest_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonCancel.Location = new System.Drawing.Point(232, 543);
            this.buttonCancel.Margin = new System.Windows.Forms.Padding(4);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(100, 28);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
            // 
            // buttonSummary
            // 
            this.buttonSummary.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSummary.Location = new System.Drawing.Point(875, 543);
            this.buttonSummary.Margin = new System.Windows.Forms.Padding(4);
            this.buttonSummary.Name = "buttonSummary";
            this.buttonSummary.Size = new System.Drawing.Size(100, 28);
            this.buttonSummary.TabIndex = 6;
            this.buttonSummary.Text = "Summary";
            this.buttonSummary.UseVisualStyleBackColor = true;
            this.buttonSummary.Click += new System.EventHandler(this.ButtonSummary_Click);
            // 
            // gridTestActivities
            // 
            this.gridTestActivities.AllowUserToAddRows = false;
            this.gridTestActivities.AllowUserToDeleteRows = false;
            this.gridTestActivities.AutoGenerateColumns = false;
            this.gridTestActivities.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.gridTestActivities.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.gridTestActivities.BackgroundColor = System.Drawing.SystemColors.Window;
            this.gridTestActivities.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.gridTestActivities.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.gridTestActivities.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridTestActivities.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridTestActivities.ColumnHeadersHeight = 29;
            this.gridTestActivities.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridTestActivities.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.toTestDataGridViewCheckBoxColumn,
            this.activityFilePathDataGridViewTextBoxColumn,
            this.defaultSortDataGridViewTextBoxColumn,
            this.routeDataGridViewTextBoxColumn,
            this.activityDataGridViewTextBoxColumn,
            this.testedDataGridViewCheckBoxColumn,
            this.passedDataGridViewCheckBoxColumn,
            this.errorsDataGridViewTextBoxColumn,
            this.loadDataGridViewTextBoxColumn,
            this.fpsDataGridViewTextBoxColumn});
            this.gridTestActivities.DataSource = this.testBindingSource;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridTestActivities.DefaultCellStyle = dataGridViewCellStyle4;
            this.gridTestActivities.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridTestActivities.Location = new System.Drawing.Point(0, 0);
            this.gridTestActivities.Margin = new System.Windows.Forms.Padding(4);
            this.gridTestActivities.Name = "gridTestActivities";
            this.gridTestActivities.ReadOnly = true;
            this.gridTestActivities.RowHeadersVisible = false;
            this.gridTestActivities.RowHeadersWidth = 51;
            this.gridTestActivities.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.gridTestActivities.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridTestActivities.Size = new System.Drawing.Size(1064, 518);
            this.gridTestActivities.TabIndex = 0;
            // 
            // toTestDataGridViewCheckBoxColumn
            // 
            this.toTestDataGridViewCheckBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.toTestDataGridViewCheckBoxColumn.DataPropertyName = "ToTest";
            this.toTestDataGridViewCheckBoxColumn.HeaderText = "ToTest";
            this.toTestDataGridViewCheckBoxColumn.MinimumWidth = 6;
            this.toTestDataGridViewCheckBoxColumn.Name = "toTestDataGridViewCheckBoxColumn";
            this.toTestDataGridViewCheckBoxColumn.ReadOnly = true;
            this.toTestDataGridViewCheckBoxColumn.Visible = false;
            this.toTestDataGridViewCheckBoxColumn.Width = 20;
            // 
            // activityFilePathDataGridViewTextBoxColumn
            // 
            this.activityFilePathDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.activityFilePathDataGridViewTextBoxColumn.DataPropertyName = "ActivityFilePath";
            this.activityFilePathDataGridViewTextBoxColumn.HeaderText = "ActivityFilePath";
            this.activityFilePathDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.activityFilePathDataGridViewTextBoxColumn.Name = "activityFilePathDataGridViewTextBoxColumn";
            this.activityFilePathDataGridViewTextBoxColumn.ReadOnly = true;
            this.activityFilePathDataGridViewTextBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.activityFilePathDataGridViewTextBoxColumn.Visible = false;
            this.activityFilePathDataGridViewTextBoxColumn.Width = 20;
            // 
            // defaultSortDataGridViewTextBoxColumn
            // 
            this.defaultSortDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.defaultSortDataGridViewTextBoxColumn.DataPropertyName = "DefaultSort";
            this.defaultSortDataGridViewTextBoxColumn.HeaderText = "DefaultSort";
            this.defaultSortDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.defaultSortDataGridViewTextBoxColumn.Name = "defaultSortDataGridViewTextBoxColumn";
            this.defaultSortDataGridViewTextBoxColumn.ReadOnly = true;
            this.defaultSortDataGridViewTextBoxColumn.Visible = false;
            this.defaultSortDataGridViewTextBoxColumn.Width = 20;
            // 
            // routeDataGridViewTextBoxColumn
            // 
            this.routeDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.routeDataGridViewTextBoxColumn.DataPropertyName = "Route";
            this.routeDataGridViewTextBoxColumn.HeaderText = "Route";
            this.routeDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.routeDataGridViewTextBoxColumn.Name = "routeDataGridViewTextBoxColumn";
            this.routeDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // activityDataGridViewTextBoxColumn
            // 
            this.activityDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.activityDataGridViewTextBoxColumn.DataPropertyName = "Activity";
            this.activityDataGridViewTextBoxColumn.HeaderText = "Activity";
            this.activityDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.activityDataGridViewTextBoxColumn.Name = "activityDataGridViewTextBoxColumn";
            this.activityDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // testedDataGridViewCheckBoxColumn
            // 
            this.testedDataGridViewCheckBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.testedDataGridViewCheckBoxColumn.DataPropertyName = "Tested";
            this.testedDataGridViewCheckBoxColumn.HeaderText = "Tested";
            this.testedDataGridViewCheckBoxColumn.MinimumWidth = 6;
            this.testedDataGridViewCheckBoxColumn.Name = "testedDataGridViewCheckBoxColumn";
            this.testedDataGridViewCheckBoxColumn.ReadOnly = true;
            this.testedDataGridViewCheckBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.testedDataGridViewCheckBoxColumn.Width = 60;
            // 
            // passedDataGridViewCheckBoxColumn
            // 
            this.passedDataGridViewCheckBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.passedDataGridViewCheckBoxColumn.DataPropertyName = "Passed";
            this.passedDataGridViewCheckBoxColumn.HeaderText = "Passed";
            this.passedDataGridViewCheckBoxColumn.MinimumWidth = 6;
            this.passedDataGridViewCheckBoxColumn.Name = "passedDataGridViewCheckBoxColumn";
            this.passedDataGridViewCheckBoxColumn.ReadOnly = true;
            this.passedDataGridViewCheckBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.passedDataGridViewCheckBoxColumn.Width = 60;
            // 
            // errorsDataGridViewTextBoxColumn
            // 
            this.errorsDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.errorsDataGridViewTextBoxColumn.DataPropertyName = "Errors";
            this.errorsDataGridViewTextBoxColumn.HeaderText = "Errors";
            this.errorsDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.errorsDataGridViewTextBoxColumn.Name = "errorsDataGridViewTextBoxColumn";
            this.errorsDataGridViewTextBoxColumn.ReadOnly = true;
            this.errorsDataGridViewTextBoxColumn.Width = 90;
            // 
            // loadDataGridViewTextBoxColumn
            // 
            this.loadDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.loadDataGridViewTextBoxColumn.DataPropertyName = "Load";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.loadDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.loadDataGridViewTextBoxColumn.HeaderText = "Load Time";
            this.loadDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.loadDataGridViewTextBoxColumn.Name = "loadDataGridViewTextBoxColumn";
            this.loadDataGridViewTextBoxColumn.ReadOnly = true;
            this.loadDataGridViewTextBoxColumn.Width = 70;
            // 
            // fpsDataGridViewTextBoxColumn
            // 
            this.fpsDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.fpsDataGridViewTextBoxColumn.DataPropertyName = "FPS";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.fpsDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            this.fpsDataGridViewTextBoxColumn.HeaderText = "FPS";
            this.fpsDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.fpsDataGridViewTextBoxColumn.Name = "fpsDataGridViewTextBoxColumn";
            this.fpsDataGridViewTextBoxColumn.ReadOnly = true;
            this.fpsDataGridViewTextBoxColumn.Width = 70;
            // 
            // testBindingSource
            // 
            this.testBindingSource.DataSource = typeof(Orts.Models.Simplified.TestActivity);
            // 
            // buttonDetails
            // 
            this.buttonDetails.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDetails.Location = new System.Drawing.Point(983, 543);
            this.buttonDetails.Margin = new System.Windows.Forms.Padding(4);
            this.buttonDetails.Name = "buttonDetails";
            this.buttonDetails.Size = new System.Drawing.Size(100, 28);
            this.buttonDetails.TabIndex = 7;
            this.buttonDetails.Text = "Details";
            this.buttonDetails.UseVisualStyleBackColor = true;
            this.buttonDetails.Click += new System.EventHandler(this.ButtonDetails_Click);
            // 
            // checkBoxOverride
            // 
            this.checkBoxOverride.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBoxOverride.Location = new System.Drawing.Point(340, 543);
            this.checkBoxOverride.Margin = new System.Windows.Forms.Padding(4);
            this.checkBoxOverride.Name = "checkBoxOverride";
            this.checkBoxOverride.Size = new System.Drawing.Size(419, 28);
            this.checkBoxOverride.TabIndex = 4;
            this.checkBoxOverride.Text = "Override user settings when running tests";
            this.checkBoxOverride.UseVisualStyleBackColor = true;
            // 
            // buttonNoSort
            // 
            this.buttonNoSort.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonNoSort.Location = new System.Drawing.Point(767, 543);
            this.buttonNoSort.Margin = new System.Windows.Forms.Padding(4);
            this.buttonNoSort.Name = "buttonNoSort";
            this.buttonNoSort.Size = new System.Drawing.Size(100, 28);
            this.buttonNoSort.TabIndex = 5;
            this.buttonNoSort.Text = "Clear sort";
            this.buttonNoSort.UseVisualStyleBackColor = true;
            this.buttonNoSort.Click += new System.EventHandler(this.ButtonNoSort_Click);
            // 
            // panelTests
            // 
            this.panelTests.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelTests.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelTests.Controls.Add(this.gridTestActivities);
            this.panelTests.Location = new System.Drawing.Point(16, 15);
            this.panelTests.Margin = new System.Windows.Forms.Padding(4);
            this.panelTests.Name = "panelTests";
            this.panelTests.Size = new System.Drawing.Size(1066, 520);
            this.panelTests.TabIndex = 13;
            // 
            // TestingForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1099, 586);
            this.Controls.Add(this.panelTests);
            this.Controls.Add(this.buttonNoSort);
            this.Controls.Add(this.buttonDetails);
            this.Controls.Add(this.buttonSummary);
            this.Controls.Add(this.checkBoxOverride);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonTest);
            this.Controls.Add(this.buttonTestAll);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimizeBox = false;
            this.Name = "TestingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Testing";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TestingForm_FormClosing);
            this.Shown += new System.EventHandler(this.TestingForm_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.gridTestActivities)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.testBindingSource)).EndInit();
            this.panelTests.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonTestAll;
        private System.Windows.Forms.Button buttonTest;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonSummary;
        private System.Windows.Forms.DataGridView gridTestActivities;
        private System.Windows.Forms.BindingSource testBindingSource;
        private System.Windows.Forms.Button buttonDetails;
        private System.Windows.Forms.CheckBox checkBoxOverride;
        private System.Windows.Forms.Button buttonNoSort;
        private System.Windows.Forms.Panel panelTests;
        private System.Windows.Forms.DataGridViewCheckBoxColumn toTestDataGridViewCheckBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn activityFilePathDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn defaultSortDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn routeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn activityDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn testedDataGridViewCheckBoxColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn passedDataGridViewCheckBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn errorsDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn loadDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn fpsDataGridViewTextBoxColumn;
    }
}
