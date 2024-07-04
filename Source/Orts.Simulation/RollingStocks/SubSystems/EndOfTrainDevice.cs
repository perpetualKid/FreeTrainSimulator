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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Scripting.Api;
using Orts.Simulation.Physics;

using SharpDX.Direct2D1;

namespace Orts.Simulation.RollingStocks.SubSystems
{
    public class EndOfTrainDevice : MSTSWagon
    {
        private Timer delayTimer;
        private EndOfTrainLevel level;

        public float CommTestDelayS { get; private set; } = 5f;
        public float LocalTestDelayS { get; private set; } = 25f;

        public int ID { get; private set; }
        public EndOfTrainState State { get; set; }
        public bool EOTEmergencyBrakingOn { get; private set; }

        public EndOfTrainDevice(string wagPath)
            : base(wagPath)
        {
            State = EndOfTrainState.Disarmed;
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
                    State = EndOfTrainState.Armed;
                    break;
                case EndOfTrainLevel.TwoWay:
                    State = EndOfTrainState.ArmedTwoWay;
                    break;
                default:
                    break;
            }
        }

        public override void Update(double elapsedClockSeconds)
        {
            UpdateState();
            Train.Cars.Last().BrakeSystem.AngleCockBOpen = simulator.PlayerLocomotive.Train == Train && State == EndOfTrainState.ArmedTwoWay &&
                (EOTEmergencyBrakingOn || BrakeController.IsEmergencyState(simulator.PlayerLocomotive.TrainBrakeController.State));
            base.Update(elapsedClockSeconds);
        }

        private void UpdateState()
        {
            switch (State)
            {
                case EndOfTrainState.Disarmed:
                    break;
                case EndOfTrainState.CommTestOn:
                    if (delayTimer.Triggered)
                    {
                        delayTimer.Stop();
                        State = EndOfTrainState.Armed;
                    }
                    break;
                case EndOfTrainState.Armed:
                    if (level == EndOfTrainLevel.TwoWay)
                    {
                        delayTimer ??= new Timer(simulator);
                        delayTimer.Setup(LocalTestDelayS);
                        State = EndOfTrainState.LocalTestOn;
                        delayTimer.Start();
                    }
                    break;
                case EndOfTrainState.LocalTestOn:
                    if (delayTimer.Triggered)
                    {
                        delayTimer.Stop();
                        State = EndOfTrainState.ArmNow;
                    }
                    break;
                case EndOfTrainState.ArmNow:
                    break;
                case EndOfTrainState.ArmedTwoWay:
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

        public override async ValueTask<TrainCarSaveState> Snapshot()
        {
            TrainCarSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.EndOfTrainSaveState = new EndOfTrainSaveState()
            {
                DeviceId = ID,
                EndOfTrainState = State,
            };

            return saveState;
        }

        public override async ValueTask Restore([NotNull] TrainCarSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(saveState.EndOfTrainSaveState, nameof(saveState.EndOfTrainSaveState));

            ID = saveState.EndOfTrainSaveState.DeviceId;
            State = saveState.EndOfTrainSaveState.EndOfTrainState;

            delayTimer = new Timer(simulator);
            switch (State)
            {
                case EndOfTrainState.CommTestOn:
                    // restart timer
                    delayTimer.Setup(CommTestDelayS);
                    delayTimer.Start();
                    break;
                case EndOfTrainState.LocalTestOn:
                    // restart timer
                    delayTimer.Setup(LocalTestDelayS);
                    delayTimer.Start();
                    break;
                default:
                    break;
            }
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
            if (State == EndOfTrainState.Disarmed &&
                (level == EndOfTrainLevel.OneWay || level == EndOfTrainLevel.TwoWay))
            {
                delayTimer ??= new Timer(simulator);
                delayTimer.Setup(CommTestDelayS);
                State = EndOfTrainState.CommTestOn;
                delayTimer.Start();
            }
        }

        public void Disarm()
        {
            State = EndOfTrainState.Disarmed;
        }

        public void ArmTwoWay()
        {
            if (State == EndOfTrainState.ArmNow)
                State = EndOfTrainState.ArmedTwoWay;
        }

        public void EmergencyBrake(bool toState)
        {
            if (State == EndOfTrainState.ArmedTwoWay)
            {
                EOTEmergencyBrakingOn = toState;
            }
        }
    }
}