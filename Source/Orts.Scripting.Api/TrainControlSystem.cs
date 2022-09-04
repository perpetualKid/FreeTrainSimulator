using System;
using System.IO;

using Orts.Common;
using Orts.Scripting.Api.Etcs;

namespace Orts.Scripting.Api
{
    public abstract class TrainControlSystem : TrainScriptBase
    {
        public bool Activated { get; set; }

        public ETCSStatus ETCSStatus { get; } = new ETCSStatus();

        /// <summary>
        /// True if train control is switched on (the locomotive is the lead locomotive and the train is not autopiloted).
        /// </summary>
        public Func<bool> IsTrainControlEnabled { get; set; }
        /// <summary>
        /// True if train is autopiloted
        /// </summary>
        public Func<bool> IsAutopiloted { get; set; }
        /// <summary>
        /// True if vigilance monitor was switched on in game options.
        /// </summary>
        public Func<bool> IsAlerterEnabled { get; set; }
        /// <summary>
        /// True if speed control was switched on in game options.
        /// </summary>
        public Func<bool> IsSpeedControlEnabled { get; set; }
        /// <summary>
        /// True if low voltage power supply is switched on.
        /// </summary>
        public Func<bool> IsLowVoltagePowerSupplyOn { get; set; }
        /// <summary>
        /// True if cab power supply is switched on.
        /// </summary>
        public Func<bool> IsCabPowerSupplyOn { get; set; }
        /// <summary>
        /// True if alerter sound rings, otherwise false
        /// </summary>
        public Func<bool> AlerterSound { get; set; }
        /// <summary>
        /// Max allowed speed for the train in that moment.
        /// </summary>
        public Func<float> TrainSpeedLimitMpS { get; set; }
        /// <summary>
        /// Max allowed speed for the train basing on consist and route max speed.
        /// </summary>
        public Func<float> TrainMaxSpeedMpS { get; set; }
        /// <summary>
        /// Max allowed speed determined by current signal.
        /// </summary>
        public Func<float> CurrentSignalSpeedLimitMpS { get; set; }
        /// <summary>
        /// Max allowed speed determined by next signal.
        /// </summary>
        public Func<int, float> NextSignalSpeedLimitMpS { get; set; }
        /// <summary>
        /// Aspect of the next signal.
        /// </summary>
        public Func<int, TrackMonitorSignalAspect> NextSignalAspect { get; set; }
        /// <summary>
        /// Distance to next signal.
        /// </summary>
        public Func<int, float> NextSignalDistanceM { get; set; }
        /// <summary>
        /// Aspect of the DISTANCE heads of next NORMAL signal.
        /// </summary>
        public Func<TrackMonitorSignalAspect> NextNormalSignalDistanceHeadsAspect { get; set; }
        /// <summary>
        /// Next normal signal has only two aspects (STOP and CLEAR_2).
        /// </summary>
        public Func<bool> DoesNextNormalSignalHaveTwoAspects { get; set; }
        /// <summary>
        /// Aspect of the next DISTANCE signal.
        /// </summary>
        public Func<TrackMonitorSignalAspect> NextDistanceSignalAspect { get; set; }
        /// <summary>
        /// Distance to next DISTANCE signal.
        /// </summary>
        public Func<float> NextDistanceSignalDistanceM { get; set; }
        /// <summary>
        /// Signal type of main head of hext generic signal. Not for NORMAL signals
        /// </summary>
        public Func<string, string> NextGenericSignalMainHeadSignalType { get; set; }
        /// <summary>
        /// Aspect of the next generic signal. Not for NORMAL signals
        /// </summary>
        public Func<string, TrackMonitorSignalAspect> NextGenericSignalAspect { get; set; }
        /// <summary>
        /// Distance to next generic signal. Not for NORMAL signals
        /// </summary>
        public Func<string, float> NextGenericSignalDistanceM { get; set; }
        /// <summary>
        /// Features of next generic signal. 
        /// string: signal type (DISTANCE etc.)
        /// int: position of signal in the signal sequence along the train route, starting from train front; 0 for first signal;
        /// float: max testing distance
        /// </summary>
        public Func<string, int, float, SignalFeatures> NextGenericSignalFeatures { get; set; }
        /// <summary>
        /// Features of next speed post
        /// int: position of speed post in the speed post sequence along the train route, starting from train front; 0 for first speed post;
        /// float: max testing distance
        /// </summary>
        public Func<int, float, SpeedPostFeatures> NextSpeedPostFeatures { get; set; }
        /// <summary>
        /// Next normal signal has a repeater head
        /// </summary>
        public Func<bool> DoesNextNormalSignalHaveRepeaterHead { get; set; }
        /// <summary>
        /// Max allowed speed determined by current speedpost.
        /// </summary>
        public Func<float> CurrentPostSpeedLimitMpS { get; set; }
        /// <summary>
        /// Max allowed speed determined by next speedpost.
        /// </summary>
        public Func<int, float> NextPostSpeedLimitMpS { get; set; }
        /// <summary>
        /// Distance to next speedpost.
        /// </summary>
        public Func<int, float> NextPostDistanceM { get; set; }
        /// <summary>
        /// Distance and length of next tunnels
        /// int: position of tunnel along the train route, starting from train front; 0 for first tunnel;
        /// If train is in tunnel, index 0 will contain the remaining length of the tunnel
        /// </summary>
        public Func<int, TunnelInfo> NextTunnel { get; set; }
        /// <summary>
        /// Distance and value of next mileposts
        /// int: return nth milepost ahead; 0 for first milepost
        /// </summary>
        public Func<int, MilepostInfo> NextMilepost { get; set; }
        /// <summary>
        /// Distance to end of authority.
        /// int: direction; 0: forwards; 1: backwards
        /// </summary>
        public Func<int, float> EOADistanceM { get; set; }
        /// <summary>
        /// Train's length
        /// </summary>
        public Func<float> TrainLengthM { get; set; }
        /// <summary>
        /// Locomotive direction.
        /// </summary>
        public Func<MidpointDirection> CurrentDirection { get; set; }
        /// <summary>
        /// True if locomotive direction is forward.
        /// </summary>
        public Func<bool> IsDirectionForward { get; set; }
        /// <summary>
        /// True if locomotive direction is neutral.
        /// </summary>
        public Func<bool> IsDirectionNeutral { get; set; }
        /// <summary>
        /// True if locomotive direction is reverse.
        /// </summary>
        public Func<bool> IsDirectionReverse { get; set; }
        /// <summary>
        /// Train direction.
        /// </summary>
        public Func<MidpointDirection> CurrentTrainMUDirection { get; set; }
        /// <summary>
        /// True if locomotive is flipped.
        /// </summary>
        public Func<bool> IsFlipped { get; set; }
        /// <summary>
        /// True if player is in rear cab.
        /// </summary>
        public Func<bool> IsRearCab { get; set; }
        /// <summary>
        /// True if left doors are open
        /// </summary>
        public Func<bool> AreLeftDoorsOpen;
        /// <summary>
        /// True if right doors are open
        /// </summary>
        public Func<bool> AreRightDoorsOpen;
        /// <summary>
        /// True if train brake controller is in emergency position, otherwise false.
        /// </summary>
        public Func<bool> IsBrakeEmergency { get; set; }
        /// <summary>
        /// True if train brake controller is in full service position, otherwise false.
        /// </summary>
        public Func<bool> IsBrakeFullService { get; set; }
        /// <summary>
        /// True if circuit breaker or power contactor closing authorization is true.
        /// </summary>
        public Func<bool> PowerAuthorization { get; set; }
        /// <summary>
        /// True if circuit breaker or power contactor closing order is true.
        /// </summary>
        public Func<bool> CircuitBreakerClosingOrder { get; set; }
        /// <summary>
        /// True if circuit breaker or power contactor opening order is true.
        /// </summary>
        public Func<bool> CircuitBreakerOpeningOrder { get; set; }
        /// <summary>
        /// Returns the number of pantographs on the locomotive.
        /// </summary>
        public Func<int> PantographCount { get; set; }
        /// <summary>
        /// Checks the state of any pantograph
        /// int: pantograph ID (1 for first pantograph)
        /// </summary>
        public Func<int, PantographState> GetPantographState { get; set; }
        /// True if all pantographs are down.
        /// </summary>
        public Func<bool> ArePantographsDown { get; set; }
        /// <summary>
        /// Returns throttle percent
        /// </summary>
        public Func<float> ThrottlePercent { get; set; }
        /// <summary>
        /// Returns maximum throttle percent
        /// </summary>
        public Func<float> MaxThrottlePercent { get; set; }
        /// <summary>
        /// Returns dynamic brake percent
        /// </summary>
        public Func<float> DynamicBrakePercent { get; set; }
        /// <summary>
        /// True if traction is authorized.
        /// </summary>
        public Func<bool> TractionAuthorization { get; set; }
        /// <summary>
        /// Train brake pipe pressure. Returns float.MaxValue if no data is available.
        /// </summary>
        public Func<float> BrakePipePressureBar { get; set; }
        /// <summary>
        /// Locomotive brake cylinder pressure. Returns float.MaxValue if no data is available.
        /// </summary>
        public Func<float> LocomotiveBrakeCylinderPressureBar { get; set; }
        /// <summary>
        /// True if power must be cut if the brake is applied.
        /// </summary>
        public Func<bool> DoesBrakeCutPower { get; set; }
        /// <summary>
        /// Train brake pressure value which triggers the power cut-off.
        /// </summary>
        public Func<float> BrakeCutsPowerAtBrakeCylinderPressureBar { get; set; }
        /// <summary>
        /// State of the train brake controller.
        /// </summary>
        public Func<ControllerState> TrainBrakeControllerState { get; set; }
        /// <summary>
        /// Locomotive acceleration.
        /// </summary>
        public Func<float> AccelerationMpSS { get; set; }
        /// <summary>
        /// Locomotive altitude.
        /// </summary>
        public Func<float> AltitudeM { get; set; }
        /// <summary>
        /// Track gradient percent at the locomotive's location (positive = uphill).
        /// </summary>
        public Func<float> CurrentGradientPercent { get; set; }
        /// <summary>
        /// Line speed taken from .trk file.
        /// </summary>
        public Func<float> LineSpeedMpS { get; set; }
        /// <summary>
        /// Running total of distance travelled - negative or positive depending on train direction
        /// </summary>
        public Func<float> SignedDistanceM { get; set; }
        /// <summary>
        /// True if starting from terminal station (no track behind the train).
        /// </summary>
        public Func<bool> DoesStartFromTerminalStation { get; set; }
        /// <summary>
        /// True if game just started and train speed = 0.
        /// </summary>
        public Func<bool> IsColdStart { get; set; }
        /// <summary>
        /// Get front traveller track node offset.
        /// </summary>
        public Func<float> GetTrackNodeOffset { get; set; }
        /// <summary>
        /// Search next diverging switch distance
        /// </summary>
        public Func<float, float> NextDivergingSwitchDistanceM { get; set; }
        /// <summary>
        /// Search next trailing diverging switch distance
        /// </summary>
        public Func<float, float> NextTrailingDivergingSwitchDistanceM { get; set; }
        /// <summary>
        /// Get Control Mode of player train
        /// </summary>
        public Func<TrainControlMode> GetControlMode { get; set; }
        /// <summary>
        /// Get name of next station if any, else empty string
        /// </summary>
        public Func<string> NextStationName { get; set; }
        /// <summary>
        /// Get distance of next station if any, else max float value
        /// </summary>
        public Func<float> NextStationDistanceM { get; set; }
        /// <summary>
        /// (float targetDistanceM, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        /// Returns a speed curve based speed limit, unit is m/s
        /// </summary>
        public Func<float, float, float, float, float, float> SpeedCurve { get; set; }
        /// <summary>
        /// (float currentSpeedMpS, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        /// Returns a distance curve based safe braking distance, unit is m
        /// </summary>
        public Func<float, float, float, float, float, float> DistanceCurve { get; set; }
        /// <summary>
        /// (float currentSpeedMpS, float targetSpeedMpS, float distanceM)
        /// Returns the deceleration needed to decrease the speed to the target speed at the target distance
        /// </summary>
        public Func<float, float, float, float> Deceleration { get; set; }

        /// <summary>
        /// Set train brake controller to full service position.
        /// </summary>
        public Action<bool> SetFullBrake { get; set; }
        /// <summary>
        /// Set emergency braking on or off.
        /// </summary>
        public Action<bool> SetEmergencyBrake { get; set; }
        /// Set full dynamic braking on or off.
        /// </summary>
        public Action<bool> SetFullDynamicBrake { get; set; }
        /// <summary>
        /// Set throttle controller to position in range [0-1].
        /// </summary>
        public Action<float> SetThrottleController { get; set; }
        /// <summary>
        /// Set dynamic brake controller to position in range [0-1].
        /// </summary>
        public Action<float> SetDynamicBrakeController { get; set; }
        /// <summary>
        /// Cut power by pull all pantographs down.
        /// </summary>
        public Action SetPantographsDown { get; set; }
        /// <summary>
        /// Raise specified pantograph
        /// int: pantographID, from 1 to 4
        /// </summary>
        public Action<int> SetPantographUp { get; set; }
        /// <summary>
        /// Lower specified pantograph
        /// int: pantographID, from 1 to 4
        /// </summary>
        public Action<int> SetPantographDown { get; set; }
        /// <summary>
        /// Set the circuit breaker or power contactor closing authorization.
        /// </summary>
        public Action<bool> SetPowerAuthorization { get; set; }
        /// <summary>
        /// Set the circuit breaker or power contactor closing order.
        /// </summary>
        public Action<bool> SetCircuitBreakerClosingOrder { get; set; }
        /// <summary>
        /// Set the circuit breaker or power contactor opening order.
        /// </summary>
        public Action<bool> SetCircuitBreakerOpeningOrder { get; set; }
        /// <summary>
        /// Set the traction authorization.
        /// </summary>
        public Action<bool> SetTractionAuthorization { get; set; }
        /// <summary>
        /// Set the maximum throttle percent
        /// Range: 0 to 100
        /// </summary>
        public Action<float> SetMaxThrottlePercent { get; set; }
        /// <summary>
        /// Switch vigilance alarm sound on (true) or off (false).
        /// </summary>
        public Action<bool> SetVigilanceAlarm { get; set; }
        /// <summary>
        /// Set horn on (true) or off (false).
        /// </summary>
        public Action<bool> SetHorn { get; set; }
        /// <summary>
        /// Trigger Alert1 sound event
        /// </summary>
        public Action TriggerSoundAlert1 { get; set; }
        /// <summary>
        /// Trigger Alert2 sound event
        /// </summary>
        public Action TriggerSoundAlert2 { get; set; }
        /// <summary>
        /// Trigger Info1 sound event
        /// </summary>
        public Action TriggerSoundInfo1 { get; set; }
        /// <summary>
        /// Trigger Info2 sound event
        /// </summary>
        public Action TriggerSoundInfo2 { get; set; }
        /// <summary>
        /// Trigger Penalty1 sound event
        /// </summary>
        public Action TriggerSoundPenalty1 { get; set; }
        /// <summary>
        /// Trigger Penalty2 sound event
        /// </summary>
        public Action TriggerSoundPenalty2 { get; set; }
        /// <summary>
        /// Trigger Warning1 sound event
        /// </summary>
        public Action TriggerSoundWarning1 { get; set; }
        /// <summary>
        /// Trigger Warning2 sound event
        /// </summary>
        public Action TriggerSoundWarning2 { get; set; }
        /// <summary>
        /// Trigger Activate sound event
        /// </summary>
        public Action TriggerSoundSystemActivate { get; set; }
        /// <summary>
        /// Trigger Deactivate sound event
        /// </summary>
        public Action TriggerSoundSystemDeactivate { get; set; }
        /// <summary>
        /// Trigger generic sound event
        /// </summary>
        public Action<TrainEvent> TriggerGenericSound { get; set; }
        /// <summary>
        /// Set ALERTER_DISPLAY cabcontrol display's alarm state on or off.
        /// </summary>
        public Action<bool> SetVigilanceAlarmDisplay { get; set; }
        /// <summary>
        /// Set ALERTER_DISPLAY cabcontrol display's emergency state on or off.
        /// </summary>
        public Action<bool> SetVigilanceEmergencyDisplay { get; set; }
        /// <summary>
        /// Set OVERSPEED cabcontrol display on or off.
        /// </summary>
        public Action<bool> SetOverspeedWarningDisplay { get; set; }
        /// <summary>
        /// Set PENALTY_APP cabcontrol display on or off.
        /// </summary>
        public Action<bool> SetPenaltyApplicationDisplay { get; set; }
        /// <summary>
        /// Monitoring status determines the colors speeds displayed with. (E.g. circular speed gauge).
        /// </summary>
        public Action<MonitoringStatus> SetMonitoringStatus { get; set; }
        /// <summary>
        /// Set current speed limit of the train, as to be shown on SPEEDLIMIT cabcontrol.
        /// </summary>
        public Action<float> SetCurrentSpeedLimitMpS { get; set; }
        /// <summary>
        /// Set speed limit of the next signal, as to be shown on SPEEDLIM_DISPLAY cabcontrol.
        /// </summary>
        public Action<float> SetNextSpeedLimitMpS { get; set; }
        /// <summary>
        /// The speed at the train control system applies brake automatically.
        /// Determines needle color (orange/red) on circular speed gauge, when the locomotive
        /// already runs above the permitted speed limit. Otherwise is unused.
        /// </summary>
        public Action<float> SetInterventionSpeedLimitMpS { get; set; }
        /// <summary>
        /// Will be whown on ASPECT_DISPLAY cabcontrol.
        /// </summary>
        public Action<TrackMonitorSignalAspect> SetNextSignalAspect { get; set; }
        /// <summary>
        /// Sets the value for a cabview control.
        /// </summary>
        public Action<int, float> SetCabDisplayControl { get; set; }
        /// <summary>
        /// Sets the name which is to be shown which putting the cursor above a cabview control.
        /// </summary>
        public Action<int, string> SetCustomizedCabviewControlName { get; set; }
        /// <summary>
        /// Requests toggle to and from Manual Mode.
        /// </summary>
        public Action RequestToggleManualMode { get; set; }
        /// <summary>
        /// Requests reset of Out of Control Mode.
        /// </summary>
        public Action ResetOutOfControlMode { get; set; }
        /// <summary>
        /// Get bool parameter in the INI file.
        /// </summary>
        public Func<string, string, bool, bool> GetBoolParameter { get; set; }
        /// <summary>
        /// Get int parameter in the INI file.
        /// </summary>
        public Func<string, string, int, int> GetIntParameter { get; set; }
        /// <summary>
        /// Get int parameter in the INI file.
        /// </summary>
        public Func<string, string, float, float> GetFloatParameter { get; set; }
        /// <summary>
        /// Get string parameter in the INI file.
        /// </summary>
        public Func<string, string, string, string> GetStringParameter { get; set; }

        /// <summary>
        /// Called once at initialization time.
        /// </summary>
        public abstract void Initialize();
        /// <summary>
        /// Called once at initialization time if the train speed is greater than 0.
        /// Set as virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        public virtual void InitializeMoving() { }
        /// <summary>
        /// Called regularly at every simulator update cycle.
        /// </summary>
        public abstract void Update();
        /// <summary>
        /// Called when a TCS event happens (like the alerter button pressed)
        /// </summary>
        /// <param name="evt">The event happened</param>
        /// <param name="message">The message the event wants to communicate. May be empty.</param>
        public abstract void HandleEvent(TCSEvent evt, string message);
        /// <summary>
        /// Called when a power supply event happens (like the circuit breaker closed)
        /// Set at virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        /// <param name="evt">The event happened</param>
        /// <param name="message">The message the event wants to communicate. May be empty.</param>
        public virtual void HandleEvent(PowerSupplyEvent evt, string message) { }
        /// <summary>
        /// Called when player has requested a game save. 
        /// Set at virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        public virtual void Save(BinaryWriter outf) { }
        /// <summary>
        /// Called when player has requested a game restore. 
        /// Set at virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        public virtual void Restore(BinaryReader inf) { }
    }

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct SignalFeatures
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        private static readonly SignalFeatures none = new SignalFeatures(string.Empty, string.Empty, TrackMonitorSignalAspect.None, string.Empty, float.MaxValue, -1f, float.MinValue);
        public static ref readonly SignalFeatures None => ref none;

        public string MainHeadSignalTypeName { get; }

        public string SignalTypeName { get; }
        public TrackMonitorSignalAspect Aspect { get; }
         public string DrawStateName { get; }
        public float Distance { get; }
        public float SpeedLimit { get; }
        public float Altitude { get; }
        public string TextAspect { get; }

        public SignalFeatures(string mainHeadSignalTypeName, string signalTypeName, TrackMonitorSignalAspect aspect, string drawStateName, float distance, float speedLimit, float altitude, string textAspect = "")
        {
            MainHeadSignalTypeName = mainHeadSignalTypeName;
            SignalTypeName = signalTypeName;
            Aspect = aspect;
            DrawStateName = drawStateName;
            Distance = distance;
            SpeedLimit = speedLimit;
            Altitude = altitude;
            TextAspect = textAspect;
        }
    }

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct SpeedPostFeatures
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        private static readonly SpeedPostFeatures none = new SpeedPostFeatures(string.Empty, false, float.MaxValue, -1f, float.MinValue);
        public static ref readonly SpeedPostFeatures None => ref none;

        public readonly string SpeedPostTypeName { get; }
        public readonly bool IsWarning { get; }
        public readonly float DistanceM { get; }
        public readonly float SpeedLimitMpS { get; }
        public readonly float AltitudeM { get; }

        public SpeedPostFeatures(string speedPostTypeName, bool isWarning, float distanceM, float speedLimitMpS, float altitudeM)
        {
            SpeedPostTypeName = speedPostTypeName;
            IsWarning = isWarning;
            DistanceM = distanceM;
            SpeedLimitMpS = speedLimitMpS;
            AltitudeM = altitudeM;
        }
    }

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct TunnelInfo
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        /// <summary>
        /// Distance to tunnel (m)
        /// -1 if train is in tunnel
        /// </summary>
        public float Distance { get; }
        /// <summary>
        /// Tunnel length (m)
        /// If train is in tunnel, remaining distance to exit
        /// </summary>
        public float Length { get; }

        public TunnelInfo(float distance, float length)
        {
            Distance = distance;
            Length = length;
        }
    }

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct MilepostInfo
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        /// <summary>
        /// Distance to milepost (m)
        /// </summary>
        public float Distance { get; }
        /// <summary>
        /// Value of the milepost
        /// </summary>
        public float Value { get; }

        public MilepostInfo(float distance, float value)
        {
            Distance = distance;
            Value = value;
        }
    }
}
