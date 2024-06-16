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

using Orts.Simulation.RollingStocks;
using Orts.Simulation.World;

namespace Orts.Simulation.AIs
{
    /// <summary>
    /// Sound a single blast just before reaching the level crossing, with a slightly randomized duration, and stop the bell after 30 seconds if triggered.
    /// </summary>
    public class AILevelCrossingSingleHorn : AILevelCrossingHornPattern
    {
        /// <summary>
        /// Sound the horn within 6s of the crossing.
        /// </summary>
        public override bool ShouldActivate(LevelCrossing crossing, float absoluteSpeedMpS, float distanceToCrossingM)
            => distanceToCrossingM / absoluteSpeedMpS < 6f;

        public override IEnumerator<int> Execute(MSTSLocomotive locomotive, int? durationS)
        {
            if (!durationS.HasValue)
            {
                // Sound the horn for a pseudorandom period of seconds between 2 and 5.
                durationS = (DateTime.Now.Millisecond % 10) / 3 + 2;
            }

            locomotive.ManualHorn = true;
            yield return durationS.Value;

            locomotive.ManualHorn = false;

            if (locomotive.DoesHornTriggerBell)
            {
                yield return 30 - durationS.Value;
                locomotive.BellState = MSTSLocomotive.SoundState.Stopped;
            }
        }
    }
}
