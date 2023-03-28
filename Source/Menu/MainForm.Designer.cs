// #define INCLUDE_TIMETABLE_INPUT

namespace Orts.Menu
{
    partial class MainForm
    {
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.buttonStart = new System.Windows.Forms.Button();
            this.labelLogo = new System.Windows.Forms.Label();
            this.folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.checkBoxWarnings = new System.Windows.Forms.CheckBox();
            this.buttonOptions = new System.Windows.Forms.Button();
            this.buttonResume = new System.Windows.Forms.Button();
            this.buttonTools = new System.Windows.Forms.Button();
            this.comboBoxFolder = new System.Windows.Forms.ComboBox();
            this.comboBoxRoute = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxMPHost = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.textBoxMPUser = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.buttonStartMP = new System.Windows.Forms.Button();
            this.buttonResumeMP = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.panelDetails = new System.Windows.Forms.Panel();
            this.pictureBoxLogo = new System.Windows.Forms.PictureBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.buttonDocuments = new System.Windows.Forms.Button();
            this.label25 = new System.Windows.Forms.Label();
            this.radioButtonModeActivity = new System.Windows.Forms.RadioButton();
            this.radioButtonModeTimetable = new System.Windows.Forms.RadioButton();
            this.panelModeActivity = new System.Windows.Forms.Panel();
            this.comboBoxHeadTo = new System.Windows.Forms.ComboBox();
            this.comboBoxStartAt = new System.Windows.Forms.ComboBox();
            this.comboBoxConsist = new System.Windows.Forms.ComboBox();
            this.comboBoxLocomotive = new System.Windows.Forms.ComboBox();
            this.comboBoxActivity = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.comboBoxStartTime = new System.Windows.Forms.ComboBox();
            this.comboBoxDuration = new System.Windows.Forms.ComboBox();
            this.comboBoxStartWeather = new System.Windows.Forms.ComboBox();
            this.label12 = new System.Windows.Forms.Label();
            this.comboBoxStartSeason = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.comboBoxDifficulty = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.panelModeTimetable = new System.Windows.Forms.Panel();
            this.labelTimetableWeatherFile = new System.Windows.Forms.Label();
            this.comboBoxTimetableWeatherFile = new System.Windows.Forms.ComboBox();
            this.label24 = new System.Windows.Forms.Label();
            this.comboBoxTimetableTrain = new System.Windows.Forms.ComboBox();
            this.label23 = new System.Windows.Forms.Label();
            this.comboBoxTimetableDay = new System.Windows.Forms.ComboBox();
            this.label22 = new System.Windows.Forms.Label();
            this.comboBoxTimetableWeather = new System.Windows.Forms.ComboBox();
            this.label20 = new System.Windows.Forms.Label();
            this.comboBoxTimetableSeason = new System.Windows.Forms.ComboBox();
            this.label21 = new System.Windows.Forms.Label();
            this.comboBoxTimetable = new System.Windows.Forms.ComboBox();
            this.comboBoxTimetableSet = new System.Windows.Forms.ComboBox();
            this.label15 = new System.Windows.Forms.Label();
            this.linkLabelUpdate = new System.Windows.Forms.LinkLabel();
            this.testingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripTools = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.contextMenuStripDocuments = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
            this.panel1.SuspendLayout();
            this.panelModeActivity.SuspendLayout();
            this.panelModeTimetable.SuspendLayout();
            this.contextMenuStripTools.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonStart
            // 
            this.buttonStart.Enabled = false;
            this.buttonStart.Location = new System.Drawing.Point(8, 22);
            this.buttonStart.Margin = new System.Windows.Forms.Padding(4);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(100, 58);
            this.buttonStart.TabIndex = 0;
            this.buttonStart.Text = "Start";
            this.buttonStart.Click += new System.EventHandler(this.ButtonStart_Click);
            // 
            // labelLogo
            // 
            this.labelLogo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelLogo.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.labelLogo.ForeColor = System.Drawing.Color.Gray;
            this.labelLogo.Location = new System.Drawing.Point(109, 581);
            this.labelLogo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelLogo.Name = "labelLogo";
            this.labelLogo.Size = new System.Drawing.Size(299, 79);
            this.labelLogo.TabIndex = 11;
            this.labelLogo.Text = "Free Train Simulator";
            this.labelLogo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelLogo.UseMnemonic = false;
            // 
            // folderBrowserDialog
            // 
            this.folderBrowserDialog.Description = "Navigate to your alternate MSTS installation folder.";
            this.folderBrowserDialog.ShowNewFolderButton = false;
            // 
            // checkBoxWarnings
            // 
            this.checkBoxWarnings.AutoSize = true;
            this.checkBoxWarnings.Checked = true;
            this.checkBoxWarnings.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxWarnings.Location = new System.Drawing.Point(145, 59);
            this.checkBoxWarnings.Margin = new System.Windows.Forms.Padding(4);
            this.checkBoxWarnings.Name = "checkBoxWarnings";
            this.checkBoxWarnings.Size = new System.Drawing.Size(70, 19);
            this.checkBoxWarnings.TabIndex = 1;
            this.checkBoxWarnings.Text = "Logging";
            this.checkBoxWarnings.UseVisualStyleBackColor = true;
            // 
            // buttonOptions
            // 
            this.buttonOptions.Location = new System.Drawing.Point(145, 22);
            this.buttonOptions.Margin = new System.Windows.Forms.Padding(4);
            this.buttonOptions.Name = "buttonOptions";
            this.buttonOptions.Size = new System.Drawing.Size(100, 28);
            this.buttonOptions.TabIndex = 0;
            this.buttonOptions.Text = "Options";
            this.buttonOptions.Click += new System.EventHandler(this.ButtonOptions_Click);
            // 
            // buttonResume
            // 
            this.buttonResume.Enabled = false;
            this.buttonResume.Location = new System.Drawing.Point(9, 98);
            this.buttonResume.Margin = new System.Windows.Forms.Padding(4);
            this.buttonResume.Name = "buttonResume";
            this.buttonResume.Size = new System.Drawing.Size(100, 42);
            this.buttonResume.TabIndex = 1;
            this.buttonResume.Text = "Resume/ Replay...";
            this.buttonResume.Click += new System.EventHandler(this.ButtonResume_Click);
            // 
            // buttonTools
            // 
            this.buttonTools.Location = new System.Drawing.Point(4, 22);
            this.buttonTools.Margin = new System.Windows.Forms.Padding(4);
            this.buttonTools.Name = "buttonTools";
            this.buttonTools.Size = new System.Drawing.Size(132, 28);
            this.buttonTools.TabIndex = 19;
            this.buttonTools.Text = "Tools ▼";
            this.buttonTools.Click += new System.EventHandler(this.ButtonTools_Click);
            // 
            // comboBoxFolder
            // 
            this.comboBoxFolder.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxFolder.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxFolder.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFolder.FormattingEnabled = true;
            this.comboBoxFolder.Location = new System.Drawing.Point(16, 38);
            this.comboBoxFolder.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxFolder.Name = "comboBoxFolder";
            this.comboBoxFolder.Size = new System.Drawing.Size(373, 23);
            this.comboBoxFolder.TabIndex = 1;
            this.comboBoxFolder.SelectedIndexChanged += new System.EventHandler(this.ComboBoxFolder_SelectedIndexChanged);
            // 
            // comboBoxRoute
            // 
            this.comboBoxRoute.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxRoute.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxRoute.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRoute.FormattingEnabled = true;
            this.comboBoxRoute.Location = new System.Drawing.Point(16, 95);
            this.comboBoxRoute.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxRoute.Name = "comboBoxRoute";
            this.comboBoxRoute.Size = new System.Drawing.Size(373, 23);
            this.comboBoxRoute.TabIndex = 3;
            this.comboBoxRoute.SelectedIndexChanged += new System.EventHandler(this.ComboBoxRoute_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 70);
            this.label2.Margin = new System.Windows.Forms.Padding(4);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(41, 15);
            this.label2.TabIndex = 2;
            this.label2.Text = "Route:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxMPHost
            // 
            this.textBoxMPHost.Location = new System.Drawing.Point(111, 55);
            this.textBoxMPHost.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxMPHost.Name = "textBoxMPHost";
            this.textBoxMPHost.Size = new System.Drawing.Size(206, 23);
            this.textBoxMPHost.TabIndex = 3;
            this.textBoxMPHost.TextChanged += new System.EventHandler(this.TextBoxMPUser_TextChanged);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(8, 59);
            this.label14.Margin = new System.Windows.Forms.Padding(4);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(62, 15);
            this.label14.TabIndex = 2;
            this.label14.Text = "Host/port:";
            this.label14.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(8, 28);
            this.label13.Margin = new System.Windows.Forms.Padding(4);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(66, 15);
            this.label13.TabIndex = 0;
            this.label13.Text = "User name:";
            this.label13.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxMPUser
            // 
            this.textBoxMPUser.Location = new System.Drawing.Point(111, 22);
            this.textBoxMPUser.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxMPUser.Name = "textBoxMPUser";
            this.textBoxMPUser.Size = new System.Drawing.Size(206, 23);
            this.textBoxMPUser.TabIndex = 1;
            this.textBoxMPUser.TextChanged += new System.EventHandler(this.TextBoxMPUser_TextChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.buttonStartMP);
            this.groupBox1.Controls.Add(this.buttonResumeMP);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.textBoxMPHost);
            this.groupBox1.Controls.Add(this.textBoxMPUser);
            this.groupBox1.Controls.Add(this.label14);
            this.groupBox1.Location = new System.Drawing.Point(796, 512);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox1.Size = new System.Drawing.Size(328, 148);
            this.groupBox1.TabIndex = 15;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Multiplayer";
            // 
            // buttonStartMP
            // 
            this.buttonStartMP.Enabled = false;
            this.buttonStartMP.Location = new System.Drawing.Point(111, 108);
            this.buttonStartMP.Margin = new System.Windows.Forms.Padding(4);
            this.buttonStartMP.Name = "buttonStartMP";
            this.buttonStartMP.Size = new System.Drawing.Size(100, 28);
            this.buttonStartMP.TabIndex = 7;
            this.buttonStartMP.Text = "Start MP";
            this.buttonStartMP.Click += new System.EventHandler(this.ButtonStartMP_Click);
            // 
            // buttonResumeMP
            // 
            this.buttonResumeMP.Enabled = false;
            this.buttonResumeMP.Location = new System.Drawing.Point(219, 108);
            this.buttonResumeMP.Margin = new System.Windows.Forms.Padding(4);
            this.buttonResumeMP.Name = "buttonResumeMP";
            this.buttonResumeMP.Size = new System.Drawing.Size(100, 28);
            this.buttonResumeMP.TabIndex = 6;
            this.buttonResumeMP.Text = "Resume MP";
            this.buttonResumeMP.Click += new System.EventHandler(this.ButtonResumeMP_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.buttonResume);
            this.groupBox3.Controls.Add(this.buttonStart);
            this.groupBox3.Location = new System.Drawing.Point(672, 512);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox3.Size = new System.Drawing.Size(116, 148);
            this.groupBox3.TabIndex = 14;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Singleplayer";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 12);
            this.label1.Margin = new System.Windows.Forms.Padding(4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(105, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Installation profile:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelDetails
            // 
            this.panelDetails.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelDetails.AutoScroll = true;
            this.panelDetails.BackColor = System.Drawing.SystemColors.Window;
            this.panelDetails.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelDetails.ForeColor = System.Drawing.SystemColors.WindowText;
            this.panelDetails.Location = new System.Drawing.Point(399, 38);
            this.panelDetails.Margin = new System.Windows.Forms.Padding(4);
            this.panelDetails.Name = "panelDetails";
            this.panelDetails.Size = new System.Drawing.Size(723, 466);
            this.panelDetails.TabIndex = 20;
            // 
            // pictureBoxLogo
            // 
            this.pictureBoxLogo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pictureBoxLogo.Image = ((System.Drawing.Image)(resources.GetObject("pictureBoxLogo.Image")));
            this.pictureBoxLogo.Location = new System.Drawing.Point(16, 581);
            this.pictureBoxLogo.Margin = new System.Windows.Forms.Padding(4);
            this.pictureBoxLogo.Name = "pictureBoxLogo";
            this.pictureBoxLogo.Size = new System.Drawing.Size(85, 79);
            this.pictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxLogo.TabIndex = 5;
            this.pictureBoxLogo.TabStop = false;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.buttonDocuments);
            this.panel1.Controls.Add(this.buttonOptions);
            this.panel1.Controls.Add(this.checkBoxWarnings);
            this.panel1.Controls.Add(this.buttonTools);
            this.panel1.Location = new System.Drawing.Point(415, 512);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(249, 148);
            this.panel1.TabIndex = 13;
            // 
            // buttonDocuments
            // 
            this.buttonDocuments.Location = new System.Drawing.Point(4, 59);
            this.buttonDocuments.Margin = new System.Windows.Forms.Padding(4);
            this.buttonDocuments.Name = "buttonDocuments";
            this.buttonDocuments.Size = new System.Drawing.Size(132, 28);
            this.buttonDocuments.TabIndex = 22;
            this.buttonDocuments.Text = "Documents ▼";
            this.buttonDocuments.UseVisualStyleBackColor = true;
            this.buttonDocuments.Click += new System.EventHandler(this.ButtonDocuments_Click);
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(16, 128);
            this.label25.Margin = new System.Windows.Forms.Padding(4);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(41, 15);
            this.label25.TabIndex = 4;
            this.label25.Text = "Mode:";
            this.label25.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // radioButtonModeActivity
            // 
            this.radioButtonModeActivity.Checked = true;
            this.radioButtonModeActivity.Location = new System.Drawing.Point(16, 152);
            this.radioButtonModeActivity.Margin = new System.Windows.Forms.Padding(4);
            this.radioButtonModeActivity.Name = "radioButtonModeActivity";
            this.radioButtonModeActivity.Size = new System.Drawing.Size(172, 25);
            this.radioButtonModeActivity.TabIndex = 6;
            this.radioButtonModeActivity.TabStop = true;
            this.radioButtonModeActivity.Text = "Activity";
            this.radioButtonModeActivity.UseVisualStyleBackColor = true;
            this.radioButtonModeActivity.CheckedChanged += new System.EventHandler(this.RadioButtonMode_CheckedChanged);
            // 
            // radioButtonModeTimetable
            // 
            this.radioButtonModeTimetable.Location = new System.Drawing.Point(218, 151);
            this.radioButtonModeTimetable.Margin = new System.Windows.Forms.Padding(4);
            this.radioButtonModeTimetable.Name = "radioButtonModeTimetable";
            this.radioButtonModeTimetable.Size = new System.Drawing.Size(172, 25);
            this.radioButtonModeTimetable.TabIndex = 7;
            this.radioButtonModeTimetable.Text = "Timetable";
            this.radioButtonModeTimetable.UseVisualStyleBackColor = true;
            this.radioButtonModeTimetable.CheckedChanged += new System.EventHandler(this.RadioButtonMode_CheckedChanged);
            // 
            // panelModeActivity
            // 
            this.panelModeActivity.Controls.Add(this.comboBoxHeadTo);
            this.panelModeActivity.Controls.Add(this.comboBoxStartAt);
            this.panelModeActivity.Controls.Add(this.comboBoxConsist);
            this.panelModeActivity.Controls.Add(this.comboBoxLocomotive);
            this.panelModeActivity.Controls.Add(this.comboBoxActivity);
            this.panelModeActivity.Controls.Add(this.label3);
            this.panelModeActivity.Controls.Add(this.label4);
            this.panelModeActivity.Controls.Add(this.label5);
            this.panelModeActivity.Controls.Add(this.label6);
            this.panelModeActivity.Controls.Add(this.label7);
            this.panelModeActivity.Controls.Add(this.label11);
            this.panelModeActivity.Controls.Add(this.label9);
            this.panelModeActivity.Controls.Add(this.comboBoxStartTime);
            this.panelModeActivity.Controls.Add(this.comboBoxDuration);
            this.panelModeActivity.Controls.Add(this.comboBoxStartWeather);
            this.panelModeActivity.Controls.Add(this.label12);
            this.panelModeActivity.Controls.Add(this.comboBoxStartSeason);
            this.panelModeActivity.Controls.Add(this.label10);
            this.panelModeActivity.Controls.Add(this.comboBoxDifficulty);
            this.panelModeActivity.Controls.Add(this.label8);
            this.panelModeActivity.Location = new System.Drawing.Point(12, 180);
            this.panelModeActivity.Margin = new System.Windows.Forms.Padding(0);
            this.panelModeActivity.Name = "panelModeActivity";
            this.panelModeActivity.Size = new System.Drawing.Size(382, 382);
            this.panelModeActivity.TabIndex = 9;
            // 
            // comboBoxHeadTo
            // 
            this.comboBoxHeadTo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxHeadTo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxHeadTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxHeadTo.Enabled = false;
            this.comboBoxHeadTo.FormattingEnabled = true;
            this.comboBoxHeadTo.Location = new System.Drawing.Point(4, 251);
            this.comboBoxHeadTo.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxHeadTo.Name = "comboBoxHeadTo";
            this.comboBoxHeadTo.Size = new System.Drawing.Size(373, 23);
            this.comboBoxHeadTo.TabIndex = 9;
            this.comboBoxHeadTo.SelectedIndexChanged += new System.EventHandler(this.ComboBoxHeadTo_SelectedIndexChanged);
            // 
            // comboBoxStartAt
            // 
            this.comboBoxStartAt.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxStartAt.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxStartAt.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStartAt.Enabled = false;
            this.comboBoxStartAt.FormattingEnabled = true;
            this.comboBoxStartAt.Location = new System.Drawing.Point(4, 194);
            this.comboBoxStartAt.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxStartAt.Name = "comboBoxStartAt";
            this.comboBoxStartAt.Size = new System.Drawing.Size(373, 23);
            this.comboBoxStartAt.TabIndex = 7;
            this.comboBoxStartAt.SelectedIndexChanged += new System.EventHandler(this.ComboBoxStartAt_SelectedIndexChanged);
            // 
            // comboBoxConsist
            // 
            this.comboBoxConsist.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxConsist.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxConsist.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxConsist.Enabled = false;
            this.comboBoxConsist.FormattingEnabled = true;
            this.comboBoxConsist.Location = new System.Drawing.Point(4, 138);
            this.comboBoxConsist.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxConsist.Name = "comboBoxConsist";
            this.comboBoxConsist.Size = new System.Drawing.Size(373, 23);
            this.comboBoxConsist.TabIndex = 5;
            this.comboBoxConsist.SelectedIndexChanged += new System.EventHandler(this.ComboBoxConsist_SelectedIndexChanged);
            // 
            // comboBoxLocomotive
            // 
            this.comboBoxLocomotive.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxLocomotive.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxLocomotive.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxLocomotive.Enabled = false;
            this.comboBoxLocomotive.FormattingEnabled = true;
            this.comboBoxLocomotive.Location = new System.Drawing.Point(4, 84);
            this.comboBoxLocomotive.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxLocomotive.Name = "comboBoxLocomotive";
            this.comboBoxLocomotive.Size = new System.Drawing.Size(373, 23);
            this.comboBoxLocomotive.TabIndex = 3;
            this.comboBoxLocomotive.SelectedIndexChanged += new System.EventHandler(this.ComboBoxLocomotive_SelectedIndexChanged);
            // 
            // comboBoxActivity
            // 
            this.comboBoxActivity.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxActivity.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxActivity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxActivity.FormattingEnabled = true;
            this.comboBoxActivity.Location = new System.Drawing.Point(4, 28);
            this.comboBoxActivity.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxActivity.Name = "comboBoxActivity";
            this.comboBoxActivity.Size = new System.Drawing.Size(373, 23);
            this.comboBoxActivity.TabIndex = 1;
            this.comboBoxActivity.SelectedIndexChanged += new System.EventHandler(this.ComboBoxActivity_SelectedIndexChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(4, 2);
            this.label3.Margin = new System.Windows.Forms.Padding(2);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(50, 15);
            this.label3.TabIndex = 0;
            this.label3.Text = "Activity:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(4, 59);
            this.label4.Margin = new System.Windows.Forms.Padding(4);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(73, 15);
            this.label4.TabIndex = 2;
            this.label4.Text = "Locomotive:";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(4, 112);
            this.label5.Margin = new System.Windows.Forms.Padding(4);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(49, 15);
            this.label5.TabIndex = 4;
            this.label5.Text = "Consist:";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(4, 169);
            this.label6.Margin = new System.Windows.Forms.Padding(4);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(64, 15);
            this.label6.TabIndex = 6;
            this.label6.Text = "Starting at:";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(4, 225);
            this.label7.Margin = new System.Windows.Forms.Padding(4);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(69, 15);
            this.label7.TabIndex = 8;
            this.label7.Text = "Heading to:";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(191, 288);
            this.label11.Margin = new System.Windows.Forms.Padding(2);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(56, 15);
            this.label11.TabIndex = 16;
            this.label11.Text = "Duration:";
            this.label11.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(5, 288);
            this.label9.Margin = new System.Windows.Forms.Padding(2);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(36, 15);
            this.label9.TabIndex = 10;
            this.label9.Text = "Time:";
            this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxStartTime
            // 
            this.comboBoxStartTime.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.comboBoxStartTime.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxStartTime.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStartTime.Enabled = false;
            this.comboBoxStartTime.FormattingEnabled = true;
            this.comboBoxStartTime.Location = new System.Drawing.Point(81, 284);
            this.comboBoxStartTime.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxStartTime.Name = "comboBoxStartTime";
            this.comboBoxStartTime.Size = new System.Drawing.Size(96, 23);
            this.comboBoxStartTime.TabIndex = 11;
            this.comboBoxStartTime.TextChanged += new System.EventHandler(this.ComboBoxStartTime_TextChanged);
            // 
            // comboBoxDuration
            // 
            this.comboBoxDuration.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxDuration.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxDuration.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDuration.Enabled = false;
            this.comboBoxDuration.FormattingEnabled = true;
            this.comboBoxDuration.Location = new System.Drawing.Point(281, 284);
            this.comboBoxDuration.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxDuration.Name = "comboBoxDuration";
            this.comboBoxDuration.Size = new System.Drawing.Size(96, 23);
            this.comboBoxDuration.TabIndex = 17;
            // 
            // comboBoxStartWeather
            // 
            this.comboBoxStartWeather.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxStartWeather.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxStartWeather.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStartWeather.Enabled = false;
            this.comboBoxStartWeather.FormattingEnabled = true;
            this.comboBoxStartWeather.Location = new System.Drawing.Point(81, 352);
            this.comboBoxStartWeather.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxStartWeather.Name = "comboBoxStartWeather";
            this.comboBoxStartWeather.Size = new System.Drawing.Size(96, 23);
            this.comboBoxStartWeather.TabIndex = 15;
            this.comboBoxStartWeather.SelectedIndexChanged += new System.EventHandler(this.ComboBoxStartWeather_SelectedIndexChanged);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(5, 356);
            this.label12.Margin = new System.Windows.Forms.Padding(2);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(54, 15);
            this.label12.TabIndex = 14;
            this.label12.Text = "Weather:";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxStartSeason
            // 
            this.comboBoxStartSeason.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxStartSeason.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxStartSeason.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStartSeason.Enabled = false;
            this.comboBoxStartSeason.FormattingEnabled = true;
            this.comboBoxStartSeason.Location = new System.Drawing.Point(81, 319);
            this.comboBoxStartSeason.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxStartSeason.Name = "comboBoxStartSeason";
            this.comboBoxStartSeason.Size = new System.Drawing.Size(96, 23);
            this.comboBoxStartSeason.TabIndex = 13;
            this.comboBoxStartSeason.SelectedIndexChanged += new System.EventHandler(this.ComboBoxStartSeason_SelectedIndexChanged);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(191, 322);
            this.label10.Margin = new System.Windows.Forms.Padding(2);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(58, 15);
            this.label10.TabIndex = 18;
            this.label10.Text = "Difficulty:";
            this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxDifficulty
            // 
            this.comboBoxDifficulty.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxDifficulty.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxDifficulty.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDifficulty.Enabled = false;
            this.comboBoxDifficulty.FormattingEnabled = true;
            this.comboBoxDifficulty.Location = new System.Drawing.Point(281, 319);
            this.comboBoxDifficulty.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxDifficulty.Name = "comboBoxDifficulty";
            this.comboBoxDifficulty.Size = new System.Drawing.Size(96, 23);
            this.comboBoxDifficulty.TabIndex = 19;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(5, 322);
            this.label8.Margin = new System.Windows.Forms.Padding(2);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 15);
            this.label8.TabIndex = 12;
            this.label8.Text = "Season:";
            this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelModeTimetable
            // 
            this.panelModeTimetable.Controls.Add(this.labelTimetableWeatherFile);
            this.panelModeTimetable.Controls.Add(this.comboBoxTimetableWeatherFile);
            this.panelModeTimetable.Controls.Add(this.label24);
            this.panelModeTimetable.Controls.Add(this.comboBoxTimetableTrain);
            this.panelModeTimetable.Controls.Add(this.label23);
            this.panelModeTimetable.Controls.Add(this.comboBoxTimetableDay);
            this.panelModeTimetable.Controls.Add(this.label22);
            this.panelModeTimetable.Controls.Add(this.comboBoxTimetableWeather);
            this.panelModeTimetable.Controls.Add(this.label20);
            this.panelModeTimetable.Controls.Add(this.comboBoxTimetableSeason);
            this.panelModeTimetable.Controls.Add(this.label21);
            this.panelModeTimetable.Controls.Add(this.comboBoxTimetable);
            this.panelModeTimetable.Controls.Add(this.comboBoxTimetableSet);
            this.panelModeTimetable.Controls.Add(this.label15);
            this.panelModeTimetable.Location = new System.Drawing.Point(399, 146);
            this.panelModeTimetable.Margin = new System.Windows.Forms.Padding(0);
            this.panelModeTimetable.Name = "panelModeTimetable";
            this.panelModeTimetable.Size = new System.Drawing.Size(382, 358);
            this.panelModeTimetable.TabIndex = 10;
            this.panelModeTimetable.Visible = false;
            // 
            // labelTimetableWeatherFile
            // 
            this.labelTimetableWeatherFile.AutoSize = true;
            this.labelTimetableWeatherFile.Location = new System.Drawing.Point(8, 261);
            this.labelTimetableWeatherFile.Margin = new System.Windows.Forms.Padding(4);
            this.labelTimetableWeatherFile.Name = "labelTimetableWeatherFile";
            this.labelTimetableWeatherFile.Size = new System.Drawing.Size(75, 15);
            this.labelTimetableWeatherFile.TabIndex = 14;
            this.labelTimetableWeatherFile.Text = "Weather File:";
            this.labelTimetableWeatherFile.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableWeatherFile
            // 
            this.comboBoxTimetableWeatherFile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTimetableWeatherFile.FormattingEnabled = true;
            this.comboBoxTimetableWeatherFile.Location = new System.Drawing.Point(121, 258);
            this.comboBoxTimetableWeatherFile.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTimetableWeatherFile.Name = "comboBoxTimetableWeatherFile";
            this.comboBoxTimetableWeatherFile.Size = new System.Drawing.Size(256, 23);
            this.comboBoxTimetableWeatherFile.TabIndex = 13;
            this.comboBoxTimetableWeatherFile.SelectedIndexChanged += new System.EventHandler(this.ComboBoxTimetableWeatherFile_SelectedIndexChanged);
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(4, 98);
            this.label24.Margin = new System.Windows.Forms.Padding(4);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(35, 15);
            this.label24.TabIndex = 4;
            this.label24.Text = "Train:";
            this.label24.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableTrain
            // 
            this.comboBoxTimetableTrain.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTimetableTrain.FormattingEnabled = true;
            this.comboBoxTimetableTrain.Location = new System.Drawing.Point(121, 94);
            this.comboBoxTimetableTrain.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTimetableTrain.Name = "comboBoxTimetableTrain";
            this.comboBoxTimetableTrain.Size = new System.Drawing.Size(256, 23);
            this.comboBoxTimetableTrain.TabIndex = 5;
            this.comboBoxTimetableTrain.SelectedIndexChanged += new System.EventHandler(this.ComboBoxTimetableTrain_SelectedIndexChanged);
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(4, 64);
            this.label23.Margin = new System.Windows.Forms.Padding(4);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(62, 15);
            this.label23.TabIndex = 2;
            this.label23.Text = "Timetable:";
            this.label23.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableDay
            // 
            this.comboBoxTimetableDay.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTimetableDay.Enabled = false;
            this.comboBoxTimetableDay.FormattingEnabled = true;
            this.comboBoxTimetableDay.Location = new System.Drawing.Point(121, 149);
            this.comboBoxTimetableDay.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTimetableDay.Name = "comboBoxTimetableDay";
            this.comboBoxTimetableDay.Size = new System.Drawing.Size(96, 23);
            this.comboBoxTimetableDay.TabIndex = 8;
            this.comboBoxTimetableDay.Visible = false;
            this.comboBoxTimetableDay.SelectedIndexChanged += new System.EventHandler(this.ComboBoxTimetableDay_SelectedIndexChanged);
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(8, 152);
            this.label22.Margin = new System.Windows.Forms.Padding(4);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(30, 15);
            this.label22.TabIndex = 7;
            this.label22.Text = "Day:";
            this.label22.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.label22.Visible = false;
            // 
            // comboBoxTimetableWeather
            // 
            this.comboBoxTimetableWeather.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxTimetableWeather.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxTimetableWeather.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTimetableWeather.FormattingEnabled = true;
            this.comboBoxTimetableWeather.Location = new System.Drawing.Point(121, 218);
            this.comboBoxTimetableWeather.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTimetableWeather.Name = "comboBoxTimetableWeather";
            this.comboBoxTimetableWeather.Size = new System.Drawing.Size(96, 23);
            this.comboBoxTimetableWeather.TabIndex = 12;
            this.comboBoxTimetableWeather.SelectedIndexChanged += new System.EventHandler(this.ComboBoxTimetableWeather_SelectedIndexChanged);
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(8, 220);
            this.label20.Margin = new System.Windows.Forms.Padding(4);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(54, 15);
            this.label20.TabIndex = 11;
            this.label20.Text = "Weather:";
            this.label20.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableSeason
            // 
            this.comboBoxTimetableSeason.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.comboBoxTimetableSeason.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxTimetableSeason.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTimetableSeason.FormattingEnabled = true;
            this.comboBoxTimetableSeason.Location = new System.Drawing.Point(121, 182);
            this.comboBoxTimetableSeason.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTimetableSeason.Name = "comboBoxTimetableSeason";
            this.comboBoxTimetableSeason.Size = new System.Drawing.Size(96, 23);
            this.comboBoxTimetableSeason.TabIndex = 10;
            this.comboBoxTimetableSeason.SelectedIndexChanged += new System.EventHandler(this.ComboBoxTimetableSeason_SelectedIndexChanged);
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(8, 188);
            this.label21.Margin = new System.Windows.Forms.Padding(4);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(47, 15);
            this.label21.TabIndex = 9;
            this.label21.Text = "Season:";
            this.label21.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetable
            // 
            this.comboBoxTimetable.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTimetable.FormattingEnabled = true;
            this.comboBoxTimetable.Location = new System.Drawing.Point(121, 60);
            this.comboBoxTimetable.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTimetable.Name = "comboBoxTimetable";
            this.comboBoxTimetable.Size = new System.Drawing.Size(256, 23);
            this.comboBoxTimetable.TabIndex = 3;
            this.comboBoxTimetable.SelectedIndexChanged += new System.EventHandler(this.ComboBoxTimetable_selectedIndexChanged);
            this.comboBoxTimetable.EnabledChanged += new System.EventHandler(this.ComboBoxTimetable_EnabledChanged);
            // 
            // comboBoxTimetableSet
            // 
            this.comboBoxTimetableSet.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTimetableSet.FormattingEnabled = true;
            this.comboBoxTimetableSet.Location = new System.Drawing.Point(4, 28);
            this.comboBoxTimetableSet.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxTimetableSet.Name = "comboBoxTimetableSet";
            this.comboBoxTimetableSet.Size = new System.Drawing.Size(373, 23);
            this.comboBoxTimetableSet.TabIndex = 1;
            this.comboBoxTimetableSet.SelectedIndexChanged += new System.EventHandler(this.ComboBoxTimetableSet_SelectedIndexChanged);
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(4, 4);
            this.label15.Margin = new System.Windows.Forms.Padding(4);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(80, 15);
            this.label15.TabIndex = 0;
            this.label15.Text = "Timetable set:";
            this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // linkLabelUpdate
            // 
            this.linkLabelUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.linkLabelUpdate.AutoSize = true;
            this.linkLabelUpdate.ImageAlign = System.Drawing.ContentAlignment.TopLeft;
            this.linkLabelUpdate.Location = new System.Drawing.Point(984, 12);
            this.linkLabelUpdate.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.linkLabelUpdate.Name = "linkLabelUpdate";
            this.linkLabelUpdate.Size = new System.Drawing.Size(110, 15);
            this.linkLabelUpdate.TabIndex = 37;
            this.linkLabelUpdate.TabStop = true;
            this.linkLabelUpdate.Text = "Link to next Update";
            this.linkLabelUpdate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.linkLabelUpdate.UseMnemonic = false;
            this.linkLabelUpdate.Visible = false;
            this.linkLabelUpdate.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelUpdate_LinkClicked);
            // 
            // testingToolStripMenuItem
            // 
            this.testingToolStripMenuItem.Name = "testingToolStripMenuItem";
            this.testingToolStripMenuItem.Size = new System.Drawing.Size(111, 22);
            this.testingToolStripMenuItem.Text = "Testing";
            this.testingToolStripMenuItem.Click += new System.EventHandler(this.TestingToolStripMenuItem_Click);
            // 
            // contextMenuStripTools
            // 
            this.contextMenuStripTools.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStripTools.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.testingToolStripMenuItem});
            this.contextMenuStripTools.Name = "contextMenuStrip1";
            this.contextMenuStripTools.Size = new System.Drawing.Size(112, 26);
            // 
            // contextMenuStripDocuments
            // 
            this.contextMenuStripDocuments.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStripDocuments.Name = "contextMenuStripDocuments";
            this.contextMenuStripDocuments.Size = new System.Drawing.Size(61, 4);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1139, 674);
            this.Controls.Add(this.panelModeTimetable);
            this.Controls.Add(this.panelModeActivity);
            this.Controls.Add(this.radioButtonModeTimetable);
            this.Controls.Add(this.radioButtonModeActivity);
            this.Controls.Add(this.label25);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.panelDetails);
            this.Controls.Add(this.comboBoxFolder);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.comboBoxRoute);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.pictureBoxLogo);
            this.Controls.Add(this.labelLogo);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.linkLabelUpdate);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Open Rails";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panelModeActivity.ResumeLayout(false);
            this.panelModeActivity.PerformLayout();
            this.panelModeTimetable.ResumeLayout(false);
            this.panelModeTimetable.PerformLayout();
            this.contextMenuStripTools.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Label labelLogo;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog;
        private System.Windows.Forms.CheckBox checkBoxWarnings;
        private System.Windows.Forms.Button buttonOptions;
        private System.Windows.Forms.Button buttonResume;
        private System.Windows.Forms.Button buttonTools;
        private System.Windows.Forms.ComboBox comboBoxFolder;
        private System.Windows.Forms.ComboBox comboBoxRoute;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox textBoxMPHost;
        private System.Windows.Forms.TextBox textBoxMPUser;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panelDetails;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.RadioButton radioButtonModeActivity;
        private System.Windows.Forms.RadioButton radioButtonModeTimetable;
        private System.Windows.Forms.Panel panelModeActivity;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboBoxActivity;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboBoxLocomotive;
        private System.Windows.Forms.ComboBox comboBoxConsist;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox comboBoxStartAt;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox comboBoxHeadTo;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox comboBoxStartTime;
        private System.Windows.Forms.ComboBox comboBoxDuration;
        private System.Windows.Forms.ComboBox comboBoxStartWeather;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.ComboBox comboBoxStartSeason;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.ComboBox comboBoxDifficulty;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Panel panelModeTimetable;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.ComboBox comboBoxTimetableTrain;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.ComboBox comboBoxTimetableDay;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.ComboBox comboBoxTimetableWeather;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.ComboBox comboBoxTimetableSeason;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.ComboBox comboBoxTimetable;
        private System.Windows.Forms.ComboBox comboBoxTimetableSet;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.LinkLabel linkLabelUpdate;
        private System.Windows.Forms.ToolStripMenuItem testingToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripTools;
        private System.Windows.Forms.Button buttonDocuments;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripDocuments;
        private System.Windows.Forms.Button buttonResumeMP;
        private System.Windows.Forms.Button buttonStartMP;
        private System.Windows.Forms.Label labelTimetableWeatherFile;
        private System.Windows.Forms.ComboBox comboBoxTimetableWeatherFile;
    }
}
