namespace Orts.ActivityRunner.Viewer3D.Debugging
{
    partial class DispatchViewer
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
                grayPen.Dispose();
                greenPen.Dispose();
                orangePen.Dispose();
                redPen.Dispose();
                pathPen.Dispose();
                trainPen.Dispose();
                trainBrush.Dispose();
                trainFont.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.refreshButton = new System.Windows.Forms.Button();
            this.resLabel = new System.Windows.Forms.Label();
            this.AvatarView = new System.Windows.Forms.ListView();
            this.rmvButton = new System.Windows.Forms.Button();
            this.chkAllowUserSwitch = new System.Windows.Forms.CheckBox();
            this.chkShowAvatars = new System.Windows.Forms.CheckBox();
            this.MSG = new System.Windows.Forms.TextBox();
            this.msgSelected = new System.Windows.Forms.Button();
            this.msgAll = new System.Windows.Forms.Button();
            this.composeMSG = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.reply2Selected = new System.Windows.Forms.Button();
            this.chkAllowNew = new System.Windows.Forms.CheckBox();
            this.messages = new System.Windows.Forms.ListBox();
            this.btnAssist = new System.Windows.Forms.Button();
            this.btnNormal = new System.Windows.Forms.Button();
            this.chkBoxPenalty = new System.Windows.Forms.CheckBox();
            this.chkPreferGreen = new System.Windows.Forms.CheckBox();
            this.btnSeeInGame = new System.Windows.Forms.Button();
            this.lblSimulationTimeText = new System.Windows.Forms.Label();
            this.lblSimulationTime = new System.Windows.Forms.Label();
            this.lblShow = new System.Windows.Forms.Label();
            this.gbTrainLabels = new System.Windows.Forms.GroupBox();
            this.bTrainKey = new System.Windows.Forms.Button();
            this.rbShowActiveTrainLabels = new System.Windows.Forms.RadioButton();
            this.rbShowAllTrainLabels = new System.Windows.Forms.RadioButton();
            this.nudDaylightOffsetHrs = new System.Windows.Forms.NumericUpDown();
            this.lblDayLightOffsetHrs = new System.Windows.Forms.Label();
            this.cdBackground = new System.Windows.Forms.ColorDialog();
            this.lblInstruction1 = new System.Windows.Forms.Label();
            this.cbShowTrainLabels = new System.Windows.Forms.CheckBox();
            this.tWindow = new System.Windows.Forms.TabControl();
            this.tDispatch = new System.Windows.Forms.TabPage();
            this.tTimetable = new System.Windows.Forms.TabPage();
            this.cbShowTrainState = new System.Windows.Forms.CheckBox();
            this.lblInstruction2 = new System.Windows.Forms.Label();
            this.lblInstruction3 = new System.Windows.Forms.Label();
            this.lblInstruction4 = new System.Windows.Forms.Label();
            this.gbTrainLabels.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudDaylightOffsetHrs)).BeginInit();
            this.tWindow.SuspendLayout();
            this.SuspendLayout();
            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.refreshButton.Location = new System.Drawing.Point(820, 137);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(93, 23);
            this.refreshButton.TabIndex = 1;
            this.refreshButton.Text = "View Train";
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // resLabel
            // 
            this.resLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.resLabel.AutoSize = true;
            this.resLabel.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.resLabel.Location = new System.Drawing.Point(887, 35);
            this.resLabel.Name = "resLabel";
            this.resLabel.Size = new System.Drawing.Size(26, 21);
            this.resLabel.TabIndex = 8;
            this.resLabel.Text = "m";
            // 
            // AvatarView
            // 
            this.AvatarView.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.AvatarView.HideSelection = false;
            this.AvatarView.Location = new System.Drawing.Point(779, 200);
            this.AvatarView.Name = "AvatarView";
            this.AvatarView.Size = new System.Drawing.Size(121, 556);
            this.AvatarView.TabIndex = 14;
            this.AvatarView.UseCompatibleStateImageBehavior = false;
            this.AvatarView.SelectedIndexChanged += new System.EventHandler(this.AvatarView_SelectedIndexChanged);
            // 
            // rmvButton
            // 
            this.rmvButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.rmvButton.Location = new System.Drawing.Point(773, 164);
            this.rmvButton.Margin = new System.Windows.Forms.Padding(2);
            this.rmvButton.Name = "rmvButton";
            this.rmvButton.Size = new System.Drawing.Size(72, 24);
            this.rmvButton.TabIndex = 15;
            this.rmvButton.Text = "Remove";
            this.rmvButton.UseVisualStyleBackColor = true;
            this.rmvButton.Click += new System.EventHandler(this.rmvButton_Click);
            // 
            // chkAllowUserSwitch
            // 
            this.chkAllowUserSwitch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkAllowUserSwitch.AutoSize = true;
            this.chkAllowUserSwitch.Checked = true;
            this.chkAllowUserSwitch.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAllowUserSwitch.Location = new System.Drawing.Point(661, 71);
            this.chkAllowUserSwitch.Name = "chkAllowUserSwitch";
            this.chkAllowUserSwitch.Size = new System.Drawing.Size(103, 21);
            this.chkAllowUserSwitch.TabIndex = 16;
            this.chkAllowUserSwitch.Text = "Auto Switch";
            this.chkAllowUserSwitch.UseVisualStyleBackColor = true;
            this.chkAllowUserSwitch.CheckedChanged += new System.EventHandler(this.chkAllowUserSwitch_CheckedChanged);
            // 
            // chkShowAvatars
            // 
            this.chkShowAvatars.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkShowAvatars.AutoSize = true;
            this.chkShowAvatars.Checked = true;
            this.chkShowAvatars.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShowAvatars.Location = new System.Drawing.Point(657, 53);
            this.chkShowAvatars.Name = "chkShowAvatars";
            this.chkShowAvatars.Size = new System.Drawing.Size(116, 21);
            this.chkShowAvatars.TabIndex = 17;
            this.chkShowAvatars.Text = "Show Avatars";
            this.chkShowAvatars.UseVisualStyleBackColor = true;
            this.chkShowAvatars.CheckedChanged += new System.EventHandler(this.chkShowAvatars_CheckedChanged);
            // 
            // MSG
            // 
            this.MSG.Enabled = false;
            this.MSG.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MSG.Location = new System.Drawing.Point(1, 38);
            this.MSG.Name = "MSG";
            this.MSG.Size = new System.Drawing.Size(560, 30);
            this.MSG.TabIndex = 18;
            this.MSG.WordWrap = false;
            this.MSG.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.checkKeys);
            // 
            // msgSelected
            // 
            this.msgSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.msgSelected.Enabled = false;
            this.msgSelected.Location = new System.Drawing.Point(569, 78);
            this.msgSelected.Margin = new System.Windows.Forms.Padding(2);
            this.msgSelected.MaximumSize = new System.Drawing.Size(200, 24);
            this.msgSelected.MinimumSize = new System.Drawing.Size(104, 24);
            this.msgSelected.Name = "msgSelected";
            this.msgSelected.Size = new System.Drawing.Size(104, 24);
            this.msgSelected.TabIndex = 19;
            this.msgSelected.Text = "MSG to Selected";
            this.msgSelected.UseVisualStyleBackColor = true;
            this.msgSelected.Click += new System.EventHandler(this.msgSelected_Click);
            // 
            // msgAll
            // 
            this.msgAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.msgAll.Enabled = false;
            this.msgAll.Location = new System.Drawing.Point(569, 53);
            this.msgAll.Margin = new System.Windows.Forms.Padding(2);
            this.msgAll.MaximumSize = new System.Drawing.Size(200, 24);
            this.msgAll.MinimumSize = new System.Drawing.Size(104, 24);
            this.msgAll.Name = "msgAll";
            this.msgAll.Size = new System.Drawing.Size(104, 24);
            this.msgAll.TabIndex = 20;
            this.msgAll.Text = "MSG to All";
            this.msgAll.UseVisualStyleBackColor = true;
            this.msgAll.Click += new System.EventHandler(this.msgAll_Click);
            // 
            // composeMSG
            // 
            this.composeMSG.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.composeMSG.Location = new System.Drawing.Point(569, 28);
            this.composeMSG.Margin = new System.Windows.Forms.Padding(2);
            this.composeMSG.MaximumSize = new System.Drawing.Size(200, 24);
            this.composeMSG.MinimumSize = new System.Drawing.Size(104, 24);
            this.composeMSG.Name = "composeMSG";
            this.composeMSG.Size = new System.Drawing.Size(104, 24);
            this.composeMSG.TabIndex = 21;
            this.composeMSG.Text = "Compose MSG";
            this.composeMSG.UseVisualStyleBackColor = true;
            this.composeMSG.Click += new System.EventHandler(this.composeMSG_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(770, 35);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(33, 17);
            this.label1.TabIndex = 7;
            this.label1.Text = "Res";
            // 
            // reply2Selected
            // 
            this.reply2Selected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.reply2Selected.Enabled = false;
            this.reply2Selected.Location = new System.Drawing.Point(569, 103);
            this.reply2Selected.Margin = new System.Windows.Forms.Padding(2);
            this.reply2Selected.MaximumSize = new System.Drawing.Size(200, 24);
            this.reply2Selected.MinimumSize = new System.Drawing.Size(104, 24);
            this.reply2Selected.Name = "reply2Selected";
            this.reply2Selected.Size = new System.Drawing.Size(104, 24);
            this.reply2Selected.TabIndex = 23;
            this.reply2Selected.Text = "Reply to Selected";
            this.reply2Selected.UseVisualStyleBackColor = true;
            this.reply2Selected.Click += new System.EventHandler(this.replySelected);
            // 
            // chkAllowNew
            // 
            this.chkAllowNew.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkAllowNew.AutoSize = true;
            this.chkAllowNew.Checked = true;
            this.chkAllowNew.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAllowNew.Location = new System.Drawing.Point(663, 34);
            this.chkAllowNew.Name = "chkAllowNew";
            this.chkAllowNew.Size = new System.Drawing.Size(85, 21);
            this.chkAllowNew.TabIndex = 29;
            this.chkAllowNew.Text = "Can Join";
            this.chkAllowNew.UseVisualStyleBackColor = true;
            this.chkAllowNew.CheckedChanged += new System.EventHandler(this.chkAllowNewCheck);
            // 
            // messages
            // 
            this.messages.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.messages.FormattingEnabled = true;
            this.messages.ItemHeight = 24;
            this.messages.Location = new System.Drawing.Point(1, 68);
            this.messages.Name = "messages";
            this.messages.Size = new System.Drawing.Size(560, 52);
            this.messages.TabIndex = 22;
            this.messages.SelectedIndexChanged += new System.EventHandler(this.msgSelectedChanged);
            // 
            // btnAssist
            // 
            this.btnAssist.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAssist.Location = new System.Drawing.Point(762, 111);
            this.btnAssist.Margin = new System.Windows.Forms.Padding(2);
            this.btnAssist.Name = "btnAssist";
            this.btnAssist.Size = new System.Drawing.Size(48, 24);
            this.btnAssist.TabIndex = 30;
            this.btnAssist.Text = "Assist";
            this.btnAssist.UseVisualStyleBackColor = true;
            this.btnAssist.Click += new System.EventHandler(this.AssistClick);
            // 
            // btnNormal
            // 
            this.btnNormal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNormal.Location = new System.Drawing.Point(762, 136);
            this.btnNormal.Margin = new System.Windows.Forms.Padding(2);
            this.btnNormal.Name = "btnNormal";
            this.btnNormal.Size = new System.Drawing.Size(58, 24);
            this.btnNormal.TabIndex = 31;
            this.btnNormal.Text = "Normal";
            this.btnNormal.UseVisualStyleBackColor = true;
            this.btnNormal.Click += new System.EventHandler(this.btnNormalClick);
            // 
            // chkBoxPenalty
            // 
            this.chkBoxPenalty.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkBoxPenalty.AutoSize = true;
            this.chkBoxPenalty.Checked = true;
            this.chkBoxPenalty.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkBoxPenalty.Location = new System.Drawing.Point(665, 107);
            this.chkBoxPenalty.Name = "chkBoxPenalty";
            this.chkBoxPenalty.Size = new System.Drawing.Size(77, 21);
            this.chkBoxPenalty.TabIndex = 33;
            this.chkBoxPenalty.Text = "Penalty";
            this.chkBoxPenalty.UseVisualStyleBackColor = true;
            this.chkBoxPenalty.CheckedChanged += new System.EventHandler(this.chkOPenaltyHandle);
            // 
            // chkPreferGreen
            // 
            this.chkPreferGreen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkPreferGreen.AutoSize = true;
            this.chkPreferGreen.Checked = true;
            this.chkPreferGreen.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkPreferGreen.Location = new System.Drawing.Point(654, 89);
            this.chkPreferGreen.Name = "chkPreferGreen";
            this.chkPreferGreen.Size = new System.Drawing.Size(113, 21);
            this.chkPreferGreen.TabIndex = 34;
            this.chkPreferGreen.Text = "Prefer Green";
            this.chkPreferGreen.UseVisualStyleBackColor = true;
            this.chkPreferGreen.Visible = false;
            this.chkPreferGreen.CheckedChanged += new System.EventHandler(this.chkPreferGreenHandle);
            // 
            // btnSeeInGame
            // 
            this.btnSeeInGame.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSeeInGame.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSeeInGame.Location = new System.Drawing.Point(820, 113);
            this.btnSeeInGame.Name = "btnSeeInGame";
            this.btnSeeInGame.Size = new System.Drawing.Size(93, 23);
            this.btnSeeInGame.TabIndex = 35;
            this.btnSeeInGame.Text = "See in Game";
            this.btnSeeInGame.UseVisualStyleBackColor = true;
            this.btnSeeInGame.Click += new System.EventHandler(this.btnSeeInGameClick);
            // 
            // lblSimulationTimeText
            // 
            this.lblSimulationTimeText.AutoSize = true;
            this.lblSimulationTimeText.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSimulationTimeText.Location = new System.Drawing.Point(5, 34);
            this.lblSimulationTimeText.Name = "lblSimulationTimeText";
            this.lblSimulationTimeText.Size = new System.Drawing.Size(129, 20);
            this.lblSimulationTimeText.TabIndex = 36;
            this.lblSimulationTimeText.Text = "Simulation Time";
            this.lblSimulationTimeText.Visible = false;
            // 
            // lblSimulationTime
            // 
            this.lblSimulationTime.AutoSize = true;
            this.lblSimulationTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSimulationTime.Location = new System.Drawing.Point(115, 34);
            this.lblSimulationTime.Name = "lblSimulationTime";
            this.lblSimulationTime.Size = new System.Drawing.Size(124, 20);
            this.lblSimulationTime.TabIndex = 37;
            this.lblSimulationTime.Text = "SimulationTime";
            this.lblSimulationTime.Visible = false;
            // 
            // lblShow
            // 
            this.lblShow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblShow.AutoSize = true;
            this.lblShow.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblShow.Location = new System.Drawing.Point(780, 180);
            this.lblShow.Name = "lblShow";
            this.lblShow.Size = new System.Drawing.Size(50, 18);
            this.lblShow.TabIndex = 38;
            this.lblShow.Text = "Show:";
            this.lblShow.Visible = false;
            // 
            // gbTrainLabels
            // 
            this.gbTrainLabels.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.gbTrainLabels.Controls.Add(this.bTrainKey);
            this.gbTrainLabels.Controls.Add(this.rbShowActiveTrainLabels);
            this.gbTrainLabels.Controls.Add(this.rbShowAllTrainLabels);
            this.gbTrainLabels.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gbTrainLabels.Location = new System.Drawing.Point(779, 323);
            this.gbTrainLabels.Name = "gbTrainLabels";
            this.gbTrainLabels.Size = new System.Drawing.Size(120, 129);
            this.gbTrainLabels.TabIndex = 43;
            this.gbTrainLabels.TabStop = false;
            this.gbTrainLabels.Text = "Train labels";
            this.gbTrainLabels.Visible = false;
            // 
            // bTrainKey
            // 
            this.bTrainKey.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bTrainKey.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bTrainKey.Location = new System.Drawing.Point(77, 89);
            this.bTrainKey.Name = "bTrainKey";
            this.bTrainKey.Size = new System.Drawing.Size(40, 23);
            this.bTrainKey.TabIndex = 57;
            this.bTrainKey.Text = "Key";
            this.bTrainKey.UseVisualStyleBackColor = true;
            this.bTrainKey.Visible = false;
            this.bTrainKey.Click += new System.EventHandler(this.bTrainKey_Click);
            // 
            // rbShowActiveTrainLabels
            // 
            this.rbShowActiveTrainLabels.AutoSize = true;
            this.rbShowActiveTrainLabels.Checked = true;
            this.rbShowActiveTrainLabels.Location = new System.Drawing.Point(13, 22);
            this.rbShowActiveTrainLabels.Name = "rbShowActiveTrainLabels";
            this.rbShowActiveTrainLabels.Size = new System.Drawing.Size(99, 22);
            this.rbShowActiveTrainLabels.TabIndex = 1;
            this.rbShowActiveTrainLabels.TabStop = true;
            this.rbShowActiveTrainLabels.Text = "Active only";
            this.rbShowActiveTrainLabels.UseVisualStyleBackColor = true;
            this.rbShowActiveTrainLabels.Visible = false;
            // 
            // rbShowAllTrainLabels
            // 
            this.rbShowAllTrainLabels.AutoSize = true;
            this.rbShowAllTrainLabels.Location = new System.Drawing.Point(13, 44);
            this.rbShowAllTrainLabels.Name = "rbShowAllTrainLabels";
            this.rbShowAllTrainLabels.Size = new System.Drawing.Size(44, 22);
            this.rbShowAllTrainLabels.TabIndex = 0;
            this.rbShowAllTrainLabels.Text = "All";
            this.rbShowAllTrainLabels.UseVisualStyleBackColor = true;
            this.rbShowAllTrainLabels.Visible = false;
            // 
            // nudDaylightOffsetHrs
            // 
            this.nudDaylightOffsetHrs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.nudDaylightOffsetHrs.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.nudDaylightOffsetHrs.Location = new System.Drawing.Point(817, 556);
            this.nudDaylightOffsetHrs.Maximum = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.nudDaylightOffsetHrs.Minimum = new decimal(new int[] {
            12,
            0,
            0,
            -2147483648});
            this.nudDaylightOffsetHrs.Name = "nudDaylightOffsetHrs";
            this.nudDaylightOffsetHrs.Size = new System.Drawing.Size(40, 24);
            this.nudDaylightOffsetHrs.TabIndex = 44;
            this.nudDaylightOffsetHrs.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.nudDaylightOffsetHrs.Visible = false;
            this.nudDaylightOffsetHrs.ValueChanged += new System.EventHandler(this.nudDaylightOffsetHrs_ValueChanged);
            // 
            // lblDayLightOffsetHrs
            // 
            this.lblDayLightOffsetHrs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDayLightOffsetHrs.AutoSize = true;
            this.lblDayLightOffsetHrs.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDayLightOffsetHrs.Location = new System.Drawing.Point(779, 534);
            this.lblDayLightOffsetHrs.Name = "lblDayLightOffsetHrs";
            this.lblDayLightOffsetHrs.Size = new System.Drawing.Size(136, 18);
            this.lblDayLightOffsetHrs.TabIndex = 45;
            this.lblDayLightOffsetHrs.Text = "Daylight offset (hrs)";
            this.lblDayLightOffsetHrs.Visible = false;
            // 
            // cdBackground
            // 
            this.cdBackground.AnyColor = true;
            this.cdBackground.ShowHelp = true;
            // 
            // lblInstruction1
            // 
            this.lblInstruction1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblInstruction1.Location = new System.Drawing.Point(8, 672);
            this.lblInstruction1.Name = "lblInstruction1";
            this.lblInstruction1.Padding = new System.Windows.Forms.Padding(3);
            this.lblInstruction1.Size = new System.Drawing.Size(327, 22);
            this.lblInstruction1.TabIndex = 48;
            this.lblInstruction1.Text = "To pan, drag with left mouse.";
            this.lblInstruction1.Visible = false;
            // 
            // cbShowTrainLabels
            // 
            this.cbShowTrainLabels.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbShowTrainLabels.AutoSize = true;
            this.cbShowTrainLabels.Checked = true;
            this.cbShowTrainLabels.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbShowTrainLabels.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbShowTrainLabels.Location = new System.Drawing.Point(780, 395);
            this.cbShowTrainLabels.Name = "cbShowTrainLabels";
            this.cbShowTrainLabels.Size = new System.Drawing.Size(70, 22);
            this.cbShowTrainLabels.TabIndex = 50;
            this.cbShowTrainLabels.Text = "Name";
            this.cbShowTrainLabels.UseVisualStyleBackColor = true;
            this.cbShowTrainLabels.Visible = false;
            // 
            // tWindow
            // 
            this.tWindow.Controls.Add(this.tDispatch);
            this.tWindow.Controls.Add(this.tTimetable);
            this.tWindow.Location = new System.Drawing.Point(0, 0);
            this.tWindow.Name = "tWindow";
            this.tWindow.SelectedIndex = 0;
            this.tWindow.Size = new System.Drawing.Size(923, 32);
            this.tWindow.TabIndex = 51;
            this.tWindow.SelectedIndexChanged += new System.EventHandler(this.tWindow_SelectedIndexChanged);
            // 
            // tDispatch
            // 
            this.tDispatch.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tDispatch.Location = new System.Drawing.Point(4, 25);
            this.tDispatch.Name = "tDispatch";
            this.tDispatch.Padding = new System.Windows.Forms.Padding(3);
            this.tDispatch.Size = new System.Drawing.Size(915, 3);
            this.tDispatch.TabIndex = 0;
            this.tDispatch.Text = "Dispatch";
            this.tDispatch.UseVisualStyleBackColor = true;
            // 
            // tTimetable
            // 
            this.tTimetable.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tTimetable.Location = new System.Drawing.Point(4, 25);
            this.tTimetable.Name = "tTimetable";
            this.tTimetable.Padding = new System.Windows.Forms.Padding(3);
            this.tTimetable.Size = new System.Drawing.Size(915, 3);
            this.tTimetable.TabIndex = 1;
            this.tTimetable.Text = "Timetable";
            this.tTimetable.UseVisualStyleBackColor = true;
            // 
            // cbShowTrainState
            // 
            this.cbShowTrainState.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbShowTrainState.AutoSize = true;
            this.cbShowTrainState.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbShowTrainState.Location = new System.Drawing.Point(792, 415);
            this.cbShowTrainState.Name = "cbShowTrainState";
            this.cbShowTrainState.Size = new System.Drawing.Size(64, 22);
            this.cbShowTrainState.TabIndex = 52;
            this.cbShowTrainState.Text = "State";
            this.cbShowTrainState.UseVisualStyleBackColor = true;
            this.cbShowTrainState.Visible = false;
            // 
            // lblInstruction2
            // 
            this.lblInstruction2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblInstruction2.Location = new System.Drawing.Point(8, 693);
            this.lblInstruction2.Name = "lblInstruction2";
            this.lblInstruction2.Padding = new System.Windows.Forms.Padding(3);
            this.lblInstruction2.Size = new System.Drawing.Size(327, 21);
            this.lblInstruction2.TabIndex = 53;
            this.lblInstruction2.Text = "To zoom, drag with left and right mouse or scroll mouse wheel.";
            this.lblInstruction2.Visible = false;
            // 
            // lblInstruction3
            // 
            this.lblInstruction3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblInstruction3.Location = new System.Drawing.Point(8, 714);
            this.lblInstruction3.Name = "lblInstruction3";
            this.lblInstruction3.Padding = new System.Windows.Forms.Padding(3);
            this.lblInstruction3.Size = new System.Drawing.Size(327, 21);
            this.lblInstruction3.TabIndex = 54;
            this.lblInstruction3.Text = "To zoom in to a location, press Shift and click the left mouse.";
            this.lblInstruction3.Visible = false;
            // 
            // lblInstruction4
            // 
            this.lblInstruction4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblInstruction4.Location = new System.Drawing.Point(8, 735);
            this.lblInstruction4.Name = "lblInstruction4";
            this.lblInstruction4.Padding = new System.Windows.Forms.Padding(3);
            this.lblInstruction4.Size = new System.Drawing.Size(327, 21);
            this.lblInstruction4.TabIndex = 55;
            this.lblInstruction4.Text = "To zoom out of a location, press Alt and click the left mouse.";
            this.lblInstruction4.Visible = false;
            // 
            // DispatchViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(923, 768);
            this.Controls.Add(this.lblInstruction4);
            this.Controls.Add(this.lblInstruction3);
            this.Controls.Add(this.lblInstruction2);
            this.Controls.Add(this.cbShowTrainState);
            this.Controls.Add(this.cbShowTrainLabels);
            this.Controls.Add(this.lblInstruction1);
            this.Controls.Add(this.lblDayLightOffsetHrs);
            this.Controls.Add(this.nudDaylightOffsetHrs);
            this.Controls.Add(this.gbTrainLabels);
            this.Controls.Add(this.lblShow);
            this.Controls.Add(this.lblSimulationTime);
            this.Controls.Add(this.lblSimulationTimeText);
            this.Controls.Add(this.btnSeeInGame);
            this.Controls.Add(this.chkPreferGreen);
            this.Controls.Add(this.chkBoxPenalty);
            this.Controls.Add(this.btnNormal);
            this.Controls.Add(this.btnAssist);
            this.Controls.Add(this.chkAllowNew);
            this.Controls.Add(this.reply2Selected);
            this.Controls.Add(this.messages);
            this.Controls.Add(this.composeMSG);
            this.Controls.Add(this.msgAll);
            this.Controls.Add(this.msgSelected);
            this.Controls.Add(this.MSG);
            this.Controls.Add(this.chkShowAvatars);
            this.Controls.Add(this.chkAllowUserSwitch);
            this.Controls.Add(this.rmvButton);
            this.Controls.Add(this.AvatarView);
            this.Controls.Add(this.resLabel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.refreshButton);
            this.Controls.Add(this.tWindow);
            this.Margin = new System.Windows.Forms.Padding(60, 28, 60, 28);
            this.Name = "DispatchViewer";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "Map Window";
            this.gbTrainLabels.ResumeLayout(false);
            this.gbTrainLabels.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudDaylightOffsetHrs)).EndInit();
            this.tWindow.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ColorDialog cdBackground;
        private System.Windows.Forms.Label lblInstruction1;
        private System.Windows.Forms.TabPage tDispatch;
        private System.Windows.Forms.TabPage tTimetable;
        public System.Windows.Forms.Button refreshButton;
        public System.Windows.Forms.Label resLabel;
        public System.Windows.Forms.ListView AvatarView;
        public System.Windows.Forms.Button rmvButton;
        public System.Windows.Forms.CheckBox chkAllowUserSwitch;
        public System.Windows.Forms.CheckBox chkShowAvatars;
        public System.Windows.Forms.TextBox MSG;
        public System.Windows.Forms.Button msgSelected;
        public System.Windows.Forms.Button msgAll;
        public System.Windows.Forms.Button composeMSG;
        public System.Windows.Forms.Label label1;
        public System.Windows.Forms.Button reply2Selected;
        public System.Windows.Forms.CheckBox chkAllowNew;
        public System.Windows.Forms.ListBox messages;
        public System.Windows.Forms.Button btnAssist;
        public System.Windows.Forms.Button btnNormal;
        public System.Windows.Forms.CheckBox chkBoxPenalty;
        public System.Windows.Forms.CheckBox chkPreferGreen;
        public System.Windows.Forms.Button btnSeeInGame;
        public System.Windows.Forms.Label lblSimulationTimeText;
        public System.Windows.Forms.Label lblSimulationTime;
        public System.Windows.Forms.Label lblShow;
        public System.Windows.Forms.GroupBox gbTrainLabels;
        public System.Windows.Forms.RadioButton rbShowActiveTrainLabels;
        public System.Windows.Forms.RadioButton rbShowAllTrainLabels;
        public System.Windows.Forms.NumericUpDown nudDaylightOffsetHrs;
        public System.Windows.Forms.Label lblDayLightOffsetHrs;
        public System.Windows.Forms.CheckBox cbShowTrainLabels;
        public System.Windows.Forms.TabControl tWindow;
        public System.Windows.Forms.CheckBox cbShowTrainState;
        private System.Windows.Forms.Label lblInstruction2;
        private System.Windows.Forms.Label lblInstruction3;
        private System.Windows.Forms.Label lblInstruction4;
        public System.Windows.Forms.Button bTrainKey;
    }
}