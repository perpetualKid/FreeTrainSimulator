using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Menu
{
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
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TestingForm));
            buttonTestAll = new System.Windows.Forms.Button();
            buttonTest = new System.Windows.Forms.Button();
            buttonCancel = new System.Windows.Forms.Button();
            buttonSummary = new System.Windows.Forms.Button();
            gridTestActivities = new System.Windows.Forms.DataGridView();
            testBindingSource = new System.Windows.Forms.BindingSource(components);
            buttonDetails = new System.Windows.Forms.Button();
            checkBoxOverride = new System.Windows.Forms.CheckBox();
            buttonNoSort = new System.Windows.Forms.Button();
            panelTests = new System.Windows.Forms.Panel();
            activityFilePathDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            defaultSortDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Folder = new System.Windows.Forms.DataGridViewTextBoxColumn();
            routeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            activityDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            testedDataGridViewCheckBoxColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            passedDataGridViewCheckBoxColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            errorsDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            loadDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            fpsDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)gridTestActivities).BeginInit();
            ((System.ComponentModel.ISupportInitialize)testBindingSource).BeginInit();
            panelTests.SuspendLayout();
            SuspendLayout();
            // 
            // buttonTestAll
            // 
            buttonTestAll.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            buttonTestAll.Location = new System.Drawing.Point(16, 543);
            buttonTestAll.Margin = new System.Windows.Forms.Padding(4);
            buttonTestAll.Name = "buttonTestAll";
            buttonTestAll.Size = new System.Drawing.Size(100, 28);
            buttonTestAll.TabIndex = 1;
            buttonTestAll.Text = "Test all";
            buttonTestAll.UseVisualStyleBackColor = true;
            buttonTestAll.Click += ButtonTestAll_Click;
            // 
            // buttonTest
            // 
            buttonTest.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            buttonTest.Location = new System.Drawing.Point(124, 543);
            buttonTest.Margin = new System.Windows.Forms.Padding(4);
            buttonTest.Name = "buttonTest";
            buttonTest.Size = new System.Drawing.Size(100, 28);
            buttonTest.TabIndex = 2;
            buttonTest.Text = "Test";
            buttonTest.UseVisualStyleBackColor = true;
            buttonTest.Click += ButtonTest_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            buttonCancel.Location = new System.Drawing.Point(232, 543);
            buttonCancel.Margin = new System.Windows.Forms.Padding(4);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new System.Drawing.Size(100, 28);
            buttonCancel.TabIndex = 3;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // buttonSummary
            // 
            buttonSummary.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonSummary.Location = new System.Drawing.Point(875, 543);
            buttonSummary.Margin = new System.Windows.Forms.Padding(4);
            buttonSummary.Name = "buttonSummary";
            buttonSummary.Size = new System.Drawing.Size(100, 28);
            buttonSummary.TabIndex = 6;
            buttonSummary.Text = "Summary";
            buttonSummary.UseVisualStyleBackColor = true;
            buttonSummary.Click += ButtonSummary_Click;
            // 
            // gridTestActivities
            // 
            gridTestActivities.AllowUserToAddRows = false;
            gridTestActivities.AllowUserToDeleteRows = false;
            gridTestActivities.AutoGenerateColumns = false;
            gridTestActivities.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            gridTestActivities.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            gridTestActivities.BackgroundColor = System.Drawing.SystemColors.Window;
            gridTestActivities.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridTestActivities.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            gridTestActivities.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            gridTestActivities.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            gridTestActivities.ColumnHeadersHeight = 29;
            gridTestActivities.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridTestActivities.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { activityFilePathDataGridViewTextBoxColumn, defaultSortDataGridViewTextBoxColumn, Folder, routeDataGridViewTextBoxColumn, activityDataGridViewTextBoxColumn, testedDataGridViewCheckBoxColumn, passedDataGridViewCheckBoxColumn, errorsDataGridViewTextBoxColumn, loadDataGridViewTextBoxColumn, fpsDataGridViewTextBoxColumn });
            gridTestActivities.DataSource = testBindingSource;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            gridTestActivities.DefaultCellStyle = dataGridViewCellStyle4;
            gridTestActivities.Dock = System.Windows.Forms.DockStyle.Fill;
            gridTestActivities.Location = new System.Drawing.Point(0, 0);
            gridTestActivities.Margin = new System.Windows.Forms.Padding(4);
            gridTestActivities.Name = "gridTestActivities";
            gridTestActivities.ReadOnly = true;
            gridTestActivities.RowHeadersVisible = false;
            gridTestActivities.RowHeadersWidth = 51;
            gridTestActivities.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            gridTestActivities.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridTestActivities.Size = new System.Drawing.Size(1064, 518);
            gridTestActivities.TabIndex = 0;
            // 
            // testBindingSource
            // 
            testBindingSource.DataSource = typeof(TestActivityModel);
            // 
            // buttonDetails
            // 
            buttonDetails.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonDetails.Location = new System.Drawing.Point(983, 543);
            buttonDetails.Margin = new System.Windows.Forms.Padding(4);
            buttonDetails.Name = "buttonDetails";
            buttonDetails.Size = new System.Drawing.Size(100, 28);
            buttonDetails.TabIndex = 7;
            buttonDetails.Text = "Details";
            buttonDetails.UseVisualStyleBackColor = true;
            buttonDetails.Click += ButtonDetails_Click;
            // 
            // checkBoxOverride
            // 
            checkBoxOverride.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            checkBoxOverride.Location = new System.Drawing.Point(340, 543);
            checkBoxOverride.Margin = new System.Windows.Forms.Padding(4);
            checkBoxOverride.Name = "checkBoxOverride";
            checkBoxOverride.Size = new System.Drawing.Size(419, 28);
            checkBoxOverride.TabIndex = 4;
            checkBoxOverride.Text = "Override user settings when running tests";
            checkBoxOverride.UseVisualStyleBackColor = true;
            // 
            // buttonNoSort
            // 
            buttonNoSort.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonNoSort.Location = new System.Drawing.Point(767, 543);
            buttonNoSort.Margin = new System.Windows.Forms.Padding(4);
            buttonNoSort.Name = "buttonNoSort";
            buttonNoSort.Size = new System.Drawing.Size(100, 28);
            buttonNoSort.TabIndex = 5;
            buttonNoSort.Text = "Clear sort";
            buttonNoSort.UseVisualStyleBackColor = true;
            buttonNoSort.Click += ButtonNoSort_Click;
            // 
            // panelTests
            // 
            panelTests.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelTests.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelTests.Controls.Add(gridTestActivities);
            panelTests.Location = new System.Drawing.Point(16, 15);
            panelTests.Margin = new System.Windows.Forms.Padding(4);
            panelTests.Name = "panelTests";
            panelTests.Size = new System.Drawing.Size(1066, 520);
            panelTests.TabIndex = 13;
            // 
            // activityFilePathDataGridViewTextBoxColumn
            // 
            activityFilePathDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            activityFilePathDataGridViewTextBoxColumn.DataPropertyName = "ActivityFilePath";
            activityFilePathDataGridViewTextBoxColumn.HeaderText = "ActivityFilePath";
            activityFilePathDataGridViewTextBoxColumn.MinimumWidth = 6;
            activityFilePathDataGridViewTextBoxColumn.Name = "activityFilePathDataGridViewTextBoxColumn";
            activityFilePathDataGridViewTextBoxColumn.ReadOnly = true;
            activityFilePathDataGridViewTextBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            activityFilePathDataGridViewTextBoxColumn.Visible = false;
            activityFilePathDataGridViewTextBoxColumn.Width = 20;
            // 
            // defaultSortDataGridViewTextBoxColumn
            // 
            defaultSortDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            defaultSortDataGridViewTextBoxColumn.DataPropertyName = "DefaultSort";
            defaultSortDataGridViewTextBoxColumn.HeaderText = "DefaultSort";
            defaultSortDataGridViewTextBoxColumn.MinimumWidth = 6;
            defaultSortDataGridViewTextBoxColumn.Name = "defaultSortDataGridViewTextBoxColumn";
            defaultSortDataGridViewTextBoxColumn.ReadOnly = true;
            defaultSortDataGridViewTextBoxColumn.Visible = false;
            defaultSortDataGridViewTextBoxColumn.Width = 20;
            // 
            // Folder
            // 
            Folder.DataPropertyName = "Folder";
            Folder.HeaderText = "Folder";
            Folder.Name = "Folder";
            Folder.ReadOnly = true;
            Folder.Width = 65;
            // 
            // routeDataGridViewTextBoxColumn
            // 
            routeDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            routeDataGridViewTextBoxColumn.DataPropertyName = "Route";
            routeDataGridViewTextBoxColumn.HeaderText = "Route";
            routeDataGridViewTextBoxColumn.MinimumWidth = 6;
            routeDataGridViewTextBoxColumn.Name = "routeDataGridViewTextBoxColumn";
            routeDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // activityDataGridViewTextBoxColumn
            // 
            activityDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            activityDataGridViewTextBoxColumn.DataPropertyName = "Activity";
            activityDataGridViewTextBoxColumn.HeaderText = "Activity";
            activityDataGridViewTextBoxColumn.MinimumWidth = 6;
            activityDataGridViewTextBoxColumn.Name = "activityDataGridViewTextBoxColumn";
            activityDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // testedDataGridViewCheckBoxColumn
            // 
            testedDataGridViewCheckBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            testedDataGridViewCheckBoxColumn.DataPropertyName = "Tested";
            testedDataGridViewCheckBoxColumn.HeaderText = "Tested";
            testedDataGridViewCheckBoxColumn.MinimumWidth = 6;
            testedDataGridViewCheckBoxColumn.Name = "testedDataGridViewCheckBoxColumn";
            testedDataGridViewCheckBoxColumn.ReadOnly = true;
            testedDataGridViewCheckBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            testedDataGridViewCheckBoxColumn.Width = 60;
            // 
            // passedDataGridViewCheckBoxColumn
            // 
            passedDataGridViewCheckBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            passedDataGridViewCheckBoxColumn.DataPropertyName = "Passed";
            passedDataGridViewCheckBoxColumn.HeaderText = "Passed";
            passedDataGridViewCheckBoxColumn.MinimumWidth = 6;
            passedDataGridViewCheckBoxColumn.Name = "passedDataGridViewCheckBoxColumn";
            passedDataGridViewCheckBoxColumn.ReadOnly = true;
            passedDataGridViewCheckBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            passedDataGridViewCheckBoxColumn.Width = 60;
            // 
            // errorsDataGridViewTextBoxColumn
            // 
            errorsDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            errorsDataGridViewTextBoxColumn.DataPropertyName = "Errors";
            errorsDataGridViewTextBoxColumn.HeaderText = "Errors";
            errorsDataGridViewTextBoxColumn.MinimumWidth = 6;
            errorsDataGridViewTextBoxColumn.Name = "errorsDataGridViewTextBoxColumn";
            errorsDataGridViewTextBoxColumn.ReadOnly = true;
            errorsDataGridViewTextBoxColumn.Width = 90;
            // 
            // loadDataGridViewTextBoxColumn
            // 
            loadDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            loadDataGridViewTextBoxColumn.DataPropertyName = "Load";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            loadDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            loadDataGridViewTextBoxColumn.HeaderText = "Load Time";
            loadDataGridViewTextBoxColumn.MinimumWidth = 6;
            loadDataGridViewTextBoxColumn.Name = "loadDataGridViewTextBoxColumn";
            loadDataGridViewTextBoxColumn.ReadOnly = true;
            loadDataGridViewTextBoxColumn.Width = 70;
            // 
            // fpsDataGridViewTextBoxColumn
            // 
            fpsDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            fpsDataGridViewTextBoxColumn.DataPropertyName = "FPS";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            fpsDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            fpsDataGridViewTextBoxColumn.HeaderText = "FPS";
            fpsDataGridViewTextBoxColumn.MinimumWidth = 6;
            fpsDataGridViewTextBoxColumn.Name = "fpsDataGridViewTextBoxColumn";
            fpsDataGridViewTextBoxColumn.ReadOnly = true;
            fpsDataGridViewTextBoxColumn.Width = 70;
            // 
            // TestingForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1099, 586);
            Controls.Add(panelTests);
            Controls.Add(buttonNoSort);
            Controls.Add(buttonDetails);
            Controls.Add(buttonSummary);
            Controls.Add(checkBoxOverride);
            Controls.Add(buttonCancel);
            Controls.Add(buttonTest);
            Controls.Add(buttonTestAll);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            MinimizeBox = false;
            Name = "TestingForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Testing";
            FormClosing += TestingForm_FormClosing;
            Shown += TestingForm_Shown;
            ((System.ComponentModel.ISupportInitialize)gridTestActivities).EndInit();
            ((System.ComponentModel.ISupportInitialize)testBindingSource).EndInit();
            panelTests.ResumeLayout(false);
            ResumeLayout(false);
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
        private System.Windows.Forms.DataGridViewTextBoxColumn activityFilePathDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn defaultSortDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn Folder;
        private System.Windows.Forms.DataGridViewTextBoxColumn routeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn activityDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn testedDataGridViewCheckBoxColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn passedDataGridViewCheckBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn errorsDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn loadDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn fpsDataGridViewTextBoxColumn;
    }
}
