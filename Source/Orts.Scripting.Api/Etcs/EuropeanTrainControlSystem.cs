// COPYRIGHT 2020 by the Open Rails project.
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

namespace Orts.Scripting.Api.Etcs
{
    /// <summary>
    /// Current status of ETCS, to be shown in DMI
    /// </summary>
    public class ETCSStatus
    {
        // General status
        /// <summary>
        /// True if the DMI is active and will be shown
        /// </summary>
        public bool DMIActive { get; set; } = true;
        /// <summary>
        /// Current operating level
        /// </summary>
        public Level CurrentLevel { get; set; }
        /// <summary>
        /// Current ETCS supervision mode
        /// </summary>
        public Mode CurrentMode { get; set; } = Mode.FS;
        /// <summary>
        /// If mode is NTC, specific name of the NTC. Not used yet
        /// </summary>
        public string NTCMode { get; set; }

        // Speed and distance monitoring
        /// <summary>
        /// True if the speedometer (needle and gauges) are to be shown
        /// </summary>
        public bool SpeedAreaShown { get; set; } = true;
        /// <summary>
        /// Permitted speed to be shown in the circular speed gauge
        /// </summary>
        public float AllowedSpeedMpS { get; set; }
        /// <summary>
        /// Intervention speed at which brakes will be applied
        /// </summary>
        public float InterventionSpeedMpS { get; set; }
        /// <summary>
        /// Target speed limit to be displayed, if any
        /// </summary>
        public float? TargetSpeedMpS { get; set; }
        /// <summary>
        /// Distance to the speed reduction being displayed, if any
        /// </summary>
        public float? TargetDistanceM { get; set; }
        /// <summary>
        /// Speed at which the train is allowed to approach to a zero target
        /// </summary>
        public float? ReleaseSpeedMpS { get; set; }
        /// <summary>
        /// Visual indication for the driver to help him follow the braking curve with reduced adheasion conditions.
        /// It is shown as a grey/white square at the top left corner of the DMI while in CSM.
        /// </summary>
        public float? TimeToIndicationS { get; set; }
        /// <summary>
        /// Visual indication for the driver to help him follow the permitted speed curve.
        /// It is shown as a yellow, orange or red square at the top left corner of the DMI while in TSM.
        /// </summary>
        public float? TimeToPermittedS { get; set; }
        /// <summary>
        /// Current speed monitoring status, either ceiling speed, target speed or release speed
        /// </summary>
        public Monitor CurrentMonitor { get; set; }
        /// <summary>
        /// Determines the color of the needle and circular speed gauge, depending on train speed
        /// </summary>
        public SupervisionStatus CurrentSupervisionStatus { get; set; }

        // Planning information
        /// <summary>
        /// Set to true to display planning area
        /// </summary>
        public bool PlanningAreaShown { get; set; }
        /// <summary>
        /// List of targets to be shown in the planning area.
        /// First target must be current speed limit, with distance = 0
        /// It will also be used to draw the planning area speed profile (PASP)
        /// </summary>
        public List<PlanningTarget> SpeedTargets { get; } = new List<PlanningTarget>();
        /// <summary>
        /// Target with the closest distance to indication.
        /// Its speed limit will be shown in yellow in the planning area
        /// </summary>
        public PlanningTarget? IndicationMarkerTarget { get; set; }
        /// <summary>
        /// Distance to the point where the monitoring status will change to either TSM or RSM from CSM
        /// Will be shown as a yellow horizontal line in the planning area
        /// </summary>
        public float? IndicationMarkerDistanceM { get; set; }
        /// <summary>
        /// Gradient of the track ahead of the train, to be shown in planning area
        /// First gradient must be with distance = 0
        /// At the point where the gradient profile ends a target must be inserted with any value, to mark the end of the profile
        /// </summary>
        public List<GradientProfileElement> GradientProfile { get; } = new List<GradientProfileElement>();
        /// <summary>
        /// Orders and announcements ahead to be displayed in the planning area
        /// </summary>
        public List<PlanningTrackCondition> PlanningTrackConditions { get; } = new List<PlanningTrackCondition>();
        /// <summary>
        /// True if the text message area shall be displayed
        /// </summary>
        public bool TextMessageAreaShown { get; set; }
        /// <summary>
        /// Legacy variable. Please use TextMessageAreaShown instead
        /// </summary>
        public bool ShowTextMessageArea { get { return TextMessageAreaShown; } set { TextMessageAreaShown = true; } }
        /// <summary>
        /// List of text messages to be displayed in text area. Do not rely on the order, as the DMI will reorder the list.
        /// </summary>
        public List<TextMessage> TextMessages { get; } = new List<TextMessage>();
        /* TODO: Interface between DMI and TCS for displaying menus to be defined in the future
        /// <summary>
        /// List of menu items to be shown
        /// </summary>
        public readonly List<DMIButtonDefinition> MenuBarButtons = new List<DMIButtonDefinition>();
        /// <summary>
        /// If not null, the window that shall be displayed to the driver
        /// </summary>
        public DMISubwindowDefinition ActiveSubwindow;*/

        /// <summary>
        /// Monitoring status of ETCS
        /// </summary>
        public enum Monitor
        {
            /// <summary>
            /// No speed restriction ahead, a fixed speed is supervised.
            /// </summary>
            CeilingSpeed,
            /// <summary>
            /// A braking curve is being supervised while approaching a speed target
            /// </summary>
            TargetSpeed,
            /// <summary>
            /// A release speed is supervised while approaching a zero speed target
            /// </summary>
            ReleaseSpeed
        }
    }
    /// <summary>
    /// Controls supervision status of ETCS
    /// </summary>
    public enum SupervisionStatus
    {
        /// <summary>
        /// Grey color. No speed restriction is ahead.
        /// </summary>
        Normal,
        /// <summary>
        /// Yellow color. Next signal is restricted, driver should start decreasing speed.
        /// </summary>
        Indication,
        /// <summary>
        /// Orange color. The locomotive is over the permitted supervision limit.
        /// </summary>
        Overspeed,
        /// <summary>
        /// Orange color. Computer is close to apply brakes, audible warning is played.
        /// </summary>
        Warning,
        /// <summary>
        /// Red color. Train control system intervention speed. Computer has to apply full service or emergency brake to maintain speed restriction.
        /// </summary>
        Intervention
    }

    public enum Level
    {
        N0,
        N1,
        N2,
        N3,
        NTC
    }
    public enum Mode
    {
        FS,
        LS,
        OS,
        SR,
        SH,
        UN,
        PS,
        SL,
        SB,
        TR,
        PT,
        SF,
        IS,
        NP,
        NL,
        SN,
        RV
    }

    public enum TrackConditionType
    {
        Custom,
        LowerPantograph,
        RaisePantograph,
        NeutralSectionAnnouncement,
        EndOfNeutralSection,
        NonStoppingArea,
        RadioHole,
        MagneticShoeInhibition,
        EddyCurrentBrakeInhibition,
        RegenerativeBrakeInhibition,
        OpenAirIntake,
        CloseAirIntake,
        SoundHorn,
        TractionSystemChange,
        // Legacy conditions used in old DMIs
        Tunnel,
        Bridge,
        Station,
        EndOfTrack
    }

    public enum TractionSystem
    {
        NonFitted,
        AC25kV,
        AC15kV,
        DC3000V,
        DC1500V,
        DC750V
    }

    public struct PlanningTrackCondition
    {
        public TrackConditionType Type { get; }
        public float DistanceToTrainM { get; set; }
        public bool YellowColour { get; }
        public TractionSystem? TractionSystem { get; set; }

        public PlanningTrackCondition(TrackConditionType type, bool isYellowColour, float distanceToTrainM)
        {
            DistanceToTrainM = distanceToTrainM;
            Type = type;
            YellowColour = isYellowColour;
            TractionSystem = null;
        }

        public PlanningTrackCondition(TractionSystem tractionSystem, bool isYellowColour, float distanceToTrainM)
        {
            DistanceToTrainM = distanceToTrainM;
            Type = TrackConditionType.TractionSystemChange;
            YellowColour = isYellowColour;
            TractionSystem = tractionSystem;
        }
    }

    public struct PlanningTarget
    {
        public float DistanceToTrainM { get; set; }
        public float TargetSpeedMpS { get; }
        public PlanningTarget(float distanceToTrainM, float targetSpeedMpS)
        {
            DistanceToTrainM = distanceToTrainM;
            TargetSpeedMpS = targetSpeedMpS;
        }
    }
    public struct GradientProfileElement
    {
        public float DistanceToTrainM { get; set; }
        public int GradientPerMille { get; set; }
        public GradientProfileElement(float distanceToTrainM, int gradientPerMille)
        {
            DistanceToTrainM = distanceToTrainM;
            GradientPerMille = gradientPerMille;
        }
    }
    /// <summary>
    /// Defines a text message to be shown in DMI
    /// </summary>
    public struct TextMessage : IEquatable<TextMessage>
    {
        /// <summary>
        /// Text to show
        /// </summary>
        public readonly string Text;
        /// <summary>
        /// Defines the priority of the message
        /// </summary>
        public readonly bool FirstGroup;
        /// <summary>
        /// Timestamp in seconds of the message
        /// </summary>
        public float TimestampS;
        /// <summary>
        /// True if it needs acknowledgement from the driver
        /// </summary>
        public bool Acknowledgeable;
        /// <summary>
        /// Will be set by the DMI if the driver acknowledges the message
        /// </summary>
        public bool Acknowledged;
        /// <summary>
        /// Internally used by the DMI to determine if it shall inform the driver about the reception
        /// </summary>
        public bool Displayed;
        public TextMessage(string text, float timestampS, bool firstGroup = false, bool acknowledgeable = false)
        {
            Text = text;
            TimestampS = timestampS;
            FirstGroup = firstGroup;
            Acknowledgeable = acknowledgeable;
            Acknowledged = false;
            Displayed = false;
        }
        public bool Equals(TextMessage other)
        {
            return other.Text == Text && other.TimestampS == TimestampS;
        }
    }
    public abstract class DMIButtonDefinition
    {
        public string ConfirmerCaption { get; }
        public bool Enabled { get; }

        protected DMIButtonDefinition(string confirmerCaption, bool enabled)
        {
            ConfirmerCaption = confirmerCaption;
            Enabled = enabled;
        }
    }

    public class DMITextButtonDefinition : DMIButtonDefinition
    {
        public string Label { get; }

        public DMITextButtonDefinition(string label, string confirmerCaption, bool enabled) : 
            base(confirmerCaption, enabled)
        {
            Label = label;
        }
    }
    public class DMIIconButtonDefinition : DMIButtonDefinition
    {
        public string EnabledIconName { get; }
        public string DisabledIconName { get; }

        public DMIIconButtonDefinition(string iconName, string confirmerCaption, bool enabled) : 
            base(confirmerCaption, enabled)
        {
            EnabledIconName = iconName;
            DisabledIconName = iconName;
        }
        public DMIIconButtonDefinition(string enabledIconName, string disabledIconName, string confirmerCaption, bool enabled) : 
            base(confirmerCaption, enabled)
        {
            EnabledIconName = enabledIconName;
            DisabledIconName = disabledIconName;
        }
    }

    public abstract class DMISubwindowDefinition
    {
        public string WindowTitle { get; }

        protected DMISubwindowDefinition(string title)
        {
            WindowTitle = title;
        }
    }

    public class DMIMenuWindowDefinition : DMISubwindowDefinition
    {
        /// <summary>
        /// List of buttons to show in the window. Made public to enable or disable buttons
        /// while the window is shown.
        /// </summary>
        public List<DMIButtonDefinition> Buttons { get; }

        public DMIMenuWindowDefinition(string windowTitle, List<DMIButtonDefinition> buttons) : 
            base(windowTitle)
        {
            Buttons = buttons;
        }
    }

    public struct DMIDataEntryValue
    {
        public string Name;
        public string Value;
        public DMIKeyboard Keyboard;
        public DMIVariableCheck TechnicalResolutionCheck;
        public DMIVariableCheck TechnicalRangeCheck;
        public DMIVariableCheck OperationalRangeCheck;
    }

    public class DMIKeyboard
    {
        public enum KeyboardType
        {
            Numeric,
            EnhancedNumeric,
            Alphanumeric,
            Dedicate
        }
        public KeyboardType Type;
        public readonly List<string> KeyLabels;
        public DMIKeyboard(List<string> keyLabels)
        {
            KeyLabels = keyLabels;
            Type = KeyboardType.Dedicate;
        }
        public DMIKeyboard(KeyboardType type)
        {
            Type = type;
        }
    }
    public struct DMIVariableCheck
    {
        public Func<string, bool> IsValid;
    }
    public struct DMIVariableCrossCheck
    {
        public Func<Dictionary<string, string>, List<string>> GetConflictingVariables;
    }

    public class DMIDataEntryDefinition : DMISubwindowDefinition
    {
        public List<DMIDataEntryValue> Fields { get; }
        public List<DMIVariableCrossCheck> TechnicalCrossChecks { get; }
        public List<DMIVariableCrossCheck> OperationalCrossChecks { get; }
        public bool FullScreen { get; }

        public DMIDataEntryDefinition(string windowTitle, List<DMIDataEntryValue> fields, bool fullscreen, List<DMIVariableCrossCheck> technicalCrossChecks = null, List<DMIVariableCrossCheck> operationalCrossChecks = null) : base(windowTitle)
        {
            Fields = fields;
            FullScreen = fullscreen;
            TechnicalCrossChecks = technicalCrossChecks ?? new List<DMIVariableCrossCheck>();
            OperationalCrossChecks = operationalCrossChecks ?? new List<DMIVariableCrossCheck>();
        }
    }
}