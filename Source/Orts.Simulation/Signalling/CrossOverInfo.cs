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

using FreeTrainSimulator.Common;

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    /// Class for cross over items
    /// </summary>
    //================================================================================================//
    internal class CrossOverInfo
    {
        public class Detail
        {
            public float Position { get; internal set; } // position within track sections //
            public int SectionIndex { get; internal set; } // indices of original sections   //
            public int ItemIndex { get; internal set; }  // TDB item indices               //
        }

        public int TrackShape { get; }

        public EnumArray<Detail, SignalLocation> Details { get; } = new EnumArray<Detail, SignalLocation>();

        public CrossOverInfo(float position0, float position1, int sectionIndex0, int sectionIndex1, int itemIndex0, int itemIndex1, int trackShape)
        {
            Details[SignalLocation.NearEnd] = new Detail()
            {
                Position = position0,
                SectionIndex = sectionIndex0,
                ItemIndex = itemIndex0,
            };
            Details[SignalLocation.FarEnd] = new Detail()
            {
                Position = position1,
                SectionIndex = sectionIndex1,
                ItemIndex = itemIndex1,
            };
            TrackShape = trackShape;
        }

        public void Update(float position1, int sectionIndex1)
        {
            Details[SignalLocation.FarEnd].Position = position1;
            Details[SignalLocation.FarEnd].SectionIndex = sectionIndex1;
        }
    }

}
