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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            buttonStart = new System.Windows.Forms.Button();
            labelLogo = new System.Windows.Forms.Label();
            folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            checkBoxWarnings = new System.Windows.Forms.CheckBox();
            buttonOptions = new System.Windows.Forms.Button();
            buttonResume = new System.Windows.Forms.Button();
            buttonTools = new System.Windows.Forms.Button();
            comboBoxFolder = new System.Windows.Forms.ComboBox();
            comboBoxRoute = new System.Windows.Forms.ComboBox();
            label2 = new System.Windows.Forms.Label();
            textBoxMPHost = new System.Windows.Forms.TextBox();
            label14 = new System.Windows.Forms.Label();
            label13 = new System.Windows.Forms.Label();
            textBoxMPUser = new System.Windows.Forms.TextBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            buttonConnectivityTest = new System.Windows.Forms.Button();
            buttonStartMP = new System.Windows.Forms.Button();
            groupBox3 = new System.Windows.Forms.GroupBox();
            label1 = new System.Windows.Forms.Label();
            panelDetails = new System.Windows.Forms.Panel();
            pictureBoxLogo = new System.Windows.Forms.PictureBox();
            panel1 = new System.Windows.Forms.Panel();
            buttonDocuments = new System.Windows.Forms.Button();
            label25 = new System.Windows.Forms.Label();
            radioButtonModeActivity = new System.Windows.Forms.RadioButton();
            radioButtonModeTimetable = new System.Windows.Forms.RadioButton();
            panelModeActivity = new System.Windows.Forms.Panel();
            comboBoxHeadTo = new System.Windows.Forms.ComboBox();
            comboBoxStartAt = new System.Windows.Forms.ComboBox();
            comboBoxConsist = new System.Windows.Forms.ComboBox();
            comboBoxLocomotive = new System.Windows.Forms.ComboBox();
            comboBoxActivity = new System.Windows.Forms.ComboBox();
            label3 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            label5 = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
            label7 = new System.Windows.Forms.Label();
            label11 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            comboBoxStartTime = new System.Windows.Forms.ComboBox();
            comboBoxDuration = new System.Windows.Forms.ComboBox();
            comboBoxStartWeather = new System.Windows.Forms.ComboBox();
            label12 = new System.Windows.Forms.Label();
            comboBoxStartSeason = new System.Windows.Forms.ComboBox();
            label10 = new System.Windows.Forms.Label();
            comboBoxDifficulty = new System.Windows.Forms.ComboBox();
            label8 = new System.Windows.Forms.Label();
            panelModeTimetable = new System.Windows.Forms.Panel();
            labelTimetableWeatherFile = new System.Windows.Forms.Label();
            comboBoxTimetableWeatherFile = new System.Windows.Forms.ComboBox();
            label24 = new System.Windows.Forms.Label();
            comboBoxTimetableTrain = new System.Windows.Forms.ComboBox();
            label23 = new System.Windows.Forms.Label();
            comboBoxTimetableDay = new System.Windows.Forms.ComboBox();
            label22 = new System.Windows.Forms.Label();
            comboBoxTimetableWeather = new System.Windows.Forms.ComboBox();
            label20 = new System.Windows.Forms.Label();
            comboBoxTimetableSeason = new System.Windows.Forms.ComboBox();
            label21 = new System.Windows.Forms.Label();
            comboBoxTimetable = new System.Windows.Forms.ComboBox();
            comboBoxTimetableSet = new System.Windows.Forms.ComboBox();
            label15 = new System.Windows.Forms.Label();
            linkLabelUpdate = new System.Windows.Forms.LinkLabel();
            testingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            contextMenuStripTools = new System.Windows.Forms.ContextMenuStrip(components);
            contextMenuStripDocuments = new System.Windows.Forms.ContextMenuStrip(components);
            groupBox1.SuspendLayout();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxLogo).BeginInit();
            panel1.SuspendLayout();
            panelModeActivity.SuspendLayout();
            panelModeTimetable.SuspendLayout();
            contextMenuStripTools.SuspendLayout();
            SuspendLayout();
            // 
            // buttonStart
            // 
            buttonStart.Enabled = false;
            buttonStart.Location = new System.Drawing.Point(8, 22);
            buttonStart.Margin = new System.Windows.Forms.Padding(4);
            buttonStart.Name = "buttonStart";
            buttonStart.Size = new System.Drawing.Size(100, 58);
            buttonStart.TabIndex = 0;
            buttonStart.Text = "Start";
            buttonStart.Click += ButtonStart_Click;
            // 
            // labelLogo
            // 
            labelLogo.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            labelLogo.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);
            labelLogo.ForeColor = System.Drawing.Color.Gray;
            labelLogo.Location = new System.Drawing.Point(109, 581);
            labelLogo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            labelLogo.Name = "labelLogo";
            labelLogo.Size = new System.Drawing.Size(299, 79);
            labelLogo.TabIndex = 11;
            labelLogo.Text = "Free Train Simulator";
            labelLogo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            labelLogo.UseMnemonic = false;
            // 
            // folderBrowserDialog
            // 
            folderBrowserDialog.Description = "Navigate to your alternate MSTS installation folder.";
            folderBrowserDialog.ShowNewFolderButton = false;
            // 
            // checkBoxWarnings
            // 
            checkBoxWarnings.AutoSize = true;
            checkBoxWarnings.Checked = true;
            checkBoxWarnings.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBoxWarnings.Location = new System.Drawing.Point(145, 59);
            checkBoxWarnings.Margin = new System.Windows.Forms.Padding(4);
            checkBoxWarnings.Name = "checkBoxWarnings";
            checkBoxWarnings.Size = new System.Drawing.Size(70, 19);
            checkBoxWarnings.TabIndex = 1;
            checkBoxWarnings.Text = "Logging";
            checkBoxWarnings.UseVisualStyleBackColor = true;
            // 
            // buttonOptions
            // 
            buttonOptions.Location = new System.Drawing.Point(145, 22);
            buttonOptions.Margin = new System.Windows.Forms.Padding(4);
            buttonOptions.Name = "buttonOptions";
            buttonOptions.Size = new System.Drawing.Size(100, 28);
            buttonOptions.TabIndex = 0;
            buttonOptions.Text = "Options";
            buttonOptions.Click += ButtonOptions_Click;
            // 
            // buttonResume
            // 
            buttonResume.Enabled = false;
            buttonResume.Location = new System.Drawing.Point(9, 98);
            buttonResume.Margin = new System.Windows.Forms.Padding(4);
            buttonResume.Name = "buttonResume";
            buttonResume.Size = new System.Drawing.Size(100, 42);
            buttonResume.TabIndex = 1;
            buttonResume.Text = "Resume/ Replay...";
            buttonResume.Click += ButtonResume_Click;
            // 
            // buttonTools
            // 
            buttonTools.Location = new System.Drawing.Point(4, 22);
            buttonTools.Margin = new System.Windows.Forms.Padding(4);
            buttonTools.Name = "buttonTools";
            buttonTools.Size = new System.Drawing.Size(132, 28);
            buttonTools.TabIndex = 19;
            buttonTools.Text = "Tools ▼";
            buttonTools.Click += ButtonTools_Click;
            // 
            // comboBoxFolder
            // 
            comboBoxFolder.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxFolder.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxFolder.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxFolder.FormattingEnabled = true;
            comboBoxFolder.Location = new System.Drawing.Point(16, 38);
            comboBoxFolder.Margin = new System.Windows.Forms.Padding(4);
            comboBoxFolder.Name = "comboBoxFolder";
            comboBoxFolder.Size = new System.Drawing.Size(373, 23);
            comboBoxFolder.TabIndex = 1;
            comboBoxFolder.SelectedIndexChanged += ComboBoxFolder_SelectedIndexChanged;
            // 
            // comboBoxRoute
            // 
            comboBoxRoute.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxRoute.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxRoute.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxRoute.FormattingEnabled = true;
            comboBoxRoute.Location = new System.Drawing.Point(16, 95);
            comboBoxRoute.Margin = new System.Windows.Forms.Padding(4);
            comboBoxRoute.Name = "comboBoxRoute";
            comboBoxRoute.Size = new System.Drawing.Size(373, 23);
            comboBoxRoute.TabIndex = 3;
            comboBoxRoute.SelectedIndexChanged += ComboBoxRoute_SelectedIndexChanged;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(16, 70);
            label2.Margin = new System.Windows.Forms.Padding(4);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(41, 15);
            label2.TabIndex = 2;
            label2.Text = "Route:";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxMPHost
            // 
            textBoxMPHost.Location = new System.Drawing.Point(111, 55);
            textBoxMPHost.Margin = new System.Windows.Forms.Padding(4);
            textBoxMPHost.Name = "textBoxMPHost";
            textBoxMPHost.Size = new System.Drawing.Size(206, 23);
            textBoxMPHost.TabIndex = 3;
            textBoxMPHost.TextChanged += TextBoxMPUser_TextChanged;
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Location = new System.Drawing.Point(8, 59);
            label14.Margin = new System.Windows.Forms.Padding(4);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(57, 15);
            label14.TabIndex = 2;
            label14.Text = "Host:Port";
            label14.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new System.Drawing.Point(8, 28);
            label13.Margin = new System.Windows.Forms.Padding(4);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(63, 15);
            label13.TabIndex = 0;
            label13.Text = "User name";
            label13.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxMPUser
            // 
            textBoxMPUser.Location = new System.Drawing.Point(111, 22);
            textBoxMPUser.Margin = new System.Windows.Forms.Padding(4);
            textBoxMPUser.Name = "textBoxMPUser";
            textBoxMPUser.Size = new System.Drawing.Size(206, 23);
            textBoxMPUser.TabIndex = 1;
            textBoxMPUser.TextChanged += TextBoxMPUser_TextChanged;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            groupBox1.Controls.Add(buttonConnectivityTest);
            groupBox1.Controls.Add(buttonStartMP);
            groupBox1.Controls.Add(label13);
            groupBox1.Controls.Add(textBoxMPHost);
            groupBox1.Controls.Add(textBoxMPUser);
            groupBox1.Controls.Add(label14);
            groupBox1.Location = new System.Drawing.Point(796, 512);
            groupBox1.Margin = new System.Windows.Forms.Padding(4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new System.Windows.Forms.Padding(4);
            groupBox1.Size = new System.Drawing.Size(328, 148);
            groupBox1.TabIndex = 15;
            groupBox1.TabStop = false;
            groupBox1.Text = "Multiplayer";
            // 
            // buttonConnectivityTest
            // 
            buttonConnectivityTest.Enabled = false;
            buttonConnectivityTest.Location = new System.Drawing.Point(111, 108);
            buttonConnectivityTest.Margin = new System.Windows.Forms.Padding(4);
            buttonConnectivityTest.Name = "buttonConnectivityTest";
            buttonConnectivityTest.Size = new System.Drawing.Size(100, 28);
            buttonConnectivityTest.TabIndex = 8;
            buttonConnectivityTest.Text = "Test Connection";
            buttonConnectivityTest.Click += ButtonConnectivityTest_Click;
            // 
            // buttonStartMP
            // 
            buttonStartMP.Enabled = false;
            buttonStartMP.Location = new System.Drawing.Point(220, 108);
            buttonStartMP.Margin = new System.Windows.Forms.Padding(4);
            buttonStartMP.Name = "buttonStartMP";
            buttonStartMP.Size = new System.Drawing.Size(100, 28);
            buttonStartMP.TabIndex = 7;
            buttonStartMP.Text = "Start MP";
            buttonStartMP.Click += ButtonStartMP_Click;
            // 
            // groupBox3
            // 
            groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            groupBox3.Controls.Add(buttonResume);
            groupBox3.Controls.Add(buttonStart);
            groupBox3.Location = new System.Drawing.Point(672, 512);
            groupBox3.Margin = new System.Windows.Forms.Padding(4);
            groupBox3.Name = "groupBox3";
            groupBox3.Padding = new System.Windows.Forms.Padding(4);
            groupBox3.Size = new System.Drawing.Size(116, 148);
            groupBox3.TabIndex = 14;
            groupBox3.TabStop = false;
            groupBox3.Text = "Singleplayer";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(16, 12);
            label1.Margin = new System.Windows.Forms.Padding(4);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(105, 15);
            label1.TabIndex = 0;
            label1.Text = "Installation profile:";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelDetails
            // 
            panelDetails.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelDetails.AutoScroll = true;
            panelDetails.BackColor = System.Drawing.SystemColors.Window;
            panelDetails.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelDetails.ForeColor = System.Drawing.SystemColors.WindowText;
            panelDetails.Location = new System.Drawing.Point(399, 38);
            panelDetails.Margin = new System.Windows.Forms.Padding(4);
            panelDetails.Name = "panelDetails";
            panelDetails.Size = new System.Drawing.Size(723, 466);
            panelDetails.TabIndex = 20;
            // 
            // pictureBoxLogo
            // 
            pictureBoxLogo.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            pictureBoxLogo.Image = (System.Drawing.Image)resources.GetObject("pictureBoxLogo.Image");
            pictureBoxLogo.Location = new System.Drawing.Point(16, 581);
            pictureBoxLogo.Margin = new System.Windows.Forms.Padding(4);
            pictureBoxLogo.Name = "pictureBoxLogo";
            pictureBoxLogo.Size = new System.Drawing.Size(85, 79);
            pictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pictureBoxLogo.TabIndex = 5;
            pictureBoxLogo.TabStop = false;
            // 
            // panel1
            // 
            panel1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            panel1.Controls.Add(buttonDocuments);
            panel1.Controls.Add(buttonOptions);
            panel1.Controls.Add(checkBoxWarnings);
            panel1.Controls.Add(buttonTools);
            panel1.Location = new System.Drawing.Point(415, 512);
            panel1.Margin = new System.Windows.Forms.Padding(4);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(249, 148);
            panel1.TabIndex = 13;
            // 
            // buttonDocuments
            // 
            buttonDocuments.Location = new System.Drawing.Point(4, 59);
            buttonDocuments.Margin = new System.Windows.Forms.Padding(4);
            buttonDocuments.Name = "buttonDocuments";
            buttonDocuments.Size = new System.Drawing.Size(132, 28);
            buttonDocuments.TabIndex = 22;
            buttonDocuments.Text = "Documents ▼";
            buttonDocuments.UseVisualStyleBackColor = true;
            buttonDocuments.Click += ButtonDocuments_Click;
            // 
            // label25
            // 
            label25.AutoSize = true;
            label25.Location = new System.Drawing.Point(16, 128);
            label25.Margin = new System.Windows.Forms.Padding(4);
            label25.Name = "label25";
            label25.Size = new System.Drawing.Size(41, 15);
            label25.TabIndex = 4;
            label25.Text = "Mode:";
            label25.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // radioButtonModeActivity
            // 
            radioButtonModeActivity.Checked = true;
            radioButtonModeActivity.Location = new System.Drawing.Point(16, 152);
            radioButtonModeActivity.Margin = new System.Windows.Forms.Padding(4);
            radioButtonModeActivity.Name = "radioButtonModeActivity";
            radioButtonModeActivity.Size = new System.Drawing.Size(172, 25);
            radioButtonModeActivity.TabIndex = 6;
            radioButtonModeActivity.TabStop = true;
            radioButtonModeActivity.Text = "Activity";
            radioButtonModeActivity.UseVisualStyleBackColor = true;
            radioButtonModeActivity.CheckedChanged += RadioButtonMode_CheckedChanged;
            // 
            // radioButtonModeTimetable
            // 
            radioButtonModeTimetable.Location = new System.Drawing.Point(218, 151);
            radioButtonModeTimetable.Margin = new System.Windows.Forms.Padding(4);
            radioButtonModeTimetable.Name = "radioButtonModeTimetable";
            radioButtonModeTimetable.Size = new System.Drawing.Size(172, 25);
            radioButtonModeTimetable.TabIndex = 7;
            radioButtonModeTimetable.Text = "Timetable";
            radioButtonModeTimetable.UseVisualStyleBackColor = true;
            radioButtonModeTimetable.CheckedChanged += RadioButtonMode_CheckedChanged;
            // 
            // panelModeActivity
            // 
            panelModeActivity.Controls.Add(comboBoxHeadTo);
            panelModeActivity.Controls.Add(comboBoxStartAt);
            panelModeActivity.Controls.Add(comboBoxConsist);
            panelModeActivity.Controls.Add(comboBoxLocomotive);
            panelModeActivity.Controls.Add(comboBoxActivity);
            panelModeActivity.Controls.Add(label3);
            panelModeActivity.Controls.Add(label4);
            panelModeActivity.Controls.Add(label5);
            panelModeActivity.Controls.Add(label6);
            panelModeActivity.Controls.Add(label7);
            panelModeActivity.Controls.Add(label11);
            panelModeActivity.Controls.Add(label9);
            panelModeActivity.Controls.Add(comboBoxStartTime);
            panelModeActivity.Controls.Add(comboBoxDuration);
            panelModeActivity.Controls.Add(comboBoxStartWeather);
            panelModeActivity.Controls.Add(label12);
            panelModeActivity.Controls.Add(comboBoxStartSeason);
            panelModeActivity.Controls.Add(label10);
            panelModeActivity.Controls.Add(comboBoxDifficulty);
            panelModeActivity.Controls.Add(label8);
            panelModeActivity.Location = new System.Drawing.Point(12, 180);
            panelModeActivity.Margin = new System.Windows.Forms.Padding(0);
            panelModeActivity.Name = "panelModeActivity";
            panelModeActivity.Size = new System.Drawing.Size(382, 382);
            panelModeActivity.TabIndex = 9;
            // 
            // comboBoxHeadTo
            // 
            comboBoxHeadTo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxHeadTo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxHeadTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxHeadTo.Enabled = false;
            comboBoxHeadTo.FormattingEnabled = true;
            comboBoxHeadTo.Location = new System.Drawing.Point(4, 251);
            comboBoxHeadTo.Margin = new System.Windows.Forms.Padding(4);
            comboBoxHeadTo.Name = "comboBoxHeadTo";
            comboBoxHeadTo.Size = new System.Drawing.Size(373, 23);
            comboBoxHeadTo.TabIndex = 9;
            comboBoxHeadTo.SelectedIndexChanged += ComboBoxHeadTo_SelectedIndexChanged;
            // 
            // comboBoxStartAt
            // 
            comboBoxStartAt.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxStartAt.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxStartAt.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxStartAt.Enabled = false;
            comboBoxStartAt.FormattingEnabled = true;
            comboBoxStartAt.Location = new System.Drawing.Point(4, 194);
            comboBoxStartAt.Margin = new System.Windows.Forms.Padding(4);
            comboBoxStartAt.Name = "comboBoxStartAt";
            comboBoxStartAt.Size = new System.Drawing.Size(373, 23);
            comboBoxStartAt.TabIndex = 7;
            comboBoxStartAt.SelectedIndexChanged += ComboBoxStartAt_SelectedIndexChanged;
            // 
            // comboBoxConsist
            // 
            comboBoxConsist.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxConsist.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxConsist.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxConsist.Enabled = false;
            comboBoxConsist.FormattingEnabled = true;
            comboBoxConsist.Location = new System.Drawing.Point(4, 138);
            comboBoxConsist.Margin = new System.Windows.Forms.Padding(4);
            comboBoxConsist.Name = "comboBoxConsist";
            comboBoxConsist.Size = new System.Drawing.Size(373, 23);
            comboBoxConsist.TabIndex = 5;
            comboBoxConsist.SelectedIndexChanged += ComboBoxConsist_SelectedIndexChanged;
            // 
            // comboBoxLocomotive
            // 
            comboBoxLocomotive.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxLocomotive.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxLocomotive.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxLocomotive.Enabled = false;
            comboBoxLocomotive.FormattingEnabled = true;
            comboBoxLocomotive.Location = new System.Drawing.Point(4, 84);
            comboBoxLocomotive.Margin = new System.Windows.Forms.Padding(4);
            comboBoxLocomotive.Name = "comboBoxLocomotive";
            comboBoxLocomotive.Size = new System.Drawing.Size(373, 23);
            comboBoxLocomotive.TabIndex = 3;
            comboBoxLocomotive.SelectedIndexChanged += ComboBoxLocomotive_SelectedIndexChanged;
            // 
            // comboBoxActivity
            // 
            comboBoxActivity.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxActivity.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxActivity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxActivity.FormattingEnabled = true;
            comboBoxActivity.Location = new System.Drawing.Point(4, 28);
            comboBoxActivity.Margin = new System.Windows.Forms.Padding(4);
            comboBoxActivity.Name = "comboBoxActivity";
            comboBoxActivity.Size = new System.Drawing.Size(373, 23);
            comboBoxActivity.TabIndex = 1;
            comboBoxActivity.SelectedIndexChanged += ComboBoxActivity_SelectedIndexChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(4, 2);
            label3.Margin = new System.Windows.Forms.Padding(2);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(50, 15);
            label3.TabIndex = 0;
            label3.Text = "Activity:";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(4, 59);
            label4.Margin = new System.Windows.Forms.Padding(4);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(73, 15);
            label4.TabIndex = 2;
            label4.Text = "Locomotive:";
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(4, 112);
            label5.Margin = new System.Windows.Forms.Padding(4);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(49, 15);
            label5.TabIndex = 4;
            label5.Text = "Consist:";
            label5.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new System.Drawing.Point(4, 169);
            label6.Margin = new System.Windows.Forms.Padding(4);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(64, 15);
            label6.TabIndex = 6;
            label6.Text = "Starting at:";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new System.Drawing.Point(4, 225);
            label7.Margin = new System.Windows.Forms.Padding(4);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(69, 15);
            label7.TabIndex = 8;
            label7.Text = "Heading to:";
            label7.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new System.Drawing.Point(191, 288);
            label11.Margin = new System.Windows.Forms.Padding(2);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(56, 15);
            label11.TabIndex = 16;
            label11.Text = "Duration:";
            label11.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new System.Drawing.Point(5, 288);
            label9.Margin = new System.Windows.Forms.Padding(2);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(36, 15);
            label9.TabIndex = 10;
            label9.Text = "Time:";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxStartTime
            // 
            comboBoxStartTime.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            comboBoxStartTime.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxStartTime.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxStartTime.Enabled = false;
            comboBoxStartTime.FormattingEnabled = true;
            comboBoxStartTime.Location = new System.Drawing.Point(81, 284);
            comboBoxStartTime.Margin = new System.Windows.Forms.Padding(4);
            comboBoxStartTime.Name = "comboBoxStartTime";
            comboBoxStartTime.Size = new System.Drawing.Size(96, 23);
            comboBoxStartTime.TabIndex = 11;
            comboBoxStartTime.TextChanged += ComboBoxStartTime_TextChanged;
            // 
            // comboBoxDuration
            // 
            comboBoxDuration.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxDuration.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxDuration.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxDuration.Enabled = false;
            comboBoxDuration.FormattingEnabled = true;
            comboBoxDuration.Location = new System.Drawing.Point(281, 284);
            comboBoxDuration.Margin = new System.Windows.Forms.Padding(4);
            comboBoxDuration.Name = "comboBoxDuration";
            comboBoxDuration.Size = new System.Drawing.Size(96, 23);
            comboBoxDuration.TabIndex = 17;
            // 
            // comboBoxStartWeather
            // 
            comboBoxStartWeather.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxStartWeather.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxStartWeather.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxStartWeather.Enabled = false;
            comboBoxStartWeather.FormattingEnabled = true;
            comboBoxStartWeather.Location = new System.Drawing.Point(81, 352);
            comboBoxStartWeather.Margin = new System.Windows.Forms.Padding(4);
            comboBoxStartWeather.Name = "comboBoxStartWeather";
            comboBoxStartWeather.Size = new System.Drawing.Size(96, 23);
            comboBoxStartWeather.TabIndex = 15;
            comboBoxStartWeather.SelectedIndexChanged += ComboBoxStartWeather_SelectedIndexChanged;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new System.Drawing.Point(5, 356);
            label12.Margin = new System.Windows.Forms.Padding(2);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(54, 15);
            label12.TabIndex = 14;
            label12.Text = "Weather:";
            label12.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxStartSeason
            // 
            comboBoxStartSeason.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxStartSeason.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxStartSeason.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxStartSeason.Enabled = false;
            comboBoxStartSeason.FormattingEnabled = true;
            comboBoxStartSeason.Location = new System.Drawing.Point(81, 319);
            comboBoxStartSeason.Margin = new System.Windows.Forms.Padding(4);
            comboBoxStartSeason.Name = "comboBoxStartSeason";
            comboBoxStartSeason.Size = new System.Drawing.Size(96, 23);
            comboBoxStartSeason.TabIndex = 13;
            comboBoxStartSeason.SelectedIndexChanged += ComboBoxStartSeason_SelectedIndexChanged;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new System.Drawing.Point(191, 322);
            label10.Margin = new System.Windows.Forms.Padding(2);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(58, 15);
            label10.TabIndex = 18;
            label10.Text = "Difficulty:";
            label10.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxDifficulty
            // 
            comboBoxDifficulty.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxDifficulty.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxDifficulty.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxDifficulty.Enabled = false;
            comboBoxDifficulty.FormattingEnabled = true;
            comboBoxDifficulty.Location = new System.Drawing.Point(281, 319);
            comboBoxDifficulty.Margin = new System.Windows.Forms.Padding(4);
            comboBoxDifficulty.Name = "comboBoxDifficulty";
            comboBoxDifficulty.Size = new System.Drawing.Size(96, 23);
            comboBoxDifficulty.TabIndex = 19;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new System.Drawing.Point(5, 322);
            label8.Margin = new System.Windows.Forms.Padding(2);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(47, 15);
            label8.TabIndex = 12;
            label8.Text = "Season:";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelModeTimetable
            // 
            panelModeTimetable.Controls.Add(labelTimetableWeatherFile);
            panelModeTimetable.Controls.Add(comboBoxTimetableWeatherFile);
            panelModeTimetable.Controls.Add(label24);
            panelModeTimetable.Controls.Add(comboBoxTimetableTrain);
            panelModeTimetable.Controls.Add(label23);
            panelModeTimetable.Controls.Add(comboBoxTimetableDay);
            panelModeTimetable.Controls.Add(label22);
            panelModeTimetable.Controls.Add(comboBoxTimetableWeather);
            panelModeTimetable.Controls.Add(label20);
            panelModeTimetable.Controls.Add(comboBoxTimetableSeason);
            panelModeTimetable.Controls.Add(label21);
            panelModeTimetable.Controls.Add(comboBoxTimetable);
            panelModeTimetable.Controls.Add(comboBoxTimetableSet);
            panelModeTimetable.Controls.Add(label15);
            panelModeTimetable.Location = new System.Drawing.Point(399, 146);
            panelModeTimetable.Margin = new System.Windows.Forms.Padding(0);
            panelModeTimetable.Name = "panelModeTimetable";
            panelModeTimetable.Size = new System.Drawing.Size(382, 358);
            panelModeTimetable.TabIndex = 10;
            panelModeTimetable.Visible = false;
            // 
            // labelTimetableWeatherFile
            // 
            labelTimetableWeatherFile.AutoSize = true;
            labelTimetableWeatherFile.Location = new System.Drawing.Point(8, 261);
            labelTimetableWeatherFile.Margin = new System.Windows.Forms.Padding(4);
            labelTimetableWeatherFile.Name = "labelTimetableWeatherFile";
            labelTimetableWeatherFile.Size = new System.Drawing.Size(75, 15);
            labelTimetableWeatherFile.TabIndex = 14;
            labelTimetableWeatherFile.Text = "Weather File:";
            labelTimetableWeatherFile.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableWeatherFile
            // 
            comboBoxTimetableWeatherFile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxTimetableWeatherFile.FormattingEnabled = true;
            comboBoxTimetableWeatherFile.Location = new System.Drawing.Point(121, 258);
            comboBoxTimetableWeatherFile.Margin = new System.Windows.Forms.Padding(4);
            comboBoxTimetableWeatherFile.Name = "comboBoxTimetableWeatherFile";
            comboBoxTimetableWeatherFile.Size = new System.Drawing.Size(256, 23);
            comboBoxTimetableWeatherFile.TabIndex = 13;
            comboBoxTimetableWeatherFile.SelectedIndexChanged += ComboBoxTimetableWeatherFile_SelectedIndexChanged;
            // 
            // label24
            // 
            label24.AutoSize = true;
            label24.Location = new System.Drawing.Point(4, 98);
            label24.Margin = new System.Windows.Forms.Padding(4);
            label24.Name = "label24";
            label24.Size = new System.Drawing.Size(35, 15);
            label24.TabIndex = 4;
            label24.Text = "Train:";
            label24.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableTrain
            // 
            comboBoxTimetableTrain.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxTimetableTrain.FormattingEnabled = true;
            comboBoxTimetableTrain.Location = new System.Drawing.Point(121, 94);
            comboBoxTimetableTrain.Margin = new System.Windows.Forms.Padding(4);
            comboBoxTimetableTrain.Name = "comboBoxTimetableTrain";
            comboBoxTimetableTrain.Size = new System.Drawing.Size(256, 23);
            comboBoxTimetableTrain.TabIndex = 5;
            comboBoxTimetableTrain.SelectedIndexChanged += ComboBoxTimetableTrain_SelectedIndexChanged;
            // 
            // label23
            // 
            label23.AutoSize = true;
            label23.Location = new System.Drawing.Point(4, 64);
            label23.Margin = new System.Windows.Forms.Padding(4);
            label23.Name = "label23";
            label23.Size = new System.Drawing.Size(62, 15);
            label23.TabIndex = 2;
            label23.Text = "Timetable:";
            label23.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableDay
            // 
            comboBoxTimetableDay.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxTimetableDay.Enabled = false;
            comboBoxTimetableDay.FormattingEnabled = true;
            comboBoxTimetableDay.Location = new System.Drawing.Point(121, 149);
            comboBoxTimetableDay.Margin = new System.Windows.Forms.Padding(4);
            comboBoxTimetableDay.Name = "comboBoxTimetableDay";
            comboBoxTimetableDay.Size = new System.Drawing.Size(96, 23);
            comboBoxTimetableDay.TabIndex = 8;
            comboBoxTimetableDay.Visible = false;
            comboBoxTimetableDay.SelectedIndexChanged += ComboBoxTimetableDay_SelectedIndexChanged;
            // 
            // label22
            // 
            label22.AutoSize = true;
            label22.Location = new System.Drawing.Point(8, 152);
            label22.Margin = new System.Windows.Forms.Padding(4);
            label22.Name = "label22";
            label22.Size = new System.Drawing.Size(30, 15);
            label22.TabIndex = 7;
            label22.Text = "Day:";
            label22.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label22.Visible = false;
            // 
            // comboBoxTimetableWeather
            // 
            comboBoxTimetableWeather.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxTimetableWeather.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxTimetableWeather.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxTimetableWeather.FormattingEnabled = true;
            comboBoxTimetableWeather.Location = new System.Drawing.Point(121, 218);
            comboBoxTimetableWeather.Margin = new System.Windows.Forms.Padding(4);
            comboBoxTimetableWeather.Name = "comboBoxTimetableWeather";
            comboBoxTimetableWeather.Size = new System.Drawing.Size(96, 23);
            comboBoxTimetableWeather.TabIndex = 12;
            comboBoxTimetableWeather.SelectedIndexChanged += ComboBoxTimetableWeather_SelectedIndexChanged;
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.Location = new System.Drawing.Point(8, 220);
            label20.Margin = new System.Windows.Forms.Padding(4);
            label20.Name = "label20";
            label20.Size = new System.Drawing.Size(54, 15);
            label20.TabIndex = 11;
            label20.Text = "Weather:";
            label20.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetableSeason
            // 
            comboBoxTimetableSeason.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            comboBoxTimetableSeason.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            comboBoxTimetableSeason.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxTimetableSeason.FormattingEnabled = true;
            comboBoxTimetableSeason.Location = new System.Drawing.Point(121, 182);
            comboBoxTimetableSeason.Margin = new System.Windows.Forms.Padding(4);
            comboBoxTimetableSeason.Name = "comboBoxTimetableSeason";
            comboBoxTimetableSeason.Size = new System.Drawing.Size(96, 23);
            comboBoxTimetableSeason.TabIndex = 10;
            comboBoxTimetableSeason.SelectedIndexChanged += ComboBoxTimetableSeason_SelectedIndexChanged;
            // 
            // label21
            // 
            label21.AutoSize = true;
            label21.Location = new System.Drawing.Point(8, 188);
            label21.Margin = new System.Windows.Forms.Padding(4);
            label21.Name = "label21";
            label21.Size = new System.Drawing.Size(47, 15);
            label21.TabIndex = 9;
            label21.Text = "Season:";
            label21.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxTimetable
            // 
            comboBoxTimetable.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxTimetable.FormattingEnabled = true;
            comboBoxTimetable.Location = new System.Drawing.Point(121, 60);
            comboBoxTimetable.Margin = new System.Windows.Forms.Padding(4);
            comboBoxTimetable.Name = "comboBoxTimetable";
            comboBoxTimetable.Size = new System.Drawing.Size(256, 23);
            comboBoxTimetable.TabIndex = 3;
            comboBoxTimetable.SelectedIndexChanged += ComboBoxTimetable_selectedIndexChanged;
            comboBoxTimetable.EnabledChanged += ComboBoxTimetable_EnabledChanged;
            // 
            // comboBoxTimetableSet
            // 
            comboBoxTimetableSet.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBoxTimetableSet.FormattingEnabled = true;
            comboBoxTimetableSet.Location = new System.Drawing.Point(4, 28);
            comboBoxTimetableSet.Margin = new System.Windows.Forms.Padding(4);
            comboBoxTimetableSet.Name = "comboBoxTimetableSet";
            comboBoxTimetableSet.Size = new System.Drawing.Size(373, 23);
            comboBoxTimetableSet.TabIndex = 1;
            comboBoxTimetableSet.SelectedIndexChanged += ComboBoxTimetableSet_SelectedIndexChanged;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new System.Drawing.Point(4, 4);
            label15.Margin = new System.Windows.Forms.Padding(4);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(80, 15);
            label15.TabIndex = 0;
            label15.Text = "Timetable set:";
            label15.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // linkLabelUpdate
            // 
            linkLabelUpdate.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            linkLabelUpdate.AutoSize = true;
            linkLabelUpdate.ImageAlign = System.Drawing.ContentAlignment.TopLeft;
            linkLabelUpdate.Location = new System.Drawing.Point(984, 12);
            linkLabelUpdate.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            linkLabelUpdate.Name = "linkLabelUpdate";
            linkLabelUpdate.Size = new System.Drawing.Size(110, 15);
            linkLabelUpdate.TabIndex = 37;
            linkLabelUpdate.TabStop = true;
            linkLabelUpdate.Text = "Link to next Update";
            linkLabelUpdate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            linkLabelUpdate.UseMnemonic = false;
            linkLabelUpdate.Visible = false;
            linkLabelUpdate.LinkClicked += LinkLabelUpdate_LinkClicked;
            // 
            // testingToolStripMenuItem
            // 
            testingToolStripMenuItem.Name = "testingToolStripMenuItem";
            testingToolStripMenuItem.Size = new System.Drawing.Size(111, 22);
            testingToolStripMenuItem.Text = "Testing";
            testingToolStripMenuItem.Click += TestingToolStripMenuItem_Click;
            // 
            // contextMenuStripTools
            // 
            contextMenuStripTools.ImageScalingSize = new System.Drawing.Size(20, 20);
            contextMenuStripTools.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { testingToolStripMenuItem });
            contextMenuStripTools.Name = "contextMenuStrip1";
            contextMenuStripTools.Size = new System.Drawing.Size(112, 26);
            // 
            // contextMenuStripDocuments
            // 
            contextMenuStripDocuments.ImageScalingSize = new System.Drawing.Size(20, 20);
            contextMenuStripDocuments.Name = "contextMenuStripDocuments";
            contextMenuStripDocuments.Size = new System.Drawing.Size(61, 4);
            // 
            // MainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1139, 674);
            Controls.Add(panelModeTimetable);
            Controls.Add(panelModeActivity);
            Controls.Add(radioButtonModeTimetable);
            Controls.Add(radioButtonModeActivity);
            Controls.Add(label25);
            Controls.Add(panel1);
            Controls.Add(panelDetails);
            Controls.Add(comboBoxFolder);
            Controls.Add(label1);
            Controls.Add(groupBox3);
            Controls.Add(comboBoxRoute);
            Controls.Add(groupBox1);
            Controls.Add(pictureBoxLogo);
            Controls.Add(labelLogo);
            Controls.Add(label2);
            Controls.Add(linkLabelUpdate);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Open Rails";
            FormClosing += MainForm_FormClosing;
            Shown += MainForm_Shown;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBoxLogo).EndInit();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panelModeActivity.ResumeLayout(false);
            panelModeActivity.PerformLayout();
            panelModeTimetable.ResumeLayout(false);
            panelModeTimetable.PerformLayout();
            contextMenuStripTools.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
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
        private System.Windows.Forms.Button buttonStartMP;
        private System.Windows.Forms.Label labelTimetableWeatherFile;
        private System.Windows.Forms.ComboBox comboBoxTimetableWeatherFile;
        private System.Windows.Forms.Button buttonConnectivityTest;
    }
}
