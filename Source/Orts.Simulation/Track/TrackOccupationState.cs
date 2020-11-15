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
using System.Collections.Generic;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Track
{
    //================================================================================================//
    /// <summary>
    ///
    /// subclass for TrackCircuitState
    /// Class for track circuit state train occupied
    ///
    /// </summary>
    //================================================================================================//
    [Serializable]
    internal class TrackOccupationState : Dictionary<Train.TrainRouted, int>
    {
        public TrackOccupationState() 
        { }

        //================================================================================================//
        /// <summary>
        /// Check if it contains specified train
	    /// Routed
        /// </summary>
        public bool ContainsTrain(Train.TrainRouted train)
        {
            if (train == null) 
                return (false);
            return (ContainsKey(train.Train.routedForward) || ContainsKey(train.Train.routedBackward));
        }

        //================================================================================================//
        /// <summary>
        /// Check if it contains specified train
	    /// Unrouted
        /// </summary>
        public bool ContainsTrain(Train train)
        {
            if (train == null) 
                return (false);
            return (ContainsKey(train.routedForward) || ContainsKey(train.routedBackward));
        }

        //================================================================================================//
        /// <summary>
        /// Remove train from list
	    /// Routed
        /// </summary>
        public void RemoveTrain(Train.TrainRouted train)
        {
            if (train != null)
            {
                Remove(train.Train.routedForward);
                Remove(train.Train.routedBackward);
            }
        }

        protected TrackOccupationState(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }
    }

}
