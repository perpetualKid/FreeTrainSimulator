// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{
    public class Pantographs :
        ISubSystem<Pantographs>,
        IList<Pantograph>,
        ISaveStateRestoreApi<PantographSaveState, Pantograph>
    {
        public static readonly int MinPantoID = 1; // minimum value of PantoID, applies to Pantograph 1
        public static readonly int MaxPantoID = 4; // maximum value of PantoID, applies to Pantograph 4
        private readonly MSTSWagon Wagon;

        private readonly List<Pantograph> pantographs = new List<Pantograph>();

        public Pantographs(MSTSWagon wagon)
        {
            Wagon = wagon;
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(ortspantographs":
                    pantographs.Clear();

                    stf.MustMatch("(");
                    stf.ParseBlock(
                        new[] {
                            new STFReader.TokenProcessor(
                                "pantograph",
                                () => {
                                    pantographs.Add(new Pantograph(Wagon));
                                    pantographs[^1].Parse(stf);
                                }
                            )
                        }
                    );

                    if (pantographs.Count == 0)
                        throw new InvalidDataException("ORTSPantographs block with no pantographs");

                    break;
            }
        }

        public void Copy(Pantographs source)
        {
            pantographs.Clear();

            foreach (Pantograph pantograph in source)
            {
                pantographs.Add(new Pantograph(Wagon));
                pantographs[^1].Copy(pantograph);
            }
        }

        public void Initialize()
        {
            while (pantographs.Count < 2)
            {
                Add(new Pantograph(Wagon));
            }

            foreach (Pantograph pantograph in this)
            {
                pantograph.Initialize();
            }
        }

        public void InitializeMoving()
        {
            foreach (Pantograph pantograph in this)
            {
                if (pantograph != null)
                {
                    pantograph.InitializeMoving();

                    break;
                }
            }
        }

        public void Update(double elapsedClockSeconds)
        {
            foreach (Pantograph pantograph in this)
            {
                pantograph.Update(elapsedClockSeconds);
            }
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            foreach (Pantograph pantograph in this)
            {
                pantograph.HandleEvent(evt);
            }
        }

        public void HandleEvent(PowerSupplyEvent evt, int id)
        {
            if (id < Count)
            {
                this[id].HandleEvent(evt);
            }
        }

        #region ListManipulation

        public void Add(Pantograph pantograph)
        {
            pantographs.Add(pantograph);
        }

        public int IndexOf(Pantograph item) => pantographs.IndexOf(item);

        public void Insert(int index, Pantograph item)
        {
            pantographs.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            pantographs.RemoveAt(index);
        }

        public void Clear()
        {
            pantographs.Clear();
        }

        public bool Contains(Pantograph item)
        {
            return pantographs.Contains(item);
        }

        public void CopyTo(Pantograph[] array, int arrayIndex)
        {
            pantographs.CopyTo(array, arrayIndex);
        }

        public bool Remove(Pantograph item)
        {
            return pantographs.Remove(item);
        }

        public IEnumerator<Pantograph> GetEnumerator()
        {
            return pantographs.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (pantographs as IEnumerable).GetEnumerator();
        }

        Pantograph ISaveStateRestoreApi<PantographSaveState, Pantograph>.CreateRuntimeTarget(PantographSaveState saveState)
        {
            return new Pantograph(Wagon);
        }

        public int Count { get { return pantographs.Count; } }

        public Pantograph this[int i]
        {
            get
            {
                return i <= 0 || i > pantographs.Count ? null : pantographs[i - 1];
            }
            set
            {
                pantographs[i - 1] = value;
            }
        }

        #endregion

        public PantographState State
        {
            get
            {
                PantographState state = PantographState.Down;

                foreach (Pantograph pantograph in this)
                {
                    if (pantograph.State > state)
                        state = pantograph.State;
                }

                return state;
            }
        }

        public bool IsReadOnly => false;
    }

    public static class PantographStateExtension
    {
        public static bool CommandUp(this PantographState pantographState)
        {
            return pantographState switch
            {
                PantographState.Up or PantographState.Raising => true,
                _ => false,
            };
        }
    }

    public class Pantograph : ISubSystem<Pantograph>, ISaveStateApi<PantographSaveState>
    {
        private readonly MSTSWagon wagon;
        private static readonly Simulator simulator = Simulator.Instance;

        private double delay;
        private double time;

        public PantographState State { get; private set; }
        public bool CommandUp => State.CommandUp();

        public int Id => wagon.Pantographs.IndexOf(this) + 1;

        public Pantograph(MSTSWagon wagon)
        {
            this.wagon = wagon;

            State = PantographState.Down;
            delay = 0f;
            time = 0f;
        }

        public void Parse(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(
                new[] {
                    new STFReader.TokenProcessor(
                        "delay",
                        () => {
                            delay = stf.ReadFloatBlock(STFReader.Units.Time, null);
                        }
                    )
                }
            );
        }

        public void Copy(Pantograph source)
        {
            State = source.State;
            delay = source.delay;
            time = source.time;
        }

        public void InitializeMoving()
        {
            State = PantographState.Up;
        }

        public void Initialize()
        {

        }

        public void Update(double elapsedClockSeconds)
        {
            switch (State)
            {
                case PantographState.Lowering:
                    time -= (float)elapsedClockSeconds;

                    if (time <= 0f)
                    {
                        time = 0f;
                        State = PantographState.Down;
                    }
                    break;

                case PantographState.Raising:
                    time += (float)elapsedClockSeconds;

                    if (time >= delay)
                    {
                        time = delay;
                        State = PantographState.Up;
                    }
                    break;
            }
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            //TrainEvent soundEvent = TrainEvent.None;

            switch (evt)
            {
                case PowerSupplyEvent.LowerPantograph:
                    if (State == PantographState.Up || State == PantographState.Raising)
                    {
                        State = PantographState.Lowering;

                        switch (Id)
                        {
                            default:
                            case 1:
                                //soundEvent = TrainEvent.Pantograph1Down;
                                Confirm(CabControl.Pantograph1, CabSetting.Off);
                                break;

                            case 2:
                                //soundEvent = TrainEvent.Pantograph2Down;
                                Confirm(CabControl.Pantograph2, CabSetting.Off);
                                break;

                            case 3:
                                //soundEvent = TrainEvent.Pantograph3Down;
                                Confirm(CabControl.Pantograph3, CabSetting.Off);
                                break;

                            case 4:
                                //soundEvent = TrainEvent.Pantograph4Down;
                                Confirm(CabControl.Pantograph4, CabSetting.Off);
                                break;
                        }
                    }

                    break;

                case PowerSupplyEvent.RaisePantograph:
                    if (State == PantographState.Down || State == PantographState.Lowering)
                    {
                        State = PantographState.Raising;

                        switch (Id)
                        {
                            default:
                            case 1:
                                //soundEvent = TrainEvent.Pantograph1Up;
                                Confirm(CabControl.Pantograph1, CabSetting.On);
                                break;

                            case 2:
                                //soundEvent = TrainEvent.Pantograph2Up;
                                Confirm(CabControl.Pantograph2, CabSetting.On);
                                break;

                            case 3:
                                //soundEvent = TrainEvent.Pantograph3Up;
                                Confirm(CabControl.Pantograph3, CabSetting.On);
                                break;

                            case 4:
                                //soundEvent = TrainEvent.Pantograph4Up;
                                Confirm(CabControl.Pantograph4, CabSetting.On);
                                break;
                        }

                        if (!simulator.Route.Electrified)
                            simulator.Confirmer.Information(Simulator.Catalog.GetString("Pantograph raised even though this route is not electrified"));
                    }
                    break;
            }
        }

        protected void Confirm(CabControl control, CabSetting setting)
        {
            if (wagon == simulator.PlayerLocomotive)
            {
                simulator.Confirmer?.Confirm(control, setting);
            }
        }

        public ValueTask<PantographSaveState> Snapshot()
        {
            return ValueTask.FromResult(new PantographSaveState()
            {
                PantographState = State,
                Delay = delay,
                Time = time,
            });
        }

        public ValueTask Restore(PantographSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
            State = saveState.PantographState;
            delay = saveState.Delay;
            time = saveState.Time;
            return ValueTask.CompletedTask;
        }
    }
}
