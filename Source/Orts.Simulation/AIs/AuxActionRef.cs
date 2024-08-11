// COPYRIGHT 2014, 2015 by the Open Rails project.
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
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.State;

namespace Orts.Simulation.AIs
{
    //================================================================================================//
    /// <summary>
    /// AuxActionRef
    /// The main class to define Auxiliary Action through the editor and used by ActivityRunner|
    /// </summary>

    public abstract class AuxActionRef : ISaveStateApi<AuxActionRefSaveState>
    {
        public bool GenericAction { get; protected set; }
        public AuxiliaryAction ActionType { get; protected set; }

        //================================================================================================//
        /// <summary>
        /// AIAuxActionsRef: Generic Constructor
        /// The specific datas are used to fired the Action.
        /// </summary>

        protected AuxActionRef(AuxiliaryAction actionType, bool isGeneric)
        {
            GenericAction = isGeneric;
            ActionType = actionType;
        }

        protected AuxActionRef(AuxiliaryAction actionType = AuxiliaryAction.None)
        {
            GenericAction = true;
            ActionType = actionType;
        }

        public virtual ValueTask<AuxActionRefSaveState> Snapshot()
        {
            return ValueTask.FromResult(new AuxActionRefSaveState());
        }

        public virtual ValueTask Restore(AuxActionRefSaveState saveState)
        { 
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
            return ValueTask.CompletedTask;
        }

    }

    //  AuxActionHorn is always a Generic Action, no need to specify a location
    public class AuxActionHorn : AuxActionRef
    {
        public int Delay { get; protected set; }
        public float RequiredDistance { get; protected set; }
        public LevelCrossingHornPattern Pattern { get; private set; }

        public AuxActionHorn(bool generic, int delay = 2, float requiredDistance = 0, LevelCrossingHornPattern hornPattern = LevelCrossingHornPattern.Single) :
            base(AuxiliaryAction.SoundHorn, generic)
        {
            Delay = delay;
            RequiredDistance = requiredDistance;
            Pattern = hornPattern;
        }
    }
}
