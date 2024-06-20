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

using System.Collections.Generic;

using Orts.Simulation.RollingStocks;
using Orts.Simulation.World;

namespace Orts.Simulation.AIs
{
    /// <summary>
    /// Sound the long-long-short-long pattern used in the United States and Canada, and stop the bell after 30 seconds if triggered.
    /// </summary>
    public class AILevelCrossingAmericanHorn : AILevelCrossingHornPattern
    {
        /// <summary>
        /// Sound the horn within 19s of crossing to accomodate the full sequence.
        /// </summary>
        public override bool ShouldActivate(LevelCrossing crossing, float absoluteSpeedMpS, float distanceToCrossingM)
            => distanceToCrossingM / absoluteSpeedMpS < 19f;

        /// <summary>
        /// This pattern ignores the supplied duration.
        /// </summary>
        public override IEnumerator<int> Execute(MSTSLocomotive locomotive, int? durationS)
        {
            locomotive.ManualHorn = true;
            yield return 3;

            locomotive.ManualHorn = false;
            yield return 2;

            locomotive.ManualHorn = true;
            yield return 3;

            locomotive.ManualHorn = false;
            yield return 2;

            locomotive.ManualHorn = true;
            yield return 0;

            locomotive.ManualHorn = false;
            yield return 1;

            locomotive.ManualHorn = true;
            yield return 8;

            locomotive.ManualHorn = false;

            if (locomotive.DoesHornTriggerBell)
            {
                yield return 11;
                locomotive.BellState = MSTSLocomotive.SoundState.Stopped;
            }
        }
    }
}
