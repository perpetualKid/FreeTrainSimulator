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

// #define DEBUG_DEADLOCK
// DEBUG flag for debug prints

using System;
using System.Collections.Generic;
using System.IO;

using Orts.Common;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.World;

namespace Orts.Simulation.AIs
{
    /// <summary>
    /// Abstract class for a programmatic horn pattern sounded by the AI at level crossings.
    /// </summary>
    public abstract class AILevelCrossingHornPattern
    {
        /// <summary>
        /// Determines whether or not to create an <see cref="AIActionHornRef"/> based on the states of the train and the approaching level crossing.
        /// </summary>
        /// <param name="crossing">The level crossing group.</param>
        /// <param name="absoluteSpeedMpS">The absolute value of the current speed of the train.</param>
        /// <param name="distanceToCrossingM">The closest distance between the train (front or rear) and the level crossing (either end).</param>
        /// <returns></returns>
        public abstract bool ShouldActivate(LevelCrossing crossing, float absoluteSpeedMpS, float distanceToCrossingM);

        /// <summary>
        /// Sound the horn pattern using the provided locomotive. Called by <see cref="AuxActionHornItem"/>.
        /// </summary>
        /// <param name="locomotive">The locomotive to manipulate.</param>
        /// <param name="durationS">The duration ("delay") set for this horn event, if set.</param>
        /// <returns>On each iteration, set the locomotive's controls, then yield the clock time until the next step.</returns>
        public abstract IEnumerator<int> Execute(MSTSLocomotive locomotive, int? durationS);

        public static LevelCrossingHornPattern LevelCrossingHornPatternType(AILevelCrossingHornPattern instance)
        {
            return instance switch
            {
                null => LevelCrossingHornPattern.None,
                AILevelCrossingSingleHorn _ => LevelCrossingHornPattern.Single,
                AILevelCrossingAmericanHorn _ => LevelCrossingHornPattern.US,
                _ => throw new InvalidCastException("Invalid LevelCrossingHornPattern"),
            };
        }

        /// <summary>
        /// Get the horn pattern that corresponds to a <see cref="LevelCrossingHornPattern"/> value.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static AILevelCrossingHornPattern CreateInstance(LevelCrossingHornPattern type)
        {
            return type switch
            {
                LevelCrossingHornPattern.Single => new AILevelCrossingSingleHorn(),
                LevelCrossingHornPattern.US => new AILevelCrossingAmericanHorn(),
                LevelCrossingHornPattern.None => null,
                _ => throw new ArgumentException("Invalid LevelCrossingHornPattern:", nameof(type)),
            };
        }
    }
}
