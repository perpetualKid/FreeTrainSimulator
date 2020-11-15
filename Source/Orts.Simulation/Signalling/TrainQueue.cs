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

using System.Collections.Generic;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// Class for track circuit state train occupied
    /// Class is child of Queue class
    ///
    /// </summary>
    //================================================================================================//
    internal class TrainQueue : Queue<Train.TrainRouted>, ICollection<Train.TrainRouted>
    {
        public bool IsReadOnly => false;

        //================================================================================================//
        /// <summary>
        /// Peek top train from queue
        /// </summary>
        public Train PeekTrain()
        {
            if (Count == 0)
                return (null);
            return Peek().Train;
        }

        //================================================================================================//
        /// <summary>
        /// Check if queue contains routed train
        /// </summary>
        public bool ContainsTrain(Train.TrainRouted thisTrain)
        {
            if (thisTrain == null)
                return (false);
            return Contains(thisTrain.Train.routedForward) || Contains(thisTrain.Train.routedBackward);
        }

        public void Add(Train.TrainRouted item)
        {
            Enqueue(item);
        }

        public bool Remove(Train.TrainRouted item)
        {
            if (item == null)
                return false;
            bool trainFound = false;

            List<Train.TrainRouted> list = new List<Train.TrainRouted>();

            // extract trains from queue and store in list, keeping the order
            // and remove the train which is to be removed
            while (Count > 0)
            {
                Train.TrainRouted top = Dequeue();
                if (top.Train != item.Train)
                {
                    list.Add(top);
                }
                else
                    trainFound = true;
            }
            // requeing
            foreach (Train.TrainRouted queueTrain in list)
            {
                Enqueue(queueTrain);
            }
            return trainFound;
        }
    }

}
