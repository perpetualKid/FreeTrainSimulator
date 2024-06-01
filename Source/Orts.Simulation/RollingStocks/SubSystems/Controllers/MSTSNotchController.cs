// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Scripting.Api;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
    public class MSTSNotch : IControllerNotch, ISaveStateApi<NotchSaveState>
    {
        public float Value { get; set; }
        public bool Smooth { get; set; }
        public ControllerState NotchStateType { get; set; }

        public MSTSNotch() { }

        public MSTSNotch(float v, int s, string type, STFReader stf)
        {
            Value = v;
            Smooth = s != 0;
            NotchStateType = ControllerState.Dummy;  // Default to a dummy controller state if no valid alternative state used
            string lower;
            if (type.StartsWith("trainbrakescontroller", StringComparison.OrdinalIgnoreCase))
                lower = type[21..];
            else if (type.StartsWith("enginebrakescontroller", StringComparison.OrdinalIgnoreCase))
                lower = type[22..];
            else if (type.StartsWith("brakemanbrakescontroller", StringComparison.OrdinalIgnoreCase))
                lower = type.Substring(24);
            else
                lower = type;
            switch (lower.ToLowerInvariant())
            {
                case "dummy":
                    break;
                case ")":
                    break;
                case "releasestart":
                    NotchStateType = ControllerState.Release;
                    break;
                case "fullquickreleasestart":
                    NotchStateType = ControllerState.FullQuickRelease;
                    break;
                case "runningstart":
                    NotchStateType = ControllerState.Running;
                    break;
                case "selflapstart":
                    NotchStateType = ControllerState.SelfLap;
                    break;
                case "holdstart":
                    NotchStateType = ControllerState.Hold;
                    break;
                case "straightbrakingreleaseonstart":
                    NotchStateType = ControllerState.StraightReleaseOn;
                    break;
                case "straightbrakingreleaseoffstart":
                    NotchStateType = ControllerState.StraightReleaseOff;
                    break;
                case "straightbrakingreleasestart":
                    NotchStateType = ControllerState.StraightRelease;
                    break;
                case "straightbrakinglapstart":
                    NotchStateType = ControllerState.StraightLap;
                    break;
                case "straightbrakingapplystart":
                    NotchStateType = ControllerState.StraightApply;
                    break;
                case "straightbrakingapplyallstart":
                    NotchStateType = ControllerState.StraightApplyAll;
                    break;
                case "straightbrakingemergencystart":
                    NotchStateType = ControllerState.StraightEmergency;
                    break;
                case "holdlappedstart":
                    NotchStateType = ControllerState.Lap;
                    break;
                case "neutralhandleoffstart":
                    NotchStateType = ControllerState.Neutral;
                    break;
                case "graduatedselflaplimitedstart":
                    NotchStateType = ControllerState.GSelfLap;
                    break;
                case "graduatedselflaplimitedholdingstart":
                    NotchStateType = ControllerState.GSelfLapH;
                    break;
                case "applystart":
                    NotchStateType = ControllerState.Apply;
                    break;
                case "continuousservicestart":
                    NotchStateType = ControllerState.ContServ;
                    break;
                case "suppressionstart":
                    NotchStateType = ControllerState.Suppression;
                    break;
                case "fullservicestart":
                    NotchStateType = ControllerState.FullServ;
                    break;
                case "emergencystart":
                    NotchStateType = ControllerState.Emergency;
                    break;
                case "minimalreductionstart":
                    NotchStateType = ControllerState.MinimalReduction;
                    break;
                case "epapplystart":
                    NotchStateType = ControllerState.EPApply;
                    break;
                case "epholdstart":
                    NotchStateType = ControllerState.SelfLap;
                    break;
                case "smeholdstart":
                    NotchStateType = ControllerState.SMESelfLap;
                    break;
                case "smeonlystart":
                    NotchStateType = ControllerState.SMEOnly;
                    break;
                case "smefullservicestart":
                    NotchStateType = ControllerState.SMEFullServ;
                    break;
                case "smereleasestart":
                    NotchStateType = ControllerState.SMEReleaseStart;
                    break;
                case "vacuumcontinuousservicestart":
                    NotchStateType = ControllerState.VacContServ;
                    break;
                case "vacuumapplycontinuousservicestart":
                    NotchStateType = ControllerState.VacApplyContServ;
                    break;
                case "manualbrakingstart":
                    NotchStateType = ControllerState.ManualBraking;
                    break;
                case "brakenotchstart":
                    NotchStateType = ControllerState.BrakeNotch;
                    break;
                case "overchargestart":
                    NotchStateType = ControllerState.Overcharge;
                    break;
                case "slowservicestart":
                    NotchStateType = ControllerState.SlowService;
                    break;
                default:
                    STFException.TraceInformation(stf, "Skipped unknown notch type " + type);
                    break;
            }
        }
        public MSTSNotch(float v, bool s, int t)
        {
            Value = v;
            Smooth = s;
            NotchStateType = (ControllerState)t;
        }

        public MSTSNotch(IControllerNotch other)
        {
            Value = other?.Value ?? throw new ArgumentNullException(nameof(other));
            Smooth = other.Smooth;
            NotchStateType = other.NotchStateType;
        }

        public MSTSNotch Clone()
        {
            return new MSTSNotch(this);
        }

        public ValueTask<NotchSaveState> Snapshot()
        {
            return ValueTask.FromResult(new NotchSaveState()
            { 
                CurrentValue = Value,
                Smooth = Smooth,
                NotchStateType = NotchStateType,
            });
        }

        public ValueTask Restore(NotchSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            Value = saveState.CurrentValue;
            Smooth = saveState.Smooth;
            NotchStateType = saveState.NotchStateType;

            return ValueTask.CompletedTask;
        }
    }

    /**
     * This is the most used controller. The main use is for diesel locomotives' Throttle control.
     * 
     * It is used with single keypress, this means that when the user press a key, only the keydown event is handled.
     * The user need to press the key multiple times to update this controller.
     * 
     */
    public class MSTSNotchController : 
        IController, 
        INameValueInformationProvider, 
        ISaveStateApi<ControllerSaveState>, 
        ISaveStateRestoreApi<NotchSaveState, MSTSNotch>
    {
        public const float StandardBoost = 5.0f; // standard step size multiplier
        public const float FastBoost = 20.0f;

        private protected readonly DetailInfoBase controllerInfo = new DetailInfoBase();
        private bool updateControllerStatus;

        private float previousValue;
        private float? controllerTarget;

        public float CurrentValue { get; set; }
        public float IntermediateValue { get; private set; }
        public float MinimumValue { get; private set; }
        public float MaximumValue { get; private set; } = 1;

        public float StepSize { get; set; }
        internal List<IControllerNotch> Notches { get; } = new List<IControllerNotch>();
        public int NotchIndex { get; set; }
        public bool ToZero { get; private set; } // true if controller zero command;

        //Does not need to persist
        //this indicates if the controller is increasing or decreasing, 0 no changes
        public float UpdateValue { get; set; }
        public double CommandStartTime { get; set; }

        #region .ctor

        public MSTSNotchController()
        {
        }

        public MSTSNotchController(int numOfNotches)
        {
            MinimumValue = 0;
            MaximumValue = numOfNotches - 1;
            StepSize = 1;
            for (int i = 0; i < numOfNotches; i++)
                Notches.Add(new MSTSNotch(i, false, 0));
        }

        public MSTSNotchController(float min, float max, float stepSize)
        {
            MinimumValue = min;
            MaximumValue = max;
            StepSize = stepSize;
        }

        public MSTSNotchController(MSTSNotchController source)
        {
            ArgumentNullException.ThrowIfNull(source);

            CurrentValue = source.CurrentValue;
            IntermediateValue = source.IntermediateValue;
            MinimumValue = source.MinimumValue;
            MaximumValue = source.MaximumValue;
            StepSize = source.StepSize;
            NotchIndex = source.NotchIndex;

            foreach (MSTSNotch notch in source.Notches)
            {
                Notches.Add(notch.Clone());
            }
        }

        public MSTSNotchController(STFReader stf)
        {
            Parse(stf);
        }

        public MSTSNotchController(List<IControllerNotch> notches)
        {
            Notches = notches;
        }

        internal void Initialize(float currentValue, float minValue, float maxValue, float stepSize)
        {
            MinimumValue = minValue;
            MaximumValue = maxValue;
            StepSize = stepSize;
            SetValue(currentValue);
        }
        #endregion

        public virtual IController Clone()
        {
            return new MSTSNotchController(this);
        }

        public virtual bool IsValid()
        {
            return StepSize != 0;
        }

        public void Parse(STFReader stf)
        {
            stf.MustMatch("(");
            MinimumValue = stf.ReadFloat(STFReader.Units.None, null);
            MaximumValue = stf.ReadFloat(STFReader.Units.None, null);
            StepSize = stf.ReadFloat(STFReader.Units.None, null);
            IntermediateValue = CurrentValue = stf.ReadFloat(STFReader.Units.None, null);
            string token = stf.ReadItem(); // s/b numnotches
            if (!string.Equals(token, "NumNotches", StringComparison.OrdinalIgnoreCase)) // handle error in gp38.eng where extra parameter provided before NumNotches statement 
                stf.ReadItem();
            stf.MustMatch("(");
            stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("notch", ()=>{
                    stf.MustMatch("(");
                    float value = stf.ReadFloat(STFReader.Units.None, null);
                    int smooth = stf.ReadInt(null);
                    string type = stf.ReadString();
                    Notches.Add(new MSTSNotch(value, smooth, type, stf));
                    if (type != ")") stf.SkipRestOfBlock();
                }),
            });
            SetValue(CurrentValue);
        }

        public int NotchCount()
        {
            return Notches.Count;
        }

        private float GetNotchBoost(float boost)
        {
            return (ToZero && ((NotchIndex >= 0 && Notches[NotchIndex].Smooth) || Notches.Count == 0 ||
                IntermediateValue - CurrentValue > StepSize) ? FastBoost : boost);
        }

        public void AddNotch(float value)
        {
            Notches.Add(new MSTSNotch(value, false, (int)ControllerState.Dummy));
        }

        /// <summary>
        /// Sets the actual value of the controller, and adjusts the actual notch to match.
        /// </summary>
        /// <param name="value">Normalized value the controller to be set to. Normally is within range [-1..1]</param>
        /// <returns>1 or -1 if there was a significant change in controller position, otherwise 0.
        /// Needed for hinting whether a serializable command is to be issued for repeatability.
        /// Sign is indicating the direction of change, being displayed by confirmer text.</returns>
        public int SetValue(float value)
        {
            CurrentValue = IntermediateValue = MathHelper.Clamp(value, MinimumValue, MaximumValue);
            var oldNotch = NotchIndex;

            for (NotchIndex = Notches.Count - 1; NotchIndex > 0; NotchIndex--)
            {
                if (Notches[NotchIndex].Value <= CurrentValue)
                    break;
            }

            if (NotchIndex >= 0 && !Notches[NotchIndex].Smooth)
                CurrentValue = Notches[NotchIndex].Value;

            var change = NotchIndex > oldNotch || CurrentValue > previousValue + 0.1f || CurrentValue == 1 && previousValue < 1
                ? 1 : NotchIndex < oldNotch || CurrentValue < previousValue - 0.1f || CurrentValue == 0 && previousValue > 0 ? -1 : 0;
            if (change != 0)
                previousValue = CurrentValue;

            return change;
        }

        public float SetPercent(float percent)
        {
            float v = (MinimumValue < 0 && percent < 0 ? -MinimumValue : MaximumValue) * percent / 100;
            CurrentValue = MathHelper.Clamp(v, MinimumValue, MaximumValue);

            if (NotchIndex >= 0)
            {
                if (Notches[Notches.Count - 1].NotchStateType == ControllerState.Emergency)
                    v = Notches[Notches.Count - 1].Value * percent / 100;
                for (; ; )
                {
                    IControllerNotch notch = Notches[NotchIndex];
                    if (NotchIndex > 0 && v < notch.Value)
                    {
                        IControllerNotch prev = Notches[NotchIndex - 1];
                        if (!notch.Smooth && !prev.Smooth && v - prev.Value > .45 * (notch.Value - prev.Value))
                            break;
                        NotchIndex--;
                        continue;
                    }
                    if (NotchIndex < Notches.Count - 1)
                    {
                        IControllerNotch next = Notches[NotchIndex + 1];
                        if (next.NotchStateType != ControllerState.Emergency)
                        {
                            if ((notch.Smooth || next.Smooth) && v < next.Value)
                                break;
                            if (!notch.Smooth && !next.Smooth && v - notch.Value < .55 * (next.Value - notch.Value))
                                break;
                            NotchIndex++;
                            continue;
                        }
                    }
                    break;
                }
                if (Notches[NotchIndex].Smooth)
                    CurrentValue = v;
                else
                    CurrentValue = Notches[NotchIndex].Value;
            }
            IntermediateValue = CurrentValue;
            return 100 * CurrentValue;
        }

        public void StartIncrease(float? target)
        {
            controllerTarget = target;
            ToZero = false;
            StartIncrease();
        }

        public void StartIncrease()
        {
            UpdateValue = 1;

            // When we have notches and the current Notch does not require smooth, we go directly to the next notch
            if ((Notches.Count > 0) && (NotchIndex < Notches.Count - 1) && (!Notches[NotchIndex].Smooth))
            {
                ++NotchIndex;
                IntermediateValue = CurrentValue = Notches[NotchIndex].Value;
            }
        }

        public void StopIncrease()
        {
            UpdateValue = 0;
        }

        public void StartDecrease(float? target, bool toZero = false)
        {
            controllerTarget = target;
            ToZero = toZero;
            StartDecrease();
        }

        public void StartDecrease()
        {
            UpdateValue = -1;

            //If we have notches and the previous Notch does not require smooth, we go directly to the previous notch
            if ((Notches.Count > 0) && (NotchIndex > 0) && SmoothMin() == null)
            {
                //Keep intermediate value with the "previous" notch, so it will take a while to change notches
                //again if the user keep holding the key
                IntermediateValue = Notches[NotchIndex].Value;
                NotchIndex--;
                CurrentValue = Notches[NotchIndex].Value;
            }
        }

        public void StopDecrease()
        {
            UpdateValue = 0;
        }

        public float Update(double elapsedSeconds)
        {
            if (UpdateValue == 1 || UpdateValue == -1)
            {
                CheckControllerTargetAchieved();
                UpdateValues(elapsedSeconds, UpdateValue, StandardBoost);
            }
            if (updateControllerStatus)
            {
                UpdateControllerStatus();
                updateControllerStatus = false;
            }
            return CurrentValue;
        }

        public float UpdateAndSetBoost(double elapsedSeconds, float boost)
        {
            if (UpdateValue == 1 || UpdateValue == -1)
            {
                CheckControllerTargetAchieved();
                UpdateValues(elapsedSeconds, UpdateValue, boost);
            }
            return CurrentValue;
        }

        /// <summary>
        /// If a target has been set, then stop once it's reached and also cancel the target.
        /// </summary>
        public void CheckControllerTargetAchieved()
        {
            if (controllerTarget != null)
            {
                if (UpdateValue > 0.0)
                {
                    if (CurrentValue >= controllerTarget)
                    {
                        StopIncrease();
                        controllerTarget = null;
                    }
                }
                else
                {
                    if (CurrentValue <= controllerTarget)
                    {
                        StopDecrease();
                        controllerTarget = null;
                    }
                }
            }
        }

        private float UpdateValues(double elapsedSeconds, float direction, float boost)
        {
            //We increment the intermediate value first
            IntermediateValue += StepSize * (float)elapsedSeconds * GetNotchBoost(boost) * direction;
            IntermediateValue = MathHelper.Clamp(IntermediateValue, MinimumValue, MaximumValue);

            //Do we have notches
            if (Notches.Count > 0)
            {
                //Increasing, check if the notch has changed
                if ((direction > 0) && (NotchIndex < Notches.Count - 1) && (IntermediateValue >= Notches[NotchIndex + 1].Value))
                {
                    // steamer_ctn - The following code was added in relation to reported bug  #1200226. However it seems to prevent the brake controller from ever being moved to EMERGENCY position.
                    // Bug conditions indicated in the bug report have not been able to be duplicated, ie there doesn't appear to be a "safety stop" when brake key(s) held down continuously
                    // Code has been reverted pending further investigation or reports of other issues
                    // Prevent TrainBrake to continuously switch to emergency
                    //      if (Notches[CurrentNotch + 1].Type == ControllerState.Emergency)
                    //         IntermediateValue = Notches[CurrentNotch + 1].Value - StepSize;
                    //      else
                    NotchIndex++;
                }
                //decreasing, again check if the current notch has changed
                else if ((direction < 0) && (NotchIndex > 0) && (IntermediateValue < Notches[NotchIndex].Value))
                {
                    NotchIndex--;
                }

                //If the notch is smooth, we use intermediate value that is being update smooth thought the frames
                if (Notches[NotchIndex].Smooth)
                    CurrentValue = IntermediateValue;
                else
                    CurrentValue = Notches[NotchIndex].Value;
            }
            else
            {
                //if no notches, we just keep updating the current value directly
                CurrentValue = IntermediateValue;
            }
            return CurrentValue;
        }

        public float GetNotchFraction()
        {
            if (Notches.Count == 0)
                return 0;
            IControllerNotch notch = Notches[NotchIndex];
            if (!notch.Smooth)
                // Respect British 3-wire EP brake configurations
                return (notch.NotchStateType is ControllerState.EPApply or ControllerState.EPOnly) ? CurrentValue : 1;
            float x = 1;
            if (NotchIndex + 1 < Notches.Count)
                x = Notches[NotchIndex + 1].Value;
            x = (CurrentValue - notch.Value) / (x - notch.Value);
            if (notch.NotchStateType == ControllerState.Release)
                x = 1 - x;
            return x;
        }

        public float? SmoothMin()
        {
            float? target = null;
            if (Notches.Count > 0)
            {
                if (NotchIndex > 0 && Notches[NotchIndex - 1].Smooth)
                    target = Notches[NotchIndex - 1].Value;
                else if (Notches[NotchIndex].Smooth && CurrentValue > Notches[NotchIndex].Value)
                    target = Notches[NotchIndex].Value;
            }
            else
                target = MinimumValue;
            return target;
        }

        public float? SmoothMax()
        {
            float? target = null;
            if (Notches.Count > 0 && NotchIndex < Notches.Count - 1 && Notches[NotchIndex].Smooth)
                target = Notches[NotchIndex + 1].Value;
            else if (Notches.Count == 0
                || (Notches.Count == 1 && Notches[NotchIndex].Smooth))
                target = MaximumValue;
            return target;
        }

        public float? DistributedPowerSmoothMax()
        {
            float? target = null;
            if (Notches.Count > 0 && NotchIndex < Notches.Count - 1 && Notches[NotchIndex].Smooth)
                target = Notches[NotchIndex + 1].Value;
            else if (Notches.Count == 0 || NotchIndex == Notches.Count - 1 && Notches[NotchIndex].Smooth)
                target = MaximumValue;
            return target;
        }

        public float? DPSmoothMax()
        {
            float? target = null;
            if (Notches.Count > 0 && NotchIndex < Notches.Count - 1 && Notches[NotchIndex].Smooth)
                target = Notches[NotchIndex + 1].Value;
            else if (Notches.Count == 0 || NotchIndex == Notches.Count - 1 && Notches[NotchIndex].Smooth)
                target = MaximumValue;
            return target;
        }

        public virtual string GetStatus()
        {
            if (Notches.Count == 0)
                return $"{100 * CurrentValue:F0}%";
            IControllerNotch notch = Notches[NotchIndex];
            string name = notch.NotchStateType.GetLocalizedDescription();
            if (!notch.Smooth && notch.NotchStateType == ControllerState.Dummy)
                return $"{100 * CurrentValue:F0}%";
            if (!notch.Smooth)
                return name;
            if (!string.IsNullOrEmpty(name))
                return $"{name} {100 * GetNotchFraction():F0}%";
            return $"{100 * GetNotchFraction():F0}%";
        }

        public async ValueTask<ControllerSaveState> Snapshot()
        {
            return new ControllerSaveState()
            {
                ControllerType = ControllerType.NotchController,
                CurrentValue = CurrentValue,
                MinimumValue = MinimumValue,
                MaximumValue = MaximumValue,
                StepSize = StepSize,
                NotchIndex = NotchIndex,
                NotchStates = await Notches.Cast<MSTSNotch>().SnapshotCollection<NotchSaveState,MSTSNotch>().ConfigureAwait(false),
            };
        }

        public async ValueTask Restore(ControllerSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            IntermediateValue = CurrentValue = saveState.CurrentValue;
            MinimumValue = saveState.MinimumValue;
            MaximumValue = saveState.MaximumValue;
            StepSize = saveState.StepSize;
            NotchIndex = saveState.NotchIndex;

            UpdateValue = 0;
            Notches.Clear();

            List<MSTSNotch> notches = new List<MSTSNotch>();
            await notches.RestoreCollectionCreateNewInstances<NotchSaveState, MSTSNotch, MSTSNotchController >(saveState.NotchStates);
            Notches.AddRange(notches);
        }

        public IControllerNotch CurrentNotch => Notches.Count == 0 ? null : Notches[NotchIndex];

        public InformationDictionary DetailInfo => GetControllerStatus();

        public Dictionary<string, FormatOption> FormattingOptions => controllerInfo.FormattingOptions;

        protected void SetCurrentNotch(ControllerState type)
        {
            for (int i = 0; i < Notches.Count; i++)
            {
                if (Notches[i].NotchStateType == type)
                {
                    NotchIndex = i;
                    CurrentValue = Notches[i].Value;

                    break;
                }
            }
        }

        public void SetStepSize(float stepSize)
        {
            StepSize = stepSize;
        }

        public void Normalize(float ratio)
        {
            for (int i = 0; i < Notches.Count; i++)
                Notches[i].Value /= ratio;
            MaximumValue /= ratio;
        }

        /// <summary>
        /// Get the nearest discrete notch position for a normalized input value.
        /// This function is not dependent on notch controller actual (current) value, so can be queried for computer-intervened value as well.
        /// </summary>
        public int GetNearestNotch(float value)
        {
            var notch = 0;
            for (notch = Notches.Count - 1; notch > 0; notch--)
            {
                if (Notches[notch].Value <= value)
                {
                    if (notch < Notches.Count - 1 && Notches[notch + 1].Value - value < value - Notches[notch].Value)
                        notch++;
                    break;
                }
            }
            return notch;
        }

        /// <summary>
        /// Get the discrete notch position for a normalized input value.
        /// This function is not dependent on notch controller actual (current) value, so can be queried for computer-intervened value as well.
        /// </summary>
        public int GetNotch(float value)
        {
            int notch;
            for (notch = Notches.Count - 1; notch > 0; notch--)
            {
                if (Notches[notch].Value <= value)
                {
                    break;
                }
            }
            return notch;
        }

        private InformationDictionary GetControllerStatus()
        {
            updateControllerStatus = true;
            return controllerInfo;
        }

        private protected virtual void UpdateControllerStatus()
        {
            IControllerNotch notch = Notches[NotchIndex];
            float fraction = (Notches.Count == 0 || !notch.Smooth) ? CurrentValue : GetNotchFraction();
            controllerInfo["State"] = notch.NotchStateType.GetLocalizedDescription();
            controllerInfo["Value"] = $"{fraction * 100:N0}%";
            controllerInfo["Status"] = FormatStrings.JoinIfNotEmpty(' ', controllerInfo["State"], controllerInfo["Value"]);
            controllerInfo["StatusShort"] = FormatStrings.JoinIfNotEmpty(' ', controllerInfo["State"].Max(string.IsNullOrEmpty(controllerInfo["Value"]) ? 20 : 5), controllerInfo["Value"]);
        }
    }
}
