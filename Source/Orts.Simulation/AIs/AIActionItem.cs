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

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;

using Orts.Models.State;
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.AIs
{
    /// <summary>
    /// AIActionItem class : class to hold info on next restrictive action
    /// </summary>
    internal class AIActionItem : DistanceTravelledItem
    {
        public float RequiredSpeedMpS { get; set; }
        public float ActivateDistanceM { get; set; }
        public float InsertedDistanceM { get; set; }
        internal SignalItemInfo ActiveItem { get; set; }
        public int ReqTablePath { get; set; }

        public AiActionType NextAction { get; set; } = AiActionType.None;

        //================================================================================================//
        /// <summary>
        /// constructor for AIActionItem
        /// </summary>

        public AIActionItem() { }

        internal AIActionItem(SignalItemInfo thisItem, AiActionType thisAction)
        {
            ActiveItem = thisItem;
            NextAction = thisAction;
        }

        public void SetParam(float requiredDistance, float requiredSpeedMpS, float activateDistanceM, float insertedDistanceM)
        {
            RequiredDistance = requiredDistance;
            RequiredSpeedMpS = requiredSpeedMpS;
            ActivateDistanceM = activateDistanceM;
            InsertedDistanceM = insertedDistanceM;
        }

        public override async ValueTask<ActionItemSaveState> Snapshot()
        {
            ActionItemSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.ActionItemType = ActionItemType.AiActionItem;

            saveState.RequiredSpeed = RequiredSpeedMpS;
            saveState.ActivateDistance = ActivateDistanceM;
            saveState.InsertedDistance = InsertedDistanceM;
            saveState.RequestedTablePath = ReqTablePath;

            saveState.SignalItemSaveState = ActiveItem == null ? null : await ActiveItem.Snapshot().ConfigureAwait(false);
            saveState.NextActionType = NextAction;
            return saveState;
        }

        public override async ValueTask Restore([NotNull] ActionItemSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);

            RequiredSpeedMpS = saveState.RequiredSpeed;
            ActivateDistanceM = saveState.ActivateDistance;
            InsertedDistanceM = saveState.InsertedDistance;
            ReqTablePath = saveState.RequestedTablePath;

            if (null != saveState.SignalItemSaveState)
            {
                ActiveItem = new SignalItemInfo();
                await ActiveItem.Restore(saveState.SignalItemSaveState).ConfigureAwait(false);
            }
            NextAction = saveState.NextActionType;
        }

        //================================================================================================//
        //
        //  Generic Handler for all derived class
        //
        public virtual bool ValidAction(Train thisTrain)
        {
            return false;
        }

        public virtual AiMovementState InitAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            return movementState;
        }

        public virtual AiMovementState HandleAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            return movementState;
        }

        public virtual AiMovementState ProcessAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            return movementState;
        }

        public virtual AiMovementState ProcessAction(Train thisTrain, int presentTime)
        {
            return AiMovementState.Static;
        }

        public virtual string AsString(AITrain thisTrain)
        {
            return " ??(";
        }
    }
}
