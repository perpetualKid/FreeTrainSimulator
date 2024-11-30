// COPYRIGHT 2013 by the Open Rails project.
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

/* TRAINS
 * 
 * Contains code to represent a train as a list of TrainCars and to handle the physics of moving
 * the train through the Track Database.
 * 
 * A train has:
 *  - a list of TrainCars 
 *  - a front and back position in the TDB ( represented by TDBTravellers )
 *  - speed
 *  - MU signals that are relayed from player locomtive to other locomotives and cars such as:
 *      - direction
 *      - throttle percent
 *      - brake percent  ( TODO, this should be changed to brake pipe pressure )
 *      
 *  Individual TrainCars provide information on friction and motive force they are generating.
 *  This is consolidated by the train class into overall movement for the train.
 */

// Debug Calculation of Aux Tender operation
// #define DEBUG_AUXTENDER

// Debug for calculation of speed forces
// #define DEBUG_SPEED_FORCES

// Debug for calculation of Advanced coupler forces
// #define DEBUG_COUPLER_FORCES

using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

namespace Orts.Simulation.Signalling
{
    public class EndAuthority : ISaveStateApi<AuthoritySaveState>
    {
        public EndAuthorityType EndAuthorityType { get; set; } = EndAuthorityType.NoPathReserved;
        public int LastReservedSection { get; set; } = -1;// index of furthest cleared section (for NODE control)
        public float Distance { get; set; }// distance to end of authority

        public ValueTask Restore(AuthoritySaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            EndAuthorityType = saveState.EndAuthorityType;
            LastReservedSection = saveState.LastReservedSection;
            Distance = saveState.Distance;

            return ValueTask.CompletedTask;
        }

        public ValueTask<AuthoritySaveState> Snapshot()
        {
            return ValueTask.FromResult(new AuthoritySaveState()
            {
                EndAuthorityType = EndAuthorityType,
                LastReservedSection = LastReservedSection,
                Distance = Distance,
            });
        }
    }
}
