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

        private readonly ICatalog catalog;
        private readonly ICatalog commonCatalog;

        public OptionsForm(UserSettings settings, UpdateManager updateManager, ICatalog catalog, ICatalog commonCatalog, bool initialContentSetup)
        {
            InitializeComponent();
            Localizer.Localize(this, catalog);

            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.commonCatalog = commonCatalog ?? throw new ArgumentNullException(nameof(catalog));

            // Collect all the available language codes by searching for
            // localisation files, but always include English (base language).
            List<string> languageCodes = new List<string> { "en" };
            if (Directory.Exists(RuntimeInfo.LocalesFolder))
                foreach (string path in Directory.EnumerateDirectories(RuntimeInfo.LocalesFolder))
                    if (Directory.EnumerateFiles(path, "*.mo").Any())
                        languageCodes.Add(Path.GetFileName(path));

            // Turn the list of codes in to a list of code + name pairs for
            // displaying in the dropdown list.
            languageCodes.Add(string.Empty);
            languageCodes.Sort();
            comboLanguage.DataSourceFromList(languageCodes, (language) => string.IsNullOrEmpty(language) ? "System" : CultureInfo.GetCultureInfo(language).NativeName);
            comboLanguage.SelectedValue = this.settings.Language;
            if (comboLanguage.SelectedValue == null)
                comboLanguage.SelectedIndex = 0;

            comboBoxOtherUnits.DataSourceFromEnum<MeasurementUnit>(commonCatalog);
            comboPressureUnit.DataSourceFromEnum<PressureUnit>(commonCatalog);

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;
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
            checkSpeedControl.Checked = this.settings.SpeedControl;
            checkConfirmations.Checked = !this.settings.SuppressConfirmations;
            checkViewDispatcher.Checked = this.settings.ViewDispatcher;
            checkRetainers.Checked = this.settings.RetainersOnAllCars;
            checkGraduatedRelease.Checked = this.settings.GraduatedRelease;
            numericBrakePipeChargingRate.Value = this.settings.BrakePipeChargingRate;
            comboLanguage.Text = this.settings.Language;
            comboPressureUnit.SelectedValue = this.settings.PressureUnit;
            comboBoxOtherUnits.SelectedValue = settings.MeasurementUnit;
            checkDisableTCSScripts.Checked = this.settings.DisableTCSScripts;
            checkEnableWebServer.Checked = this.settings.WebServer;
            numericWebServerPort.Value = this.settings.WebServerPort;

            // Audio tab
            numericSoundVolumePercent.Value = this.settings.SoundVolumePercent;
            numericSoundDetailLevel.Value = this.settings.SoundDetailLevel;
            numericExternalSoundPassThruPercent.Value = this.settings.ExternalSoundPassThruPercent;

            // Video tab
            checkDynamicShadows.Checked = this.settings.DynamicShadows;
            checkShadowAllShapes.Checked = this.settings.ShadowAllShapes;
            checkFastFullScreenAltTab.Checked = this.settings.FastFullScreenAltTab;
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
            numericViewingFOV.Value = this.settings.ViewingFOV;
            numericWorldObjectDensity.Value = this.settings.WorldObjectDensity;
            comboWindowSize.Text = this.settings.WindowSize;
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
            checkCurveResistanceDependent.Checked = this.settings.CurveResistanceDependent;
            checkCurveSpeedDependent.Checked = this.settings.CurveSpeedDependent;
            checkTunnelResistanceDependent.Checked = this.settings.TunnelResistanceDependent;
            checkWindResistanceDependent.Checked = this.settings.WindResistanceDependent;
            checkOverrideNonElectrifiedRoutes.Checked = this.settings.OverrideNonElectrifiedRoutes;
            checkHotStart.Checked = this.settings.HotStart;
            checkSimpleControlPhysics.Checked = this.settings.SimpleControlPhysics;
            checkForcedRedAtStationStops.Checked = !this.settings.NoForcedRedAtStationStops;
            checkDoorsAITrains.Checked = this.settings.OpenDoorsInAITrains;

            //// Keyboard tab
            //InitializeKeyboardSettings();

            ////RailDriver tab
            //InitializeRailDriverSettings());

            // DataLogger tab
            comboDataLoggerSeparator.DataSourceFromEnum<SeparatorChar>(commonCatalog);
            comboDataLoggerSeparator.SelectedValue = settings.DataLoggerSeparator;

            comboDataLogSpeedUnits.DataSourceFromEnum<SpeedUnit>(commonCatalog);
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

            string context = EnumExtension.EnumDescription<EvaluationLogContents>();
            checkListDataLogTSContents.Items.AddRange(EnumExtension.GetValues<EvaluationLogContents>().
                Where(content => content != EvaluationLogContents.None).
                Select(content => commonCatalog.GetParticularString(context, content.GetDescription())).ToArray());

            for (int i = 0; i < checkListDataLogTSContents.Items.Count; i++)
            {
                checkListDataLogTSContents.SetItemChecked(i, settings.EvaluationContent.HasFlag((EvaluationLogContents)(1<<i)));
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
            TrackBarUpdaterFrequency_Scroll(this, null);

            buttonUpdatesRefresh.Font = new Font("Wingdings 3", 14);
            buttonUpdatesRefresh.Text = char.ConvertFromUtf32(81);
            buttonUpdatesRefresh.Width = 23;
            buttonUpdatesRefresh.Height = 23;

            comboBoxUpdateChannels.DataSourceFromList(updateManager.GetChannels().OrderByDescending((s) => s), (channel) => catalog.GetString(channel));
            comboBoxUpdateChannels.SelectedIndex = comboBoxUpdateChannels.FindStringExact(this.settings.UpdateChannel);

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
            checkConditionalLoadOfNightTextures.Checked = this.settings.ConditionalLoadOfDayOrNightTextures;
            checkSignalLightGlow.Checked = this.settings.SignalLightGlow;
            checkCircularSpeedGauge.Checked = this.settings.CircularSpeedGauge;
            checkLODViewingExtention.Checked = this.settings.LODViewingExtention;
            checkPreferDDSTexture.Checked = this.settings.PreferDDSTexture;
            checkUseLocationPassingPaths.Checked = this.settings.UseLocationPassingPaths;
            checkUseMSTSEnv.Checked = this.settings.UseMSTSEnv;
            trackAdhesionFactor.Value = this.settings.AdhesionFactor;
            checkAdhesionPropToWeather.Checked = this.settings.AdhesionProportionalToWeather;
            trackAdhesionFactorChange.Value = this.settings.AdhesionFactorChange;
            TrackAdhesionFactor_ValueChanged(null, null);
            checkShapeWarnings.Checked = !this.settings.SuppressShapeWarnings;
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
            settings.SpeedControl = checkSpeedControl.Checked;
            settings.SuppressConfirmations = !checkConfirmations.Checked;
            settings.ViewDispatcher = checkViewDispatcher.Checked;
            settings.RetainersOnAllCars = checkRetainers.Checked;
            settings.GraduatedRelease = checkGraduatedRelease.Checked;
            settings.BrakePipeChargingRate = (int)numericBrakePipeChargingRate.Value;
            settings.Language = comboLanguage.SelectedValue.ToString();
            settings.PressureUnit = (PressureUnit)comboPressureUnit.SelectedValue;
            settings.MeasurementUnit = (MeasurementUnit)comboBoxOtherUnits.SelectedValue;
            settings.DisableTCSScripts = checkDisableTCSScripts.Checked;
            settings.WebServer = checkEnableWebServer.Checked;

            // Audio tab
            settings.SoundVolumePercent = (int)numericSoundVolumePercent.Value;
            settings.SoundDetailLevel = (int)numericSoundDetailLevel.Value;
            settings.ExternalSoundPassThruPercent = (int)numericExternalSoundPassThruPercent.Value;

            // Video tab
            settings.DynamicShadows = checkDynamicShadows.Checked;
            settings.ShadowAllShapes = checkShadowAllShapes.Checked;
            settings.FastFullScreenAltTab = checkFastFullScreenAltTab.Checked;
            settings.WindowGlass = checkWindowGlass.Checked;
            settings.ModelInstancing = checkModelInstancing.Checked;
            settings.Wire = checkWire.Checked;
            settings.VerticalSync = checkVerticalSync.Checked;
            settings.MultisamplingCount = 1 << trackbarMultiSampling.Value;
            settings.Cab2DStretch = (int)numericCab2DStretch.Value;
            settings.ViewingDistance = (int)numericViewingDistance.Value;
            settings.DistantMountains = checkDistantMountains.Checked;
            settings.DistantMountainsViewingDistance = (int)numericDistantMountainsViewingDistance.Value * 1000;
            settings.ViewingFOV = (int)numericViewingFOV.Value;
            settings.WorldObjectDensity = (int)numericWorldObjectDensity.Value;
            settings.WindowSize = GetValidWindowSize(comboWindowSize.Text);

            settings.DayAmbientLight = (int)trackDayAmbientLight.Value;
            settings.DoubleWire = checkDoubleWire.Checked;
            settings.NativeFullscreenResolution = checkBoxFullScreenNativeResolution.Checked;
            settings.FullScreen = radioButtonFullScreen.Checked;

            // Simulation tab
            settings.UseAdvancedAdhesion = checkUseAdvancedAdhesion.Checked;
            settings.AdhesionMovingAverageFilterSize = (int)numericAdhesionMovingAverageFilterSize.Value;
            settings.BreakCouplers = checkBreakCouplers.Checked;
            settings.CurveResistanceDependent = checkCurveResistanceDependent.Checked;
            settings.CurveSpeedDependent = checkCurveSpeedDependent.Checked;
            settings.TunnelResistanceDependent = checkTunnelResistanceDependent.Checked;
            settings.WindResistanceDependent = checkWindResistanceDependent.Checked;
            settings.OverrideNonElectrifiedRoutes = checkOverrideNonElectrifiedRoutes.Checked;
            settings.HotStart = checkHotStart.Checked;
            settings.SimpleControlPhysics = checkSimpleControlPhysics.Checked;
            settings.NoForcedRedAtStationStops = !checkForcedRedAtStationStops.Checked;
            settings.OpenDoorsInAITrains = checkDoorsAITrains.Checked;

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

            settings.UpdateChannel = (comboBoxUpdateChannels.SelectedItem as ComboBoxItem<string>)?.Key ?? string.Empty;
            settings.UpdateCheckFrequency = trackBarUpdaterFrequency.Value;

            // Experimental tab
            settings.UseSuperElevation = (int)numericUseSuperElevation.Value;
            settings.SuperElevationMinLen = (int)numericSuperElevationMinLen.Value;
            settings.SuperElevationGauge = (int)numericSuperElevationGauge.Value;
            settings.PerformanceTuner = checkPerformanceTuner.Checked;
            settings.PerformanceTunerTarget = (int)numericPerformanceTunerTarget.Value;
            settings.LODBias = trackLODBias.Value;
            settings.ConditionalLoadOfDayOrNightTextures = checkConditionalLoadOfNightTextures.Checked;
            settings.SignalLightGlow = checkSignalLightGlow.Checked;
            settings.CircularSpeedGauge = checkCircularSpeedGauge.Checked;
            settings.LODViewingExtention = checkLODViewingExtention.Checked;
            settings.PreferDDSTexture = checkPreferDDSTexture.Checked;
            settings.UseLocationPassingPaths = checkUseLocationPassingPaths.Checked;
            settings.UseMSTSEnv = checkUseMSTSEnv.Checked;
            settings.AdhesionFactor = (int)trackAdhesionFactor.Value;
            settings.AdhesionProportionalToWeather = checkAdhesionPropToWeather.Checked;
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
        private string GetValidWindowSize(string text)
        {
            Match match = Regex.Match(text, @"^\s*([1-9]\d{2,3})\s*[Xx]\s*([1-9]\d{2,3})\s*$");//capturing 2 groups of 3-4digits, separated by X or x, ignoring whitespace in beginning/end and in between
            if (match.Success)
            {
                return $"{match.Groups[1]}x{match.Groups[2]}";
            }
            return settings.WindowSize; // i.e. no change or message. Just ignore non-numeric entries
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
            AdhesionFactorChangeValueLabel.Text = $"{ trackAdhesionFactorChange.Value}%";
        }

        private void SetAdhesionLevelValue()
        {
            int level = trackAdhesionFactor.Value - trackAdhesionFactorChange.Value;
            if (checkAdhesionPropToWeather.Checked)
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

        private void TrackBarUpdaterFrequency_Scroll(object sender, EventArgs e)
        {
            labelUpdaterFrequency.Text = catalog.GetString(((UpdateCheckFrequency)trackBarUpdaterFrequency.Value).GetDescription());
        }

        private async void ButtonUpdatesRefresh_Click(object sender, EventArgs e)
        {
            await updateManager.RefreshUpdateInfo(UpdateCheckFrequency.Always).ConfigureAwait(true);

            comboBoxUpdateChannels.DataSourceFromList(updateManager.GetChannels(), (channel) => catalog.GetString(channel));
            comboBoxUpdateChannels.SelectedIndex = comboBoxUpdateChannels.FindStringExact(settings.UpdateChannel);
        }

        private void ComboBoxUpdateChannels_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxUpdateChannels.SelectedIndex != -1 &&
                EnumExtension.GetValue(((ComboBoxItem<string>)comboBoxUpdateChannels.SelectedItem).Key, out UpdateChannel channel))
            {
                labelChannelDescription.Text = catalog.GetString(channel.GetDescription());
                labelChannelVersion.Text = updateManager.GetChannelByName(channel.ToString())?.NormalizedVersion ?? "n/a";
                labelBestVersion.Text = updateManager.GetBestAvailableVersion(string.Empty, channel.ToString()) ?? "n/a";
            }
            else
            {
                labelChannelDescription.Text = string.Empty;
            }
        }

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
