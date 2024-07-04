using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;

namespace Orts.Scripting.Api
{
    public abstract class BrakeController : TrainScriptBase
    {
        /// <summary>
        /// True if the driver has asked for an emergency braking (push button)
        /// </summary>
        public Func<bool> EmergencyBrakingPushButton { get; set; }
        /// <summary>
        /// True if the TCS has asked for an emergency braking
        /// </summary>
        public Func<bool> TCSEmergencyBraking { get; set; }
        /// <summary>
        /// True if the TCS has asked for a full service braking
        /// </summary>
        public Func<bool> TCSFullServiceBraking { get; set; }
        /// <summary>
        /// True if the driver has pressed the Quick Release button
        /// </summary>
        public Func<bool> QuickReleaseButtonPressed { get; set; }
        /// <summary>
        /// True if the driver has pressed the Overcharge button
        /// </summary>
        public Func<bool> OverchargeButtonPressed { get; set; }
        /// <summary>
        /// True if low voltage power supply is switched on.
        /// </summary>
        public Func<bool> IsLowVoltagePowerSupplyOn { get; set; }
        /// <summary>
        /// True if cab power supply is switched on.
        /// </summary>
        public Func<bool> IsCabPowerSupplyOn { get; set; }
        /// <summary>
        /// <summary>
        /// Main reservoir pressure
        /// </summary>
        public Func<float> MainReservoirPressureBar { get; set; }
        /// <summary>
        /// Maximum pressure in the brake pipes and the equalizing reservoir
        /// </summary>
        public Func<float> MaxPressureBar { get; set; }
        /// <summary>
        /// Maximum pressure in the brake pipes when they are overcharged
        /// </summary>
        public Func<float> MaxOverchargePressureBar { get; set; }
        /// <summary>
        /// Release rate of the equalizing reservoir
        /// </summary>
        public Func<float> ReleaseRateBarpS { get; set; }
        /// <summary>
        /// Quick release rate of the equalizing reservoir
        /// </summary>
        public Func<float> QuickReleaseRateBarpS { get; set; }
        /// <summary>
        /// Pressure decrease rate of equalizing reservoir when eliminating overcharge
        /// </summary>
        public Func<float> OverchargeEliminationRateBarpS { get; set; }
        /// <summary>
        /// Slow application rate of the equalizing reservoir
        /// </summary>
        public Func<float> SlowApplicationRateBarpS { get; set; }
        /// <summary>
        /// Apply rate of the equalizing reservoir
        /// </summary>
        public Func<float> ApplyRateBarpS { get; set; }
        /// <summary>
        /// Emergency rate of the equalizing reservoir
        /// </summary>
        public Func<float> EmergencyRateBarpS { get; set; }
        /// <summary>
        /// Depressure needed in order to obtain the full service braking
        /// </summary>
        public Func<float> FullServReductionBar { get; set; }
        /// <summary>
        /// Release rate of the equalizing reservoir
        /// </summary>
        public Func<float> MinReductionBar { get; set; }
        /// <summary>
        /// Current value of the brake controller
        /// </summary>
        public Func<float> CurrentValue { get; set; }
        /// <summary>
        /// Intermediate value of the brake controller
        /// </summary>
        public Func<float> IntermediateValue { get; set; }
        /// <summary>
        /// Minimum value of the brake controller
        /// </summary>
        public Func<float> MinimumValue { get; set; }
        /// <summary>
        /// Maximum value of the brake controller
        /// </summary>
        public Func<float> MaximumValue { get; set; }
        /// <summary>
        /// Step size of the brake controller
        /// </summary>
        public Func<float> StepSize { get; set; }
        /// <summary>
        /// State of the brake pressure (1 = increasing, -1 = decreasing)
        /// </summary>
        public Func<float> UpdateValue { get; set; }
        /// <summary>
        /// Gives the list of notches
        /// </summary>
        public Func<List<IControllerNotch>> Notches { get; set; }
        /// <summary>
        /// Fraction of train brake demanded by cruise control
        /// </summary>
        public Func<float> CruiseControlBrakeDemand { get; set; }
        /// <summary>
        /// Current notch of the brake controller
        /// </summary>
        public Func<int> CurrentNotch { get; set; }
        /// <summary>
        /// Sets the current value of the brake controller lever
        /// </summary>
        public Action<float> SetCurrentValue { get; set; }
        /// <summary>
        /// Sets the state of the brake pressure (1 = increasing, -1 = decreasing)
        /// </summary>
        public Action<float> SetUpdateValue { get; set; }
        /// <summary>
        /// Sets the dynamic brake intervention value
        /// </summary>
        public Action<float> SetDynamicBrakeIntervention { get; set; }

        /// <summary>
        /// Called once at initialization time.
        /// </summary>
        public abstract void Initialize();
        /// <summary>
        /// Called regularly at every simulator update cycle.
        /// </summary>
        ///
        public abstract void InitializeMoving();
        /// <summary>
        /// Called when starting speed > 0
        /// </summary>
        /// 
        public abstract float Update(double elapsedSeconds);
        /// <summary>
        /// Called regularly at every simulator update cycle.
        /// </summary>
        public abstract Tuple<double, double> UpdatePressure(double pressureBar, double epPressureBar, double elapsedClockSeconds);
        /// <summary>
        /// Called regularly at every simulator update cycle.
        /// </summary>
        public abstract double UpdateEngineBrakePressure(double pressureBar, double elapsedClockSeconds);
        /// <summary>
        /// Called when an event happens (like the alerter button pressed)
        /// </summary>
        /// <param name="evt">The event happened</param>
        public abstract void HandleEvent(BrakeControllerEvent evt);
        /// <summary>
        /// Called when an event happens (like the alerter button pressed)
        /// </summary>
        /// <param name="evt">The event happened</param>
        /// <param name="value">The value assigned to the event (a target for example). May be null.</param>
        public abstract void HandleEvent(BrakeControllerEvent evt, float? value);
        /// <summary>
        /// Called in order to check if the controller is valid
        /// </summary>
        public abstract bool IsValid();
        /// <summary>
        /// Called in order to get a state for the debug overlay
        /// </summary>
        public abstract ControllerState State { get; }
        /// <summary>
        /// Called in order to get a state fraction for the debug overlay
        /// </summary>
        /// <returns>The nullable state fraction</returns>
        public abstract float StateFraction { get; }

        public static bool IsEmergencyState(ControllerState state)
        {
            return state is ControllerState.Emergency or ControllerState.StraightEmergency or ControllerState.TCSEmergency or ControllerState.EBPB;
        }
    }

}
