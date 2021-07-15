// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

using Orts.Simulation.Physics;

namespace Orts.Simulation.RollingStocks
{

    public class TrainSwitcherData
    {
        public Train PickedTrainFromList { get; set; }
        public bool ClickedTrainFromList { get; set; }
        public Train SelectedAsPlayer { get; set; }
        public bool ClickedSelectedAsPlayer { get; set; }
        public bool SuspendOldPlayer { get; set; }
    }
}
