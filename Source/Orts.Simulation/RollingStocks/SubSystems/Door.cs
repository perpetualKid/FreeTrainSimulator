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
using System.IO;

using Orts.Common;
using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api;

namespace Orts.Simulation.RollingStocks.SubSystems
{
    public class Door : ISubSystem<Door>
    {
        
        // Parameters
        public float OpeningDelayS { get; protected set; }
        public float ClosingDelayS { get; protected set; }

        // Variables
        private readonly MSTSWagon wagon;
        public bool RightSide { get; }
        private readonly Timer openingTimer;
        private readonly Timer closingTimer;
        
        public DoorState State { get; protected set; } = DoorState.Closed;
        public bool Locked {get; protected set; }

        public Door(MSTSWagon wagon, bool right)
        {
            this.wagon = wagon;
            RightSide = right;

            openingTimer = new Timer(Simulator.Instance);
            closingTimer = new Timer(Simulator.Instance);
        }

        public virtual void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(ortsdoors(closingdelay":
                    ClosingDelayS = stf.ReadFloatBlock(STFReader.Units.Time, 0f);
                    break;

                case "engine(ortsdoors(openingdelay":
                    OpeningDelayS = stf.ReadFloatBlock(STFReader.Units.Time, 0f);
                    break;
            }
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
            switch(State)
            {
                case DoorState.Opening:
                    closingTimer.Stop();
                    if (!openingTimer.Started) openingTimer.Start();
                    if (openingTimer.Triggered) State = DoorState.Open;
                    break;
                case DoorState.Closing:
                    openingTimer.Stop();
                    if (!closingTimer.Started) closingTimer.Start();
                    if (closingTimer.Triggered) State = DoorState.Closed;
                    break;
                case DoorState.Closed:
                    closingTimer.Stop();
                    openingTimer.Stop();
                    break;
                case DoorState.Open:
                    closingTimer.Stop();
                    openingTimer.Stop();
                    break;
            }
        }
        public void SetDoorLock(bool lck)
        {
            Locked = lck;
            if (lck) SetDoor(false);
        }
        public void SetDoor(bool open)
        {
            if (!Locked && open && (State == DoorState.Closed || State == DoorState.Closing))
            {
                State = DoorState.Opening;
                wagon.SignalEvent(TrainEvent.DoorOpen);
                bool driverRightSide = RightSide ^ wagon.GetCabFlipped();
                Confirm(driverRightSide ? CabControl.DoorsRight : CabControl.DoorsLeft, CabSetting.On);
            }
            else if (!open && (State == DoorState.Open || State == DoorState.Opening))
            {
                State = DoorState.Closing;
                wagon.SignalEvent(TrainEvent.DoorClose);
                bool driverRightSide = RightSide ^ wagon.GetCabFlipped();
                Confirm(driverRightSide ? CabControl.DoorsRight : CabControl.DoorsLeft, CabSetting.Off);
            }
        }
        protected void Confirm(CabControl control, CabSetting setting)
        {
            if (wagon == Simulator.Instance.PlayerLocomotive)
            {
                Simulator.Instance.Confirmer?.Confirm(control, setting);
            }
        }
    }
}
