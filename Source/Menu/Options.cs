// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using GetText;
using GetText.WindowsForms;

using Orts.Common;
using Orts.Common.Info;
using Orts.Formats.Msts;
using Orts.Settings;
using Orts.Updater;

namespace Orts.Menu
{
    public partial class OptionsForm : Form
    {
        private readonly UserSettings settings;
        private readonly UpdateManager updateManager;

        private readonly Catalog catalog;
        private readonly Dictionary<Control, HelpIconHover> helpIconMap = new Dictionary<Control, HelpIconHover>();

        private const string baseUrl = "https://open-rails.readthedocs.io/en/latest";

        public OptionsForm(UserSettings settings, UpdateManager updateManager, bool initialContentSetup)
        {
            InitializeComponent();
            catalog = CatalogManager.Catalog;
            Localizer.Localize(this, catalog);

            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));

            InitializeHelpIcons();

            // Collect all the available language codes by searching for
            // localisation files, but always include English (base language).
            List<string> languageCodes = new List<string> { "en" };
            if (Directory.Exists(RuntimeInfo.LocalesFolder))
                foreach (string path in Directory.EnumerateDirectories(RuntimeInfo.LocalesFolder))
                    if (Directory.EnumerateFiles(path, "*.mo").Any())
                    {
                        try
                        {
                            string languageCode = Path.GetFileName(path);
                            CultureInfo.GetCultureInfo(languageCode);
                            languageCodes.Add(languageCode);
                        }
                        catch (CultureNotFoundException) { }
                    }
            // Turn the list of codes in to a list of code + name pairs for
            // displaying in the dropdown list.
            languageCodes.Add(string.Empty);
            languageCodes.Sort();
            comboLanguage.DataSourceFromList(languageCodes, (language) => string.IsNullOrEmpty(language) ? "System" : CultureInfo.GetCultureInfo(language).NativeName);
            comboLanguage.SelectedValue = this.settings.Language;
            if (comboLanguage.SelectedValue == null)
                comboLanguage.SelectedIndex = 0;

            comboOtherUnits.DataSourceFromEnum<MeasurementUnit>();
            comboPressureUnit.DataSourceFromEnum<PressureUnit>();

            AdhesionLevelValue.Font = new Font(Font, FontStyle.Bold);

            // Fix up the TrackBars on TabPanels to match the current theme.
            if (!Application.RenderWithVisualStyles)
            {
                trackAdhesionFactor.BackColor = BackColor;
                trackAdhesionFactorChange.BackColor = BackColor;
                trackDayAmbientLight.BackColor = BackColor;
                trackLODBias.BackColor = BackColor;
            }

            // General tab
            checkAlerter.Checked = this.settings.Alerter;
            checkAlerterExternal.Enabled = this.settings.Alerter;
            checkAlerterExternal.Checked = this.settings.Alerter && !this.settings.AlerterDisableExternal;
            checkSpeedMonitor.Checked = this.settings.SpeedControl;
            checkConfirmations.Checked = !this.settings.SuppressConfirmations;// Inverted as "Show confirmations" is better UI than "Suppress confirmations"
            checkRetainers.Checked = this.settings.RetainersOnAllCars;
            checkGraduatedRelease.Checked = this.settings.GraduatedRelease;
            numericBrakePipeChargingRate.Value = this.settings.BrakePipeChargingRate;
            comboLanguage.Text = this.settings.Language;
            comboPressureUnit.SelectedValue = this.settings.PressureUnit;
            comboOtherUnits.SelectedValue = settings.MeasurementUnit;
            checkEnableTCSScripts.Checked = !this.settings.DisableTCSScripts;// Inverted as "Enable scripts" is better UI than "Disable scripts"
            checkEnableWebServer.Checked = this.settings.WebServer;
            numericWebServerPort.Value = this.settings.WebServerPort;

            // Audio tab
            numericSoundVolumePercent.Value = this.settings.SoundVolumePercent;
            numericSoundDetailLevel.Value = this.settings.SoundDetailLevel;
            numericExternalSoundPassThruPercent.Value = this.settings.ExternalSoundPassThruPercent;

            // Video tab
            checkDynamicShadows.Checked = this.settings.DynamicShadows;
            checkShadowAllShapes.Checked = this.settings.ShadowAllShapes;
            checkWindowGlass.Checked = this.settings.WindowGlass;
            checkModelInstancing.Checked = this.settings.ModelInstancing;
            checkWire.Checked = this.settings.Wire;
            checkVerticalSync.Checked = this.settings.VerticalSync;
            trackbarMultiSampling.Value = (int)Math.Log(this.settings.MultisamplingCount, 2);
            TrackbarMultiSampling_Scroll(this, null);
            numericCab2DStretch.Value = this.settings.Cab2DStretch;
            numericViewingDistance.Value = this.settings.ViewingDistance;
            checkDistantMountains.Checked = this.settings.DistantMountains;
            labelDistantMountainsViewingDistance.Enabled = checkDistantMountains.Checked;
            numericDistantMountainsViewingDistance.Enabled = checkDistantMountains.Checked;
            numericDistantMountainsViewingDistance.Value = this.settings.DistantMountainsViewingDistance / 1000;
            checkLODViewingExtension.Checked = this.settings.LODViewingExtension;
            numericViewingFOV.Value = this.settings.ViewingFOV;
            numericWorldObjectDensity.Value = this.settings.WorldObjectDensity;
            comboWindowSize.Text = $"{this.settings.WindowSettings[WindowSetting.Size][0]}x{this.settings.WindowSettings[WindowSetting.Size][1]}";
            trackDayAmbientLight.Value = this.settings.DayAmbientLight;
            TrackDayAmbientLight_ValueChanged(null, null);
            checkDoubleWire.Checked = this.settings.DoubleWire;
            checkBoxFullScreenNativeResolution.Checked = this.settings.NativeFullscreenResolution;
            radioButtonFullScreen.Checked = this.settings.FullScreen;
            radioButtonWindow.Checked = !radioButtonFullScreen.Checked;

            // Simulation tab
            checkUseAdvancedAdhesion.Checked = this.settings.UseAdvancedAdhesion;
            labelAdhesionMovingAverageFilterSize.Enabled = checkUseAdvancedAdhesion.Checked;
            numericAdhesionMovingAverageFilterSize.Enabled = checkUseAdvancedAdhesion.Checked;
            numericAdhesionMovingAverageFilterSize.Value = this.settings.AdhesionMovingAverageFilterSize;
            checkBreakCouplers.Checked = this.settings.BreakCouplers;
            checkCurveSpeedDependent.Checked = this.settings.CurveSpeedDependent;
            checkBoilerPreheated.Checked = this.settings.HotStart;
            checkSimpleControlPhysics.Checked = this.settings.SimpleControlPhysics;
            checkForcedRedAtStationStops.Checked = !this.settings.NoForcedRedAtStationStops;
            checkDoorsAITrains.Checked = this.settings.OpenDoorsInAITrains;
            checkDieselEnginesStarted.Checked = this.settings.DieselEngineStart;

            //// Keyboard tab
            //InitializeKeyboardSettings();

            ////RailDriver tab
            //InitializeRailDriverSettings());

            // DataLogger tab
            comboDataLoggerSeparator.DataSourceFromEnum<SeparatorChar>();
            comboDataLoggerSeparator.SelectedValue = settings.DataLoggerSeparator;

            comboDataLogSpeedUnits.DataSourceFromEnum<SpeedUnit>();
            comboDataLogSpeedUnits.SelectedValue = settings.DataLogSpeedUnits;

            checkDataLogger.Checked = this.settings.DataLogger;
            checkDataLogPerformance.Checked = this.settings.DataLogPerformance;
            checkDataLogPhysics.Checked = this.settings.DataLogPhysics;
            checkDataLogMisc.Checked = this.settings.DataLogMisc;
            checkDataLogSteamPerformance.Checked = this.settings.DataLogSteamPerformance;
            checkVerboseConfigurationMessages.Checked = this.settings.VerboseConfigurationMessages;

            // Evaluation tab
            checkDataLogTrainSpeed.Checked = this.settings.EvaluationTrainSpeed;
            labelDataLogTSInterval.Enabled = checkDataLogTrainSpeed.Checked;
            numericDataLogTSInterval.Enabled = checkDataLogTrainSpeed.Checked;
            checkListDataLogTSContents.Enabled = checkDataLogTrainSpeed.Checked;
            numericDataLogTSInterval.Value = this.settings.EvaluationInterval;

            checkListDataLogTSContents.Items.AddRange(EnumExtension.GetValues<EvaluationLogContents>().
                Where(content => content != EvaluationLogContents.None).
                Select(content => content.GetLocalizedDescription()).ToArray());

            for (int i = 0; i < checkListDataLogTSContents.Items.Count; i++)
            {
                checkListDataLogTSContents.SetItemChecked(i, settings.EvaluationContent.HasFlag((EvaluationLogContents)(1 << i)));
            }
            checkDataLogStationStops.Checked = this.settings.EvaluationStationStops;

            // Content tab
            bindingSourceContent.DataSource = (from folder in this.settings.FolderSettings.Folders
                                               orderby folder.Key
                                               select new ContentFolder() { Name = folder.Key, Path = folder.Value }).ToList();
            if (initialContentSetup)
            {
                tabOptions.SelectedTab = tabPageContent;
                buttonContentBrowse.Enabled = false; // Initial state because browsing a null path leads to an exception
                bindingSourceContent.Add(new ContentFolder() { Name = "Train Simulator", Path = FolderStructure.MstsFolder });
            }

            // Updater tab
            trackBarUpdaterFrequency.Value = this.settings.UpdateCheckFrequency;
            labelUpdaterFrequency.Text = ((UpdateCheckFrequency)trackBarUpdaterFrequency.Value).GetLocalizedDescription();
            labelCurrentVersion.Text = VersionInfo.Version;
            if (updateManager.UpdaterNeedsElevation)
            {
                using (Icon icon = new Icon(SystemIcons.Shield, SystemInformation.SmallIconSize))
                    buttonUpdaterExecute.Image = icon.ToBitmap();
            }

            PresetUpdateSelections();

            // Experimental tab
            numericUseSuperElevation.Value = this.settings.UseSuperElevation;
            numericSuperElevationMinLen.Value = this.settings.SuperElevationMinLen;
            numericSuperElevationGauge.Value = this.settings.SuperElevationGauge;
            checkPerformanceTuner.Checked = this.settings.PerformanceTuner;
            labelPerformanceTunerTarget.Enabled = checkPerformanceTuner.Checked;
            numericPerformanceTunerTarget.Enabled = checkPerformanceTuner.Checked;
            numericPerformanceTunerTarget.Value = this.settings.PerformanceTunerTarget;
            trackLODBias.Value = this.settings.LODBias;
            TrackLODBias_ValueChanged(null, null);
            checkSignalLightGlow.Checked = this.settings.SignalLightGlow;
            checkUseLocationPassingPaths.Checked = this.settings.UseLocationPassingPaths;
            checkUseMSTSEnv.Checked = this.settings.UseMSTSEnv;
            trackAdhesionFactor.Value = this.settings.AdhesionFactor;
            trackAdhesionFactorChange.Value = this.settings.AdhesionFactorChange;
            TrackAdhesionFactor_ValueChanged(null, null);
            checkShapeWarnings.Checked = !this.settings.SuppressShapeWarnings;   // Inverted as "Show warnings" is better UI than "Suppress warnings"
            precipitationBoxHeight.Value = this.settings.PrecipitationBoxHeight;
            precipitationBoxWidth.Value = this.settings.PrecipitationBoxWidth;
            precipitationBoxLength.Value = this.settings.PrecipitationBoxLength;
            checkCorrectQuestionableBrakingParams.Checked = this.settings.CorrectQuestionableBrakingParams;
            numericActRandomizationLevel.Value = this.settings.ActRandomizationLevel;
            numericActWeatherRandomizationLevel.Value = this.settings.ActWeatherRandomizationLevel;
        }

        private void OptionsForm_Shown(object sender, EventArgs e)
        {
            InitializeKeyboardSettings();
            InitializeRailDriverSettings();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                railDriverLegend?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ButtonOK_Click(object sender, EventArgs e)
        {
            string result = settings.Input.CheckForErrors();
            if (!string.IsNullOrEmpty(result) && DialogResult.Yes != MessageBox.Show(catalog.GetString("Continue with conflicting key assignments?\n\n") + result, RuntimeInfo.ProductName, MessageBoxButtons.YesNo))
                return;

            result = CheckButtonAssignments();
            if (!string.IsNullOrEmpty(result) && DialogResult.Yes != MessageBox.Show(catalog.GetString("Continue with conflicting button assignments?\n\n") + result, RuntimeInfo.ProductName, MessageBoxButtons.YesNo))
                return;

            DialogResult = DialogResult.OK;
            if (settings.Language != comboLanguage.SelectedValue.ToString())
                DialogResult = DialogResult.Retry;

            // General tab
            settings.Alerter = checkAlerter.Checked;
            settings.AlerterDisableExternal = !checkAlerterExternal.Checked;
            settings.SpeedControl = checkSpeedMonitor.Checked;
            settings.SuppressConfirmations = !checkConfirmations.Checked;
            settings.RetainersOnAllCars = checkRetainers.Checked;
            settings.GraduatedRelease = checkGraduatedRelease.Checked;
            settings.BrakePipeChargingRate = (int)numericBrakePipeChargingRate.Value;
            settings.Language = comboLanguage.SelectedValue.ToString();
            settings.PressureUnit = (PressureUnit)comboPressureUnit.SelectedValue;
            settings.MeasurementUnit = (MeasurementUnit)comboOtherUnits.SelectedValue;
            settings.DisableTCSScripts = !checkEnableTCSScripts.Checked; // Inverted as "Enable scripts" is better UI than "Disable scripts"
            settings.WebServer = checkEnableWebServer.Checked;
            settings.WebServerPort = (int)numericWebServerPort.Value;

            // Audio tab
            settings.SoundVolumePercent = (int)numericSoundVolumePercent.Value;
            settings.SoundDetailLevel = (int)numericSoundDetailLevel.Value;
            settings.ExternalSoundPassThruPercent = (int)numericExternalSoundPassThruPercent.Value;

            // Video tab
            settings.DynamicShadows = checkDynamicShadows.Checked;
            settings.ShadowAllShapes = checkShadowAllShapes.Checked;
            settings.WindowGlass = checkWindowGlass.Checked;
            settings.ModelInstancing = checkModelInstancing.Checked;
            settings.Wire = checkWire.Checked;
            settings.VerticalSync = checkVerticalSync.Checked;
            settings.MultisamplingCount = 1 << trackbarMultiSampling.Value;
            settings.Cab2DStretch = (int)numericCab2DStretch.Value;
            settings.ViewingDistance = (int)numericViewingDistance.Value;
            settings.DistantMountains = checkDistantMountains.Checked;
            settings.DistantMountainsViewingDistance = (int)numericDistantMountainsViewingDistance.Value * 1000;
            settings.LODViewingExtension = checkLODViewingExtension.Checked;
            settings.ViewingFOV = (int)numericViewingFOV.Value;
            settings.WorldObjectDensity = (int)numericWorldObjectDensity.Value;
            settings.WindowSettings[WindowSetting.Size] = GetValidWindowSize(comboWindowSize.Text);

            settings.DayAmbientLight = (int)trackDayAmbientLight.Value;
            settings.DoubleWire = checkDoubleWire.Checked;
            settings.NativeFullscreenResolution = checkBoxFullScreenNativeResolution.Checked;
            settings.FullScreen = radioButtonFullScreen.Checked;

            // Simulation tab
            settings.UseAdvancedAdhesion = checkUseAdvancedAdhesion.Checked;
            settings.AdhesionMovingAverageFilterSize = (int)numericAdhesionMovingAverageFilterSize.Value;
            settings.BreakCouplers = checkBreakCouplers.Checked;
            settings.CurveSpeedDependent = checkCurveSpeedDependent.Checked;
            settings.HotStart = checkBoilerPreheated.Checked;
            settings.SimpleControlPhysics = checkSimpleControlPhysics.Checked;
            settings.NoForcedRedAtStationStops = !checkForcedRedAtStationStops.Checked;
            settings.OpenDoorsInAITrains = checkDoorsAITrains.Checked;
            settings.DieselEngineStart = checkDieselEnginesStarted.Checked;

            // Keyboard tab
            // These are edited live.
            //SaveKeyboardSettings();

            // Raildriver Tab
            SaveRailDriverSettings();

            // DataLogger tab
            settings.DataLoggerSeparator = (SeparatorChar)comboDataLoggerSeparator.SelectedValue;
            settings.DataLogSpeedUnits = (SpeedUnit)comboDataLogSpeedUnits.SelectedValue;
            settings.DataLogger = checkDataLogger.Checked;
            settings.DataLogPerformance = checkDataLogPerformance.Checked;
            settings.DataLogPhysics = checkDataLogPhysics.Checked;
            settings.DataLogMisc = checkDataLogMisc.Checked;
            settings.DataLogSteamPerformance = checkDataLogSteamPerformance.Checked;
            settings.VerboseConfigurationMessages = checkVerboseConfigurationMessages.Checked;

            // Evaluation tab
            settings.EvaluationTrainSpeed = checkDataLogTrainSpeed.Checked;
            settings.EvaluationInterval = (int)numericDataLogTSInterval.Value;
            for (int i = 0; i < checkListDataLogTSContents.Items.Count; i++)
            {
                settings.EvaluationContent = checkListDataLogTSContents.GetItemChecked(i) ? settings.EvaluationContent | (EvaluationLogContents)(1 << i) : settings.EvaluationContent & ~(EvaluationLogContents)(1 << i);
            }
            settings.EvaluationStationStops = checkDataLogStationStops.Checked;

            // Content tab
            settings.FolderSettings.Folders.Clear();
            foreach (ContentFolder folder in bindingSourceContent.DataSource as List<ContentFolder>)
                settings.FolderSettings.Folders.Add(folder.Name, folder.Path);

            // Updater tab

            settings.UpdateCheckFrequency = trackBarUpdaterFrequency.Value;

            // Experimental tab
            settings.UseSuperElevation = (int)numericUseSuperElevation.Value;
            settings.SuperElevationMinLen = (int)numericSuperElevationMinLen.Value;
            settings.SuperElevationGauge = (int)numericSuperElevationGauge.Value;
            settings.PerformanceTuner = checkPerformanceTuner.Checked;
            settings.PerformanceTunerTarget = (int)numericPerformanceTunerTarget.Value;
            settings.LODBias = trackLODBias.Value;
            settings.SignalLightGlow = checkSignalLightGlow.Checked;
            settings.UseLocationPassingPaths = checkUseLocationPassingPaths.Checked;
            settings.UseMSTSEnv = checkUseMSTSEnv.Checked;
            settings.AdhesionFactor = (int)trackAdhesionFactor.Value;
            settings.AdhesionFactorChange = (int)trackAdhesionFactorChange.Value;
            settings.SuppressShapeWarnings = !checkShapeWarnings.Checked;
            settings.PrecipitationBoxHeight = (int)precipitationBoxHeight.Value;
            settings.PrecipitationBoxWidth = (int)precipitationBoxWidth.Value;
            settings.PrecipitationBoxLength = (int)precipitationBoxLength.Value;
            settings.CorrectQuestionableBrakingParams = checkCorrectQuestionableBrakingParams.Checked;
            settings.ActRandomizationLevel = (int)numericActRandomizationLevel.Value;
            settings.ActWeatherRandomizationLevel = (int)numericActWeatherRandomizationLevel.Value;

            settings.Save();
        }

        /// <summary>
        /// Returns user's [width]x[height] if expression is valid and values are sane, else returns previous value of setting.
        /// </summary>
        private int[] GetValidWindowSize(string text)
        {
            Match match = Regex.Match(text, @"^\s*([1-9]\d{2,3})\s*[Xx]\s*([1-9]\d{2,3})\s*$");//capturing 2 groups of 3-4digits, separated by X or x, ignoring whitespace in beginning/end and in between
            if (match.Success)
            {
                return new int[2] { int.Parse(match.Groups[1].ValueSpan), int.Parse(match.Groups[2].ValueSpan) };
            }
            return settings.WindowSettings[WindowSetting.Size]; // i.e. no change or message. Just ignore non-numeric entries
        }

        private void NumericUpDownFOV_ValueChanged(object sender, EventArgs e)
        {
            labelFOVHelp.Text = catalog.GetString($"{numericViewingFOV.Value:F0}° vertical FOV is the same as:\n{numericViewingFOV.Value * 4 / 3:F0}° horizontal FOV on 4:3\n{numericViewingFOV.Value * 16 / 9:F0}° horizontal FOV on 16:9");
        }

        private void TrackBarDayAmbientLight_Scroll(object sender, EventArgs e)
        {
            toolTip1.SetToolTip(trackDayAmbientLight, $"{trackDayAmbientLight.Value * 5}%");
        }

        private void TrackAdhesionFactor_ValueChanged(object sender, EventArgs e)
        {
            SetAdhesionLevelValue();
            AdhesionFactorValueLabel.Text = $"{trackAdhesionFactor.Value}%";
            AdhesionFactorChangeValueLabel.Text = $"{trackAdhesionFactorChange.Value}%";
        }

        private void SetAdhesionLevelValue()
        {
            int level = trackAdhesionFactor.Value - trackAdhesionFactorChange.Value;

            // Allowance to make adhesion proportional to rain/snow/fog
            level -= 40;

            if (level > 159)
                AdhesionLevelValue.Text = catalog.GetString("Very easy");
            else if (level > 139)
                AdhesionLevelValue.Text = catalog.GetString("Easy");
            else if (level > 119)
                AdhesionLevelValue.Text = catalog.GetString("MSTS Compatible");
            else if (level > 89)
                AdhesionLevelValue.Text = catalog.GetString("Normal");
            else if (level > 69)
                AdhesionLevelValue.Text = catalog.GetString("Hard");
            else if (level > 59)
                AdhesionLevelValue.Text = catalog.GetString("Very Hard");
            else
                AdhesionLevelValue.Text = catalog.GetString("Good luck!");
        }

        private void AdhesionPropToWeatherCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetAdhesionLevelValue();
        }

        private void TrackDayAmbientLight_ValueChanged(object sender, EventArgs e)
        {
            labelDayAmbientLight.Text = catalog.GetString("{0}%", trackDayAmbientLight.Value * 5);
        }

        private void TrackbarMultiSampling_Scroll(object sender, EventArgs e)
        {
            labelMSAACount.Text = trackbarMultiSampling.Value == 0 ? catalog.GetString("Disabled") : catalog.GetString($"{1 << trackbarMultiSampling.Value}x");
        }

        private void CheckBoxFullScreenNativeResolution_CheckedChanged(object sender, EventArgs e)
        {
            comboWindowSize.Enabled = !checkBoxFullScreenNativeResolution.Checked;
        }

        private void TrackLODBias_ValueChanged(object sender, EventArgs e)
        {
            if (trackLODBias.Value == -100)
                labelLODBias.Text = catalog.GetString("No detail (-{0}%)", -trackLODBias.Value);
            else if (trackLODBias.Value < 0)
                labelLODBias.Text = catalog.GetString("Less detail (-{0}%)", -trackLODBias.Value);
            else if (trackLODBias.Value == 0)
                labelLODBias.Text = catalog.GetString("Default detail (+{0}%)", trackLODBias.Value);
            else if (trackLODBias.Value < 100)
                labelLODBias.Text = catalog.GetString("More detail (+{0}%)", trackLODBias.Value);
            else
                labelLODBias.Text = catalog.GetString("All detail (+{0}%)", trackLODBias.Value);
        }

        private void DataGridViewContent_SelectionChanged(object sender, EventArgs e)
        {
            ContentFolder current = bindingSourceContent.Current as ContentFolder;
            textBoxContentName.Enabled = buttonContentBrowse.Enabled = current != null;
            if (current == null)
            {
                textBoxContentName.Text = textBoxContentPath.Text = "";
            }
            else
            {
                textBoxContentName.Text = current.Name;
                textBoxContentPath.Text = current.Path;
            }
        }

        private void ButtonContentAdd_Click(object sender, EventArgs e)
        {
            bindingSourceContent.AddNew();
            ButtonContentBrowse_Click(sender, e);
        }

        private void ButtonContentDelete_Click(object sender, EventArgs e)
        {
            DeleteContent();
        }

        private void DeleteContent()
        {
            bindingSourceContent.RemoveCurrent();
            // ResetBindings() is to work around a bug in the binding and/or data grid where by deleting the bottom item doesn't show the selection moving to the new bottom item.
            bindingSourceContent.ResetBindings(false);
        }

        private void ButtonContentBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderBrowser = new FolderBrowserDialog())
            {
                folderBrowser.SelectedPath = textBoxContentPath.Text;
                folderBrowser.Description = catalog.GetString("Select an installation profile (MSTS folder) to add:");
                folderBrowser.ShowNewFolderButton = false;
                if (folderBrowser.ShowDialog(this) == DialogResult.OK)
                {
                    ContentFolder current = bindingSourceContent.Current as ContentFolder;
                    System.Diagnostics.Debug.Assert(current != null, "List should not be empty");
                    textBoxContentPath.Text = current.Path = folderBrowser.SelectedPath;
                    if (string.IsNullOrEmpty(current.Name))
                        // Don't need to set current.Name here as next statement triggers event textBoxContentName_TextChanged()
                        // which does that and also checks for duplicate names 
                        textBoxContentName.Text = Path.GetFileName(textBoxContentPath.Text);
                    bindingSourceContent.ResetCurrentItem();
                }
            }
        }

        /// <summary>
        /// Edits to the input field are copied back to the list of content.
        /// They are also checked for duplicate names which would lead to an exception when saving.
        /// if duplicate, then " copy" is silently appended to the entry in list of content.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBoxContentName_TextChanged(object sender, EventArgs e)
        {
            if (bindingSourceContent.Current is ContentFolder current && current.Name != textBoxContentName.Text)
            {
                if (!Path.GetRelativePath(RuntimeInfo.ProgramRoot, current.Path).StartsWith(".."))
                {
                    // Block added because a succesful Update operation will empty the Open Rails folder and lose any content stored within it.
                    MessageBox.Show(catalog.GetString
                        ($"Cannot use content from any folder inside the Open Rails folder {RuntimeInfo.ProgramRoot}\n\n")
                        , "Invalid content location"
                        , MessageBoxButtons.OK
                        , MessageBoxIcon.Error);
                    DeleteContent();
                    return;
                }
                // Duplicate names lead to an exception, so append " copy" if not unique
                string suffix = "";
                bool isNameUnique = true;
                while (isNameUnique)
                {
                    isNameUnique = false; // to exit after a single pass
                    foreach (object item in bindingSourceContent)
                        if (((ContentFolder)item).Name == textBoxContentName.Text + suffix)
                        {
                            suffix += " copy"; // To ensure uniqueness
                            isNameUnique = true; // to force another pass
                            break;
                        }
                }
                current.Name = textBoxContentName.Text + suffix;
                bindingSourceContent.ResetCurrentItem();
            }
        }

        private void CheckAlerter_CheckedChanged(object sender, EventArgs e)
        {
            //Disable checkAlerterExternal when checkAlerter is not checked
            if (checkAlerter.Checked)
            {
                checkAlerterExternal.Enabled = true;
            }
            else
            {
                checkAlerterExternal.Enabled = false;
                checkAlerterExternal.Checked = false;
            }
        }

        private void CheckDistantMountains_Click(object sender, EventArgs e)
        {
            labelDistantMountainsViewingDistance.Enabled = checkDistantMountains.Checked;
            numericDistantMountainsViewingDistance.Enabled = checkDistantMountains.Checked;
        }

        private void CheckUseAdvancedAdhesion_Click(object sender, EventArgs e)
        {
            labelAdhesionMovingAverageFilterSize.Enabled = checkUseAdvancedAdhesion.Checked;
            numericAdhesionMovingAverageFilterSize.Enabled = checkUseAdvancedAdhesion.Checked;
        }

        private void CheckDataLogTrainSpeed_Click(object sender, EventArgs e)
        {
            checkListDataLogTSContents.Enabled = checkDataLogTrainSpeed.Checked;
            labelDataLogTSInterval.Enabled = checkDataLogTrainSpeed.Checked;
            numericDataLogTSInterval.Enabled = checkDataLogTrainSpeed.Checked;
        }

        private void CheckPerformanceTuner_Click(object sender, EventArgs e)
        {
            numericPerformanceTunerTarget.Enabled = checkPerformanceTuner.Checked;
            labelPerformanceTunerTarget.Enabled = checkPerformanceTuner.Checked;
        }

        private async void TrackBarUpdaterFrequency_Scroll(object sender, EventArgs e)
        {
            labelUpdaterFrequency.Text = ((UpdateCheckFrequency)trackBarUpdaterFrequency.Value).GetLocalizedDescription();
            settings.UpdateCheckFrequency = trackBarUpdaterFrequency.Value;
            string availableVersion = await updateManager.GetBestAvailableVersionString(false).ConfigureAwait(true);
            labelAvailableVersion.Text = UpdateManager.NormalizedPackageVersion(availableVersion) ?? "n/a";
            buttonUpdaterExecute.Tag = availableVersion;
            buttonUpdaterExecute.Visible = !string.IsNullOrEmpty(availableVersion);
        }

        private async void PresetUpdateSelections()
        {
            if (!string.Equals(settings.UpdateSource, settings.GetDefaultValue(nameof(settings.UpdateSource)) as string, StringComparison.OrdinalIgnoreCase))
            {
                rbDeveloperPrereleases.Checked = true;
            }
            else if (settings.UpdatePreReleases)
            {
                rbPublicPrereleases.Checked = true;
            }
            else
            {
                rbPublicReleases.Checked = true;
            }
            string availableVersion = await updateManager.GetBestAvailableVersionString(false).ConfigureAwait(true);
            labelAvailableVersion.Text = UpdateManager.NormalizedPackageVersion(availableVersion) ?? "n/a";
            buttonUpdaterExecute.Tag = availableVersion;
            buttonUpdaterExecute.Visible = !string.IsNullOrEmpty(availableVersion);
            rbDeveloperPrereleases.CheckedChanged += UpdaterSelection_CheckedChanged;
            rbPublicPrereleases.CheckedChanged += UpdaterSelection_CheckedChanged;
            rbPublicReleases.CheckedChanged += UpdaterSelection_CheckedChanged;
        }

        private async void UpdaterSelection_CheckedChanged(object sender, EventArgs e)
        {
            updateManager.SetUpdateChannel(rbPublicPrereleases.Checked, rbDeveloperPrereleases.Checked);
            if (sender == rbDeveloperPrereleases && rbDeveloperPrereleases.Checked)
            {
                if (MessageBox.Show("While we encourage users to support us in testing new versions and features, " + Environment.NewLine +
                    "be aware that development versions may contain serious bugs, regressions or may not be optimized for performance." + Environment.NewLine + Environment.NewLine +
                    "Please confirm that you want to use development code versions. Otherwise we recommend using public prerelease versions, which may run more stable and contain less defects.",
                    "Confirm Developer Releases", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.Cancel)
                {
                    rbPublicPrereleases.Checked = true;
                }
            }
            string availableVersion = await updateManager.GetBestAvailableVersionString(false).ConfigureAwait(true);
            labelAvailableVersion.Text = UpdateManager.NormalizedPackageVersion(availableVersion) ?? "n/a";
            buttonUpdaterExecute.Tag = availableVersion;
            buttonUpdaterExecute.Visible = !string.IsNullOrEmpty(availableVersion);
        }

        private async void ButtonUpdaterExecute_Click(object sender, EventArgs e)
        {
            await updateManager.RunUpdateProcess(buttonUpdaterExecute.Tag as string).ConfigureAwait(false);
        }
        #region Help for General Options
        // The icons all share the same code which assumes they are named according to a simple scheme as follows:
        //   1. To add a new Help Icon, copy an existing one and paste it onto the tab.
        //   2. Give it the same name as the associated control but change the prefix to "pb" for Picture Box.
        //   3. Add a Click event named HelpIcon_Click to each HelpIcon
        //      Do not add code for this event (or press Return/double click in the Properties field which creates a code stub for you). 
        //   4. Add MouseEnter/Leave events to each HelpIcon, label and checkbox:
        //     - MouseEnter event named HelpIcon_MouseEnter
        //     - MouseLeave event named HelpIcon_MouseLeave
        //     Numeric controls do not have MouseEnter/Leave events so, for them, use:
        //     - Enter event named HelpIcon_MouseEnter
        //     - Leave event named HelpIcon_MouseLeave
        //      Do not add code for these events (or press Return/double click in the Properties field which creates a code stub for you). 
        //   5. Add an entry to InitializeHelpIcons() which links the icon to the control and, if there is one, the label.
        //      This link will highlight the icon when the user hovers (mouses over) the control or the label.
        //   6. Add an entry to HelpIcon_Click() which opens the user's browser with the correct help page.
        //      The URL can be found from visiting the page and hovering over the title of the section.

        /// <summary>
        /// Allows multiple controls to change a single help icon with their hover events.
        /// </summary>
        private class HelpIconHover
        {
            private readonly PictureBox icon;

            public HelpIconHover(PictureBox pb)
            {
                icon = pb;
            }

            public void Enter()
            {
                SetHoverState(true);
            }

            public void Leave()
            {
                SetHoverState(false);
            }

            private void SetHoverState(bool state)
            {
                icon.Image = state ? Properties.Resources.InfoHover : Properties.Resources.Info;
            }
        }

        private void InitializeHelpIcons()
        {
            // static mapping of picture boxes to controls
            (PictureBox, Control[], string)[] helpIconControls = new (PictureBox, Control[], string)[]
            {
                // General Tab
                (pbAlerter, new[] { checkAlerter }, "/options.html#alerter-in-cab"),
                (pbControlConfirmations, new[] { checkConfirmations }, "/options.html#control-confirmations"),
                (pbRetainers, new[] { checkRetainers }, "/options.html#retainer-valve-on-all-cars"),
                (pbGraduatedRelease, new[] { checkGraduatedRelease }, "/options.html#graduated-release-air-brakes"),
                (pbBrakePipeChargingRate, new[] { numericBrakePipeChargingRate }, "/options.html#brake-pipe-charging-rate"),
                (pbLanguage, new Control[] { labelLanguage, comboLanguage }, "/options.html#language"),
                (pbPressureUnit, new Control[] { labelPressureUnit, comboPressureUnit }, "/options.html#pressure-unit"),
                (pbOtherUnits, new Control[] { labelOtherUnits, comboOtherUnits }, "/options.html#other-units"),
                (pbEnableTcsScripts, new[] { checkEnableTCSScripts }, "/options.html#disable-tcs-scripts"),
                (pbEnableWebServer, new[] { checkEnableWebServer }, "/options.html#enable-web-server"),
                (pbOverspeedMonitor, new[] { checkSpeedMonitor }, "/options.html#overspeed-monitor"),

                // Audio tab
                (pbSoundVolumePercent, new Control[] { labelSoundVolume, numericSoundVolumePercent }, "/options.html#audio-options"),
                (pbSoundDetailLevel, new Control[]  { labelSoundDetailLevel, numericSoundDetailLevel },"/options.html#audio-options"),
                (pbExternalSoundPassThruPercent, new Control[]  { labelExternalSound, numericExternalSoundPassThruPercent },"/options.html#audio-options"),

            };
            foreach ((PictureBox pb, Control[] controls, string url) in helpIconControls)
            {
                pb.Tag = url;
                HelpIconHover hover = new HelpIconHover(pb);
                helpIconMap[pb] = hover;
                foreach (Control control in controls)
                    helpIconMap[control] = hover;
            }
        }

        /// <summary>
        /// Loads a relevant page from the manual maintained.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HelpIcon_Click(object sender, EventArgs _)
        {
            if (sender is PictureBox pictureBox)
            {
#pragma warning disable CA2234 // Pass system uri objects instead of strings
                SystemInfo.OpenBrowser(baseUrl + pictureBox.Tag);
#pragma warning restore CA2234 // Pass system uri objects instead of strings
            }
        }

        /// <summary>
        /// Highlight the Help Icon if the user mouses over the icon or its control.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="_"></param>
        private void HelpIcon_MouseEnter(object sender, EventArgs _)
        {
            if (sender is Control control && helpIconMap.TryGetValue(control, out HelpIconHover hover))
                hover.Enter();
        }

        private void HelpIcon_MouseLeave(object sender, EventArgs _)
        {
            if (sender is Control control && helpIconMap.TryGetValue(control, out HelpIconHover hover))
                hover.Leave();
        }
        #endregion

    }

    public class ContentFolder
    {
        public string Name { get; set; }
        public string Path { get; set; }

        public ContentFolder()
        {
            Name = "";
            Path = "";
        }
    }

}
