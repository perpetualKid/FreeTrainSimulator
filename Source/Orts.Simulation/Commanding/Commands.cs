// COPYRIGHT 2012, 2013, 2014, 2015 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team.

using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using System;
using System.Diagnostics;   // Used by Trace.Warnings

using Orts.Common;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;
using Orts.Simulation.World;
using System.IO;
using Orts.Simulation.RollingStocks.SubSystems.ControlSystems;

namespace Orts.Simulation.Commanding
{

    #region base Command class
    [Serializable()]
    public abstract class Command : ICommand
    {
        public double Time { get; set; }

        private protected static object CommandReceiver;

        /// <summary>
        /// Each command adds itself to the log when it is constructed.
        /// </summary>
        protected Command(CommandLog log)
        {
            log.CommandAdd(this);
        }

        // Method required by ICommand
        public abstract void Redo();

        public override string ToString()
        {
            return GetType().Name;
        }

        protected string ToString(string param)
        {
            return $"{GetType().Name} - {param}";
        }

        protected string ToString(string param, string target)
        {
            return $"{GetType().Name} - {param}, target= {target}";
        }

        // Method required by ICommand
        public virtual void Report()
        {
            Trace.WriteLine($"Command: {FormatStrings.FormatPreciseTime(Time)} {ToString()}");
        }
    }
    #endregion

    // <Superclasses>
    [Serializable()]
    public abstract class BooleanCommand : Command
    {
        protected bool targetState;

        protected BooleanCommand(CommandLog log, bool targetState)
            : base(log)
        {
            this.targetState = targetState;
        }
    }

    [Serializable()]
    public abstract class IndexCommand : Command
    {
        protected int index;

        protected IndexCommand(CommandLog log, int index)
            : base(log)
        {
            this.index = index;
        }
    }

    /// <summary>
    /// Superclass for continuous commands. Do not create a continuous command until the operation is complete.
    /// </summary>
    [Serializable()]
    public abstract class ContinuousCommand : BooleanCommand
    {
        protected float? target;

        protected ContinuousCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState)
        {
            this.target = target;
            Time = startTime;   // Continuous commands are created at end of change, so overwrite time when command was created
        }

        public override string ToString()
        {
            return ToString((targetState ? "increase" : "decrease"), target.ToString());
        }
    }

    [Serializable()]
    public abstract class PausedCommand : Command
    {
        public double PauseDurationS { get; private set; }

        protected PausedCommand(CommandLog log, double pauseDurationS)
            : base(log)
        {
            PauseDurationS = pauseDurationS;
        }

        public override string ToString()
        {
            return $"{base.ToString()} Paused Duration: {PauseDurationS}";
        }
    }

    [Serializable()]
    public abstract class CameraCommand : Command
    {
        protected CameraCommand(CommandLog log)
            : base(log)
        {
        }
    }

    [Serializable()]
    public sealed class SaveCommand : Command
    {
        public string FileStem { get; private set; }

        public SaveCommand(CommandLog log, string fileStem)
            : base(log)
        {
            FileStem = fileStem;
            Redo();
        }

        public override void Redo()
        {
            // Redo does nothing as SaveCommand is just a marker and saves the fileStem but is not used during replay to redo the save.
            // Report();
        }

        public override string ToString()
        {
            return $"{GetType().Name} to file \"{FileStem}.replay\"";
        }

    }

    // Direction
    [Serializable()]
    public sealed class ReverserCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ReverserCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            if (targetState)
            {
                Receiver.StartReverseIncrease(null);
            }
            else
            {
                Receiver.StartReverseDecrease(null);
            }
        }

        public override string ToString()
        {
            return ToString(targetState ? "step forward" : "step back");
        }
    }

    [Serializable()]
    public sealed class ContinuousReverserCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ContinuousReverserCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.ReverserChangeTo(targetState, target);
        }
    }

    // Power : Raise/lower pantograph
    [Serializable()]
    public sealed class PantographCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        private int item;

        public PantographCommand(CommandLog log, int item, bool targetState)
            : base(log, targetState)
        {
            this.item = item;
            Redo();
        }

        public override void Redo()
        {
            Receiver?.Train?.SignalEvent((targetState ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), item);
        }

        public override string ToString()
        {
            return ToString(targetState ? "raise" : "lower") + $", item = {item}";
        }
    }

    // Power : Close/open circuit breaker switch
    [Serializable()]
    public sealed class CircuitBreakerClosingOrderCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public CircuitBreakerClosingOrderCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseCircuitBreaker : PowerSupplyEvent.OpenCircuitBreaker);
        }

        public override string ToString()
        {
            return ToString(targetState ? "close" : "open");
        }
    }

    // Power : Close circuit breaker button
    [Serializable()]
    public sealed class CircuitBreakerClosingOrderButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public CircuitBreakerClosingOrderButtonCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseCircuitBreakerButtonPressed : PowerSupplyEvent.CloseCircuitBreakerButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    // Power : Open circuit breaker button
    [Serializable()]
    public sealed class CircuitBreakerOpeningOrderButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public CircuitBreakerOpeningOrderButtonCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.OpenCircuitBreakerButtonPressed : PowerSupplyEvent.OpenCircuitBreakerButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    // Power : Give/remove circuit breaker authorization switch
    [Serializable()]
    public sealed class CircuitBreakerClosingAuthorizationCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public CircuitBreakerClosingAuthorizationCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization : PowerSupplyEvent.RemoveCircuitBreakerClosingAuthorization);
        }

        public override string ToString()
        {
            return ToString(targetState ? "given" : "removed");

        }
    }

    // Power : Close/open traction cut-off relay switch
    [Serializable()]
    public sealed class TractionCutOffRelayClosingOrderCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public TractionCutOffRelayClosingOrderCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseTractionCutOffRelay : PowerSupplyEvent.OpenTractionCutOffRelay);
        }

        public override string ToString()
        {
            return ToString(targetState ? "close" : "open");
        }
    }

    // Power : Close traction cut-off relay button
    [Serializable()]
    public sealed class TractionCutOffRelayClosingOrderButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public TractionCutOffRelayClosingOrderButtonCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseTractionCutOffRelayButtonPressed : PowerSupplyEvent.CloseTractionCutOffRelayButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    // Power : Open traction cut-off relay button
    [Serializable()]
    public sealed class TractionCutOffRelayOpeningOrderButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public TractionCutOffRelayOpeningOrderButtonCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.OpenTractionCutOffRelayButtonPressed : PowerSupplyEvent.OpenTractionCutOffRelayButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    // Power : Give/remove traction cut-off relay closing authorization switch
    [Serializable()]
    public sealed class TractionCutOffRelayClosingAuthorizationCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public TractionCutOffRelayClosingAuthorizationCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.GiveTractionCutOffRelayClosingAuthorization : PowerSupplyEvent.RemoveTractionCutOffRelayClosingAuthorization);
        }

        public override string ToString()
        {
            return ToString(targetState ? "given" : "removed");
        }
    }

    // Power : Service retention button
    [Serializable()]
    public sealed class ServiceRetentionButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public ServiceRetentionButtonCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.ServiceRetentionButtonPressed : PowerSupplyEvent.ServiceRetentionButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    // Power : Service retention cancellation button
    [Serializable()]
    public sealed class ServiceRetentionCancellationButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public ServiceRetentionCancellationButtonCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.ServiceRetentionCancellationButtonPressed : PowerSupplyEvent.ServiceRetentionCancellationButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    // Power
    [Serializable()]
    public sealed class PowerCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public PowerCommand(CommandLog log, MSTSLocomotive receiver, bool targetState)
            : base(log, targetState)
        {
            Receiver = receiver;
            Redo();
        }

        public override void Redo()
        {
            Receiver?.SetPower(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "ON" : "OFF");
        }
    }

    // MU commands connection
    [Serializable()]
    public sealed class ToggleMUCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleMUCommand(CommandLog log, MSTSLocomotive receiver, bool targetState)
            : base(log, targetState)
        {
            Receiver = receiver;
            Redo();
        }

        public override void Redo()
        {
            Receiver?.ToggleMUCommand(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "ON" : "OFF");
        }
    }

    [Serializable()]
    public sealed class NotchedThrottleCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public NotchedThrottleCommand(CommandLog log, bool targetState) : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.AdjustNotchedThrottle(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "step forward" : "step back");
        }
    }

    [Serializable()]
    public sealed class ContinuousThrottleCommand : ContinuousCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ContinuousThrottleCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ThrottleChangeTo(targetState, target);
        }
    }

    // Brakes
    [Serializable()]
    public sealed class TrainBrakeCommand : ContinuousCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public TrainBrakeCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.TrainBrakeChangeTo(targetState, target);
        }
    }

    [Serializable()]
    public sealed class EngineBrakeCommand : ContinuousCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public EngineBrakeCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.EngineBrakeChangeTo(targetState, target);
        }
    }

    [Serializable()]
    public sealed class BrakemanBrakeCommand : ContinuousCommand
    {
        public static MSTSLocomotive Receiver { get; set; }
        public BrakemanBrakeCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.BrakemanBrakeChangeTo(targetState, target);
            // Report();
        }
    }

    [Serializable()]
    public sealed class DynamicBrakeCommand : ContinuousCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public DynamicBrakeCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.DynamicBrakeChangeTo(targetState, target);
        }
    }

    [Serializable()]
    public sealed class InitializeBrakesCommand : Command
    {
        public static Train Receiver { get; set; }

        public InitializeBrakesCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.UnconditionalInitializeBrakes();
        }
    }

    [Serializable()]
    public sealed class ResetOutOfControlModeCommand : Command
    {
        public static Train Receiver { get; set; }
        public ResetOutOfControlModeCommand(CommandLog log) : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ManualResetOutOfControlMode();
            Receiver.SignalEvent(TCSEvent.ManualResetOutOfControlMode);
        }
    }

    [Serializable()]
    public sealed class EmergencyPushButtonCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public EmergencyPushButtonCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.EmergencyButtonPressed = targetState;
            Receiver.TrainBrakeController.EmergencyBrakingPushButton = targetState;
        }
    }

    [Serializable()]
    public sealed class BailOffCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public BailOffCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.SetBailOff(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "disengage" : "engage");
        }
    }

    [Serializable()]
    public sealed class QuickReleaseCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public QuickReleaseCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.TrainBrakeController.QuickReleaseButtonPressed = targetState;
            // Report();
        }

        public override string ToString()
        {
            return ToString(targetState ? "off" : "on");
        }
    }

    [Serializable()]
    public sealed class BrakeOverchargeCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public BrakeOverchargeCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.TrainBrakeController.OverchargeButtonPressed = targetState;
            // Report();
        }

        public override string ToString()
        {
            return ToString(targetState ? "off" : "on");
        }
    }

    [Serializable()]
    public sealed class HandbrakeCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public HandbrakeCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.SetTrainHandbrake(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "apply" : "release");
        }
    }

    [Serializable()]
    public sealed class WagonHandbrakeCommand : BooleanCommand
    {
        public static MSTSWagon Receiver { get; set; }

        public WagonHandbrakeCommand(CommandLog log, MSTSWagon car, bool targetState)
            : base(log, targetState)
        {
            Receiver = car;
            Redo();
        }

        public override void Redo()
        {
            Receiver.SetWagonHandbrake(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "apply" : "release");
        }
    }

    [Serializable()]
    public sealed class RetainersCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public RetainersCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.SetTrainRetainers(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "apply" : "release");
        }
    }

    [Serializable()]
    public sealed class BrakeHoseConnectCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public BrakeHoseConnectCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.BrakeHoseConnect(targetState);
        }

        public override string ToString()
        {
            return ToString(targetState ? "connect" : "disconnect");
        }
    }

    [Serializable()]
    public sealed class WagonBrakeHoseConnectCommand : BooleanCommand
    {
        public static MSTSWagon Receiver { get; set; }

        public WagonBrakeHoseConnectCommand(CommandLog log, MSTSWagon car, bool targetState)
            : base(log, targetState)
        {
            Receiver = car;
            Redo();
        }

        public override void Redo()
        {
            Receiver.BrakeSystem.FrontBrakeHoseConnected = targetState;
        }

        public override string ToString()
        {
            return ToString(targetState ? "connect" : "disconnect");
        }
    }

    [Serializable()]
    public sealed class ToggleAngleCockACommand : BooleanCommand
    {
        public static MSTSWagon Receiver { get; set; }

        public ToggleAngleCockACommand(CommandLog log, MSTSWagon car, bool targetState)
            : base(log, targetState)
        {
            Receiver = car;
            Redo();
        }

        public override void Redo()
        {
            Receiver.BrakeSystem.AngleCockAOpen = targetState;
            // Report();
        }

        public override string ToString()
        {
            return ToString(targetState ? "open" : "close");
        }
    }

    [Serializable()]
    public sealed class ToggleAngleCockBCommand : BooleanCommand
    {
        public static MSTSWagon Receiver { get; set; }

        public ToggleAngleCockBCommand(CommandLog log, MSTSWagon car, bool targetState)
            : base(log, targetState)
        {
            Receiver = car;
            Redo();
        }

        public override void Redo()
        {
            Receiver.BrakeSystem.AngleCockBOpen = targetState;
        }

        public override string ToString()
        {
            return ToString(targetState ? "open" : "close");
        }
    }

    [Serializable()]
    public sealed class ToggleBleedOffValveCommand : BooleanCommand
    {
        public static MSTSWagon Receiver { get; set; }

        public ToggleBleedOffValveCommand(CommandLog log, MSTSWagon car, bool targetState)
            : base(log, targetState)
        {
            Receiver = car;
            Redo();
        }

        public override void Redo()
        {
            Receiver.BrakeSystem.BleedOffValveOpen = targetState;
        }

        public override string ToString()
        {
            return ToString(targetState ? "open" : "close");
        }
    }

    [Serializable()]
    public sealed class SanderCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public SanderCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            if (targetState)
            {
                if (!Receiver.Sander)
                    Receiver.Train.SignalEvent(TrainEvent.SanderOn);
            }
            else
            {
                Receiver.Train.SignalEvent(TrainEvent.SanderOff);
            }
        }

        public override string ToString()
        {
            return ToString(targetState ? "on" : "off");
        }
    }

    [Serializable()]
    public sealed class AlerterCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public AlerterCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            if (targetState)
                Receiver.SignalEvent(TrainEvent.VigilanceAlarmReset); // There is no Event.VigilanceAlarmResetReleased
            Receiver.AlerterPressed(targetState);
        }
    }

    [Serializable()]
    public sealed class VacuumExhausterCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public VacuumExhausterCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            if (targetState)
            {
                if (!Receiver.VacuumExhausterPressed)
                    Receiver.Train.SignalEvent(TrainEvent.VacuumExhausterOn);
            }
            else
            {
                Receiver.Train.SignalEvent(TrainEvent.VacuumExhausterOff);
            }
        }

        public override string ToString()
        {
            return ToString(targetState ? "fast" : "normal");
        }
    }

    [Serializable()]
    public sealed class HornCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public HornCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ManualHorn = targetState;
            if (targetState)
            {
                Receiver.AlerterReset(TCSEvent.HornActivated);
                Simulator.Instance.HazardManager.Horn();
            }
        }

        public override string ToString()
        {
            return ToString(targetState ? "sound" : "off");
        }
    }

    [Serializable()]
    public sealed class BellCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public BellCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ManualBell = targetState;
        }

        public override string ToString()
        {
            return ToString(targetState ? "ring" : "off");
        }
    }

    [Serializable()]
    public sealed class ToggleCabLightCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleCabLightCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ToggleCabLight();
        }

        public override string ToString()
        {
            return GetType().Name;
        }
    }

    [Serializable()]
    public sealed class HeadlightCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }
        public static bool MasterKeyHeadlightControl => Receiver.LocomotivePowerSupply.MasterKey.HeadlightControl;

        public HeadlightCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            if (targetState)
            {
                switch (Receiver.Headlight)
                {
                    case 0:
                        if (!MasterKeyHeadlightControl)
                        {
                            Receiver.Headlight = 1;
                            Simulator.Instance.Confirmer.Confirm(CabControl.Headlight, CabSetting.Neutral);
                        }
                        break;
                    case 1:
                        Receiver.Headlight = 2;
                        Simulator.Instance.Confirmer.Confirm(CabControl.Headlight, CabSetting.On);
                        break;
                }
                Receiver.SignalEvent(TrainEvent.LightSwitchToggle);
            }
            else
            {
                switch (Receiver.Headlight)
                {
                    case 1:
                        if (!MasterKeyHeadlightControl)
                        {
                            Receiver.Headlight = 0;
                            Simulator.Instance.Confirmer.Confirm(CabControl.Headlight, CabSetting.Off);
                        }
                        break;
                    case 2:
                        Receiver.Headlight = 1;
                        Simulator.Instance.Confirmer.Confirm(CabControl.Headlight, CabSetting.Neutral);
                        break;
                }
                Receiver.SignalEvent(TrainEvent.LightSwitchToggle);
            }
        }
    }

    [Serializable()]
    public sealed class WipersCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public WipersCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ToggleWipers(targetState);
        }
    }

    [Serializable()]
    public sealed class ToggleDoorsLeftCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public ToggleDoorsLeftCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            bool right = Receiver.GetCabFlipped() ^ Receiver.Flipped;
            var state = Receiver.Train.GetDoorState(right);
            Receiver.Train.ToggleDoors(right, state == DoorState.Closed || state == DoorState.Closing);
        }
    }

    [Serializable()]
    public sealed class ToggleDoorsRightCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public ToggleDoorsRightCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            bool right = !Receiver.GetCabFlipped() ^ Receiver.Flipped;
            var state = Receiver.Train.GetDoorState(right);
            Receiver.Train.ToggleDoors(right, state == DoorState.Closed || state == DoorState.Closing);
        }
    }

    [Serializable()]
    public sealed class ToggleMirrorsCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public ToggleMirrorsCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ToggleMirrors();
        }
    }

    [Serializable()]
    public sealed class ToggleBatterySwitchCommand : BooleanCommand
    {
        public static BatterySwitch Receiver { get; set; }

        public ToggleBatterySwitchCommand(CommandLog log, MSTSWagon wagon, bool toState)
            : base(log, toState)
        {
            Receiver = wagon?.PowerSupply?.BatterySwitch;
            Redo();
        }

        public override void Redo()
        {
            if (Receiver?.Mode == BatterySwitch.ModeType.Switch)
            {
                Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseBatterySwitch : PowerSupplyEvent.OpenBatterySwitch);
            }
            else if (Receiver?.Mode == BatterySwitch.ModeType.PushButtons)
            {
                Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseBatterySwitchButtonPressed : PowerSupplyEvent.OpenBatterySwitchButtonPressed);
                Receiver?.Update(0f);
                Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseBatterySwitchButtonReleased : PowerSupplyEvent.OpenBatterySwitchButtonReleased);
            }
        }
    }

    [Serializable()]
    public sealed class BatterySwitchCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public BatterySwitchCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseBatterySwitch : PowerSupplyEvent.OpenBatterySwitch);
        }

        public override string ToString()
        {
            return ToString(targetState ? "close" : "open");
        }
    }

    [Serializable()]
    public sealed class BatterySwitchCloseButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public BatterySwitchCloseButtonCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.CloseBatterySwitchButtonPressed : PowerSupplyEvent.CloseBatterySwitchButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    [Serializable()]
    public sealed class BatterySwitchOpenButtonCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public BatterySwitchOpenButtonCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.OpenBatterySwitchButtonPressed : PowerSupplyEvent.OpenBatterySwitchButtonReleased);
        }

        public override string ToString()
        {
            return ToString(targetState ? "pressed" : "released");
        }
    }

    [Serializable()]
    public sealed class ToggleMasterKeyCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public ToggleMasterKeyCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.TurnOnMasterKey : PowerSupplyEvent.TurnOffMasterKey);
        }

        public override string ToString()
        {
            return ToString(targetState ? "turned on" : "turned off");
        }
    }

    [Serializable()]
    public sealed class ElectricTrainSupplyCommand : BooleanCommand
    {
        public static ILocomotivePowerSupply Receiver { get; set; }

        public ElectricTrainSupplyCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.HandleEvent(targetState ? PowerSupplyEvent.SwitchOnElectricTrainSupply : PowerSupplyEvent.SwitchOffElectricTrainSupply);
        }

        public override string ToString()
        {
            return ToString(targetState ? "switched on" : "switched off");
        }
    }

    [Serializable()]
    public sealed class ConnectElectricTrainSupplyCableCommand : BooleanCommand
    {
        public static MSTSWagon Receiver { get; set; }

        public ConnectElectricTrainSupplyCableCommand(CommandLog log, MSTSWagon car, bool toState)
            : base(log, toState)
        {
            Receiver = car;
            Redo();
        }

        public override void Redo()
        {
            Receiver.PowerSupply.FrontElectricTrainSupplyCableConnected = targetState;
        }

        public override string ToString()
        {
            return ToString(targetState ? "connect" : "disconnect");
        }
    }

    // Distributed Power controls
    [Serializable()]
    public sealed class DistributedPowerMoveToFrontCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public DistributedPowerMoveToFrontCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.DistributedPowerMoveToFront();
            // Report();
        }
    }

    [Serializable()]
    public sealed class DistributedPowerMoveToBackCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public DistributedPowerMoveToBackCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.DistributedPowerMoveToBack();
            // Report();
        }
    }

    [Serializable()]
    public sealed class DistributedPowerIdleCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public DistributedPowerIdleCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.DistributedPowerIdle();
            // Report();
        }
    }

    [Serializable()]
    public sealed class DistributedPowerTractionCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public DistributedPowerTractionCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.DistributedPowerTraction();
            // Report();
        }
    }

    [Serializable()]
    public sealed class DistributedPowerDynamicBrakeCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public DistributedPowerDynamicBrakeCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.DistributedPowerDynamicBrake();
            // Report();
        }
    }

    [Serializable()]
    public sealed class DistributedPowerIncreaseCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public DistributedPowerIncreaseCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.DistributedPowerIncrease();
            // Report();
        }
    }

    [Serializable()]
    public sealed class DistributedPowerDecreaseCommand : Command
    {
        public static MSTSWagon Receiver { get; set; }

        public DistributedPowerDecreaseCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.DistributedPowerDecrease();
            // Report();
        }
    }


    // Steam controls
    [Serializable()]
    public sealed class ContinuousSteamHeatCommand : ContinuousCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ContinuousSteamHeatCommand(CommandLog log, int injector, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            {
                Receiver.SteamHeatChangeTo(targetState, target);
            }
        }
    }

    // Large Ejector command
    [Serializable()]
    public sealed class ContinuousLargeEjectorCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ContinuousLargeEjectorCommand(CommandLog log, int injector, bool toState, float? target, double startTime)
            : base(log, toState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            {
                Receiver.LargeEjectorChangeTo(targetState, target);
            }
            // Report();
        }
    }

    [Serializable()]
    public sealed class ContinuousSmallEjectorCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ContinuousSmallEjectorCommand(CommandLog log, int injector, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            {
                Receiver.SmallEjectorChangeTo(targetState, target);
            }
        }
    }

    [Serializable()]
    public sealed class ContinuousInjectorCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }
        private readonly int injector;

        public ContinuousInjectorCommand(CommandLog log, int injector, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            this.injector = injector;
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            switch (injector)
            {
                case 1:
                    { Receiver.Injector1ChangeTo(targetState, target); break; }
                case 2:
                    { Receiver.Injector2ChangeTo(targetState, target); break; }
            }
        }

        public override string ToString()
        {
            return $"Command: {FormatStrings.FormatPreciseTime(Time)} {GetType().Name} {injector}"
                + (targetState ? "open" : "close") + ", target = " + target.ToString();
        }
    }

    [Serializable()]
    public sealed class ToggleInjectorCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }
        private readonly int injector;

        public ToggleInjectorCommand(CommandLog log, int injector)
            : base(log)
        {
            this.injector = injector;
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            switch (injector)
            {
                case 1:
                    { Receiver.ToggleInjector1(); break; }
                case 2:
                    { Receiver.ToggleInjector2(); break; }
            }
        }

        public override string ToString()
        {
            return $"{base.ToString()}{injector}";
        }
    }

    [Serializable()]
    public sealed class ContinuousBlowerCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ContinuousBlowerCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.BlowerChangeTo(targetState, target);
        }
    }

    [Serializable()]
    public sealed class ContinuousDamperCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ContinuousDamperCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.DamperChangeTo(targetState, target);
        }
    }

    [Serializable()]
    public sealed class ContinuousFireboxDoorCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ContinuousFireboxDoorCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.FireboxDoorChangeTo(targetState, target);
        }
    }

    [Serializable()]
    public sealed class ContinuousFiringRateCommand : ContinuousCommand
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ContinuousFiringRateCommand(CommandLog log, bool targetState, float? target, double startTime)
            : base(log, targetState, target, startTime)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.FiringRateChangeTo(targetState, target);
        }
    }

    [Serializable()]
    public sealed class ToggleManualFiringCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ToggleManualFiringCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.ToggleManualFiring();
        }
    }

    [Serializable()]
    public sealed class AIFireOnCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public AIFireOnCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.AIFireOn();

        }

    }

    [Serializable()]
    public sealed class AIFireOffCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public AIFireOffCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.AIFireOff();

        }

    }

    [Serializable()]
    public sealed class AIFireResetCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public AIFireResetCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.AIFireReset();

        }

    }

    [Serializable()]
    public sealed class FireShovelfullCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public FireShovelfullCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.FireShovelfull();
        }
    }

    [Serializable()]
    public sealed class ResetOdometerCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ResetOdometerCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.OdometerReset(targetState);
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    [Serializable()]
    public sealed class ToggleOdometerDirectionCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleOdometerDirectionCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.OdometerToggleDirection();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    // Cylinder Cocks command
    [Serializable()]
    public sealed class ToggleWaterScoopCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleWaterScoopCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.ToggleWaterScoop();
        }
    }


    [Serializable()]
    public sealed class ToggleCylinderCocksCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ToggleCylinderCocksCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.ToggleCylinderCocks();
        }
    }

    // Compound Valve command
    [Serializable()]
    public sealed class ToggleCylinderCompoundCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ToggleCylinderCompoundCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.ToggleCylinderCompound();
        }
    }

    [Serializable()]
    public sealed class ToggleBlowdownValveCommand : Command
    {
        public static MSTSSteamLocomotive Receiver { get; set; }

        public ToggleBlowdownValveCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver == null)
                return;
            Receiver.ToggleBlowdownValve();
            // Report();
        }
    }

    // Diesel player engine on / off command
    [Serializable()]
    public sealed class TogglePlayerEngineCommand : Command
    {
        public static MSTSDieselLocomotive Receiver { get; set; }

        public TogglePlayerEngineCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.LocomotivePowerSupply.HandleEvent(PowerSupplyEvent.TogglePlayerEngine);
        }
    }

    // Diesel helpers engine on / off command
    [Serializable()]
    public sealed class ToggleHelpersEngineCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleHelpersEngineCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.LocomotivePowerSupply.HandleEvent(PowerSupplyEvent.ToggleHelperEngine);
        }
    }

    // Cab radio switch on-switch off command
    [Serializable()]
    public sealed class CabRadioCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public CabRadioCommand(CommandLog log, bool targetState)
            : base(log, targetState)
        {
            Redo();
        }

        public override void Redo()
        {
            if (Receiver != null)
            {
                Receiver.ToggleCabRadio(targetState);
            }
        }

        public override string ToString()
        {
            return ToString(targetState ? "switched on" : "switched off");
        }
    }

    #region TurnTable
    [Serializable()]
    public sealed class TurntableClockwiseCommand : Command
    {
        public static MovingTable Receiver { get; set; }
        public TurntableClockwiseCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.StartContinuous(true);
        }

        public override string ToString()
        {
            return ToString("Clockwise");
        }
    }


    [Serializable()]
    public sealed class TurntableClockwiseTargetCommand : Command
    {
        public static MovingTable Receiver { get; set; }
        public TurntableClockwiseTargetCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ComputeTarget(true);
        }

        public override string ToString()
        {
            return ToString("Clockwise with target");
        }
    }

    [Serializable()]
    public sealed class TurntableCounterclockwiseCommand : Command
    {
        public static MovingTable Receiver { get; set; }
        public TurntableCounterclockwiseCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.StartContinuous(false);
        }

        public override string ToString()
        {
            return ToString("Counterclockwise");
        }
    }


    [Serializable()]
    public sealed class TurntableCounterclockwiseTargetCommand : Command
    {
        public static MovingTable Receiver { get; set; }
        public TurntableCounterclockwiseTargetCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.ComputeTarget(false);
        }

        public override string ToString()
        {
            return ToString("Counterclockwise with target");
        }
    }
    #endregion

    #region TCS
    /// <summary>
    /// This is the list of commands available for TCS scripts; they are generic commands, whose action will specified by the active script
    /// All commands record the time when the command is created, but a continuous command backdates the time to when the key
    /// was pressed.
    /// </summary>

    // Generic TCS button command
    [Serializable()]
    public sealed class TCSButtonCommand : BooleanCommand
    {
        public int CommandIndex { get; private set; }
        public static ScriptedTrainControlSystem Receiver { get; set; }

        public TCSButtonCommand(CommandLog log, bool toState, int commandIndex)
            : base(log, toState)
        {
            CommandIndex = commandIndex;
            Redo();
        }

        public override void Redo()
        {
            if (Receiver != null)
            {
                Receiver.TCSCommandButtonDown[CommandIndex] = targetState;
                Receiver.HandleEvent(targetState ? TCSEvent.GenericTCSButtonPressed : TCSEvent.GenericTCSButtonReleased, CommandIndex);
            }

        }

        public override string ToString()
        {
            return ToString(targetState ? "on" : "off");
        }
    }

    // Generic TCS switch command
    [Serializable()]
    public sealed class TCSSwitchCommand : BooleanCommand
    {
        public int CommandIndex { get; private set; }
        public static ScriptedTrainControlSystem Receiver { get; set; }

        public TCSSwitchCommand(CommandLog log, bool toState, int commandIndex)
            : base(log, toState)
        {
            CommandIndex = commandIndex;
            Redo();
        }

        public override void Redo()
        {
            if (Receiver != null)
            {
                Receiver.TCSCommandSwitchOn[CommandIndex] = targetState;
                Receiver.HandleEvent(targetState ? TCSEvent.GenericTCSSwitchOn : TCSEvent.GenericTCSSwitchOff, CommandIndex);
            }
        }

        public override string ToString()
        {
            return ToString(targetState ? "on" : "off");
        }
    }
    #endregion

    [Serializable()]
    public sealed class ToggleGenericItem1Command : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleGenericItem1Command(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.GenericItem1Toggle();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    [Serializable()]
    public sealed class ToggleGenericItem2Command : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleGenericItem2Command(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.GenericItem2Toggle();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    // EOT Commands

    [Serializable()]
    public sealed class EOTCommTestCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public EOTCommTestCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.Train?.EndOfTrainDevice?.CommTest();
        }
    }

    [Serializable()]
    public sealed class EOTDisarmCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public EOTDisarmCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.Train?.EndOfTrainDevice?.Disarm();
        }
    }

    [Serializable()]
    public sealed class EOTArmTwoWayCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public EOTArmTwoWayCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.Train?.EndOfTrainDevice?.ArmTwoWay();
        }
    }

    [Serializable()]
    public sealed class EOTEmergencyBrakeCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }

        public EOTEmergencyBrakeCommand(CommandLog log, bool toState)
            : base(log, toState)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver?.Train?.EndOfTrainDevice?.EmergencyBrake(targetState);
        }
    }

    [Serializable()]
    public sealed class ToggleEOTEmergencyBrakeCommand : Command
    {
        public static MSTSLocomotive Receiver { get; set; }

        public ToggleEOTEmergencyBrakeCommand(CommandLog log)
            : base(log)
        {
            Redo();
        }

        public override void Redo()
        {
            Receiver.Train.EndOfTrainDevice?.EmergencyBrake(!Receiver.Train.EndOfTrainDevice.EOTEmergencyBrakingOn);
        }
    }

    [Serializable()]
    public sealed class EOTMountCommand : BooleanCommand
    {
        public static MSTSLocomotive Receiver { get; set; }
        public string PickedEOTType { get; set; }

        public EOTMountCommand(CommandLog log, bool toState, string pickedEOTType)
            : base(log, toState)
        {
            PickedEOTType = pickedEOTType;
            Redo();
        }

        public override void Redo()
        {
            if (Receiver?.Train != null)
            {
                if (targetState)
                {
                    string wagonFilePath = PickedEOTType.ToLowerInvariant();
                    try
                    {
                        EndOfTrainDevice eot = (EndOfTrainDevice)RollingStock.Load(Receiver.Train, wagonFilePath);
                        eot.CarID = $"{Receiver.Train.Number} - EOT";
                        eot.Train.EndOfTrainDevice = eot;
                    }
                    catch (Exception error)
                    {
                        Trace.WriteLine(new FileLoadException(wagonFilePath, error));
                        return;
                    }
                    Receiver.Train.CalculatePositionOfEOT();
                    Receiver.Train.PhysicsUpdate(0);
                }
                else
                {
                    Receiver.Train.RecalculateRearTDBTraveller();
                    var car = Receiver.Train.Cars[^1];
                    car.Train = null;
                    car.IsPartOfActiveTrain = false;  // to stop sounds
                    Receiver.Train.Cars.Remove(car);
                    Receiver.Train.EndOfTrainDevice = null;
                    Receiver.Train.PhysicsUpdate(0);
                }
            }
        }
    }

}
