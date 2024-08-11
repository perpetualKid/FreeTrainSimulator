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

// This module covers all classes and code for signal, speed post, track occupation and track reservation control

using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.State;

using Orts.Formats.Msts;

namespace Orts.Simulation.Signalling
{
    internal class SignalItemInfo : ISaveStateApi<SignalItemSaveState>
    {
        public SignalItemType ItemType { get; private set; }                     // type information
        public SignalItemFindState State { get; private set; }               // state information

        public Signal SignalDetails { get; private set; }                    // actual object 

        public float DistanceFound { get; private set; }
        public float DistanceToTrain { get; internal set; }
        public float DistanceToObject { get; internal set; }

        public SignalAspectState SignalState { get; internal set; }                   // UNKNOWN if type = speedlimit

        internal SpeedInfo SpeedInfo { get; set; } // set active by TRAIN, speed values are -1 if not set
        public float ActualSpeed { get; internal set; }

        public bool Processed { get; internal set; }                       // for AI trains, set active by TRAIN

        public SignalItemInfo(Signal signal, float distance)
        {
            State = SignalItemFindState.Item;

            DistanceFound = distance;

            SignalDetails = signal ?? throw new ArgumentNullException(nameof(signal));

            if (signal.SignalType == SignalCategory.Signal)
            {
                ItemType = SignalItemType.Signal;
                SignalState = SignalAspectState.Unknown;  // set active by TRAIN
                SpeedInfo = new SpeedInfo(null); // set active by TRAIN
            }
            else
            {
                ItemType = SignalItemType.SpeedLimit;
                SignalState = SignalAspectState.Unknown;
                SpeedInfo = signal.SpeedLimit(SignalFunction.Speed);
            }
        }

        public SignalItemInfo() { }

        public SignalItemInfo(SignalItemFindState state)
        {
            State = state;
        }

        public ValueTask<SignalItemSaveState> Snapshot()
        {
            return ValueTask.FromResult(new SignalItemSaveState()
            {
                SignalItemType = ItemType,
                SignalItemState = State,
                SignalIndex = SignalDetails.Index,
                DistanceFound = DistanceFound,
                DistanceTrain = DistanceToTrain,
                DistanceObject = DistanceToObject,
                PassengerSpeed = SpeedInfo.PassengerSpeed,
                FreightSpeed = SpeedInfo.FreightSpeed,
                Flag = SpeedInfo.Flag,
                ActualSpeed = ActualSpeed,
                Processed = Processed,
            });
        }

        public ValueTask Restore(SignalItemSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            ItemType = saveState.SignalItemType;
            State = saveState.SignalItemState;
            SignalDetails = Simulator.Instance.SignalEnvironment.Signals[saveState.SignalIndex];
            DistanceFound = saveState.DistanceFound;
            DistanceToTrain = saveState.DistanceTrain;
            DistanceToObject = saveState.DistanceObject;
            SpeedInfo = new SpeedInfo(saveState.PassengerSpeed, saveState.FreightSpeed, saveState.Flag, false, 0, false);
            ActualSpeed = saveState.ActualSpeed;

            Processed = saveState.Processed;
            SignalState = SignalDetails.SignalType == SignalCategory.Signal ? SignalDetails.SignalLR(SignalFunction.Normal) : SignalAspectState.Unknown;

            return ValueTask.CompletedTask;
        }
    }

}
