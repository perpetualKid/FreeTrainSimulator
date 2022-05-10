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
using System.IO;
using System.Linq;

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

    public class FullEOTPaths : List<string>
    {
        public FullEOTPaths(string eotPath)
        {
            foreach (string directory in Directory.EnumerateDirectories(eotPath))
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*.eot"))
                {
                    Add(file);
                }
            }
        }
    }

    public enum EoTState
    {
        Disarmed,
        CommTestOn,
        Armed,
        LocalTestOn,
        ArmNow,
        ArmedTwoWay
    }

    public class EndOfTrainDevice : MSTSWagon
    {
        public float CommTestDelayS { get; protected set; } = 5f;
        public float LocalTestDelayS { get; protected set; } = 25f;

        public static Random IDRandom { get; } = new Random();
        public int ID;
        public EoTState State;
        public bool EOTEmergencyBrakingOn;
        public EndOfTrainLevel EOTLevel;

        private protected Timer delayTimer;

        public EndOfTrainDevice(string wagPath)
            : base(wagPath)
        {
            State = EoTState.Disarmed;
            ID = IDRandom.Next(0, 99999);
            delayTimer = new Timer(this);
        }

        /*        public EOT(MSTSLocomotive.EOTenabled eotEnabled, bool armed, Train train)
                {
                    Train = train;
                    EOTState = EOTstate.Disarmed;
                    EOTType = eotEnabled;
                    ID = IDRandom.Next(0, 99999);
                    if (armed)
                        EOTState = EOTstate.Armed;
                    DelayTimer = new Timer(this);
                }*/

        /*       public EOT(BinaryReader inf, Train train)
               {
                   Train = train;
                   ID = inf.ReadInt32();
                   EOTState = (EOTstate)(inf.ReadInt32());
                   DelayTimer = new Timer(this);
               }*/

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
            switch (EOTLevel)
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
            if (simulator.PlayerLocomotive.Train == Train && State == EoTState.ArmedTwoWay &&
                (EOTEmergencyBrakingOn ||
                (simulator.PlayerLocomotive as MSTSLocomotive).TrainBrakeController.GetStatus().ToLower().StartsWith("emergency")))
                Train.Cars.Last().BrakeSystem.AngleCockBOpen = true;
            else
                Train.Cars.Last().BrakeSystem.AngleCockBOpen = false;
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
                    if (EOTLevel == EndOfTrainLevel.TwoWay)
                    {
                        if (delayTimer == null)
                            delayTimer = new Timer(this);
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
            switch (lowercasetoken)
            {
                case "ortseot(level":
                    stf.MustMatch("(");
                    var eotLevel = stf.ReadString();
                    try
                    {
                        EOTLevel = (EndOfTrainLevel)Enum.Parse(typeof(EndOfTrainLevel), eotLevel.First().ToString().ToUpper() + eotLevel.Substring(1));
                    }
                    catch
                    {
                        STFException.TraceWarning(stf, "Skipped unknown EOT Level " + eotLevel);
                    }
                    break;
                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }
        }

        public override void Save(BinaryWriter outf)
        {
            outf.Write(ID);
            outf.Write((int)State);
            base.Save(outf);
        }

        public override void Restore(BinaryReader inf)
        {
            ID = inf.ReadInt32();
            State = (EoTState)(inf.ReadInt32());
            delayTimer = new Timer(this);
            base.Restore(inf);
            if (Train != null)
                Train.EndOfTrainDevice = this;
        }

        public override void Copy(MSTSWagon source)
        {
            base.Copy(source);
            EndOfTrainDevice eotcopy = (EndOfTrainDevice)source;
            EOTLevel = eotcopy.EOTLevel;
        }

        public float GetDataOf(CabViewControl cvc)
        {
            float data = 0;
            switch (cvc.ControlType)
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
                (EOTLevel == EndOfTrainLevel.OneWay || EOTLevel == EndOfTrainLevel.TwoWay))
            {
                if (delayTimer == null)
                    delayTimer = new Timer(this);
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