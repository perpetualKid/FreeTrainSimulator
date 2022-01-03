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
using System.IO;

using Orts.Formats.Msts;

namespace Orts.Simulation.Signalling
{
    internal class SignalItemInfo
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

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

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

        public SignalItemInfo(SignalItemFindState state)
        {
            State = state;
        }

        public static SignalItemInfo Restore(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            SignalItemInfo result = new SignalItemInfo(SignalItemFindState.None)
            {
                ItemType = (SignalItemType)inf.ReadInt32(),
                State = (SignalItemFindState)inf.ReadInt32(),
                SignalDetails = Simulator.Instance.SignalEnvironment.Signals[inf.ReadInt32()],
                DistanceFound = inf.ReadSingle(),
                DistanceToTrain = inf.ReadSingle(),
                DistanceToObject = inf.ReadSingle(),
                SpeedInfo = new SpeedInfo(inf.ReadSingle(), inf.ReadSingle(), inf.ReadBoolean(), false, 0, false),
                ActualSpeed = inf.ReadSingle(),

                Processed = inf.ReadBoolean()
            };
            result.SignalState = result.SignalDetails.SignalType == SignalCategory.Signal ? result.SignalDetails.SignalLR(SignalFunction.Normal) : SignalAspectState.Unknown;

            return (result);
        }

        public static void Save(BinaryWriter outf, SignalItemInfo item)
        {
            if (null == item)
                return;
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));

            outf.Write((int)item.ItemType);
            outf.Write((int)item.State);

            outf.Write(item.SignalDetails.Index);

            outf.Write(item.DistanceFound);
            outf.Write(item.DistanceToTrain);
            outf.Write(item.DistanceToObject);

            outf.Write(item.SpeedInfo.PassengerSpeed);
            outf.Write(item.SpeedInfo.FreightSpeed);
            outf.Write(item.SpeedInfo.Flag);
            outf.Write(item.ActualSpeed);

            outf.Write(item.Processed);
        }


    }

}
