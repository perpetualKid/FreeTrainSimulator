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

/* AI
 * 
 * Contains code to initialize and control AI trains.
 * 
 */

using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Models.State;

namespace Orts.Simulation.Timetables
{
    public class TriggerActivation : ISaveStateApi<TriggerActivationSaveState>
    {
        public int ActivatedTrain { get; set; }                                     // train to be activated
        public TriggerActivationType ActivationType { get; set; }                   // type of activation
        public int PlatformId { get; set; }                                         // trigger platform ident (in case of station stop)
        public string ActivatedTrainName { get; set; }                              // name of activated train (used in processing timetable only)

        public ValueTask Restore(TriggerActivationSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            ActivatedTrain = saveState.ActivatedTrain;
            ActivationType = saveState.ActivationType;
            PlatformId = saveState.PlatformId;
            ActivatedTrainName = saveState.ActivatedTrainName;

            return ValueTask.CompletedTask;
        }

        public ValueTask<TriggerActivationSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TriggerActivationSaveState()
            { 
                ActivationType = ActivationType,
                PlatformId = PlatformId,
                ActivatedTrain = ActivatedTrain,
                ActivatedTrainName = ActivatedTrainName
            });
        }
    }
}

