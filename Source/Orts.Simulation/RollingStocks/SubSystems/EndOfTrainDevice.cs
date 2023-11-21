// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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
using System.ComponentModel;
using System.IO;
using System.Linq;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api;
using Orts.Simulation.Physics;

namespace Orts.Simulation.RollingStocks.SubSystems
{
    public enum EndOfTrainLevel
    {
        NoComm,
        OneWay,
        TwoWay
    }

    [Description("EoT")]
    public enum EoTState
    {
        [Description("Disarmed")] Disarmed,
        [Description("Comm Test")] CommTestOn,
        [Description("Armed")] Armed,
        [Description("Local Test")] LocalTestOn,
        [Description(" Arm Now")] ArmNow,
        [Description("2-way Armed")] ArmedTwoWay
    }

    public class EndOfTrainDevice : MSTSWagon
    {
        public float CommTestDelayS { get; protected set; } = 5f;
        public float LocalTestDelayS { get; protected set; } = 25f;

        public int ID { get; private set; }
        public EoTState State { get; set; }
        public bool EOTEmergencyBrakingOn { get; private set; }
        private EndOfTrainLevel level;

        private protected Timer delayTimer;

        public EndOfTrainDevice(string wagPath)
            : base(wagPath)
        {
            State = EoTState.Disarmed;
            ID = StaticRandom.Next(0, 99999);
            delayTimer = new Timer(simulator);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void InitializeMoving()
        {
            base.InitializeMoving();
            InitializeLevel();
        }

        public void InitializeLevel()
        {
            switch (level)
            {
                case EndOfTrainLevel.OneWay:
                    State = EoTState.Armed;
                    break;
                case EndOfTrainLevel.TwoWay:
                    State = EoTState.ArmedTwoWay;
                    break;
                default:
                    break;
            }
        }

        public override void Update(double elapsedClockSeconds)
        {
            UpdateState();
            Train.Cars.Last().BrakeSystem.AngleCockBOpen = simulator.PlayerLocomotive.Train == Train && State == EoTState.ArmedTwoWay &&
                (EOTEmergencyBrakingOn || BrakeController.IsEmergencyState(simulator.PlayerLocomotive.TrainBrakeController.State));
            base.Update(elapsedClockSeconds);
        }

        private void UpdateState()
        {
            switch (State)
            {
                case EoTState.Disarmed:
                    break;
                case EoTState.CommTestOn:
                    if (delayTimer.Triggered)
                    {
                        delayTimer.Stop();
                        State = EoTState.Armed;
                    }
                    break;
                case EoTState.Armed:
                    if (level == EndOfTrainLevel.TwoWay)
                    {
                        delayTimer ??= new Timer(simulator);
                        delayTimer.Setup(LocalTestDelayS);
                        State = EoTState.LocalTestOn;
                        delayTimer.Start();
                    }
                    break;
                case EoTState.LocalTestOn:
                    if (delayTimer.Triggered)
                    {
                        delayTimer.Stop();
                        State = EoTState.ArmNow;
                    }
                    break;
                case EoTState.ArmNow:
                    break;
                case EoTState.ArmedTwoWay:
                    break;
            }
        }

        public override void Parse(string lowercasetoken, STFReader stf)
        {
            ArgumentNullException.ThrowIfNull(stf);
            switch (lowercasetoken)
            {
                case "ortseot(level":
                    stf.MustMatch("(");
                    var eotLevel = stf.ReadString();
                    if (!EnumExtension.GetValue(eotLevel, out level))
                        STFException.TraceWarning(stf, "Skipped unknown EOT Level " + eotLevel);
                    break;
                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }
        }

        public override void Save(BinaryWriter outf)
        {
            ArgumentNullException.ThrowIfNull(outf);
            outf.Write(ID);
            outf.Write((int)State);
            base.Save(outf);
        }

        public override void Restore(BinaryReader inf)
        {
            ArgumentNullException.ThrowIfNull(inf);
            ID = inf.ReadInt32();
            State = (EoTState)(inf.ReadInt32());
            delayTimer = new Timer(simulator);
            switch (State)
            {
                case EoTState.CommTestOn:
                    // restart timer
                    delayTimer.Setup(CommTestDelayS);
                    delayTimer.Start();
                    break;
                case EoTState.LocalTestOn:
                    // restart timer
                    delayTimer.Setup(LocalTestDelayS);
                    delayTimer.Start();
                    break;
                default:
                    break;
            }
            base.Restore(inf);
            if (Train != null)
                Train.EndOfTrainDevice = this;
        }

        public override void Copy(MSTSWagon source)
        {
            base.Copy(source);
            level = (source as EndOfTrainDevice)?.level ?? throw new InvalidCastException();
        }

        public float GetDataOf(CabViewControl cabViewControl)
        {
            ArgumentNullException.ThrowIfNull(cabViewControl);
            float data = 0;
            switch (cabViewControl.ControlType.CabViewControlType)
            {
                case CabViewControlType.Orts_Eot_Id:
                    data = ID;
                    break;
                case CabViewControlType.Orts_Eot_State_Display:
                    data = (float)(int)State;
                    break;
                case CabViewControlType.Orts_Eot_Emergency_Brake:
                    data = EOTEmergencyBrakingOn ? 1 : 0;
                    break;
            }
            return data;
        }

        public void CommTest()
        {
            if (State == EoTState.Disarmed &&
                (level == EndOfTrainLevel.OneWay || level == EndOfTrainLevel.TwoWay))
            {
                delayTimer ??= new Timer(simulator);
                delayTimer.Setup(CommTestDelayS);
                State = EoTState.CommTestOn;
                delayTimer.Start();
            }
        }

        public void Disarm()
        {
            State = EoTState.Disarmed;
        }

        public void ArmTwoWay()
        {
            if (State == EoTState.ArmNow)
                State = EoTState.ArmedTwoWay;
        }

        public void EmergencyBrake(bool toState)
        {
            if (State == EoTState.ArmedTwoWay)
            {
                EOTEmergencyBrakingOn = toState;
            }
        }

    }
}