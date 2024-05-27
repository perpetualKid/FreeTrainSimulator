// COPYRIGHT 2022 by the Open Rails project.
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
using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Scripting.Api;

namespace Orts.Simulation.RollingStocks.SubSystems
{
    public class Doors : EnumArray<Door, DoorSide>,
        ISubSystem<Doors>
    {
        public Doors(MSTSWagon wagon)
        {
            this[DoorSide.Left] = new Door(wagon, DoorSide.Left);
            this[DoorSide.Right] = new Door(wagon, DoorSide.Right);
        }

        public virtual void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(ortsdoors(closingdelay":
                    {
                        float delayS = stf.ReadFloatBlock(STFReader.Units.Time, 0f);
                        this[DoorSide.Left].ClosingDelayS = delayS;
                        this[DoorSide.Right].ClosingDelayS = delayS;
                        break;
                    }
                case "wagon(ortsdoors(openingdelay":
                    {
                        float delayS = stf.ReadFloatBlock(STFReader.Units.Time, 0f);
                        this[DoorSide.Left].OpeningDelayS = delayS;
                        this[DoorSide.Right].OpeningDelayS = delayS;
                        break;
                    }
            }
        }

        public void Copy(Doors source)
        {
            ArgumentNullException.ThrowIfNull(source);

            this[DoorSide.Left].Copy(source[DoorSide.Left]);
            this[DoorSide.Right].Copy(source[DoorSide.Right]);
        }

        public virtual void Initialize()
        {
            this[DoorSide.Left].Initialize();
            this[DoorSide.Right].Initialize();
        }

        /// <summary>
        /// Initialization when simulation starts with moving train
        /// </summary>
        public virtual void InitializeMoving()
        {
        }

        public virtual void Update(double elapsedClockSeconds)
        {
            this[DoorSide.Left].Update(elapsedClockSeconds);
            this[DoorSide.Right].Update(elapsedClockSeconds);
        }

        public static DoorSide FlippedDoorSide(DoorSide trainSide)
        {
            return trainSide switch
            {
                DoorSide.Left => DoorSide.Right,
                DoorSide.Right => DoorSide.Left,
                _ => DoorSide.Both,
            };
        }

        public void Save(BinaryWriter outf)
        {
            throw new NotImplementedException();
        }

        public void Restore(BinaryReader inf)
        {
            throw new NotImplementedException();
        }
    }

    public class Door : ISubSystem<Door>, ISaveStateApi<DoorSaveState>
    {

        // Parameters
        public float OpeningDelayS { get; set; }
        public float ClosingDelayS { get; set; }

        // Variables
        private readonly MSTSWagon wagon;
        private readonly Timer openingTimer;
        private readonly Timer closingTimer;

        public DoorSide DoorSide { get; }

        public DoorState State { get; protected set; } = DoorState.Closed;
        public bool Locked { get; protected set; }

        public Door(MSTSWagon wagon, DoorSide doorSide)
        {
            this.wagon = wagon;
            DoorSide = doorSide;

            openingTimer = new Timer(Simulator.Instance);
            closingTimer = new Timer(Simulator.Instance);
        }

        public virtual void Parse(string lowercasetoken, STFReader stf)
        {
        }

        public void Copy(Door source)
        {
            ArgumentNullException.ThrowIfNull(source);

            ClosingDelayS = source.ClosingDelayS;
            OpeningDelayS = source.OpeningDelayS;
        }

        public virtual void Initialize()
        {
            closingTimer.Setup(ClosingDelayS);
            openingTimer.Setup(OpeningDelayS);
        }

        /// <summary>
        /// Initialization when simulation starts with moving train
        /// </summary>
        public virtual void InitializeMoving()
        {
        }

        public virtual void Save(BinaryWriter outf)
        {
            outf.Write((int)State);
            outf.Write(Locked);
        }

        public virtual void Restore(BinaryReader inf)
        {
            State = (DoorState)inf.ReadInt32();
            Locked = inf.ReadBoolean();
        }

        public virtual void Update(double elapsedClockSeconds)
        {
            switch (State)
            {
                case DoorState.Opening:
                    if (!openingTimer.Started)
                        openingTimer.Start();
                    if (openingTimer.Triggered)
                    {
                        State = DoorState.Open;
                        openingTimer.Stop();
                    }
                    break;
                case DoorState.Closing:
                    if (!closingTimer.Started)
                        closingTimer.Start();
                    if (closingTimer.Triggered)
                    {
                        State = DoorState.Closed;
                        closingTimer.Stop();
                    }
                    break;
            }
        }
        public void SetDoorLock(bool lck)
        {
            Locked = lck;
            if (lck)
                SetDoor(false);
        }
        public void SetDoor(bool open)
        {
            switch (State)
            {
                case DoorState.Closed:
                case DoorState.Closing:
                    if (!Locked && open)
                    {
                        closingTimer.Stop();
                        State = DoorState.Opening;
                        wagon.SignalEvent(TrainEvent.DoorOpen);
                        bool driverRightSide = (DoorSide == DoorSide.Right) ^ wagon.GetCabFlipped();
                        Confirm(driverRightSide ? CabControl.DoorsRight : CabControl.DoorsLeft, CabSetting.On);
                    }
                    break;
                case DoorState.Open:
                case DoorState.Opening:
                    if (!open)
                    {
                        openingTimer.Stop();
                        State = DoorState.Closing;
                        wagon.SignalEvent(TrainEvent.DoorClose);
                        bool driverRightSide = (DoorSide == DoorSide.Right) ^ wagon.GetCabFlipped();
                        Confirm(driverRightSide ? CabControl.DoorsRight : CabControl.DoorsLeft, CabSetting.Off);
                    }
                    break;
            }
        }

        protected void Confirm(CabControl control, CabSetting setting)
        {
            if (wagon == Simulator.Instance.PlayerLocomotive)
            {
                Simulator.Instance.Confirmer?.Confirm(control, setting);
            }
        }

        public ValueTask<DoorSaveState> Snapshot()
        {
            return ValueTask.FromResult(new DoorSaveState()
            {
                DoorState = State,
                Locked = Locked,
            });
        }

        public ValueTask Restore(DoorSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
            Locked = saveState.Locked;
            State = saveState.DoorState;
            return ValueTask.CompletedTask;
        }
    }
}
