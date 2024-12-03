using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Menu
{
    partial class OptionsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OptionsForm));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            buttonOK = new System.Windows.Forms.Button();
            numericBrakePipeChargingRate = new System.Windows.Forms.NumericUpDown();
            label4 = new System.Windows.Forms.Label();
            checkGraduatedRelease = new System.Windows.Forms.CheckBox();
            buttonCancel = new System.Windows.Forms.Button();
            checkAlerter = new System.Windows.Forms.CheckBox();
            checkConfirmations = new System.Windows.Forms.CheckBox();
            tabOptions = new System.Windows.Forms.TabControl();
            tabPageGeneral = new System.Windows.Forms.TabPage();
            pbEnableWebServer = new System.Windows.Forms.PictureBox();
            pbEnableTcsScripts = new System.Windows.Forms.PictureBox();
            pbOtherUnits = new System.Windows.Forms.PictureBox();
            pbPressureUnit = new System.Windows.Forms.PictureBox();
            pbLanguage = new System.Windows.Forms.PictureBox();
            pbBrakePipeChargingRate = new System.Windows.Forms.PictureBox();
            pbGraduatedRelease = new System.Windows.Forms.PictureBox();
            pbRetainers = new System.Windows.Forms.PictureBox();
            pbControlConfirmations = new System.Windows.Forms.PictureBox();
            pbAlerter = new System.Windows.Forms.PictureBox();
            pbOverspeedMonitor = new System.Windows.Forms.PictureBox();
            label29 = new System.Windows.Forms.Label();
            numericWebServerPort = new System.Windows.Forms.NumericUpDown();
            checkEnableWebServer = new System.Windows.Forms.CheckBox();
            checkSpeedMonitor = new System.Windows.Forms.CheckBox();
            checkEnableTCSScripts = new System.Windows.Forms.CheckBox();
            labelOtherUnits = new System.Windows.Forms.Label();
            labelPressureUnit = new System.Windows.Forms.Label();
            comboOtherUnits = new System.Windows.Forms.ComboBox();
            comboPressureUnit = new System.Windows.Forms.ComboBox();
            labelLanguage = new System.Windows.Forms.Label();
            comboLanguage = new System.Windows.Forms.ComboBox();
            checkAlerterExternal = new System.Windows.Forms.CheckBox();
            checkRetainers = new System.Windows.Forms.CheckBox();
            tabPageAudio = new System.Windows.Forms.TabPage();
            pbExternalSoundPassThruPercent = new System.Windows.Forms.PictureBox();
            pbSoundDetailLevel = new System.Windows.Forms.PictureBox();
            pbSoundVolumePercent = new System.Windows.Forms.PictureBox();
            numericExternalSoundPassThruPercent = new System.Windows.Forms.NumericUpDown();
            labelExternalSound = new System.Windows.Forms.Label();
            numericSoundVolumePercent = new System.Windows.Forms.NumericUpDown();
            labelSoundVolume = new System.Windows.Forms.Label();
            labelSoundDetailLevel = new System.Windows.Forms.Label();
            numericSoundDetailLevel = new System.Windows.Forms.NumericUpDown();
            tabPageVideo = new System.Windows.Forms.TabPage();
            checkLODViewingExtension = new System.Windows.Forms.CheckBox();
            panel1 = new System.Windows.Forms.Panel();
            radioButtonWindow = new System.Windows.Forms.RadioButton();
            radioButtonFullScreen = new System.Windows.Forms.RadioButton();
            checkBoxFullScreenNativeResolution = new System.Windows.Forms.CheckBox();
            labelMSAACount = new System.Windows.Forms.Label();
            label28 = new System.Windows.Forms.Label();
            trackbarMultiSampling = new System.Windows.Forms.TrackBar();
            checkShadowAllShapes = new System.Windows.Forms.CheckBox();
            checkDoubleWire = new System.Windows.Forms.CheckBox();
            label15 = new System.Windows.Forms.Label();
            labelDayAmbientLight = new System.Windows.Forms.Label();
            checkModelInstancing = new System.Windows.Forms.CheckBox();
            trackDayAmbientLight = new System.Windows.Forms.TrackBar();
            checkVerticalSync = new System.Windows.Forms.CheckBox();
            labelDistantMountainsViewingDistance = new System.Windows.Forms.Label();
            numericDistantMountainsViewingDistance = new System.Windows.Forms.NumericUpDown();
            checkDistantMountains = new System.Windows.Forms.CheckBox();
            label14 = new System.Windows.Forms.Label();
            numericViewingDistance = new System.Windows.Forms.NumericUpDown();
            labelFOVHelp = new System.Windows.Forms.Label();
            numericViewingFOV = new System.Windows.Forms.NumericUpDown();
            label10 = new System.Windows.Forms.Label();
            numericCab2DStretch = new System.Windows.Forms.NumericUpDown();
            labelCab2DStretch = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            numericWorldObjectDensity = new System.Windows.Forms.NumericUpDown();
            comboWindowSize = new System.Windows.Forms.ComboBox();
            checkWindowGlass = new System.Windows.Forms.CheckBox();
            label3 = new System.Windows.Forms.Label();
            checkDynamicShadows = new System.Windows.Forms.CheckBox();
            checkWire = new System.Windows.Forms.CheckBox();
            tabPageSimulation = new System.Windows.Forms.TabPage();
            checkElectricPowerConnected = new System.Windows.Forms.CheckBox();
            label40 = new System.Windows.Forms.Label();
            checkDieselEnginesStarted = new System.Windows.Forms.CheckBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            checkUseLocationPassingPaths = new System.Windows.Forms.CheckBox();
            checkDoorsAITrains = new System.Windows.Forms.CheckBox();
            checkForcedRedAtStationStops = new System.Windows.Forms.CheckBox();
            checkBoilerPreheated = new System.Windows.Forms.CheckBox();
            checkSimpleControlPhysics = new System.Windows.Forms.CheckBox();
            checkCurveSpeedDependent = new System.Windows.Forms.CheckBox();
            labelAdhesionMovingAverageFilterSize = new System.Windows.Forms.Label();
            numericAdhesionMovingAverageFilterSize = new System.Windows.Forms.NumericUpDown();
            checkBreakCouplers = new System.Windows.Forms.CheckBox();
            checkUseAdvancedAdhesion = new System.Windows.Forms.CheckBox();
            tabPageKeyboard = new System.Windows.Forms.TabPage();
            buttonExport = new System.Windows.Forms.Button();
            buttonDefaultKeys = new System.Windows.Forms.Button();
            buttonCheckKeys = new System.Windows.Forms.Button();
            panelKeys = new System.Windows.Forms.Panel();
            tabPageRailDriver = new System.Windows.Forms.TabPage();
            buttonRDSettingsExport = new System.Windows.Forms.Button();
            buttonCheck = new System.Windows.Forms.Button();
            buttonRDReset = new System.Windows.Forms.Button();
            buttonStartRDCalibration = new System.Windows.Forms.Button();
            buttonShowRDLegend = new System.Windows.Forms.Button();
            panelRDSettings = new System.Windows.Forms.Panel();
            panelRDOptions = new System.Windows.Forms.Panel();
            groupBoxReverseRDLevers = new System.Windows.Forms.GroupBox();
            checkFullRangeThrottle = new System.Windows.Forms.CheckBox();
            checkReverseIndependentBrake = new System.Windows.Forms.CheckBox();
            checkReverseAutoBrake = new System.Windows.Forms.CheckBox();
            checkReverseThrottle = new System.Windows.Forms.CheckBox();
            checkReverseReverser = new System.Windows.Forms.CheckBox();
            panelRDButtons = new System.Windows.Forms.Panel();
            tabPageDataLogger = new System.Windows.Forms.TabPage();
            comboDataLogSpeedUnits = new System.Windows.Forms.ComboBox();
            comboDataLoggerSeparator = new System.Windows.Forms.ComboBox();
            label19 = new System.Windows.Forms.Label();
            label18 = new System.Windows.Forms.Label();
            checkDataLogMisc = new System.Windows.Forms.CheckBox();
            checkDataLogPerformance = new System.Windows.Forms.CheckBox();
            checkDataLogger = new System.Windows.Forms.CheckBox();
            label17 = new System.Windows.Forms.Label();
            checkDataLogPhysics = new System.Windows.Forms.CheckBox();
            checkDataLogSteamPerformance = new System.Windows.Forms.CheckBox();
            checkVerboseConfigurationMessages = new System.Windows.Forms.CheckBox();
            tabPageEvaluate = new System.Windows.Forms.TabPage();
            checkListDataLogTSContents = new System.Windows.Forms.CheckedListBox();
            labelDataLogTSInterval = new System.Windows.Forms.Label();
            checkDataLogStationStops = new System.Windows.Forms.CheckBox();
            numericDataLogTSInterval = new System.Windows.Forms.NumericUpDown();
            checkDataLogTrainSpeed = new System.Windows.Forms.CheckBox();
            tabPageContent = new System.Windows.Forms.TabPage();
            labelContent = new System.Windows.Forms.Label();
            buttonContentDelete = new System.Windows.Forms.Button();
            groupBoxContent = new System.Windows.Forms.GroupBox();
            buttonContentBrowse = new System.Windows.Forms.Button();
            textBoxContentPath = new System.Windows.Forms.TextBox();
            label20 = new System.Windows.Forms.Label();
            label22 = new System.Windows.Forms.Label();
            textBoxContentName = new System.Windows.Forms.TextBox();
            buttonContentAdd = new System.Windows.Forms.Button();
            panelContent = new System.Windows.Forms.Panel();
            dataGridViewContent = new System.Windows.Forms.DataGridView();
            NameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            PathColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            bindingSourceContent = new System.Windows.Forms.BindingSource(components);
            tabPageUpdater = new System.Windows.Forms.TabPage();
            buttonUpdaterExecute = new System.Windows.Forms.Button();
            groupBoxUpdateFrequency = new System.Windows.Forms.GroupBox();
            labelUpdaterFrequency = new System.Windows.Forms.Label();
            trackBarUpdaterFrequency = new System.Windows.Forms.TrackBar();
            labelCurrentVersion = new System.Windows.Forms.Label();
            labelCurrentVersionDesc = new System.Windows.Forms.Label();
            groupBoxUpdates = new System.Windows.Forms.GroupBox();
            rbDeveloperPrereleases = new System.Windows.Forms.RadioButton();
            rbPublicPrereleases = new System.Windows.Forms.RadioButton();
            rbPublicReleases = new System.Windows.Forms.RadioButton();
            label30 = new System.Windows.Forms.Label();
            labelPublicReleaseDesc = new System.Windows.Forms.Label();
            label32 = new System.Windows.Forms.Label();
            labelAvailableVersionDesc = new System.Windows.Forms.Label();
            labelAvailableVersion = new System.Windows.Forms.Label();
            tabPageExperimental = new System.Windows.Forms.TabPage();
            label27 = new System.Windows.Forms.Label();
            numericActWeatherRandomizationLevel = new System.Windows.Forms.NumericUpDown();
            label26 = new System.Windows.Forms.Label();
            label13 = new System.Windows.Forms.Label();
            label12 = new System.Windows.Forms.Label();
            numericActRandomizationLevel = new System.Windows.Forms.NumericUpDown();
            checkCorrectQuestionableBrakingParams = new System.Windows.Forms.CheckBox();
            label25 = new System.Windows.Forms.Label();
            precipitationBoxLength = new System.Windows.Forms.NumericUpDown();
            label24 = new System.Windows.Forms.Label();
            precipitationBoxWidth = new System.Windows.Forms.NumericUpDown();
            label23 = new System.Windows.Forms.Label();
            precipitationBoxHeight = new System.Windows.Forms.NumericUpDown();
            label16 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            label21 = new System.Windows.Forms.Label();
            AdhesionFactorChangeValueLabel = new System.Windows.Forms.Label();
            AdhesionFactorValueLabel = new System.Windows.Forms.Label();
            labelLODBias = new System.Windows.Forms.Label();
            checkShapeWarnings = new System.Windows.Forms.CheckBox();
            trackLODBias = new System.Windows.Forms.TrackBar();
            AdhesionLevelValue = new System.Windows.Forms.Label();
            AdhesionLevelLabel = new System.Windows.Forms.Label();
            trackAdhesionFactorChange = new System.Windows.Forms.TrackBar();
            trackAdhesionFactor = new System.Windows.Forms.TrackBar();
            checkSignalLightGlow = new System.Windows.Forms.CheckBox();
            checkUseMSTSEnv = new System.Windows.Forms.CheckBox();
            labelPerformanceTunerTarget = new System.Windows.Forms.Label();
            numericPerformanceTunerTarget = new System.Windows.Forms.NumericUpDown();
            checkPerformanceTuner = new System.Windows.Forms.CheckBox();
            label8 = new System.Windows.Forms.Label();
            numericSuperElevationGauge = new System.Windows.Forms.NumericUpDown();
            label7 = new System.Windows.Forms.Label();
            numericSuperElevationMinLen = new System.Windows.Forms.NumericUpDown();
            label6 = new System.Windows.Forms.Label();
            numericUseSuperElevation = new System.Windows.Forms.NumericUpDown();
            ElevationText = new System.Windows.Forms.Label();
            label5 = new System.Windows.Forms.Label();
            toolTip1 = new System.Windows.Forms.ToolTip(components);
            ((System.ComponentModel.ISupportInitialize)numericBrakePipeChargingRate).BeginInit();
            tabOptions.SuspendLayout();
            tabPageGeneral.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pbEnableWebServer).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbEnableTcsScripts).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbOtherUnits).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbPressureUnit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbLanguage).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbBrakePipeChargingRate).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbGraduatedRelease).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbRetainers).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbControlConfirmations).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbAlerter).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbOverspeedMonitor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericWebServerPort).BeginInit();
            tabPageAudio.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pbExternalSoundPassThruPercent).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbSoundDetailLevel).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbSoundVolumePercent).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericExternalSoundPassThruPercent).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericSoundVolumePercent).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericSoundDetailLevel).BeginInit();
            tabPageVideo.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackbarMultiSampling).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackDayAmbientLight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericDistantMountainsViewingDistance).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericViewingDistance).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericViewingFOV).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericCab2DStretch).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericWorldObjectDensity).BeginInit();
            tabPageSimulation.SuspendLayout();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericAdhesionMovingAverageFilterSize).BeginInit();
            tabPageKeyboard.SuspendLayout();
            tabPageRailDriver.SuspendLayout();
            panelRDSettings.SuspendLayout();
            panelRDOptions.SuspendLayout();
            groupBoxReverseRDLevers.SuspendLayout();
            tabPageDataLogger.SuspendLayout();
            tabPageEvaluate.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericDataLogTSInterval).BeginInit();
            tabPageContent.SuspendLayout();
            groupBoxContent.SuspendLayout();
            panelContent.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewContent).BeginInit();
            ((System.ComponentModel.ISupportInitialize)bindingSourceContent).BeginInit();
            tabPageUpdater.SuspendLayout();
            groupBoxUpdateFrequency.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackBarUpdaterFrequency).BeginInit();
            groupBoxUpdates.SuspendLayout();
            tabPageExperimental.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericActWeatherRandomizationLevel).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericActRandomizationLevel).BeginInit();
            ((System.ComponentModel.ISupportInitialize)precipitationBoxLength).BeginInit();
            ((System.ComponentModel.ISupportInitialize)precipitationBoxWidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)precipitationBoxHeight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackLODBias).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackAdhesionFactorChange).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackAdhesionFactor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericPerformanceTunerTarget).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericSuperElevationGauge).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericSuperElevationMinLen).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUseSuperElevation).BeginInit();
            SuspendLayout();
            // 
            // buttonOK
            // 
            buttonOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonOK.Location = new System.Drawing.Point(497, 439);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new System.Drawing.Size(80, 22);
            buttonOK.TabIndex = 1;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += ButtonOK_Click;
            // 
            // numericBrakePipeChargingRate
            // 
            numericBrakePipeChargingRate.Location = new System.Drawing.Point(31, 168);
            numericBrakePipeChargingRate.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numericBrakePipeChargingRate.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericBrakePipeChargingRate.Name = "numericBrakePipeChargingRate";
            numericBrakePipeChargingRate.Size = new System.Drawing.Size(58, 23);
            numericBrakePipeChargingRate.TabIndex = 7;
            numericBrakePipeChargingRate.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(95, 170);
            label4.Margin = new System.Windows.Forms.Padding(3);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(172, 15);
            label4.TabIndex = 8;
            label4.Text = "Brake pipe charging rate (PSI/s)";
            // 
            // checkGraduatedRelease
            // 
            checkGraduatedRelease.AutoSize = true;
            checkGraduatedRelease.Location = new System.Drawing.Point(31, 142);
            checkGraduatedRelease.Name = "checkGraduatedRelease";
            checkGraduatedRelease.Size = new System.Drawing.Size(173, 19);
            checkGraduatedRelease.TabIndex = 6;
            checkGraduatedRelease.Text = "Graduated release air brakes";
            checkGraduatedRelease.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            buttonCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            buttonCancel.Location = new System.Drawing.Point(583, 439);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new System.Drawing.Size(80, 22);
            buttonCancel.TabIndex = 2;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            // 
            // checkAlerter
            // 
            checkAlerter.AutoSize = true;
            checkAlerter.Location = new System.Drawing.Point(31, 6);
            checkAlerter.Name = "checkAlerter";
            checkAlerter.Size = new System.Drawing.Size(96, 19);
            checkAlerter.TabIndex = 0;
            checkAlerter.Text = "Alerter in cab";
            checkAlerter.UseVisualStyleBackColor = true;
            checkAlerter.CheckedChanged += CheckAlerter_CheckedChanged;
            // 
            // checkConfirmations
            // 
            checkConfirmations.AutoSize = true;
            checkConfirmations.Location = new System.Drawing.Point(31, 51);
            checkConfirmations.Name = "checkConfirmations";
            checkConfirmations.Size = new System.Drawing.Size(175, 19);
            checkConfirmations.TabIndex = 4;
            checkConfirmations.Text = "Show Control confirmations";
            checkConfirmations.UseVisualStyleBackColor = true;
            // 
            // tabOptions
            // 
            tabOptions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tabOptions.Controls.Add(tabPageGeneral);
            tabOptions.Controls.Add(tabPageAudio);
            tabOptions.Controls.Add(tabPageVideo);
            tabOptions.Controls.Add(tabPageSimulation);
            tabOptions.Controls.Add(tabPageKeyboard);
            tabOptions.Controls.Add(tabPageRailDriver);
            tabOptions.Controls.Add(tabPageDataLogger);
            tabOptions.Controls.Add(tabPageEvaluate);
            tabOptions.Controls.Add(tabPageContent);
            tabOptions.Controls.Add(tabPageUpdater);
            tabOptions.Controls.Add(tabPageExperimental);
            tabOptions.Location = new System.Drawing.Point(13, 12);
            tabOptions.Name = "tabOptions";
            tabOptions.SelectedIndex = 0;
            tabOptions.Size = new System.Drawing.Size(650, 422);
            tabOptions.TabIndex = 0;
            // 
            // tabPageGeneral
            // 
            tabPageGeneral.Controls.Add(pbEnableWebServer);
            tabPageGeneral.Controls.Add(pbEnableTcsScripts);
            tabPageGeneral.Controls.Add(pbOtherUnits);
            tabPageGeneral.Controls.Add(pbPressureUnit);
            tabPageGeneral.Controls.Add(pbLanguage);
            tabPageGeneral.Controls.Add(pbBrakePipeChargingRate);
            tabPageGeneral.Controls.Add(pbGraduatedRelease);
            tabPageGeneral.Controls.Add(pbRetainers);
            tabPageGeneral.Controls.Add(pbControlConfirmations);
            tabPageGeneral.Controls.Add(pbAlerter);
            tabPageGeneral.Controls.Add(pbOverspeedMonitor);
            tabPageGeneral.Controls.Add(label29);
            tabPageGeneral.Controls.Add(numericWebServerPort);
            tabPageGeneral.Controls.Add(checkEnableWebServer);
            tabPageGeneral.Controls.Add(checkSpeedMonitor);
            tabPageGeneral.Controls.Add(checkEnableTCSScripts);
            tabPageGeneral.Controls.Add(labelOtherUnits);
            tabPageGeneral.Controls.Add(labelPressureUnit);
            tabPageGeneral.Controls.Add(comboOtherUnits);
            tabPageGeneral.Controls.Add(comboPressureUnit);
            tabPageGeneral.Controls.Add(labelLanguage);
            tabPageGeneral.Controls.Add(comboLanguage);
            tabPageGeneral.Controls.Add(checkConfirmations);
            tabPageGeneral.Controls.Add(checkAlerterExternal);
            tabPageGeneral.Controls.Add(checkAlerter);
            tabPageGeneral.Controls.Add(numericBrakePipeChargingRate);
            tabPageGeneral.Controls.Add(checkRetainers);
            tabPageGeneral.Controls.Add(checkGraduatedRelease);
            tabPageGeneral.Controls.Add(label4);
            tabPageGeneral.Location = new System.Drawing.Point(4, 24);
            tabPageGeneral.Name = "tabPageGeneral";
            tabPageGeneral.Padding = new System.Windows.Forms.Padding(3);
            tabPageGeneral.Size = new System.Drawing.Size(642, 394);
            tabPageGeneral.TabIndex = 0;
            tabPageGeneral.Text = "General";
            tabPageGeneral.UseVisualStyleBackColor = true;
            // 
            // pbEnableWebServer
            // 
            pbEnableWebServer.Image = (System.Drawing.Image)resources.GetObject("pbEnableWebServer.Image");
            pbEnableWebServer.InitialImage = (System.Drawing.Image)resources.GetObject("pbEnableWebServer.InitialImage");
            pbEnableWebServer.Location = new System.Drawing.Point(7, 298);
            pbEnableWebServer.Name = "pbEnableWebServer";
            pbEnableWebServer.Size = new System.Drawing.Size(18, 18);
            pbEnableWebServer.TabIndex = 42;
            pbEnableWebServer.TabStop = false;
            pbEnableWebServer.Click += HelpIcon_Click;
            pbEnableWebServer.MouseEnter += HelpIcon_MouseEnter;
            pbEnableWebServer.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbEnableTcsScripts
            // 
            pbEnableTcsScripts.Image = (System.Drawing.Image)resources.GetObject("pbEnableTcsScripts.Image");
            pbEnableTcsScripts.InitialImage = (System.Drawing.Image)resources.GetObject("pbEnableTcsScripts.InitialImage");
            pbEnableTcsScripts.Location = new System.Drawing.Point(7, 274);
            pbEnableTcsScripts.Name = "pbEnableTcsScripts";
            pbEnableTcsScripts.Size = new System.Drawing.Size(18, 18);
            pbEnableTcsScripts.TabIndex = 41;
            pbEnableTcsScripts.TabStop = false;
            pbEnableTcsScripts.Click += HelpIcon_Click;
            pbEnableTcsScripts.MouseEnter += HelpIcon_MouseEnter;
            pbEnableTcsScripts.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbOtherUnits
            // 
            pbOtherUnits.Image = (System.Drawing.Image)resources.GetObject("pbOtherUnits.Image");
            pbOtherUnits.InitialImage = (System.Drawing.Image)resources.GetObject("pbOtherUnits.InitialImage");
            pbOtherUnits.Location = new System.Drawing.Point(7, 250);
            pbOtherUnits.Name = "pbOtherUnits";
            pbOtherUnits.Size = new System.Drawing.Size(18, 18);
            pbOtherUnits.TabIndex = 40;
            pbOtherUnits.TabStop = false;
            pbOtherUnits.Click += HelpIcon_Click;
            pbOtherUnits.MouseEnter += HelpIcon_MouseEnter;
            pbOtherUnits.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbPressureUnit
            // 
            pbPressureUnit.Image = (System.Drawing.Image)resources.GetObject("pbPressureUnit.Image");
            pbPressureUnit.InitialImage = (System.Drawing.Image)resources.GetObject("pbPressureUnit.InitialImage");
            pbPressureUnit.Location = new System.Drawing.Point(7, 223);
            pbPressureUnit.Name = "pbPressureUnit";
            pbPressureUnit.Size = new System.Drawing.Size(18, 18);
            pbPressureUnit.TabIndex = 39;
            pbPressureUnit.TabStop = false;
            pbPressureUnit.Click += HelpIcon_Click;
            pbPressureUnit.MouseEnter += HelpIcon_MouseEnter;
            pbPressureUnit.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbLanguage
            // 
            pbLanguage.Image = (System.Drawing.Image)resources.GetObject("pbLanguage.Image");
            pbLanguage.InitialImage = (System.Drawing.Image)resources.GetObject("pbLanguage.InitialImage");
            pbLanguage.Location = new System.Drawing.Point(7, 197);
            pbLanguage.Name = "pbLanguage";
            pbLanguage.Size = new System.Drawing.Size(18, 18);
            pbLanguage.TabIndex = 38;
            pbLanguage.TabStop = false;
            pbLanguage.Click += HelpIcon_Click;
            pbLanguage.MouseEnter += HelpIcon_MouseEnter;
            pbLanguage.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbBrakePipeChargingRate
            // 
            pbBrakePipeChargingRate.Image = (System.Drawing.Image)resources.GetObject("pbBrakePipeChargingRate.Image");
            pbBrakePipeChargingRate.InitialImage = (System.Drawing.Image)resources.GetObject("pbBrakePipeChargingRate.InitialImage");
            pbBrakePipeChargingRate.Location = new System.Drawing.Point(7, 170);
            pbBrakePipeChargingRate.Name = "pbBrakePipeChargingRate";
            pbBrakePipeChargingRate.Size = new System.Drawing.Size(18, 18);
            pbBrakePipeChargingRate.TabIndex = 37;
            pbBrakePipeChargingRate.TabStop = false;
            pbBrakePipeChargingRate.Click += HelpIcon_Click;
            pbBrakePipeChargingRate.MouseEnter += HelpIcon_MouseEnter;
            pbBrakePipeChargingRate.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbGraduatedRelease
            // 
            pbGraduatedRelease.Image = (System.Drawing.Image)resources.GetObject("pbGraduatedRelease.Image");
            pbGraduatedRelease.InitialImage = (System.Drawing.Image)resources.GetObject("pbGraduatedRelease.InitialImage");
            pbGraduatedRelease.Location = new System.Drawing.Point(7, 142);
            pbGraduatedRelease.Name = "pbGraduatedRelease";
            pbGraduatedRelease.Size = new System.Drawing.Size(18, 18);
            pbGraduatedRelease.TabIndex = 36;
            pbGraduatedRelease.TabStop = false;
            pbGraduatedRelease.Click += HelpIcon_Click;
            pbGraduatedRelease.MouseEnter += HelpIcon_MouseEnter;
            pbGraduatedRelease.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbRetainers
            // 
            pbRetainers.Image = (System.Drawing.Image)resources.GetObject("pbRetainers.Image");
            pbRetainers.InitialImage = (System.Drawing.Image)resources.GetObject("pbRetainers.InitialImage");
            pbRetainers.Location = new System.Drawing.Point(7, 118);
            pbRetainers.Name = "pbRetainers";
            pbRetainers.Size = new System.Drawing.Size(18, 18);
            pbRetainers.TabIndex = 35;
            pbRetainers.TabStop = false;
            pbRetainers.Click += HelpIcon_Click;
            pbRetainers.MouseEnter += HelpIcon_MouseEnter;
            pbRetainers.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbControlConfirmations
            // 
            pbControlConfirmations.Image = (System.Drawing.Image)resources.GetObject("pbControlConfirmations.Image");
            pbControlConfirmations.InitialImage = (System.Drawing.Image)resources.GetObject("pbControlConfirmations.InitialImage");
            pbControlConfirmations.Location = new System.Drawing.Point(7, 51);
            pbControlConfirmations.Name = "pbControlConfirmations";
            pbControlConfirmations.Size = new System.Drawing.Size(18, 18);
            pbControlConfirmations.TabIndex = 33;
            pbControlConfirmations.TabStop = false;
            pbControlConfirmations.Click += HelpIcon_Click;
            pbControlConfirmations.MouseEnter += HelpIcon_MouseEnter;
            pbControlConfirmations.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbAlerter
            // 
            pbAlerter.Image = (System.Drawing.Image)resources.GetObject("pbAlerter.Image");
            pbAlerter.InitialImage = (System.Drawing.Image)resources.GetObject("pbAlerter.InitialImage");
            pbAlerter.Location = new System.Drawing.Point(7, 6);
            pbAlerter.Name = "pbAlerter";
            pbAlerter.Size = new System.Drawing.Size(18, 18);
            pbAlerter.TabIndex = 32;
            pbAlerter.TabStop = false;
            pbAlerter.Click += HelpIcon_Click;
            pbAlerter.MouseEnter += HelpIcon_MouseEnter;
            pbAlerter.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbOverspeedMonitor
            // 
            pbOverspeedMonitor.Image = (System.Drawing.Image)resources.GetObject("pbOverspeedMonitor.Image");
            pbOverspeedMonitor.InitialImage = (System.Drawing.Image)resources.GetObject("pbOverspeedMonitor.InitialImage");
            pbOverspeedMonitor.Location = new System.Drawing.Point(321, 6);
            pbOverspeedMonitor.Name = "pbOverspeedMonitor";
            pbOverspeedMonitor.Size = new System.Drawing.Size(18, 18);
            pbOverspeedMonitor.TabIndex = 31;
            pbOverspeedMonitor.TabStop = false;
            pbOverspeedMonitor.Click += HelpIcon_Click;
            pbOverspeedMonitor.MouseEnter += HelpIcon_MouseEnter;
            pbOverspeedMonitor.MouseLeave += HelpIcon_MouseLeave;
            // 
            // label29
            // 
            label29.AutoSize = true;
            label29.Location = new System.Drawing.Point(112, 322);
            label29.Name = "label29";
            label29.Size = new System.Drawing.Size(76, 15);
            label29.TabIndex = 17;
            label29.Text = "Port Number";
            // 
            // numericWebServerPort
            // 
            numericWebServerPort.Location = new System.Drawing.Point(31, 320);
            numericWebServerPort.Maximum = new decimal(new int[] { 65534, 0, 0, 0 });
            numericWebServerPort.Minimum = new decimal(new int[] { 1025, 0, 0, 0 });
            numericWebServerPort.Name = "numericWebServerPort";
            numericWebServerPort.Size = new System.Drawing.Size(74, 23);
            numericWebServerPort.TabIndex = 16;
            numericWebServerPort.Value = new decimal(new int[] { 1025, 0, 0, 0 });
            // 
            // checkEnableWebServer
            // 
            checkEnableWebServer.AutoSize = true;
            checkEnableWebServer.Location = new System.Drawing.Point(31, 298);
            checkEnableWebServer.Name = "checkEnableWebServer";
            checkEnableWebServer.Size = new System.Drawing.Size(120, 19);
            checkEnableWebServer.TabIndex = 15;
            checkEnableWebServer.Text = "Enable WebServer";
            checkEnableWebServer.UseVisualStyleBackColor = true;
            // 
            // checkSpeedMonitor
            // 
            checkSpeedMonitor.AutoSize = true;
            checkSpeedMonitor.Location = new System.Drawing.Point(345, 6);
            checkSpeedMonitor.Name = "checkSpeedMonitor";
            checkSpeedMonitor.Size = new System.Drawing.Size(99, 19);
            checkSpeedMonitor.TabIndex = 14;
            checkSpeedMonitor.Text = "Speed control";
            checkSpeedMonitor.UseVisualStyleBackColor = true;
            checkSpeedMonitor.MouseEnter += HelpIcon_MouseEnter;
            checkSpeedMonitor.MouseLeave += HelpIcon_MouseLeave;
            // 
            // checkEnableTCSScripts
            // 
            checkEnableTCSScripts.AutoSize = true;
            checkEnableTCSScripts.Location = new System.Drawing.Point(31, 274);
            checkEnableTCSScripts.Name = "checkEnableTCSScripts";
            checkEnableTCSScripts.Size = new System.Drawing.Size(121, 19);
            checkEnableTCSScripts.TabIndex = 13;
            checkEnableTCSScripts.Text = "Enable TCS scripts";
            checkEnableTCSScripts.UseVisualStyleBackColor = true;
            // 
            // labelOtherUnits
            // 
            labelOtherUnits.AutoSize = true;
            labelOtherUnits.Location = new System.Drawing.Point(167, 250);
            labelOtherUnits.Margin = new System.Windows.Forms.Padding(3);
            labelOtherUnits.Name = "labelOtherUnits";
            labelOtherUnits.Size = new System.Drawing.Size(66, 15);
            labelOtherUnits.TabIndex = 9;
            labelOtherUnits.Text = "Other units";
            // 
            // labelPressureUnit
            // 
            labelPressureUnit.AutoSize = true;
            labelPressureUnit.Location = new System.Drawing.Point(167, 223);
            labelPressureUnit.Margin = new System.Windows.Forms.Padding(3);
            labelPressureUnit.Name = "labelPressureUnit";
            labelPressureUnit.Size = new System.Drawing.Size(75, 15);
            labelPressureUnit.TabIndex = 12;
            labelPressureUnit.Text = "Pressure unit";
            // 
            // comboOtherUnits
            // 
            comboOtherUnits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboOtherUnits.FormattingEnabled = true;
            comboOtherUnits.Location = new System.Drawing.Point(31, 247);
            comboOtherUnits.Name = "comboOtherUnits";
            comboOtherUnits.Size = new System.Drawing.Size(129, 23);
            comboOtherUnits.TabIndex = 8;
            // 
            // comboPressureUnit
            // 
            comboPressureUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboPressureUnit.FormattingEnabled = true;
            comboPressureUnit.Location = new System.Drawing.Point(31, 221);
            comboPressureUnit.Name = "comboPressureUnit";
            comboPressureUnit.Size = new System.Drawing.Size(129, 23);
            comboPressureUnit.TabIndex = 11;
            // 
            // labelLanguage
            // 
            labelLanguage.AutoSize = true;
            labelLanguage.Location = new System.Drawing.Point(167, 197);
            labelLanguage.Margin = new System.Windows.Forms.Padding(3);
            labelLanguage.Name = "labelLanguage";
            labelLanguage.Size = new System.Drawing.Size(59, 15);
            labelLanguage.TabIndex = 10;
            labelLanguage.Text = "Language";
            // 
            // comboLanguage
            // 
            comboLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboLanguage.FormattingEnabled = true;
            comboLanguage.Location = new System.Drawing.Point(31, 194);
            comboLanguage.Name = "comboLanguage";
            comboLanguage.Size = new System.Drawing.Size(129, 23);
            comboLanguage.TabIndex = 9;
            // 
            // checkAlerterExternal
            // 
            checkAlerterExternal.AutoSize = true;
            checkAlerterExternal.Location = new System.Drawing.Point(53, 29);
            checkAlerterExternal.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            checkAlerterExternal.Name = "checkAlerterExternal";
            checkAlerterExternal.Size = new System.Drawing.Size(138, 19);
            checkAlerterExternal.TabIndex = 1;
            checkAlerterExternal.Text = "Also in external views";
            checkAlerterExternal.UseVisualStyleBackColor = true;
            // 
            // checkRetainers
            // 
            checkRetainers.AutoSize = true;
            checkRetainers.Location = new System.Drawing.Point(31, 119);
            checkRetainers.Name = "checkRetainers";
            checkRetainers.Size = new System.Drawing.Size(155, 19);
            checkRetainers.TabIndex = 5;
            checkRetainers.Text = "Retainer valve on all cars";
            checkRetainers.UseVisualStyleBackColor = true;
            // 
            // tabPageAudio
            // 
            tabPageAudio.Controls.Add(pbExternalSoundPassThruPercent);
            tabPageAudio.Controls.Add(pbSoundDetailLevel);
            tabPageAudio.Controls.Add(pbSoundVolumePercent);
            tabPageAudio.Controls.Add(numericExternalSoundPassThruPercent);
            tabPageAudio.Controls.Add(labelExternalSound);
            tabPageAudio.Controls.Add(numericSoundVolumePercent);
            tabPageAudio.Controls.Add(labelSoundVolume);
            tabPageAudio.Controls.Add(labelSoundDetailLevel);
            tabPageAudio.Controls.Add(numericSoundDetailLevel);
            tabPageAudio.Location = new System.Drawing.Point(4, 24);
            tabPageAudio.Name = "tabPageAudio";
            tabPageAudio.Padding = new System.Windows.Forms.Padding(3);
            tabPageAudio.Size = new System.Drawing.Size(642, 394);
            tabPageAudio.TabIndex = 5;
            tabPageAudio.Text = "Audio";
            tabPageAudio.UseVisualStyleBackColor = true;
            // 
            // pbExternalSoundPassThruPercent
            // 
            pbExternalSoundPassThruPercent.Image = (System.Drawing.Image)resources.GetObject("pbExternalSoundPassThruPercent.Image");
            pbExternalSoundPassThruPercent.InitialImage = (System.Drawing.Image)resources.GetObject("pbExternalSoundPassThruPercent.InitialImage");
            pbExternalSoundPassThruPercent.Location = new System.Drawing.Point(7, 60);
            pbExternalSoundPassThruPercent.Name = "pbExternalSoundPassThruPercent";
            pbExternalSoundPassThruPercent.Size = new System.Drawing.Size(18, 18);
            pbExternalSoundPassThruPercent.TabIndex = 31;
            pbExternalSoundPassThruPercent.TabStop = false;
            pbExternalSoundPassThruPercent.Click += HelpIcon_Click;
            pbExternalSoundPassThruPercent.MouseEnter += HelpIcon_MouseEnter;
            pbExternalSoundPassThruPercent.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbSoundDetailLevel
            // 
            pbSoundDetailLevel.Image = (System.Drawing.Image)resources.GetObject("pbSoundDetailLevel.Image");
            pbSoundDetailLevel.InitialImage = (System.Drawing.Image)resources.GetObject("pbSoundDetailLevel.InitialImage");
            pbSoundDetailLevel.Location = new System.Drawing.Point(7, 33);
            pbSoundDetailLevel.Name = "pbSoundDetailLevel";
            pbSoundDetailLevel.Size = new System.Drawing.Size(18, 18);
            pbSoundDetailLevel.TabIndex = 30;
            pbSoundDetailLevel.TabStop = false;
            pbSoundDetailLevel.Click += HelpIcon_Click;
            pbSoundDetailLevel.MouseEnter += HelpIcon_MouseEnter;
            pbSoundDetailLevel.MouseLeave += HelpIcon_MouseLeave;
            // 
            // pbSoundVolumePercent
            // 
            pbSoundVolumePercent.Image = (System.Drawing.Image)resources.GetObject("pbSoundVolumePercent.Image");
            pbSoundVolumePercent.InitialImage = (System.Drawing.Image)resources.GetObject("pbSoundVolumePercent.InitialImage");
            pbSoundVolumePercent.Location = new System.Drawing.Point(7, 6);
            pbSoundVolumePercent.Name = "pbSoundVolumePercent";
            pbSoundVolumePercent.Size = new System.Drawing.Size(18, 18);
            pbSoundVolumePercent.TabIndex = 29;
            pbSoundVolumePercent.TabStop = false;
            pbSoundVolumePercent.Click += HelpIcon_Click;
            pbSoundVolumePercent.MouseEnter += HelpIcon_MouseEnter;
            pbSoundVolumePercent.MouseLeave += HelpIcon_MouseLeave;
            // 
            // numericExternalSoundPassThruPercent
            // 
            numericExternalSoundPassThruPercent.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            numericExternalSoundPassThruPercent.Location = new System.Drawing.Point(35, 60);
            numericExternalSoundPassThruPercent.Name = "numericExternalSoundPassThruPercent";
            numericExternalSoundPassThruPercent.Size = new System.Drawing.Size(58, 23);
            numericExternalSoundPassThruPercent.TabIndex = 5;
            toolTip1.SetToolTip(numericExternalSoundPassThruPercent, "Min 0 Max 100. Higher: louder sound\r\n\r\n");
            numericExternalSoundPassThruPercent.Value = new decimal(new int[] { 50, 0, 0, 0 });
            numericExternalSoundPassThruPercent.MouseEnter += HelpIcon_MouseEnter;
            numericExternalSoundPassThruPercent.MouseLeave += HelpIcon_MouseLeave;
            // 
            // labelExternalSound
            // 
            labelExternalSound.AutoSize = true;
            labelExternalSound.Location = new System.Drawing.Point(95, 62);
            labelExternalSound.Margin = new System.Windows.Forms.Padding(3);
            labelExternalSound.Name = "labelExternalSound";
            labelExternalSound.Size = new System.Drawing.Size(182, 15);
            labelExternalSound.TabIndex = 6;
            labelExternalSound.Text = "% external sound heard internally";
            labelExternalSound.MouseEnter += HelpIcon_MouseEnter;
            labelExternalSound.MouseLeave += HelpIcon_MouseLeave;
            // 
            // numericSoundVolumePercent
            // 
            numericSoundVolumePercent.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numericSoundVolumePercent.Location = new System.Drawing.Point(35, 6);
            numericSoundVolumePercent.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numericSoundVolumePercent.Name = "numericSoundVolumePercent";
            numericSoundVolumePercent.Size = new System.Drawing.Size(58, 23);
            numericSoundVolumePercent.TabIndex = 1;
            toolTip1.SetToolTip(numericSoundVolumePercent, "Sound Volume 0-100");
            numericSoundVolumePercent.Value = new decimal(new int[] { 10, 0, 0, 0 });
            numericSoundVolumePercent.MouseEnter += HelpIcon_MouseEnter;
            numericSoundVolumePercent.MouseLeave += HelpIcon_MouseLeave;
            // 
            // labelSoundVolume
            // 
            labelSoundVolume.AutoSize = true;
            labelSoundVolume.Location = new System.Drawing.Point(95, 8);
            labelSoundVolume.Margin = new System.Windows.Forms.Padding(3);
            labelSoundVolume.Name = "labelSoundVolume";
            labelSoundVolume.Size = new System.Drawing.Size(96, 15);
            labelSoundVolume.TabIndex = 2;
            labelSoundVolume.Text = "% sound volume";
            labelSoundVolume.MouseEnter += HelpIcon_MouseEnter;
            labelSoundVolume.MouseLeave += HelpIcon_MouseLeave;
            // 
            // labelSoundDetailLevel
            // 
            labelSoundDetailLevel.AutoSize = true;
            labelSoundDetailLevel.Location = new System.Drawing.Point(95, 35);
            labelSoundDetailLevel.Margin = new System.Windows.Forms.Padding(3);
            labelSoundDetailLevel.Name = "labelSoundDetailLevel";
            labelSoundDetailLevel.Size = new System.Drawing.Size(100, 15);
            labelSoundDetailLevel.TabIndex = 4;
            labelSoundDetailLevel.Text = "Sound detail level";
            labelSoundDetailLevel.MouseEnter += HelpIcon_MouseEnter;
            labelSoundDetailLevel.MouseLeave += HelpIcon_MouseLeave;
            // 
            // numericSoundDetailLevel
            // 
            numericSoundDetailLevel.Location = new System.Drawing.Point(35, 33);
            numericSoundDetailLevel.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            numericSoundDetailLevel.Name = "numericSoundDetailLevel";
            numericSoundDetailLevel.Size = new System.Drawing.Size(58, 23);
            numericSoundDetailLevel.TabIndex = 3;
            numericSoundDetailLevel.MouseEnter += HelpIcon_MouseEnter;
            numericSoundDetailLevel.MouseLeave += HelpIcon_MouseLeave;
            // 
            // tabPageVideo
            // 
            tabPageVideo.Controls.Add(checkLODViewingExtension);
            tabPageVideo.Controls.Add(panel1);
            tabPageVideo.Controls.Add(checkBoxFullScreenNativeResolution);
            tabPageVideo.Controls.Add(labelMSAACount);
            tabPageVideo.Controls.Add(label28);
            tabPageVideo.Controls.Add(trackbarMultiSampling);
            tabPageVideo.Controls.Add(checkShadowAllShapes);
            tabPageVideo.Controls.Add(checkDoubleWire);
            tabPageVideo.Controls.Add(label15);
            tabPageVideo.Controls.Add(labelDayAmbientLight);
            tabPageVideo.Controls.Add(checkModelInstancing);
            tabPageVideo.Controls.Add(trackDayAmbientLight);
            tabPageVideo.Controls.Add(checkVerticalSync);
            tabPageVideo.Controls.Add(labelDistantMountainsViewingDistance);
            tabPageVideo.Controls.Add(numericDistantMountainsViewingDistance);
            tabPageVideo.Controls.Add(checkDistantMountains);
            tabPageVideo.Controls.Add(label14);
            tabPageVideo.Controls.Add(numericViewingDistance);
            tabPageVideo.Controls.Add(labelFOVHelp);
            tabPageVideo.Controls.Add(numericViewingFOV);
            tabPageVideo.Controls.Add(label10);
            tabPageVideo.Controls.Add(numericCab2DStretch);
            tabPageVideo.Controls.Add(labelCab2DStretch);
            tabPageVideo.Controls.Add(label1);
            tabPageVideo.Controls.Add(numericWorldObjectDensity);
            tabPageVideo.Controls.Add(comboWindowSize);
            tabPageVideo.Controls.Add(checkWindowGlass);
            tabPageVideo.Controls.Add(label3);
            tabPageVideo.Controls.Add(checkDynamicShadows);
            tabPageVideo.Controls.Add(checkWire);
            tabPageVideo.Location = new System.Drawing.Point(4, 24);
            tabPageVideo.Name = "tabPageVideo";
            tabPageVideo.Padding = new System.Windows.Forms.Padding(3);
            tabPageVideo.Size = new System.Drawing.Size(642, 394);
            tabPageVideo.TabIndex = 4;
            tabPageVideo.Text = "Video";
            tabPageVideo.UseVisualStyleBackColor = true;
            // 
            // checkLODViewingExtension
            // 
            checkLODViewingExtension.AutoSize = true;
            checkLODViewingExtension.Location = new System.Drawing.Point(6, 141);
            checkLODViewingExtension.Name = "checkLODViewingExtension";
            checkLODViewingExtension.Size = new System.Drawing.Size(302, 19);
            checkLODViewingExtension.TabIndex = 25;
            checkLODViewingExtension.Text = "Extend object maximum viewing distance to horizon";
            checkLODViewingExtension.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            panel1.Controls.Add(radioButtonWindow);
            panel1.Controls.Add(radioButtonFullScreen);
            panel1.Location = new System.Drawing.Point(336, 90);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(301, 26);
            panel1.TabIndex = 32;
            // 
            // radioButtonWindow
            // 
            radioButtonWindow.AutoSize = true;
            radioButtonWindow.Location = new System.Drawing.Point(170, 3);
            radioButtonWindow.Name = "radioButtonWindow";
            radioButtonWindow.Size = new System.Drawing.Size(69, 19);
            radioButtonWindow.TabIndex = 1;
            radioButtonWindow.Text = "Window";
            radioButtonWindow.UseVisualStyleBackColor = true;
            // 
            // radioButtonFullScreen
            // 
            radioButtonFullScreen.AutoSize = true;
            radioButtonFullScreen.Checked = true;
            radioButtonFullScreen.Location = new System.Drawing.Point(22, 3);
            radioButtonFullScreen.Name = "radioButtonFullScreen";
            radioButtonFullScreen.Size = new System.Drawing.Size(79, 19);
            radioButtonFullScreen.TabIndex = 0;
            radioButtonFullScreen.TabStop = true;
            radioButtonFullScreen.Text = "FullScreen";
            radioButtonFullScreen.UseVisualStyleBackColor = true;
            // 
            // checkBoxFullScreenNativeResolution
            // 
            checkBoxFullScreenNativeResolution.AutoSize = true;
            checkBoxFullScreenNativeResolution.Location = new System.Drawing.Point(336, 67);
            checkBoxFullScreenNativeResolution.Name = "checkBoxFullScreenNativeResolution";
            checkBoxFullScreenNativeResolution.Size = new System.Drawing.Size(245, 19);
            checkBoxFullScreenNativeResolution.TabIndex = 29;
            checkBoxFullScreenNativeResolution.Text = "Use native screen resolution for fullscreen";
            checkBoxFullScreenNativeResolution.UseVisualStyleBackColor = true;
            checkBoxFullScreenNativeResolution.CheckedChanged += CheckBoxFullScreenNativeResolution_CheckedChanged;
            // 
            // labelMSAACount
            // 
            labelMSAACount.Location = new System.Drawing.Point(506, 164);
            labelMSAACount.Margin = new System.Windows.Forms.Padding(3);
            labelMSAACount.Name = "labelMSAACount";
            labelMSAACount.Size = new System.Drawing.Size(122, 13);
            labelMSAACount.TabIndex = 28;
            labelMSAACount.Text = "0x";
            labelMSAACount.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label28
            // 
            label28.AutoSize = true;
            label28.Location = new System.Drawing.Point(333, 164);
            label28.Margin = new System.Windows.Forms.Padding(3);
            label28.Name = "label28";
            label28.Size = new System.Drawing.Size(168, 15);
            label28.TabIndex = 27;
            label28.Text = "MultiSampling (Anti-Aliasing):";
            // 
            // trackbarMultiSampling
            // 
            trackbarMultiSampling.AutoSize = false;
            trackbarMultiSampling.BackColor = System.Drawing.SystemColors.Window;
            trackbarMultiSampling.LargeChange = 2;
            trackbarMultiSampling.Location = new System.Drawing.Point(336, 182);
            trackbarMultiSampling.Maximum = 5;
            trackbarMultiSampling.Name = "trackbarMultiSampling";
            trackbarMultiSampling.Size = new System.Drawing.Size(300, 44);
            trackbarMultiSampling.TabIndex = 26;
            trackbarMultiSampling.Scroll += TrackbarMultiSampling_Scroll;
            // 
            // checkShadowAllShapes
            // 
            checkShadowAllShapes.AutoSize = true;
            checkShadowAllShapes.Location = new System.Drawing.Point(6, 29);
            checkShadowAllShapes.Name = "checkShadowAllShapes";
            checkShadowAllShapes.Size = new System.Drawing.Size(140, 19);
            checkShadowAllShapes.TabIndex = 24;
            checkShadowAllShapes.Text = "Shadow for all shapes";
            checkShadowAllShapes.UseVisualStyleBackColor = true;
            // 
            // checkDoubleWire
            // 
            checkDoubleWire.AutoSize = true;
            checkDoubleWire.Location = new System.Drawing.Point(6, 119);
            checkDoubleWire.Name = "checkDoubleWire";
            checkDoubleWire.Size = new System.Drawing.Size(146, 19);
            checkDoubleWire.TabIndex = 23;
            checkDoubleWire.Text = "Double overhead wires";
            checkDoubleWire.UseVisualStyleBackColor = true;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new System.Drawing.Point(3, 320);
            label15.Margin = new System.Windows.Forms.Padding(3);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(160, 15);
            label15.TabIndex = 20;
            label15.Text = "Ambient daylight brightness:";
            // 
            // labelDayAmbientLight
            // 
            labelDayAmbientLight.Location = new System.Drawing.Point(157, 320);
            labelDayAmbientLight.Margin = new System.Windows.Forms.Padding(3);
            labelDayAmbientLight.Name = "labelDayAmbientLight";
            labelDayAmbientLight.Size = new System.Drawing.Size(158, 13);
            labelDayAmbientLight.TabIndex = 22;
            labelDayAmbientLight.Text = "100%";
            labelDayAmbientLight.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // checkModelInstancing
            // 
            checkModelInstancing.AutoSize = true;
            checkModelInstancing.Location = new System.Drawing.Point(6, 74);
            checkModelInstancing.Name = "checkModelInstancing";
            checkModelInstancing.Size = new System.Drawing.Size(118, 19);
            checkModelInstancing.TabIndex = 3;
            checkModelInstancing.Text = "Model instancing";
            checkModelInstancing.UseVisualStyleBackColor = true;
            // 
            // trackDayAmbientLight
            // 
            trackDayAmbientLight.AutoSize = false;
            trackDayAmbientLight.BackColor = System.Drawing.SystemColors.Window;
            trackDayAmbientLight.LargeChange = 4;
            trackDayAmbientLight.Location = new System.Drawing.Point(6, 338);
            trackDayAmbientLight.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            trackDayAmbientLight.Maximum = 30;
            trackDayAmbientLight.Minimum = 15;
            trackDayAmbientLight.Name = "trackDayAmbientLight";
            trackDayAmbientLight.Size = new System.Drawing.Size(311, 26);
            trackDayAmbientLight.SmallChange = 2;
            trackDayAmbientLight.TabIndex = 21;
            toolTip1.SetToolTip(trackDayAmbientLight, "Default is 100%");
            trackDayAmbientLight.Value = 20;
            trackDayAmbientLight.Scroll += TrackBarDayAmbientLight_Scroll;
            trackDayAmbientLight.ValueChanged += TrackDayAmbientLight_ValueChanged;
            // 
            // checkVerticalSync
            // 
            checkVerticalSync.AutoSize = true;
            checkVerticalSync.Location = new System.Drawing.Point(336, 141);
            checkVerticalSync.Name = "checkVerticalSync";
            checkVerticalSync.Size = new System.Drawing.Size(91, 19);
            checkVerticalSync.TabIndex = 5;
            checkVerticalSync.Text = "Vertical sync";
            checkVerticalSync.UseVisualStyleBackColor = true;
            // 
            // labelDistantMountainsViewingDistance
            // 
            labelDistantMountainsViewingDistance.AutoSize = true;
            labelDistantMountainsViewingDistance.Location = new System.Drawing.Point(92, 245);
            labelDistantMountainsViewingDistance.Margin = new System.Windows.Forms.Padding(3);
            labelDistantMountainsViewingDistance.Name = "labelDistantMountainsViewingDistance";
            labelDistantMountainsViewingDistance.Size = new System.Drawing.Size(124, 15);
            labelDistantMountainsViewingDistance.TabIndex = 12;
            labelDistantMountainsViewingDistance.Text = "Viewing distance (km)";
            // 
            // numericDistantMountainsViewingDistance
            // 
            numericDistantMountainsViewingDistance.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            numericDistantMountainsViewingDistance.Location = new System.Drawing.Point(28, 243);
            numericDistantMountainsViewingDistance.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericDistantMountainsViewingDistance.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numericDistantMountainsViewingDistance.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numericDistantMountainsViewingDistance.Name = "numericDistantMountainsViewingDistance";
            numericDistantMountainsViewingDistance.Size = new System.Drawing.Size(58, 23);
            numericDistantMountainsViewingDistance.TabIndex = 11;
            toolTip1.SetToolTip(numericDistantMountainsViewingDistance, "Distance to see mountains");
            numericDistantMountainsViewingDistance.Value = new decimal(new int[] { 40, 0, 0, 0 });
            // 
            // checkDistantMountains
            // 
            checkDistantMountains.AutoSize = true;
            checkDistantMountains.Location = new System.Drawing.Point(6, 221);
            checkDistantMountains.Name = "checkDistantMountains";
            checkDistantMountains.Size = new System.Drawing.Size(123, 19);
            checkDistantMountains.TabIndex = 10;
            checkDistantMountains.Text = "Distant mountains";
            checkDistantMountains.UseVisualStyleBackColor = true;
            checkDistantMountains.Click += CheckDistantMountains_Click;
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Location = new System.Drawing.Point(70, 197);
            label14.Margin = new System.Windows.Forms.Padding(3);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(118, 15);
            label14.TabIndex = 9;
            label14.Text = "Viewing distance (m)";
            // 
            // numericViewingDistance
            // 
            numericViewingDistance.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            numericViewingDistance.Location = new System.Drawing.Point(6, 195);
            numericViewingDistance.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numericViewingDistance.Minimum = new decimal(new int[] { 500, 0, 0, 0 });
            numericViewingDistance.Name = "numericViewingDistance";
            numericViewingDistance.Size = new System.Drawing.Size(58, 23);
            numericViewingDistance.TabIndex = 8;
            numericViewingDistance.Value = new decimal(new int[] { 2000, 0, 0, 0 });
            // 
            // labelFOVHelp
            // 
            labelFOVHelp.AutoSize = true;
            labelFOVHelp.Location = new System.Drawing.Point(324, 270);
            labelFOVHelp.Margin = new System.Windows.Forms.Padding(3);
            labelFOVHelp.Name = "labelFOVHelp";
            labelFOVHelp.Size = new System.Drawing.Size(28, 15);
            labelFOVHelp.TabIndex = 15;
            labelFOVHelp.Text = "XXX";
            // 
            // numericViewingFOV
            // 
            numericViewingFOV.Location = new System.Drawing.Point(6, 269);
            numericViewingFOV.Maximum = new decimal(new int[] { 120, 0, 0, 0 });
            numericViewingFOV.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericViewingFOV.Name = "numericViewingFOV";
            numericViewingFOV.Size = new System.Drawing.Size(58, 23);
            numericViewingFOV.TabIndex = 13;
            numericViewingFOV.Value = new decimal(new int[] { 1, 0, 0, 0 });
            numericViewingFOV.ValueChanged += NumericUpDownFOV_ValueChanged;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new System.Drawing.Point(70, 270);
            label10.Margin = new System.Windows.Forms.Padding(3);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(115, 15);
            label10.TabIndex = 14;
            label10.Text = "Viewing vertical FOV";
            // 
            // numericCab2DStretch
            // 
            numericCab2DStretch.Increment = new decimal(new int[] { 25, 0, 0, 0 });
            numericCab2DStretch.Location = new System.Drawing.Point(6, 170);
            numericCab2DStretch.Name = "numericCab2DStretch";
            numericCab2DStretch.Size = new System.Drawing.Size(58, 23);
            numericCab2DStretch.TabIndex = 6;
            toolTip1.SetToolTip(numericCab2DStretch, "0 to clip cab view, 100 to stretch it. For cab views that match the display, use 100.");
            // 
            // labelCab2DStretch
            // 
            labelCab2DStretch.AutoSize = true;
            labelCab2DStretch.Location = new System.Drawing.Point(70, 171);
            labelCab2DStretch.Margin = new System.Windows.Forms.Padding(3);
            labelCab2DStretch.Name = "labelCab2DStretch";
            labelCab2DStretch.Size = new System.Drawing.Size(95, 15);
            labelCab2DStretch.TabIndex = 7;
            labelCab2DStretch.Text = "% cab 2D stretch";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(70, 296);
            label1.Margin = new System.Windows.Forms.Padding(3);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(116, 15);
            label1.TabIndex = 17;
            label1.Text = "World object density";
            // 
            // numericWorldObjectDensity
            // 
            numericWorldObjectDensity.Location = new System.Drawing.Point(6, 294);
            numericWorldObjectDensity.Maximum = new decimal(new int[] { 99, 0, 0, 0 });
            numericWorldObjectDensity.Name = "numericWorldObjectDensity";
            numericWorldObjectDensity.Size = new System.Drawing.Size(58, 23);
            numericWorldObjectDensity.TabIndex = 16;
            // 
            // comboWindowSize
            // 
            comboWindowSize.FormattingEnabled = true;
            comboWindowSize.Items.AddRange(new object[] { "800x600", "1024x768", "1280x720", "1280x800", "1280x1024", "1360x768", "1366x768", "1440x900", "1536x864", "1600x900", "1680x1050", "1920x1080", "1920x1200", "2560x1440" });
            comboWindowSize.Location = new System.Drawing.Point(336, 16);
            comboWindowSize.Name = "comboWindowSize";
            comboWindowSize.Size = new System.Drawing.Size(129, 23);
            comboWindowSize.TabIndex = 18;
            // 
            // checkWindowGlass
            // 
            checkWindowGlass.AutoSize = true;
            checkWindowGlass.Location = new System.Drawing.Point(6, 51);
            checkWindowGlass.Name = "checkWindowGlass";
            checkWindowGlass.Size = new System.Drawing.Size(168, 19);
            checkWindowGlass.TabIndex = 2;
            checkWindowGlass.Text = "Glass on in-game windows";
            checkWindowGlass.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(471, 7);
            label3.Margin = new System.Windows.Forms.Padding(3);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(164, 54);
            label3.TabIndex = 19;
            label3.Text = "Window size or FullScreen resolution (type WIDTH x HEIGHT for custom size)";
            // 
            // checkDynamicShadows
            // 
            checkDynamicShadows.AutoSize = true;
            checkDynamicShadows.Location = new System.Drawing.Point(6, 6);
            checkDynamicShadows.Name = "checkDynamicShadows";
            checkDynamicShadows.Size = new System.Drawing.Size(122, 19);
            checkDynamicShadows.TabIndex = 0;
            checkDynamicShadows.Text = "Dynamic shadows";
            checkDynamicShadows.UseVisualStyleBackColor = true;
            // 
            // checkWire
            // 
            checkWire.AutoSize = true;
            checkWire.Location = new System.Drawing.Point(6, 97);
            checkWire.Name = "checkWire";
            checkWire.Size = new System.Drawing.Size(102, 19);
            checkWire.TabIndex = 4;
            checkWire.Text = "Overhead wire";
            checkWire.UseVisualStyleBackColor = true;
            // 
            // tabPageSimulation
            // 
            tabPageSimulation.Controls.Add(checkElectricPowerConnected);
            tabPageSimulation.Controls.Add(label40);
            tabPageSimulation.Controls.Add(checkDieselEnginesStarted);
            tabPageSimulation.Controls.Add(groupBox1);
            tabPageSimulation.Controls.Add(checkBoilerPreheated);
            tabPageSimulation.Controls.Add(checkSimpleControlPhysics);
            tabPageSimulation.Controls.Add(checkCurveSpeedDependent);
            tabPageSimulation.Controls.Add(labelAdhesionMovingAverageFilterSize);
            tabPageSimulation.Controls.Add(numericAdhesionMovingAverageFilterSize);
            tabPageSimulation.Controls.Add(checkBreakCouplers);
            tabPageSimulation.Controls.Add(checkUseAdvancedAdhesion);
            tabPageSimulation.Location = new System.Drawing.Point(4, 24);
            tabPageSimulation.Name = "tabPageSimulation";
            tabPageSimulation.Padding = new System.Windows.Forms.Padding(3);
            tabPageSimulation.Size = new System.Drawing.Size(642, 394);
            tabPageSimulation.TabIndex = 2;
            tabPageSimulation.Text = "Simulation";
            tabPageSimulation.UseVisualStyleBackColor = true;
            // 
            // checkElectricPowerConnected
            // 
            checkElectricPowerConnected.AutoSize = true;
            checkElectricPowerConnected.Checked = true;
            checkElectricPowerConnected.CheckState = System.Windows.Forms.CheckState.Checked;
            checkElectricPowerConnected.Enabled = false;
            checkElectricPowerConnected.Location = new System.Drawing.Point(6, 263);
            checkElectricPowerConnected.Name = "checkElectricPowerConnected";
            checkElectricPowerConnected.Size = new System.Drawing.Size(154, 19);
            checkElectricPowerConnected.TabIndex = 10;
            checkElectricPowerConnected.Text = "Electric - connect power";
            checkElectricPowerConnected.UseVisualStyleBackColor = true;
            // 
            // label40
            // 
            label40.AutoSize = true;
            label40.Location = new System.Drawing.Point(6, 169);
            label40.Name = "label40";
            label40.Size = new System.Drawing.Size(81, 15);
            label40.TabIndex = 10;
            label40.Text = "At game start,";
            // 
            // checkDieselEnginesStarted
            // 
            checkDieselEnginesStarted.AutoSize = true;
            checkDieselEnginesStarted.Location = new System.Drawing.Point(6, 238);
            checkDieselEnginesStarted.Name = "checkDieselEnginesStarted";
            checkDieselEnginesStarted.Size = new System.Drawing.Size(130, 19);
            checkDieselEnginesStarted.TabIndex = 9;
            checkDieselEnginesStarted.Text = "Diesel - run engines";
            checkDieselEnginesStarted.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(checkUseLocationPassingPaths);
            groupBox1.Controls.Add(checkDoorsAITrains);
            groupBox1.Controls.Add(checkForcedRedAtStationStops);
            groupBox1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            groupBox1.Location = new System.Drawing.Point(346, 6);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(290, 166);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Activity Options";
            // 
            // checkUseLocationPassingPaths
            // 
            checkUseLocationPassingPaths.AutoSize = true;
            checkUseLocationPassingPaths.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkUseLocationPassingPaths.Location = new System.Drawing.Point(6, 70);
            checkUseLocationPassingPaths.Name = "checkUseLocationPassingPaths";
            checkUseLocationPassingPaths.Size = new System.Drawing.Size(239, 19);
            checkUseLocationPassingPaths.TabIndex = 46;
            checkUseLocationPassingPaths.Text = "Location-linked passing path processing";
            checkUseLocationPassingPaths.UseVisualStyleBackColor = true;
            // 
            // checkDoorsAITrains
            // 
            checkDoorsAITrains.AutoSize = true;
            checkDoorsAITrains.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkDoorsAITrains.Location = new System.Drawing.Point(6, 46);
            checkDoorsAITrains.Name = "checkDoorsAITrains";
            checkDoorsAITrains.Size = new System.Drawing.Size(179, 19);
            checkDoorsAITrains.TabIndex = 45;
            checkDoorsAITrains.Text = "Open/close doors in AI trains";
            checkDoorsAITrains.UseVisualStyleBackColor = true;
            // 
            // checkForcedRedAtStationStops
            // 
            checkForcedRedAtStationStops.AutoSize = true;
            checkForcedRedAtStationStops.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkForcedRedAtStationStops.Location = new System.Drawing.Point(6, 22);
            checkForcedRedAtStationStops.Name = "checkForcedRedAtStationStops";
            checkForcedRedAtStationStops.Size = new System.Drawing.Size(165, 19);
            checkForcedRedAtStationStops.TabIndex = 23;
            checkForcedRedAtStationStops.Text = "Forced red at station stops";
            checkForcedRedAtStationStops.UseVisualStyleBackColor = true;
            // 
            // checkBoilerPreheated
            // 
            checkBoilerPreheated.AutoSize = true;
            checkBoilerPreheated.Location = new System.Drawing.Point(6, 189);
            checkBoilerPreheated.Name = "checkBoilerPreheated";
            checkBoilerPreheated.Size = new System.Drawing.Size(148, 19);
            checkBoilerPreheated.TabIndex = 8;
            checkBoilerPreheated.Text = "Steam - pre-heat boiler";
            checkBoilerPreheated.UseVisualStyleBackColor = true;
            // 
            // checkSimpleControlPhysics
            // 
            checkSimpleControlPhysics.AutoSize = true;
            checkSimpleControlPhysics.Location = new System.Drawing.Point(6, 213);
            checkSimpleControlPhysics.Name = "checkSimpleControlPhysics";
            checkSimpleControlPhysics.Size = new System.Drawing.Size(170, 19);
            checkSimpleControlPhysics.TabIndex = 8;
            checkSimpleControlPhysics.Text = "Simple Control and Physics";
            checkSimpleControlPhysics.UseVisualStyleBackColor = true;
            // 
            // checkCurveSpeedDependent
            // 
            checkCurveSpeedDependent.AutoSize = true;
            checkCurveSpeedDependent.Location = new System.Drawing.Point(6, 98);
            checkCurveSpeedDependent.Name = "checkCurveSpeedDependent";
            checkCurveSpeedDependent.Size = new System.Drawing.Size(178, 19);
            checkCurveSpeedDependent.TabIndex = 5;
            checkCurveSpeedDependent.Text = "Curve dependent speed limit";
            checkCurveSpeedDependent.UseVisualStyleBackColor = true;
            // 
            // labelAdhesionMovingAverageFilterSize
            // 
            labelAdhesionMovingAverageFilterSize.AutoSize = true;
            labelAdhesionMovingAverageFilterSize.Location = new System.Drawing.Point(92, 30);
            labelAdhesionMovingAverageFilterSize.Margin = new System.Windows.Forms.Padding(3);
            labelAdhesionMovingAverageFilterSize.Name = "labelAdhesionMovingAverageFilterSize";
            labelAdhesionMovingAverageFilterSize.Size = new System.Drawing.Size(194, 15);
            labelAdhesionMovingAverageFilterSize.TabIndex = 2;
            labelAdhesionMovingAverageFilterSize.Text = "Adhesion moving average filter size";
            // 
            // numericAdhesionMovingAverageFilterSize
            // 
            numericAdhesionMovingAverageFilterSize.Location = new System.Drawing.Point(28, 29);
            numericAdhesionMovingAverageFilterSize.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericAdhesionMovingAverageFilterSize.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericAdhesionMovingAverageFilterSize.Name = "numericAdhesionMovingAverageFilterSize";
            numericAdhesionMovingAverageFilterSize.Size = new System.Drawing.Size(58, 23);
            numericAdhesionMovingAverageFilterSize.TabIndex = 1;
            numericAdhesionMovingAverageFilterSize.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // checkBreakCouplers
            // 
            checkBreakCouplers.AutoSize = true;
            checkBreakCouplers.Location = new System.Drawing.Point(6, 53);
            checkBreakCouplers.Name = "checkBreakCouplers";
            checkBreakCouplers.Size = new System.Drawing.Size(103, 19);
            checkBreakCouplers.TabIndex = 3;
            checkBreakCouplers.Text = "Break couplers";
            checkBreakCouplers.UseVisualStyleBackColor = true;
            // 
            // checkUseAdvancedAdhesion
            // 
            checkUseAdvancedAdhesion.AutoSize = true;
            checkUseAdvancedAdhesion.Location = new System.Drawing.Point(6, 6);
            checkUseAdvancedAdhesion.Name = "checkUseAdvancedAdhesion";
            checkUseAdvancedAdhesion.Size = new System.Drawing.Size(167, 19);
            checkUseAdvancedAdhesion.TabIndex = 0;
            checkUseAdvancedAdhesion.Text = "Advanced adhesion model";
            checkUseAdvancedAdhesion.UseVisualStyleBackColor = true;
            checkUseAdvancedAdhesion.Click += CheckUseAdvancedAdhesion_Click;
            // 
            // tabPageKeyboard
            // 
            tabPageKeyboard.AutoScroll = true;
            tabPageKeyboard.Controls.Add(buttonExport);
            tabPageKeyboard.Controls.Add(buttonDefaultKeys);
            tabPageKeyboard.Controls.Add(buttonCheckKeys);
            tabPageKeyboard.Controls.Add(panelKeys);
            tabPageKeyboard.Location = new System.Drawing.Point(4, 24);
            tabPageKeyboard.Name = "tabPageKeyboard";
            tabPageKeyboard.Padding = new System.Windows.Forms.Padding(3);
            tabPageKeyboard.Size = new System.Drawing.Size(642, 394);
            tabPageKeyboard.TabIndex = 1;
            tabPageKeyboard.Text = "Keyboard";
            tabPageKeyboard.UseVisualStyleBackColor = true;
            // 
            // buttonExport
            // 
            buttonExport.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonExport.Location = new System.Drawing.Point(556, 367);
            buttonExport.Name = "buttonExport";
            buttonExport.Size = new System.Drawing.Size(80, 22);
            buttonExport.TabIndex = 4;
            buttonExport.Text = "Export";
            toolTip1.SetToolTip(buttonExport, "Generate a listing of your keyboard assignments.  \r\nThe output is placed on your desktop.");
            buttonExport.UseVisualStyleBackColor = true;
            buttonExport.Click += ButtonExport_Click;
            // 
            // buttonDefaultKeys
            // 
            buttonDefaultKeys.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            buttonDefaultKeys.Location = new System.Drawing.Point(93, 367);
            buttonDefaultKeys.Name = "buttonDefaultKeys";
            buttonDefaultKeys.Size = new System.Drawing.Size(80, 22);
            buttonDefaultKeys.TabIndex = 2;
            buttonDefaultKeys.Text = "Defaults";
            toolTip1.SetToolTip(buttonDefaultKeys, "Load the factory default key assignments.");
            buttonDefaultKeys.UseVisualStyleBackColor = true;
            buttonDefaultKeys.Click += ButtonDefaultKeys_Click;
            // 
            // buttonCheckKeys
            // 
            buttonCheckKeys.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            buttonCheckKeys.Location = new System.Drawing.Point(6, 367);
            buttonCheckKeys.Name = "buttonCheckKeys";
            buttonCheckKeys.Size = new System.Drawing.Size(80, 22);
            buttonCheckKeys.TabIndex = 1;
            buttonCheckKeys.Text = "Check";
            toolTip1.SetToolTip(buttonCheckKeys, "Check for incorrect key assignments.");
            buttonCheckKeys.UseVisualStyleBackColor = true;
            buttonCheckKeys.Click += ButtonCheckKeys_Click;
            // 
            // panelKeys
            // 
            panelKeys.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelKeys.AutoScroll = true;
            panelKeys.Location = new System.Drawing.Point(6, 6);
            panelKeys.Name = "panelKeys";
            panelKeys.Size = new System.Drawing.Size(630, 355);
            panelKeys.TabIndex = 0;
            // 
            // tabPageRailDriver
            // 
            tabPageRailDriver.Controls.Add(buttonRDSettingsExport);
            tabPageRailDriver.Controls.Add(buttonCheck);
            tabPageRailDriver.Controls.Add(buttonRDReset);
            tabPageRailDriver.Controls.Add(buttonStartRDCalibration);
            tabPageRailDriver.Controls.Add(buttonShowRDLegend);
            tabPageRailDriver.Controls.Add(panelRDSettings);
            tabPageRailDriver.Location = new System.Drawing.Point(4, 24);
            tabPageRailDriver.Name = "tabPageRailDriver";
            tabPageRailDriver.Size = new System.Drawing.Size(642, 394);
            tabPageRailDriver.TabIndex = 10;
            tabPageRailDriver.Text = "RailDriver";
            tabPageRailDriver.UseVisualStyleBackColor = true;
            // 
            // buttonRDSettingsExport
            // 
            buttonRDSettingsExport.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonRDSettingsExport.Location = new System.Drawing.Point(556, 367);
            buttonRDSettingsExport.Name = "buttonRDSettingsExport";
            buttonRDSettingsExport.Size = new System.Drawing.Size(80, 22);
            buttonRDSettingsExport.TabIndex = 5;
            buttonRDSettingsExport.Text = "Export";
            toolTip1.SetToolTip(buttonRDSettingsExport, "Generate a listing of your keyboard assignments.  \r\nThe output is placed on your desktop.");
            buttonRDSettingsExport.UseVisualStyleBackColor = true;
            buttonRDSettingsExport.Click += BtnRDSettingsExport_Click;
            // 
            // buttonCheck
            // 
            buttonCheck.Location = new System.Drawing.Point(263, 366);
            buttonCheck.Name = "buttonCheck";
            buttonCheck.Size = new System.Drawing.Size(80, 22);
            buttonCheck.TabIndex = 4;
            buttonCheck.Text = "Check";
            toolTip1.SetToolTip(buttonCheck, "Load the factory default button assignments.");
            buttonCheck.UseVisualStyleBackColor = true;
            buttonCheck.Click += BtnCheck_Click;
            // 
            // buttonRDReset
            // 
            buttonRDReset.Location = new System.Drawing.Point(177, 366);
            buttonRDReset.Name = "buttonRDReset";
            buttonRDReset.Size = new System.Drawing.Size(80, 22);
            buttonRDReset.TabIndex = 2;
            buttonRDReset.Text = "Defaults";
            toolTip1.SetToolTip(buttonRDReset, "Load the factory default button assignments.");
            buttonRDReset.UseVisualStyleBackColor = true;
            buttonRDReset.Click += BtnRDReset_Click;
            // 
            // buttonStartRDCalibration
            // 
            buttonStartRDCalibration.Location = new System.Drawing.Point(92, 366);
            buttonStartRDCalibration.Margin = new System.Windows.Forms.Padding(2);
            buttonStartRDCalibration.Name = "buttonStartRDCalibration";
            buttonStartRDCalibration.Size = new System.Drawing.Size(80, 22);
            buttonStartRDCalibration.TabIndex = 3;
            buttonStartRDCalibration.Text = "Calibration";
            toolTip1.SetToolTip(buttonStartRDCalibration, "Calibrate the lever position reading");
            buttonStartRDCalibration.UseVisualStyleBackColor = true;
            buttonStartRDCalibration.Click += StartRDCalibration_Click;
            // 
            // buttonShowRDLegend
            // 
            buttonShowRDLegend.Location = new System.Drawing.Point(6, 366);
            buttonShowRDLegend.Name = "buttonShowRDLegend";
            buttonShowRDLegend.Size = new System.Drawing.Size(80, 22);
            buttonShowRDLegend.TabIndex = 1;
            buttonShowRDLegend.Text = "Legend";
            toolTip1.SetToolTip(buttonShowRDLegend, "Show a legend of RailDriver board with button and lever description. Press cancel to close again.");
            buttonShowRDLegend.UseVisualStyleBackColor = true;
            buttonShowRDLegend.Click += BtnShowRDLegend_Click;
            // 
            // panelRDSettings
            // 
            panelRDSettings.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelRDSettings.AutoScroll = true;
            panelRDSettings.BackColor = System.Drawing.Color.Transparent;
            panelRDSettings.Controls.Add(panelRDOptions);
            panelRDSettings.Controls.Add(panelRDButtons);
            panelRDSettings.Location = new System.Drawing.Point(6, 6);
            panelRDSettings.Name = "panelRDSettings";
            panelRDSettings.Size = new System.Drawing.Size(630, 355);
            panelRDSettings.TabIndex = 0;
            // 
            // panelRDOptions
            // 
            panelRDOptions.Controls.Add(groupBoxReverseRDLevers);
            panelRDOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            panelRDOptions.Location = new System.Drawing.Point(302, 0);
            panelRDOptions.Name = "panelRDOptions";
            panelRDOptions.Size = new System.Drawing.Size(328, 355);
            panelRDOptions.TabIndex = 2;
            // 
            // groupBoxReverseRDLevers
            // 
            groupBoxReverseRDLevers.Controls.Add(checkFullRangeThrottle);
            groupBoxReverseRDLevers.Controls.Add(checkReverseIndependentBrake);
            groupBoxReverseRDLevers.Controls.Add(checkReverseAutoBrake);
            groupBoxReverseRDLevers.Controls.Add(checkReverseThrottle);
            groupBoxReverseRDLevers.Controls.Add(checkReverseReverser);
            groupBoxReverseRDLevers.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            groupBoxReverseRDLevers.Location = new System.Drawing.Point(6, 18);
            groupBoxReverseRDLevers.Name = "groupBoxReverseRDLevers";
            groupBoxReverseRDLevers.Size = new System.Drawing.Size(318, 150);
            groupBoxReverseRDLevers.TabIndex = 2;
            groupBoxReverseRDLevers.TabStop = false;
            groupBoxReverseRDLevers.Text = "Reverse Levers";
            // 
            // checkFullRangeThrottle
            // 
            checkFullRangeThrottle.AutoSize = true;
            checkFullRangeThrottle.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkFullRangeThrottle.Location = new System.Drawing.Point(7, 119);
            checkFullRangeThrottle.Name = "checkFullRangeThrottle";
            checkFullRangeThrottle.Size = new System.Drawing.Size(126, 19);
            checkFullRangeThrottle.TabIndex = 4;
            checkFullRangeThrottle.Text = "Full Range Throttle";
            toolTip1.SetToolTip(checkFullRangeThrottle, "Use the full range of the Throttle Lever. There will be no Auto Brake!");
            checkFullRangeThrottle.UseVisualStyleBackColor = true;
            // 
            // checkReverseIndependentBrake
            // 
            checkReverseIndependentBrake.AutoSize = true;
            checkReverseIndependentBrake.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkReverseIndependentBrake.Location = new System.Drawing.Point(7, 88);
            checkReverseIndependentBrake.Name = "checkReverseIndependentBrake";
            checkReverseIndependentBrake.Size = new System.Drawing.Size(219, 19);
            checkReverseIndependentBrake.TabIndex = 3;
            checkReverseIndependentBrake.Text = "Reverse Independent Brake Direction";
            checkReverseIndependentBrake.UseVisualStyleBackColor = true;
            // 
            // checkReverseAutoBrake
            // 
            checkReverseAutoBrake.AutoSize = true;
            checkReverseAutoBrake.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkReverseAutoBrake.Location = new System.Drawing.Point(7, 65);
            checkReverseAutoBrake.Name = "checkReverseAutoBrake";
            checkReverseAutoBrake.Size = new System.Drawing.Size(178, 19);
            checkReverseAutoBrake.TabIndex = 2;
            checkReverseAutoBrake.Text = "Reverse Auto Brake Direction";
            checkReverseAutoBrake.UseVisualStyleBackColor = true;
            // 
            // checkReverseThrottle
            // 
            checkReverseThrottle.AutoSize = true;
            checkReverseThrottle.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkReverseThrottle.Location = new System.Drawing.Point(7, 42);
            checkReverseThrottle.Name = "checkReverseThrottle";
            checkReverseThrottle.Size = new System.Drawing.Size(162, 19);
            checkReverseThrottle.TabIndex = 1;
            checkReverseThrottle.Text = "Reverse Throttle Direction";
            checkReverseThrottle.UseVisualStyleBackColor = true;
            // 
            // checkReverseReverser
            // 
            checkReverseReverser.AutoSize = true;
            checkReverseReverser.Font = new System.Drawing.Font("Segoe UI", 9F);
            checkReverseReverser.Location = new System.Drawing.Point(7, 20);
            checkReverseReverser.Name = "checkReverseReverser";
            checkReverseReverser.Size = new System.Drawing.Size(164, 19);
            checkReverseReverser.TabIndex = 0;
            checkReverseReverser.Text = "Reverse Reverser Direction";
            checkReverseReverser.UseVisualStyleBackColor = true;
            // 
            // panelRDButtons
            // 
            panelRDButtons.BackColor = System.Drawing.Color.Transparent;
            panelRDButtons.Dock = System.Windows.Forms.DockStyle.Left;
            panelRDButtons.Location = new System.Drawing.Point(0, 0);
            panelRDButtons.Name = "panelRDButtons";
            panelRDButtons.Size = new System.Drawing.Size(302, 355);
            panelRDButtons.TabIndex = 3;
            // 
            // tabPageDataLogger
            // 
            tabPageDataLogger.Controls.Add(comboDataLogSpeedUnits);
            tabPageDataLogger.Controls.Add(comboDataLoggerSeparator);
            tabPageDataLogger.Controls.Add(label19);
            tabPageDataLogger.Controls.Add(label18);
            tabPageDataLogger.Controls.Add(checkDataLogMisc);
            tabPageDataLogger.Controls.Add(checkDataLogPerformance);
            tabPageDataLogger.Controls.Add(checkDataLogger);
            tabPageDataLogger.Controls.Add(label17);
            tabPageDataLogger.Controls.Add(checkDataLogPhysics);
            tabPageDataLogger.Controls.Add(checkDataLogSteamPerformance);
            tabPageDataLogger.Controls.Add(checkVerboseConfigurationMessages);
            tabPageDataLogger.Location = new System.Drawing.Point(4, 24);
            tabPageDataLogger.Name = "tabPageDataLogger";
            tabPageDataLogger.Padding = new System.Windows.Forms.Padding(3);
            tabPageDataLogger.Size = new System.Drawing.Size(642, 394);
            tabPageDataLogger.TabIndex = 6;
            tabPageDataLogger.Text = "Data logger";
            tabPageDataLogger.UseVisualStyleBackColor = true;
            // 
            // comboDataLogSpeedUnits
            // 
            comboDataLogSpeedUnits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboDataLogSpeedUnits.FormattingEnabled = true;
            comboDataLogSpeedUnits.Location = new System.Drawing.Point(6, 66);
            comboDataLogSpeedUnits.Margin = new System.Windows.Forms.Padding(2);
            comboDataLogSpeedUnits.Name = "comboDataLogSpeedUnits";
            comboDataLogSpeedUnits.Size = new System.Drawing.Size(129, 23);
            comboDataLogSpeedUnits.TabIndex = 3;
            // 
            // comboDataLoggerSeparator
            // 
            comboDataLoggerSeparator.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboDataLoggerSeparator.FormattingEnabled = true;
            comboDataLoggerSeparator.Location = new System.Drawing.Point(6, 42);
            comboDataLoggerSeparator.Margin = new System.Windows.Forms.Padding(2);
            comboDataLoggerSeparator.Name = "comboDataLoggerSeparator";
            comboDataLoggerSeparator.Size = new System.Drawing.Size(129, 23);
            comboDataLoggerSeparator.TabIndex = 1;
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.Location = new System.Drawing.Point(140, 69);
            label19.Margin = new System.Windows.Forms.Padding(3);
            label19.Name = "label19";
            label19.Size = new System.Drawing.Size(68, 15);
            label19.TabIndex = 4;
            label19.Text = "Speed units";
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.ForeColor = System.Drawing.SystemColors.Highlight;
            label18.Location = new System.Drawing.Point(6, 6);
            label18.Margin = new System.Windows.Forms.Padding(3);
            label18.MaximumSize = new System.Drawing.Size(630, 0);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(422, 30);
            label18.TabIndex = 0;
            label18.Text = "Use data logger to record your simulation data (in-game command: F12).\r\nPlease remember that the size of the dump file grows with the simulation time!";
            // 
            // checkDataLogMisc
            // 
            checkDataLogMisc.AutoSize = true;
            checkDataLogMisc.Location = new System.Drawing.Point(6, 159);
            checkDataLogMisc.Name = "checkDataLogMisc";
            checkDataLogMisc.Size = new System.Drawing.Size(150, 19);
            checkDataLogMisc.TabIndex = 8;
            checkDataLogMisc.Text = "Log miscellaneous data";
            checkDataLogMisc.UseVisualStyleBackColor = true;
            // 
            // checkDataLogPerformance
            // 
            checkDataLogPerformance.AutoSize = true;
            checkDataLogPerformance.Location = new System.Drawing.Point(6, 114);
            checkDataLogPerformance.Name = "checkDataLogPerformance";
            checkDataLogPerformance.Size = new System.Drawing.Size(143, 19);
            checkDataLogPerformance.TabIndex = 6;
            checkDataLogPerformance.Text = "Log performance data";
            checkDataLogPerformance.UseVisualStyleBackColor = true;
            // 
            // checkDataLogger
            // 
            checkDataLogger.AutoSize = true;
            checkDataLogger.Location = new System.Drawing.Point(6, 91);
            checkDataLogger.Name = "checkDataLogger";
            checkDataLogger.Size = new System.Drawing.Size(225, 19);
            checkDataLogger.TabIndex = 5;
            checkDataLogger.Text = "Start logging with the simulation start";
            checkDataLogger.UseVisualStyleBackColor = true;
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.Location = new System.Drawing.Point(140, 44);
            label17.Margin = new System.Windows.Forms.Padding(3);
            label17.Name = "label17";
            label17.Size = new System.Drawing.Size(57, 15);
            label17.TabIndex = 2;
            label17.Text = "Separator";
            // 
            // checkDataLogPhysics
            // 
            checkDataLogPhysics.AutoSize = true;
            checkDataLogPhysics.Location = new System.Drawing.Point(6, 137);
            checkDataLogPhysics.Name = "checkDataLogPhysics";
            checkDataLogPhysics.Size = new System.Drawing.Size(114, 19);
            checkDataLogPhysics.TabIndex = 7;
            checkDataLogPhysics.Text = "Log physics data";
            checkDataLogPhysics.UseVisualStyleBackColor = true;
            // 
            // checkDataLogSteamPerformance
            // 
            checkDataLogSteamPerformance.AutoSize = true;
            checkDataLogSteamPerformance.Location = new System.Drawing.Point(6, 182);
            checkDataLogSteamPerformance.Name = "checkDataLogSteamPerformance";
            checkDataLogSteamPerformance.Size = new System.Drawing.Size(179, 19);
            checkDataLogSteamPerformance.TabIndex = 6;
            checkDataLogSteamPerformance.Text = "Log Steam performance data";
            checkDataLogSteamPerformance.UseVisualStyleBackColor = true;
            // 
            // checkVerboseConfigurationMessages
            // 
            checkVerboseConfigurationMessages.AutoSize = true;
            checkVerboseConfigurationMessages.Location = new System.Drawing.Point(6, 237);
            checkVerboseConfigurationMessages.Name = "checkVerboseConfigurationMessages";
            checkVerboseConfigurationMessages.Size = new System.Drawing.Size(254, 19);
            checkVerboseConfigurationMessages.TabIndex = 6;
            checkVerboseConfigurationMessages.Text = "Verbose ENG/WAG configuration messages";
            checkVerboseConfigurationMessages.UseVisualStyleBackColor = true;
            // 
            // tabPageEvaluate
            // 
            tabPageEvaluate.Controls.Add(checkListDataLogTSContents);
            tabPageEvaluate.Controls.Add(labelDataLogTSInterval);
            tabPageEvaluate.Controls.Add(checkDataLogStationStops);
            tabPageEvaluate.Controls.Add(numericDataLogTSInterval);
            tabPageEvaluate.Controls.Add(checkDataLogTrainSpeed);
            tabPageEvaluate.Location = new System.Drawing.Point(4, 24);
            tabPageEvaluate.Name = "tabPageEvaluate";
            tabPageEvaluate.Padding = new System.Windows.Forms.Padding(3);
            tabPageEvaluate.Size = new System.Drawing.Size(642, 394);
            tabPageEvaluate.TabIndex = 7;
            tabPageEvaluate.Text = "Evaluation";
            tabPageEvaluate.UseVisualStyleBackColor = true;
            // 
            // checkListDataLogTSContents
            // 
            checkListDataLogTSContents.FormattingEnabled = true;
            checkListDataLogTSContents.Location = new System.Drawing.Point(28, 54);
            checkListDataLogTSContents.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            checkListDataLogTSContents.Name = "checkListDataLogTSContents";
            checkListDataLogTSContents.Size = new System.Drawing.Size(158, 148);
            checkListDataLogTSContents.TabIndex = 3;
            // 
            // labelDataLogTSInterval
            // 
            labelDataLogTSInterval.AutoSize = true;
            labelDataLogTSInterval.Location = new System.Drawing.Point(92, 30);
            labelDataLogTSInterval.Margin = new System.Windows.Forms.Padding(3);
            labelDataLogTSInterval.Name = "labelDataLogTSInterval";
            labelDataLogTSInterval.Size = new System.Drawing.Size(74, 15);
            labelDataLogTSInterval.TabIndex = 2;
            labelDataLogTSInterval.Text = "Interval (sec)";
            // 
            // checkDataLogStationStops
            // 
            checkDataLogStationStops.AutoSize = true;
            checkDataLogStationStops.Location = new System.Drawing.Point(6, 276);
            checkDataLogStationStops.Name = "checkDataLogStationStops";
            checkDataLogStationStops.Size = new System.Drawing.Size(116, 19);
            checkDataLogStationStops.TabIndex = 4;
            checkDataLogStationStops.Text = "Log station stops";
            checkDataLogStationStops.UseVisualStyleBackColor = true;
            // 
            // numericDataLogTSInterval
            // 
            numericDataLogTSInterval.Location = new System.Drawing.Point(28, 29);
            numericDataLogTSInterval.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericDataLogTSInterval.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
            numericDataLogTSInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericDataLogTSInterval.Name = "numericDataLogTSInterval";
            numericDataLogTSInterval.Size = new System.Drawing.Size(58, 23);
            numericDataLogTSInterval.TabIndex = 1;
            numericDataLogTSInterval.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // checkDataLogTrainSpeed
            // 
            checkDataLogTrainSpeed.AutoSize = true;
            checkDataLogTrainSpeed.Location = new System.Drawing.Point(6, 6);
            checkDataLogTrainSpeed.Name = "checkDataLogTrainSpeed";
            checkDataLogTrainSpeed.Size = new System.Drawing.Size(107, 19);
            checkDataLogTrainSpeed.TabIndex = 0;
            checkDataLogTrainSpeed.Text = "Log train speed";
            checkDataLogTrainSpeed.UseVisualStyleBackColor = true;
            checkDataLogTrainSpeed.Click += CheckDataLogTrainSpeed_Click;
            // 
            // tabPageContent
            // 
            tabPageContent.Controls.Add(labelContent);
            tabPageContent.Controls.Add(buttonContentDelete);
            tabPageContent.Controls.Add(groupBoxContent);
            tabPageContent.Controls.Add(buttonContentAdd);
            tabPageContent.Controls.Add(panelContent);
            tabPageContent.Location = new System.Drawing.Point(4, 24);
            tabPageContent.Name = "tabPageContent";
            tabPageContent.Padding = new System.Windows.Forms.Padding(3);
            tabPageContent.Size = new System.Drawing.Size(642, 394);
            tabPageContent.TabIndex = 9;
            tabPageContent.Text = "Content";
            tabPageContent.UseVisualStyleBackColor = true;
            // 
            // labelContent
            // 
            labelContent.AutoSize = true;
            labelContent.ForeColor = System.Drawing.SystemColors.Highlight;
            labelContent.Location = new System.Drawing.Point(6, 6);
            labelContent.Margin = new System.Windows.Forms.Padding(3);
            labelContent.MaximumSize = new System.Drawing.Size(630, 0);
            labelContent.Name = "labelContent";
            labelContent.Size = new System.Drawing.Size(485, 15);
            labelContent.TabIndex = 3;
            labelContent.Text = "Content folders contain the game content. Add each full and mini-route MSTS installation.";
            // 
            // buttonContentDelete
            // 
            buttonContentDelete.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            buttonContentDelete.Location = new System.Drawing.Point(6, 358);
            buttonContentDelete.Name = "buttonContentDelete";
            buttonContentDelete.Size = new System.Drawing.Size(80, 22);
            buttonContentDelete.TabIndex = 1;
            buttonContentDelete.Text = "Delete";
            buttonContentDelete.UseVisualStyleBackColor = true;
            buttonContentDelete.Click += ButtonContentDelete_Click;
            // 
            // groupBoxContent
            // 
            groupBoxContent.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupBoxContent.Controls.Add(buttonContentBrowse);
            groupBoxContent.Controls.Add(textBoxContentPath);
            groupBoxContent.Controls.Add(label20);
            groupBoxContent.Controls.Add(label22);
            groupBoxContent.Controls.Add(textBoxContentName);
            groupBoxContent.Location = new System.Drawing.Point(93, 312);
            groupBoxContent.Name = "groupBoxContent";
            groupBoxContent.Size = new System.Drawing.Size(543, 78);
            groupBoxContent.TabIndex = 2;
            groupBoxContent.TabStop = false;
            groupBoxContent.Text = "Installation profile";
            // 
            // buttonContentBrowse
            // 
            buttonContentBrowse.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            buttonContentBrowse.Location = new System.Drawing.Point(457, 18);
            buttonContentBrowse.Name = "buttonContentBrowse";
            buttonContentBrowse.Size = new System.Drawing.Size(80, 22);
            buttonContentBrowse.TabIndex = 2;
            buttonContentBrowse.Text = "Change...";
            buttonContentBrowse.UseVisualStyleBackColor = true;
            buttonContentBrowse.Click += ButtonContentBrowse_Click;
            // 
            // textBoxContentPath
            // 
            textBoxContentPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            textBoxContentPath.Location = new System.Drawing.Point(54, 21);
            textBoxContentPath.Name = "textBoxContentPath";
            textBoxContentPath.ReadOnly = true;
            textBoxContentPath.Size = new System.Drawing.Size(397, 23);
            textBoxContentPath.TabIndex = 1;
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.Location = new System.Drawing.Point(6, 51);
            label20.Name = "label20";
            label20.Size = new System.Drawing.Size(42, 15);
            label20.TabIndex = 3;
            label20.Text = "Name:";
            // 
            // label22
            // 
            label22.AutoSize = true;
            label22.Location = new System.Drawing.Point(6, 24);
            label22.Name = "label22";
            label22.Size = new System.Drawing.Size(34, 15);
            label22.TabIndex = 0;
            label22.Text = "Path:";
            // 
            // textBoxContentName
            // 
            textBoxContentName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            textBoxContentName.Location = new System.Drawing.Point(54, 48);
            textBoxContentName.Name = "textBoxContentName";
            textBoxContentName.Size = new System.Drawing.Size(483, 23);
            textBoxContentName.TabIndex = 4;
            textBoxContentName.TextChanged += TextBoxContentName_TextChanged;
            // 
            // buttonContentAdd
            // 
            buttonContentAdd.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            buttonContentAdd.Location = new System.Drawing.Point(6, 331);
            buttonContentAdd.Name = "buttonContentAdd";
            buttonContentAdd.Size = new System.Drawing.Size(80, 22);
            buttonContentAdd.TabIndex = 0;
            buttonContentAdd.Text = "Add...";
            buttonContentAdd.UseVisualStyleBackColor = true;
            buttonContentAdd.Click += ButtonContentAdd_Click;
            // 
            // panelContent
            // 
            panelContent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelContent.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelContent.Controls.Add(dataGridViewContent);
            panelContent.Location = new System.Drawing.Point(6, 38);
            panelContent.Name = "panelContent";
            panelContent.Size = new System.Drawing.Size(629, 269);
            panelContent.TabIndex = 2;
            // 
            // dataGridViewContent
            // 
            dataGridViewContent.AllowUserToAddRows = false;
            dataGridViewContent.AllowUserToDeleteRows = false;
            dataGridViewContent.AutoGenerateColumns = false;
            dataGridViewContent.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewContent.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            dataGridViewContent.BackgroundColor = System.Drawing.SystemColors.Window;
            dataGridViewContent.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dataGridViewContent.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            dataGridViewContent.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dataGridViewContent.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dataGridViewContent.ColumnHeadersHeight = 29;
            dataGridViewContent.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewContent.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { NameColumn, PathColumn });
            dataGridViewContent.DataSource = bindingSourceContent;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            dataGridViewContent.DefaultCellStyle = dataGridViewCellStyle2;
            dataGridViewContent.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridViewContent.Location = new System.Drawing.Point(0, 0);
            dataGridViewContent.MultiSelect = false;
            dataGridViewContent.Name = "dataGridViewContent";
            dataGridViewContent.ReadOnly = true;
            dataGridViewContent.RowHeadersVisible = false;
            dataGridViewContent.RowHeadersWidth = 51;
            dataGridViewContent.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            dataGridViewContent.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewContent.Size = new System.Drawing.Size(627, 267);
            dataGridViewContent.TabIndex = 0;
            dataGridViewContent.SelectionChanged += DataGridViewContent_SelectionChanged;
            // 
            // NameColumn
            // 
            NameColumn.DataPropertyName = "Name";
            NameColumn.FillWeight = 25F;
            NameColumn.HeaderText = "Name";
            NameColumn.Name = "NameColumn";
            NameColumn.ReadOnly = true;
            // 
            // PathColumn
            // 
            PathColumn.DataPropertyName = "ContentPath";
            PathColumn.FillWeight = 75F;
            PathColumn.HeaderText = "Directory";
            PathColumn.Name = "PathColumn";
            PathColumn.ReadOnly = true;
            // 
            // bindingSourceContent
            // 
            bindingSourceContent.AllowNew = true;
            bindingSourceContent.DataSource = typeof(FolderModel);
            bindingSourceContent.AddingNew += BindingSourceContent_AddingNew;
            // 
            // tabPageUpdater
            // 
            tabPageUpdater.Controls.Add(buttonUpdaterExecute);
            tabPageUpdater.Controls.Add(groupBoxUpdateFrequency);
            tabPageUpdater.Controls.Add(labelCurrentVersion);
            tabPageUpdater.Controls.Add(labelCurrentVersionDesc);
            tabPageUpdater.Controls.Add(groupBoxUpdates);
            tabPageUpdater.Controls.Add(labelAvailableVersionDesc);
            tabPageUpdater.Controls.Add(labelAvailableVersion);
            tabPageUpdater.Location = new System.Drawing.Point(4, 24);
            tabPageUpdater.Name = "tabPageUpdater";
            tabPageUpdater.Padding = new System.Windows.Forms.Padding(3);
            tabPageUpdater.Size = new System.Drawing.Size(642, 394);
            tabPageUpdater.TabIndex = 8;
            tabPageUpdater.Text = "Updater";
            tabPageUpdater.UseVisualStyleBackColor = true;
            // 
            // buttonUpdaterExecute
            // 
            buttonUpdaterExecute.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonUpdaterExecute.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            buttonUpdaterExecute.Location = new System.Drawing.Point(534, 318);
            buttonUpdaterExecute.Name = "buttonUpdaterExecute";
            buttonUpdaterExecute.Size = new System.Drawing.Size(80, 22);
            buttonUpdaterExecute.TabIndex = 41;
            buttonUpdaterExecute.Text = "Update";
            buttonUpdaterExecute.UseVisualStyleBackColor = true;
            buttonUpdaterExecute.Visible = false;
            buttonUpdaterExecute.Click += ButtonUpdaterExecute_Click;
            // 
            // groupBoxUpdateFrequency
            // 
            groupBoxUpdateFrequency.Controls.Add(labelUpdaterFrequency);
            groupBoxUpdateFrequency.Controls.Add(trackBarUpdaterFrequency);
            groupBoxUpdateFrequency.Location = new System.Drawing.Point(6, 230);
            groupBoxUpdateFrequency.Margin = new System.Windows.Forms.Padding(2);
            groupBoxUpdateFrequency.Name = "groupBoxUpdateFrequency";
            groupBoxUpdateFrequency.Padding = new System.Windows.Forms.Padding(2);
            groupBoxUpdateFrequency.Size = new System.Drawing.Size(633, 80);
            groupBoxUpdateFrequency.TabIndex = 40;
            groupBoxUpdateFrequency.TabStop = false;
            groupBoxUpdateFrequency.Text = "Update Check Frequency";
            // 
            // labelUpdaterFrequency
            // 
            labelUpdaterFrequency.Location = new System.Drawing.Point(5, 54);
            labelUpdaterFrequency.Margin = new System.Windows.Forms.Padding(3);
            labelUpdaterFrequency.Name = "labelUpdaterFrequency";
            labelUpdaterFrequency.Size = new System.Drawing.Size(622, 20);
            labelUpdaterFrequency.TabIndex = 31;
            labelUpdaterFrequency.Text = "Always";
            labelUpdaterFrequency.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // trackBarUpdaterFrequency
            // 
            trackBarUpdaterFrequency.BackColor = System.Drawing.SystemColors.Window;
            trackBarUpdaterFrequency.LargeChange = 1;
            trackBarUpdaterFrequency.Location = new System.Drawing.Point(5, 24);
            trackBarUpdaterFrequency.Maximum = 4;
            trackBarUpdaterFrequency.Minimum = -1;
            trackBarUpdaterFrequency.Name = "trackBarUpdaterFrequency";
            trackBarUpdaterFrequency.Size = new System.Drawing.Size(622, 45);
            trackBarUpdaterFrequency.TabIndex = 30;
            trackBarUpdaterFrequency.Scroll += TrackBarUpdaterFrequency_Scroll;
            // 
            // labelCurrentVersion
            // 
            labelCurrentVersion.AutoSize = true;
            labelCurrentVersion.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            labelCurrentVersion.Location = new System.Drawing.Point(305, 343);
            labelCurrentVersion.Name = "labelCurrentVersion";
            labelCurrentVersion.Size = new System.Drawing.Size(25, 15);
            labelCurrentVersion.TabIndex = 39;
            labelCurrentVersion.Text = "n/a";
            // 
            // labelCurrentVersionDesc
            // 
            labelCurrentVersionDesc.AutoSize = true;
            labelCurrentVersionDesc.Location = new System.Drawing.Point(6, 344);
            labelCurrentVersionDesc.Margin = new System.Windows.Forms.Padding(3);
            labelCurrentVersionDesc.Name = "labelCurrentVersionDesc";
            labelCurrentVersionDesc.Size = new System.Drawing.Size(91, 15);
            labelCurrentVersionDesc.TabIndex = 38;
            labelCurrentVersionDesc.Text = "Current Version:";
            // 
            // groupBoxUpdates
            // 
            groupBoxUpdates.Controls.Add(rbDeveloperPrereleases);
            groupBoxUpdates.Controls.Add(rbPublicPrereleases);
            groupBoxUpdates.Controls.Add(rbPublicReleases);
            groupBoxUpdates.Controls.Add(label30);
            groupBoxUpdates.Controls.Add(labelPublicReleaseDesc);
            groupBoxUpdates.Controls.Add(label32);
            groupBoxUpdates.Location = new System.Drawing.Point(6, 6);
            groupBoxUpdates.Margin = new System.Windows.Forms.Padding(2);
            groupBoxUpdates.Name = "groupBoxUpdates";
            groupBoxUpdates.Padding = new System.Windows.Forms.Padding(2);
            groupBoxUpdates.Size = new System.Drawing.Size(633, 214);
            groupBoxUpdates.TabIndex = 37;
            groupBoxUpdates.TabStop = false;
            groupBoxUpdates.Text = "Update mode";
            // 
            // rbDeveloperPrereleases
            // 
            rbDeveloperPrereleases.AutoSize = true;
            rbDeveloperPrereleases.Location = new System.Drawing.Point(5, 150);
            rbDeveloperPrereleases.Margin = new System.Windows.Forms.Padding(2);
            rbDeveloperPrereleases.Name = "rbDeveloperPrereleases";
            rbDeveloperPrereleases.Size = new System.Drawing.Size(146, 19);
            rbDeveloperPrereleases.TabIndex = 2;
            rbDeveloperPrereleases.Text = "Developer Test releases";
            rbDeveloperPrereleases.UseVisualStyleBackColor = true;
            // 
            // rbPublicPrereleases
            // 
            rbPublicPrereleases.AutoSize = true;
            rbPublicPrereleases.Location = new System.Drawing.Point(5, 86);
            rbPublicPrereleases.Margin = new System.Windows.Forms.Padding(2);
            rbPublicPrereleases.Name = "rbPublicPrereleases";
            rbPublicPrereleases.Size = new System.Drawing.Size(127, 19);
            rbPublicPrereleases.TabIndex = 1;
            rbPublicPrereleases.Text = "Public pre-Releases";
            rbPublicPrereleases.UseVisualStyleBackColor = true;
            // 
            // rbPublicReleases
            // 
            rbPublicReleases.AutoSize = true;
            rbPublicReleases.Checked = true;
            rbPublicReleases.Location = new System.Drawing.Point(5, 22);
            rbPublicReleases.Margin = new System.Windows.Forms.Padding(2);
            rbPublicReleases.Name = "rbPublicReleases";
            rbPublicReleases.Size = new System.Drawing.Size(105, 19);
            rbPublicReleases.TabIndex = 0;
            rbPublicReleases.TabStop = true;
            rbPublicReleases.Text = "Public Releases";
            rbPublicReleases.UseVisualStyleBackColor = true;
            // 
            // label30
            // 
            label30.Location = new System.Drawing.Point(21, 107);
            label30.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            label30.Name = "label30";
            label30.Size = new System.Drawing.Size(612, 40);
            label30.TabIndex = 4;
            label30.Text = "Early access versions for testing in a wider community.";
            // 
            // labelPublicReleaseDesc
            // 
            labelPublicReleaseDesc.Location = new System.Drawing.Point(21, 43);
            labelPublicReleaseDesc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            labelPublicReleaseDesc.Name = "labelPublicReleaseDesc";
            labelPublicReleaseDesc.Size = new System.Drawing.Size(612, 40);
            labelPublicReleaseDesc.TabIndex = 3;
            labelPublicReleaseDesc.Text = "Recommended for general use. Stabilized versions and final feature implementations.";
            // 
            // label32
            // 
            label32.Location = new System.Drawing.Point(21, 171);
            label32.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            label32.Name = "label32";
            label32.Size = new System.Drawing.Size(612, 40);
            label32.TabIndex = 5;
            label32.Text = "Continously integrated updates from development process. These versions may contain serious defect, and not all features shows here may be merged into public releases.\r\n";
            // 
            // labelAvailableVersionDesc
            // 
            labelAvailableVersionDesc.AutoSize = true;
            labelAvailableVersionDesc.Location = new System.Drawing.Point(6, 321);
            labelAvailableVersionDesc.Margin = new System.Windows.Forms.Padding(3);
            labelAvailableVersionDesc.Name = "labelAvailableVersionDesc";
            labelAvailableVersionDesc.Size = new System.Drawing.Size(168, 15);
            labelAvailableVersionDesc.TabIndex = 33;
            labelAvailableVersionDesc.Text = "Available Update in this mode:";
            // 
            // labelAvailableVersion
            // 
            labelAvailableVersion.AutoSize = true;
            labelAvailableVersion.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            labelAvailableVersion.Location = new System.Drawing.Point(305, 321);
            labelAvailableVersion.Name = "labelAvailableVersion";
            labelAvailableVersion.Size = new System.Drawing.Size(25, 15);
            labelAvailableVersion.TabIndex = 33;
            labelAvailableVersion.Text = "n/a";
            // 
            // tabPageExperimental
            // 
            tabPageExperimental.Controls.Add(label27);
            tabPageExperimental.Controls.Add(numericActWeatherRandomizationLevel);
            tabPageExperimental.Controls.Add(label26);
            tabPageExperimental.Controls.Add(label13);
            tabPageExperimental.Controls.Add(label12);
            tabPageExperimental.Controls.Add(numericActRandomizationLevel);
            tabPageExperimental.Controls.Add(checkCorrectQuestionableBrakingParams);
            tabPageExperimental.Controls.Add(label25);
            tabPageExperimental.Controls.Add(precipitationBoxLength);
            tabPageExperimental.Controls.Add(label24);
            tabPageExperimental.Controls.Add(precipitationBoxWidth);
            tabPageExperimental.Controls.Add(label23);
            tabPageExperimental.Controls.Add(precipitationBoxHeight);
            tabPageExperimental.Controls.Add(label16);
            tabPageExperimental.Controls.Add(label9);
            tabPageExperimental.Controls.Add(label21);
            tabPageExperimental.Controls.Add(AdhesionFactorChangeValueLabel);
            tabPageExperimental.Controls.Add(AdhesionFactorValueLabel);
            tabPageExperimental.Controls.Add(labelLODBias);
            tabPageExperimental.Controls.Add(checkShapeWarnings);
            tabPageExperimental.Controls.Add(trackLODBias);
            tabPageExperimental.Controls.Add(AdhesionLevelValue);
            tabPageExperimental.Controls.Add(AdhesionLevelLabel);
            tabPageExperimental.Controls.Add(trackAdhesionFactorChange);
            tabPageExperimental.Controls.Add(trackAdhesionFactor);
            tabPageExperimental.Controls.Add(checkSignalLightGlow);
            tabPageExperimental.Controls.Add(checkUseMSTSEnv);
            tabPageExperimental.Controls.Add(labelPerformanceTunerTarget);
            tabPageExperimental.Controls.Add(numericPerformanceTunerTarget);
            tabPageExperimental.Controls.Add(checkPerformanceTuner);
            tabPageExperimental.Controls.Add(label8);
            tabPageExperimental.Controls.Add(numericSuperElevationGauge);
            tabPageExperimental.Controls.Add(label7);
            tabPageExperimental.Controls.Add(numericSuperElevationMinLen);
            tabPageExperimental.Controls.Add(label6);
            tabPageExperimental.Controls.Add(numericUseSuperElevation);
            tabPageExperimental.Controls.Add(ElevationText);
            tabPageExperimental.Controls.Add(label5);
            tabPageExperimental.Location = new System.Drawing.Point(4, 24);
            tabPageExperimental.Name = "tabPageExperimental";
            tabPageExperimental.Padding = new System.Windows.Forms.Padding(3);
            tabPageExperimental.Size = new System.Drawing.Size(642, 394);
            tabPageExperimental.TabIndex = 3;
            tabPageExperimental.Text = "Experimental";
            tabPageExperimental.UseVisualStyleBackColor = true;
            // 
            // label27
            // 
            label27.AutoSize = true;
            label27.Location = new System.Drawing.Point(561, 117);
            label27.Margin = new System.Windows.Forms.Padding(3);
            label27.Name = "label27";
            label27.Size = new System.Drawing.Size(34, 15);
            label27.TabIndex = 51;
            label27.Text = "Level";
            // 
            // numericActWeatherRandomizationLevel
            // 
            numericActWeatherRandomizationLevel.Location = new System.Drawing.Point(497, 115);
            numericActWeatherRandomizationLevel.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericActWeatherRandomizationLevel.Maximum = new decimal(new int[] { 3, 0, 0, 0 });
            numericActWeatherRandomizationLevel.Name = "numericActWeatherRandomizationLevel";
            numericActWeatherRandomizationLevel.Size = new System.Drawing.Size(58, 23);
            numericActWeatherRandomizationLevel.TabIndex = 50;
            toolTip1.SetToolTip(numericActWeatherRandomizationLevel, "0: no randomization, 1: moderate, 2: significant; 3: high (may be unrealistic)");
            // 
            // label26
            // 
            label26.AutoSize = true;
            label26.Location = new System.Drawing.Point(467, 94);
            label26.Margin = new System.Windows.Forms.Padding(3);
            label26.Name = "label26";
            label26.Size = new System.Drawing.Size(172, 15);
            label26.TabIndex = 49;
            label26.Text = "Activity weather randomization";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new System.Drawing.Point(321, 94);
            label13.Margin = new System.Windows.Forms.Padding(3);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(127, 15);
            label13.TabIndex = 48;
            label13.Text = "Activity randomization";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new System.Drawing.Point(410, 117);
            label12.Margin = new System.Windows.Forms.Padding(3);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(34, 15);
            label12.TabIndex = 47;
            label12.Text = "Level";
            // 
            // numericActRandomizationLevel
            // 
            numericActRandomizationLevel.Location = new System.Drawing.Point(346, 115);
            numericActRandomizationLevel.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericActRandomizationLevel.Maximum = new decimal(new int[] { 3, 0, 0, 0 });
            numericActRandomizationLevel.Name = "numericActRandomizationLevel";
            numericActRandomizationLevel.Size = new System.Drawing.Size(58, 23);
            numericActRandomizationLevel.TabIndex = 46;
            toolTip1.SetToolTip(numericActRandomizationLevel, "0: no randomization, 1: moderate, 2: significant; 3: high (may be unrealistic)");
            // 
            // checkCorrectQuestionableBrakingParams
            // 
            checkCorrectQuestionableBrakingParams.AutoSize = true;
            checkCorrectQuestionableBrakingParams.Location = new System.Drawing.Point(324, 73);
            checkCorrectQuestionableBrakingParams.Name = "checkCorrectQuestionableBrakingParams";
            checkCorrectQuestionableBrakingParams.Size = new System.Drawing.Size(241, 19);
            checkCorrectQuestionableBrakingParams.TabIndex = 43;
            checkCorrectQuestionableBrakingParams.Text = "Correct questionable braking parameters";
            checkCorrectQuestionableBrakingParams.UseVisualStyleBackColor = true;
            // 
            // label25
            // 
            label25.AutoSize = true;
            label25.Location = new System.Drawing.Point(74, 358);
            label25.Margin = new System.Windows.Forms.Padding(3);
            label25.Name = "label25";
            label25.Size = new System.Drawing.Size(155, 15);
            label25.TabIndex = 42;
            label25.Text = "Precipitation box length (m)";
            // 
            // precipitationBoxLength
            // 
            precipitationBoxLength.Increment = new decimal(new int[] { 25, 0, 0, 0 });
            precipitationBoxLength.Location = new System.Drawing.Point(10, 355);
            precipitationBoxLength.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            precipitationBoxLength.Minimum = new decimal(new int[] { 500, 0, 0, 0 });
            precipitationBoxLength.Name = "precipitationBoxLength";
            precipitationBoxLength.Size = new System.Drawing.Size(58, 23);
            precipitationBoxLength.TabIndex = 41;
            precipitationBoxLength.Value = new decimal(new int[] { 500, 0, 0, 0 });
            // 
            // label24
            // 
            label24.AutoSize = true;
            label24.Location = new System.Drawing.Point(74, 332);
            label24.Margin = new System.Windows.Forms.Padding(3);
            label24.Name = "label24";
            label24.Size = new System.Drawing.Size(151, 15);
            label24.TabIndex = 40;
            label24.Text = "Precipitation box width (m)";
            // 
            // precipitationBoxWidth
            // 
            precipitationBoxWidth.Increment = new decimal(new int[] { 25, 0, 0, 0 });
            precipitationBoxWidth.Location = new System.Drawing.Point(10, 330);
            precipitationBoxWidth.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            precipitationBoxWidth.Minimum = new decimal(new int[] { 500, 0, 0, 0 });
            precipitationBoxWidth.Name = "precipitationBoxWidth";
            precipitationBoxWidth.Size = new System.Drawing.Size(58, 23);
            precipitationBoxWidth.TabIndex = 39;
            precipitationBoxWidth.Value = new decimal(new int[] { 500, 0, 0, 0 });
            // 
            // label23
            // 
            label23.AutoSize = true;
            label23.Location = new System.Drawing.Point(74, 306);
            label23.Margin = new System.Windows.Forms.Padding(3);
            label23.Name = "label23";
            label23.Size = new System.Drawing.Size(155, 15);
            label23.TabIndex = 38;
            label23.Text = "Precipitation box height (m)";
            // 
            // precipitationBoxHeight
            // 
            precipitationBoxHeight.Increment = new decimal(new int[] { 25, 0, 0, 0 });
            precipitationBoxHeight.Location = new System.Drawing.Point(10, 304);
            precipitationBoxHeight.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
            precipitationBoxHeight.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            precipitationBoxHeight.Name = "precipitationBoxHeight";
            precipitationBoxHeight.Size = new System.Drawing.Size(58, 23);
            precipitationBoxHeight.TabIndex = 37;
            precipitationBoxHeight.Value = new decimal(new int[] { 100, 0, 0, 0 });
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Location = new System.Drawing.Point(321, 304);
            label16.Margin = new System.Windows.Forms.Padding(3);
            label16.Name = "label16";
            label16.Size = new System.Drawing.Size(181, 15);
            label16.TabIndex = 30;
            label16.Text = "Adhesion factor random change:";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new System.Drawing.Point(321, 254);
            label9.Margin = new System.Windows.Forms.Padding(3);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(151, 15);
            label9.TabIndex = 26;
            label9.Text = "Adhesion factor correction:";
            // 
            // label21
            // 
            label21.AutoSize = true;
            label21.Location = new System.Drawing.Point(6, 254);
            label21.Margin = new System.Windows.Forms.Padding(3);
            label21.Name = "label21";
            label21.Size = new System.Drawing.Size(107, 15);
            label21.TabIndex = 14;
            label21.Text = "Level of detail bias:";
            // 
            // AdhesionFactorChangeValueLabel
            // 
            AdhesionFactorChangeValueLabel.Location = new System.Drawing.Point(321, 304);
            AdhesionFactorChangeValueLabel.Margin = new System.Windows.Forms.Padding(3);
            AdhesionFactorChangeValueLabel.Name = "AdhesionFactorChangeValueLabel";
            AdhesionFactorChangeValueLabel.Size = new System.Drawing.Size(311, 13);
            AdhesionFactorChangeValueLabel.TabIndex = 31;
            AdhesionFactorChangeValueLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // AdhesionFactorValueLabel
            // 
            AdhesionFactorValueLabel.Location = new System.Drawing.Point(321, 254);
            AdhesionFactorValueLabel.Margin = new System.Windows.Forms.Padding(3);
            AdhesionFactorValueLabel.Name = "AdhesionFactorValueLabel";
            AdhesionFactorValueLabel.Size = new System.Drawing.Size(311, 13);
            AdhesionFactorValueLabel.TabIndex = 27;
            AdhesionFactorValueLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // labelLODBias
            // 
            labelLODBias.Location = new System.Drawing.Point(6, 254);
            labelLODBias.Margin = new System.Windows.Forms.Padding(3);
            labelLODBias.Name = "labelLODBias";
            labelLODBias.Size = new System.Drawing.Size(311, 13);
            labelLODBias.TabIndex = 15;
            labelLODBias.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // checkShapeWarnings
            // 
            checkShapeWarnings.AutoSize = true;
            checkShapeWarnings.Location = new System.Drawing.Point(6, 186);
            checkShapeWarnings.Name = "checkShapeWarnings";
            checkShapeWarnings.Size = new System.Drawing.Size(140, 19);
            checkShapeWarnings.TabIndex = 36;
            checkShapeWarnings.Text = "Show shape warnings";
            checkShapeWarnings.UseVisualStyleBackColor = true;
            // 
            // trackLODBias
            // 
            trackLODBias.AutoSize = false;
            trackLODBias.BackColor = System.Drawing.SystemColors.Window;
            trackLODBias.LargeChange = 10;
            trackLODBias.Location = new System.Drawing.Point(6, 273);
            trackLODBias.Maximum = 100;
            trackLODBias.Minimum = -100;
            trackLODBias.Name = "trackLODBias";
            trackLODBias.Size = new System.Drawing.Size(311, 26);
            trackLODBias.TabIndex = 16;
            trackLODBias.TickFrequency = 10;
            toolTip1.SetToolTip(trackLODBias, "Default is 0%");
            trackLODBias.ValueChanged += TrackLODBias_ValueChanged;
            // 
            // AdhesionLevelValue
            // 
            AdhesionLevelValue.Location = new System.Drawing.Point(381, 354);
            AdhesionLevelValue.Margin = new System.Windows.Forms.Padding(3);
            AdhesionLevelValue.Name = "AdhesionLevelValue";
            AdhesionLevelValue.Size = new System.Drawing.Size(252, 13);
            AdhesionLevelValue.TabIndex = 34;
            // 
            // AdhesionLevelLabel
            // 
            AdhesionLevelLabel.Location = new System.Drawing.Point(321, 354);
            AdhesionLevelLabel.Margin = new System.Windows.Forms.Padding(3);
            AdhesionLevelLabel.Name = "AdhesionLevelLabel";
            AdhesionLevelLabel.Size = new System.Drawing.Size(54, 13);
            AdhesionLevelLabel.TabIndex = 33;
            AdhesionLevelLabel.Text = "Level:";
            // 
            // trackAdhesionFactorChange
            // 
            trackAdhesionFactorChange.AutoSize = false;
            trackAdhesionFactorChange.BackColor = System.Drawing.SystemColors.Window;
            trackAdhesionFactorChange.LargeChange = 10;
            trackAdhesionFactorChange.Location = new System.Drawing.Point(321, 323);
            trackAdhesionFactorChange.Maximum = 100;
            trackAdhesionFactorChange.Name = "trackAdhesionFactorChange";
            trackAdhesionFactorChange.Size = new System.Drawing.Size(311, 26);
            trackAdhesionFactorChange.TabIndex = 32;
            trackAdhesionFactorChange.TickFrequency = 10;
            toolTip1.SetToolTip(trackAdhesionFactorChange, "Default is 10%");
            trackAdhesionFactorChange.Value = 10;
            trackAdhesionFactorChange.ValueChanged += TrackAdhesionFactor_ValueChanged;
            // 
            // trackAdhesionFactor
            // 
            trackAdhesionFactor.AutoSize = false;
            trackAdhesionFactor.BackColor = System.Drawing.SystemColors.Window;
            trackAdhesionFactor.LargeChange = 10;
            trackAdhesionFactor.Location = new System.Drawing.Point(321, 273);
            trackAdhesionFactor.Maximum = 200;
            trackAdhesionFactor.Minimum = 10;
            trackAdhesionFactor.Name = "trackAdhesionFactor";
            trackAdhesionFactor.Size = new System.Drawing.Size(311, 26);
            trackAdhesionFactor.TabIndex = 28;
            trackAdhesionFactor.TickFrequency = 10;
            toolTip1.SetToolTip(trackAdhesionFactor, "Default is 130%");
            trackAdhesionFactor.Value = 130;
            trackAdhesionFactor.ValueChanged += TrackAdhesionFactor_ValueChanged;
            // 
            // checkSignalLightGlow
            // 
            checkSignalLightGlow.AutoSize = true;
            checkSignalLightGlow.Location = new System.Drawing.Point(324, 50);
            checkSignalLightGlow.Name = "checkSignalLightGlow";
            checkSignalLightGlow.Size = new System.Drawing.Size(114, 19);
            checkSignalLightGlow.TabIndex = 18;
            checkSignalLightGlow.Text = "Signal light glow";
            checkSignalLightGlow.UseVisualStyleBackColor = true;
            // 
            // checkUseMSTSEnv
            // 
            checkUseMSTSEnv.AutoSize = true;
            checkUseMSTSEnv.Location = new System.Drawing.Point(324, 209);
            checkUseMSTSEnv.Name = "checkUseMSTSEnv";
            checkUseMSTSEnv.Size = new System.Drawing.Size(132, 19);
            checkUseMSTSEnv.TabIndex = 25;
            checkUseMSTSEnv.Text = "MSTS environments";
            checkUseMSTSEnv.UseVisualStyleBackColor = true;
            // 
            // labelPerformanceTunerTarget
            // 
            labelPerformanceTunerTarget.AutoSize = true;
            labelPerformanceTunerTarget.Location = new System.Drawing.Point(92, 142);
            labelPerformanceTunerTarget.Margin = new System.Windows.Forms.Padding(3);
            labelPerformanceTunerTarget.Name = "labelPerformanceTunerTarget";
            labelPerformanceTunerTarget.Size = new System.Drawing.Size(97, 15);
            labelPerformanceTunerTarget.TabIndex = 10;
            labelPerformanceTunerTarget.Text = "Target frame rate";
            // 
            // numericPerformanceTunerTarget
            // 
            numericPerformanceTunerTarget.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            numericPerformanceTunerTarget.Location = new System.Drawing.Point(28, 140);
            numericPerformanceTunerTarget.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericPerformanceTunerTarget.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
            numericPerformanceTunerTarget.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numericPerformanceTunerTarget.Name = "numericPerformanceTunerTarget";
            numericPerformanceTunerTarget.Size = new System.Drawing.Size(58, 23);
            numericPerformanceTunerTarget.TabIndex = 9;
            toolTip1.SetToolTip(numericPerformanceTunerTarget, "Distance to see mountains");
            numericPerformanceTunerTarget.Value = new decimal(new int[] { 60, 0, 0, 0 });
            // 
            // checkPerformanceTuner
            // 
            checkPerformanceTuner.AutoSize = true;
            checkPerformanceTuner.Location = new System.Drawing.Point(6, 118);
            checkPerformanceTuner.Name = "checkPerformanceTuner";
            checkPerformanceTuner.Size = new System.Drawing.Size(311, 19);
            checkPerformanceTuner.TabIndex = 8;
            checkPerformanceTuner.Text = "Automatically tune settings to keep performance level";
            checkPerformanceTuner.UseVisualStyleBackColor = true;
            checkPerformanceTuner.Click += CheckPerformanceTuner_Click;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new System.Drawing.Point(92, 94);
            label8.Margin = new System.Windows.Forms.Padding(3);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(74, 15);
            label8.TabIndex = 7;
            label8.Text = "Gauge (mm)";
            // 
            // numericSuperElevationGauge
            // 
            numericSuperElevationGauge.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            numericSuperElevationGauge.Location = new System.Drawing.Point(28, 93);
            numericSuperElevationGauge.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericSuperElevationGauge.Maximum = new decimal(new int[] { 1800, 0, 0, 0 });
            numericSuperElevationGauge.Minimum = new decimal(new int[] { 600, 0, 0, 0 });
            numericSuperElevationGauge.Name = "numericSuperElevationGauge";
            numericSuperElevationGauge.Size = new System.Drawing.Size(58, 23);
            numericSuperElevationGauge.TabIndex = 6;
            numericSuperElevationGauge.Value = new decimal(new int[] { 600, 0, 0, 0 });
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new System.Drawing.Point(92, 69);
            label7.Margin = new System.Windows.Forms.Padding(3);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(119, 15);
            label7.TabIndex = 5;
            label7.Text = "Minimum length (m)";
            // 
            // numericSuperElevationMinLen
            // 
            numericSuperElevationMinLen.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            numericSuperElevationMinLen.Location = new System.Drawing.Point(28, 67);
            numericSuperElevationMinLen.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericSuperElevationMinLen.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numericSuperElevationMinLen.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numericSuperElevationMinLen.Name = "numericSuperElevationMinLen";
            numericSuperElevationMinLen.Size = new System.Drawing.Size(58, 23);
            numericSuperElevationMinLen.TabIndex = 4;
            toolTip1.SetToolTip(numericSuperElevationMinLen, "Shortest curve to have elevation");
            numericSuperElevationMinLen.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new System.Drawing.Point(92, 43);
            label6.Margin = new System.Windows.Forms.Padding(3);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(34, 15);
            label6.TabIndex = 3;
            label6.Text = "Level";
            // 
            // numericUseSuperElevation
            // 
            numericUseSuperElevation.Location = new System.Drawing.Point(28, 42);
            numericUseSuperElevation.Margin = new System.Windows.Forms.Padding(25, 3, 3, 3);
            numericUseSuperElevation.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            numericUseSuperElevation.Name = "numericUseSuperElevation";
            numericUseSuperElevation.Size = new System.Drawing.Size(58, 23);
            numericUseSuperElevation.TabIndex = 2;
            toolTip1.SetToolTip(numericUseSuperElevation, "0: no elevation, 1: 9cm max; 10: 18cm max");
            // 
            // ElevationText
            // 
            ElevationText.AutoSize = true;
            ElevationText.Location = new System.Drawing.Point(6, 22);
            ElevationText.Margin = new System.Windows.Forms.Padding(3);
            ElevationText.Name = "ElevationText";
            ElevationText.Size = new System.Drawing.Size(90, 15);
            ElevationText.TabIndex = 1;
            ElevationText.Text = "Super-elevation";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.ForeColor = System.Drawing.SystemColors.Highlight;
            label5.Location = new System.Drawing.Point(6, 6);
            label5.Margin = new System.Windows.Forms.Padding(3);
            label5.MaximumSize = new System.Drawing.Size(630, 0);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(397, 15);
            label5.TabIndex = 0;
            label5.Text = "Experimental features that may slow down the game, use at your own risk.";
            // 
            // OptionsForm
            // 
            AcceptButton = buttonOK;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new System.Drawing.Size(676, 474);
            Controls.Add(tabOptions);
            Controls.Add(buttonCancel);
            Controls.Add(buttonOK);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            ForeColor = System.Drawing.SystemColors.ControlText;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "OptionsForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Options";
            Shown += OptionsForm_Shown;
            ((System.ComponentModel.ISupportInitialize)numericBrakePipeChargingRate).EndInit();
            tabOptions.ResumeLayout(false);
            tabPageGeneral.ResumeLayout(false);
            tabPageGeneral.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pbEnableWebServer).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbEnableTcsScripts).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbOtherUnits).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbPressureUnit).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbLanguage).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbBrakePipeChargingRate).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbGraduatedRelease).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbRetainers).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbControlConfirmations).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbAlerter).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbOverspeedMonitor).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericWebServerPort).EndInit();
            tabPageAudio.ResumeLayout(false);
            tabPageAudio.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pbExternalSoundPassThruPercent).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbSoundDetailLevel).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbSoundVolumePercent).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericExternalSoundPassThruPercent).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericSoundVolumePercent).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericSoundDetailLevel).EndInit();
            tabPageVideo.ResumeLayout(false);
            tabPageVideo.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackbarMultiSampling).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackDayAmbientLight).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericDistantMountainsViewingDistance).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericViewingDistance).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericViewingFOV).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericCab2DStretch).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericWorldObjectDensity).EndInit();
            tabPageSimulation.ResumeLayout(false);
            tabPageSimulation.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericAdhesionMovingAverageFilterSize).EndInit();
            tabPageKeyboard.ResumeLayout(false);
            tabPageRailDriver.ResumeLayout(false);
            panelRDSettings.ResumeLayout(false);
            panelRDOptions.ResumeLayout(false);
            groupBoxReverseRDLevers.ResumeLayout(false);
            groupBoxReverseRDLevers.PerformLayout();
            tabPageDataLogger.ResumeLayout(false);
            tabPageDataLogger.PerformLayout();
            tabPageEvaluate.ResumeLayout(false);
            tabPageEvaluate.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericDataLogTSInterval).EndInit();
            tabPageContent.ResumeLayout(false);
            tabPageContent.PerformLayout();
            groupBoxContent.ResumeLayout(false);
            groupBoxContent.PerformLayout();
            panelContent.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewContent).EndInit();
            ((System.ComponentModel.ISupportInitialize)bindingSourceContent).EndInit();
            tabPageUpdater.ResumeLayout(false);
            tabPageUpdater.PerformLayout();
            groupBoxUpdateFrequency.ResumeLayout(false);
            groupBoxUpdateFrequency.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackBarUpdaterFrequency).EndInit();
            groupBoxUpdates.ResumeLayout(false);
            groupBoxUpdates.PerformLayout();
            tabPageExperimental.ResumeLayout(false);
            tabPageExperimental.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericActWeatherRandomizationLevel).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericActRandomizationLevel).EndInit();
            ((System.ComponentModel.ISupportInitialize)precipitationBoxLength).EndInit();
            ((System.ComponentModel.ISupportInitialize)precipitationBoxWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)precipitationBoxHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackLODBias).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackAdhesionFactorChange).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackAdhesionFactor).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericPerformanceTunerTarget).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericSuperElevationGauge).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericSuperElevationMinLen).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUseSuperElevation).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.NumericUpDown numericBrakePipeChargingRate;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox checkGraduatedRelease;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.CheckBox checkAlerter;
        private System.Windows.Forms.CheckBox checkConfirmations;
        private System.Windows.Forms.TabControl tabOptions;
        private System.Windows.Forms.TabPage tabPageGeneral;
        private System.Windows.Forms.TabPage tabPageKeyboard;
        private System.Windows.Forms.Button buttonDefaultKeys;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button buttonCheckKeys;
        private System.Windows.Forms.Panel panelKeys;
        private System.Windows.Forms.Button buttonExport;
        private System.Windows.Forms.TabPage tabPageSimulation;
        private System.Windows.Forms.CheckBox checkUseAdvancedAdhesion;
        private System.Windows.Forms.CheckBox checkBreakCouplers;
        private System.Windows.Forms.TabPage tabPageExperimental;
        private System.Windows.Forms.NumericUpDown numericUseSuperElevation;
        private System.Windows.Forms.Label ElevationText;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TabPage tabPageAudio;
        private System.Windows.Forms.TabPage tabPageVideo;
        private System.Windows.Forms.NumericUpDown numericSoundVolumePercent;
        private System.Windows.Forms.Label labelSoundVolume;
        private System.Windows.Forms.Label labelSoundDetailLevel;
        private System.Windows.Forms.NumericUpDown numericSoundDetailLevel;
        private System.Windows.Forms.NumericUpDown numericCab2DStretch;
        private System.Windows.Forms.Label labelCab2DStretch;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numericWorldObjectDensity;
        private System.Windows.Forms.ComboBox comboWindowSize;
        private System.Windows.Forms.CheckBox checkWindowGlass;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox checkDynamicShadows;
        private System.Windows.Forms.CheckBox checkWire;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown numericSuperElevationMinLen;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.NumericUpDown numericSuperElevationGauge;
        private System.Windows.Forms.Label labelFOVHelp;
        private System.Windows.Forms.NumericUpDown numericViewingFOV;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.NumericUpDown numericViewingDistance;
        private System.Windows.Forms.TabPage tabPageDataLogger;
        private System.Windows.Forms.ComboBox comboDataLoggerSeparator;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.CheckBox checkDataLogPhysics;
        private System.Windows.Forms.CheckBox checkDataLogPerformance;
        private System.Windows.Forms.CheckBox checkDataLogSteamPerformance;
        private System.Windows.Forms.CheckBox checkVerboseConfigurationMessages;
        private System.Windows.Forms.CheckBox checkDataLogger;
        private System.Windows.Forms.CheckBox checkDataLogMisc;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.ComboBox comboDataLogSpeedUnits;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label labelAdhesionMovingAverageFilterSize;
        private System.Windows.Forms.NumericUpDown numericAdhesionMovingAverageFilterSize;
        private System.Windows.Forms.Label labelPerformanceTunerTarget;
        private System.Windows.Forms.NumericUpDown numericPerformanceTunerTarget;
        private System.Windows.Forms.CheckBox checkPerformanceTuner;
        private System.Windows.Forms.TabPage tabPageEvaluate;
        private System.Windows.Forms.CheckedListBox checkListDataLogTSContents;
        private System.Windows.Forms.Label labelDataLogTSInterval;
        private System.Windows.Forms.CheckBox checkDataLogStationStops;
        private System.Windows.Forms.NumericUpDown numericDataLogTSInterval;
        private System.Windows.Forms.CheckBox checkDataLogTrainSpeed;
        private System.Windows.Forms.CheckBox checkUseMSTSEnv;
        private System.Windows.Forms.Label labelLanguage;
        private System.Windows.Forms.ComboBox comboLanguage;
        private System.Windows.Forms.Label labelDistantMountainsViewingDistance;
        private System.Windows.Forms.NumericUpDown numericDistantMountainsViewingDistance;
        private System.Windows.Forms.CheckBox checkDistantMountains;
        private System.Windows.Forms.CheckBox checkAlerterExternal;
        private System.Windows.Forms.CheckBox checkCurveSpeedDependent;
        private System.Windows.Forms.CheckBox checkBoilerPreheated;
        private System.Windows.Forms.CheckBox checkSimpleControlPhysics;
        private System.Windows.Forms.CheckBox checkVerticalSync;
        private System.Windows.Forms.ComboBox comboPressureUnit;
        private System.Windows.Forms.Label labelPressureUnit;
        private System.Windows.Forms.CheckBox checkSignalLightGlow;
        private System.Windows.Forms.TabPage tabPageUpdater;
        private System.Windows.Forms.Label AdhesionFactorChangeValueLabel;
        private System.Windows.Forms.Label AdhesionFactorValueLabel;
        private System.Windows.Forms.Label AdhesionLevelValue;
        private System.Windows.Forms.Label AdhesionLevelLabel;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TrackBar trackAdhesionFactorChange;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TrackBar trackAdhesionFactor;
        private System.Windows.Forms.CheckBox checkModelInstancing;
        private System.Windows.Forms.TrackBar trackDayAmbientLight;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.CheckBox checkRetainers;
        private System.Windows.Forms.Label labelLODBias;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.TrackBar trackLODBias;
        private System.Windows.Forms.Label labelOtherUnits;
        private System.Windows.Forms.ComboBox comboOtherUnits;
        private System.Windows.Forms.TabPage tabPageContent;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.DataGridView dataGridViewContent;
        private System.Windows.Forms.GroupBox groupBoxContent;
        private System.Windows.Forms.TextBox textBoxContentPath;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label22;
        public System.Windows.Forms.TextBox textBoxContentName;
        private System.Windows.Forms.Button buttonContentDelete;
        private System.Windows.Forms.Button buttonContentBrowse;
        private System.Windows.Forms.Button buttonContentAdd;
        private System.Windows.Forms.Label labelContent;
        private System.Windows.Forms.CheckBox checkShapeWarnings;
        private System.Windows.Forms.Label labelDayAmbientLight;
        private System.Windows.Forms.CheckBox checkEnableTCSScripts;
        private System.Windows.Forms.NumericUpDown precipitationBoxHeight;
        private System.Windows.Forms.NumericUpDown precipitationBoxWidth;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.NumericUpDown precipitationBoxLength;
        private System.Windows.Forms.CheckBox checkCorrectQuestionableBrakingParams;
        private System.Windows.Forms.CheckBox checkSpeedMonitor;
        private System.Windows.Forms.NumericUpDown numericExternalSoundPassThruPercent;
        private System.Windows.Forms.Label labelExternalSound;
        private System.Windows.Forms.CheckBox checkDoubleWire;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox checkDoorsAITrains;
        private System.Windows.Forms.CheckBox checkForcedRedAtStationStops;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.NumericUpDown numericActRandomizationLevel;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.NumericUpDown numericActWeatherRandomizationLevel;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.CheckBox checkShadowAllShapes;
        private System.Windows.Forms.CheckBox checkEnableWebServer;
        private System.Windows.Forms.NumericUpDown numericWebServerPort;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.TabPage tabPageRailDriver;
        private System.Windows.Forms.Panel panelRDSettings;
        private System.Windows.Forms.Button buttonShowRDLegend;
        private System.Windows.Forms.Button buttonStartRDCalibration;
        private System.Windows.Forms.Button buttonRDReset;
        private System.Windows.Forms.Panel panelRDOptions;
        private System.Windows.Forms.Panel panelRDButtons;
        private System.Windows.Forms.GroupBox groupBoxReverseRDLevers;
        private System.Windows.Forms.CheckBox checkReverseReverser;
        private System.Windows.Forms.CheckBox checkReverseIndependentBrake;
        private System.Windows.Forms.CheckBox checkReverseAutoBrake;
        private System.Windows.Forms.CheckBox checkReverseThrottle;
        private System.Windows.Forms.CheckBox checkFullRangeThrottle;
        private System.Windows.Forms.Button buttonCheck;
        private System.Windows.Forms.Button buttonRDSettingsExport;
        private System.Windows.Forms.Label label28;
        private System.Windows.Forms.TrackBar trackbarMultiSampling;
        private System.Windows.Forms.CheckBox checkUseLocationPassingPaths;
        private System.Windows.Forms.Label labelMSAACount;
        private System.Windows.Forms.CheckBox checkDieselEnginesStarted;
        private System.Windows.Forms.CheckBox checkBoxFullScreenNativeResolution;
        private System.Windows.Forms.RadioButton radioButtonWindow;
        private System.Windows.Forms.RadioButton radioButtonFullScreen;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label labelAvailableVersion;
        private System.Windows.Forms.Label labelAvailableVersionDesc;
        private System.Windows.Forms.GroupBox groupBoxUpdates;
        private System.Windows.Forms.RadioButton rbDeveloperPrereleases;
        private System.Windows.Forms.RadioButton rbPublicPrereleases;
        private System.Windows.Forms.RadioButton rbPublicReleases;
        private System.Windows.Forms.Label labelCurrentVersionDesc;
        private System.Windows.Forms.Label labelCurrentVersion;
        private System.Windows.Forms.Label label32;
        private System.Windows.Forms.Label label30;
        private System.Windows.Forms.Label labelPublicReleaseDesc;
        private System.Windows.Forms.GroupBox groupBoxUpdateFrequency;
        private System.Windows.Forms.Label labelUpdaterFrequency;
        private System.Windows.Forms.TrackBar trackBarUpdaterFrequency;
        private System.Windows.Forms.Button buttonUpdaterExecute;
        private System.Windows.Forms.PictureBox pbOverspeedMonitor;
        private System.Windows.Forms.PictureBox pbEnableWebServer;
        private System.Windows.Forms.PictureBox pbEnableTcsScripts;
        private System.Windows.Forms.PictureBox pbOtherUnits;
        private System.Windows.Forms.PictureBox pbPressureUnit;
        private System.Windows.Forms.PictureBox pbLanguage;
        private System.Windows.Forms.PictureBox pbBrakePipeChargingRate;
        private System.Windows.Forms.PictureBox pbGraduatedRelease;
        private System.Windows.Forms.PictureBox pbRetainers;
        private System.Windows.Forms.PictureBox pbControlConfirmations;
        private System.Windows.Forms.PictureBox pbAlerter;
        private System.Windows.Forms.CheckBox checkElectricPowerConnected;
        private System.Windows.Forms.Label label40;
        private System.Windows.Forms.CheckBox checkLODViewingExtension;
        private System.Windows.Forms.PictureBox pbSoundVolumePercent;
        private System.Windows.Forms.PictureBox pbExternalSoundPassThruPercent;
        private System.Windows.Forms.PictureBox pbSoundDetailLevel;
        private System.Windows.Forms.BindingSource bindingSourceContent;
        private System.Windows.Forms.DataGridViewTextBoxColumn NameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn PathColumn;
    }
}
