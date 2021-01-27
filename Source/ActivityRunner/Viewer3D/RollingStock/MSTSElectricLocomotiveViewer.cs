// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using System;

using Orts.Common;
using Orts.Common.Input;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.ActivityRunner.Viewer3D.RollingStock
{
    public class MSTSElectricLocomotiveViewer : MSTSLocomotiveViewer
    {
        private readonly MSTSElectricLocomotive electricLocomotive;

        public MSTSElectricLocomotiveViewer(Viewer viewer, MSTSElectricLocomotive car)
            : base(viewer, car)
        {
            electricLocomotive = car;
            if (electricLocomotive.Train != null && (car.Train.TrainType == TrainType.Ai ||
                ((car.Train.TrainType == TrainType.Player || car.Train.TrainType == TrainType.AiPlayerDriven || car.Train.TrainType == TrainType.AiPlayerHosting) &&
                (car.Train.MUDirection != MidpointDirection.N && electricLocomotive.PowerOn))))
                // following reactivates the sound triggers related to certain states
                // for pantos the sound trigger related to the raised panto must be reactivated, else SignalEvent() would raise also another panto
            {
                int iPanto = 0;
                foreach (Pantograph panto in electricLocomotive.Pantographs.List)
                {
                    if (panto.State == PantographState.Up)
                    {
                        switch (iPanto)
                        {
                            case 0: electricLocomotive.SignalEvent(TrainEvent.Pantograph1Up); break;
                            case 1: electricLocomotive.SignalEvent(TrainEvent.Pantograph2Up); break;
                            case 2: electricLocomotive.SignalEvent(TrainEvent.Pantograph3Up); break;
                            case 3: electricLocomotive.SignalEvent(TrainEvent.Pantograph4Up); break;
                            default: electricLocomotive.SignalEvent(TrainEvent.Pantograph1Up); break;
                        }
                    }
                    iPanto++;
                }
                electricLocomotive.SignalEvent(TrainEvent.EnginePowerOn);
                electricLocomotive.SignalEvent(TrainEvent.ReverserToForwardBackward);
                electricLocomotive.SignalEvent(TrainEvent.ReverserChange);
            }
        }

        /// <summary>
        /// A keyboard or mouse click has occured. Read the UserInput
        /// structure to determine what was pressed.
        /// </summary>
        public override void HandleUserInput(in ElapsedTime elapsedTime)
        {
            base.HandleUserInput(elapsedTime);
        }

        public override void InitializeUserInputCommands()
        {
            UserInputCommands.Add(UserCommand.ControlCircuitBreakerClosingOrder, new Action[] {
                () => new CircuitBreakerClosingOrderButtonCommand(Viewer.Log, false),
                () => {
                    new CircuitBreakerClosingOrderCommand(Viewer.Log, !electricLocomotive.PowerSupply.CircuitBreaker.DriverClosingOrder);
                    new CircuitBreakerClosingOrderButtonCommand(Viewer.Log, true);
                }
            });
            UserInputCommands.Add(UserCommand.ControlCircuitBreakerOpeningOrder, new Action[] { () => new CircuitBreakerOpeningOrderButtonCommand(Viewer.Log, false), () => new CircuitBreakerOpeningOrderButtonCommand(Viewer.Log, true)});
            UserInputCommands.Add(UserCommand.ControlCircuitBreakerClosingAuthorization, new Action[] { Noop, () => new CircuitBreakerClosingAuthorizationCommand(Viewer.Log, !electricLocomotive.PowerSupply.CircuitBreaker.DriverClosingAuthorization) });
            base.InitializeUserInputCommands();
        }

        /// <summary>
        /// We are about to display a video frame.  Calculate positions for 
        /// animated objects, and add their primitives to the RenderFrame list.
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {

            base.PrepareFrame(frame, elapsedTime);
        }
    }
}
