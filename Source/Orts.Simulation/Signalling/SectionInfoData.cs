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

using Orts.Common;

namespace Orts.Simulation.Signalling
{
    internal class SectionInfoBase
    {
        public EnumArray<float, TrackDirection> Start { get; } = new EnumArray<float, TrackDirection>();              // start position of tunnel : -1 if start is in tunnel

        public EnumArray<float, TrackDirection> End { get; } = new EnumArray<float, TrackDirection>();                // end position of tunnel : -1 if end is in tunnel
        public float LengthInSection { get; }                                                           // length of tunnel within this TCS
        public float LengthTotal { get; }                                                               // total length of tunnel
        public EnumArray<float, TrackDirection> SectionStartOffset { get; } = new EnumArray<float, TrackDirection>(); // offset in tunnel of start of this TCS : -1 if tunnel start in this TCS

        public SectionInfoBase(float start, float end, float lengthInSection, float length, float trackcircuitSectionLength, float startOffset)
        {
            LengthInSection = lengthInSection;
            LengthTotal = length;
            Start[TrackDirection.Reverse] = start;
            End[TrackDirection.Reverse] = end;
            SectionStartOffset[TrackDirection.Reverse] = startOffset;

            Start[TrackDirection.Ahead] = end < 0 ? -1 : trackcircuitSectionLength - end;
            End[TrackDirection.Ahead] = start < 0 ? -1 : trackcircuitSectionLength - start;

            if (start >= 0)
            {
                SectionStartOffset[TrackDirection.Ahead] = -1;
            }
            else if (startOffset < 0)
            {
                SectionStartOffset[TrackDirection.Ahead] = LengthTotal - lengthInSection;
            }
            else
            {
                SectionStartOffset[TrackDirection.Ahead] = LengthTotal - startOffset - trackcircuitSectionLength;
            }
        }
    }

    internal class TunnelInfoData: SectionInfoBase
    {
        public int NumberPaths { get; }                                                                 // number of paths through this item

        public TunnelInfoData(int numberPaths, float tunnelStart, float tunnelEnd, float lengthInSection, float length,  float trackcircuitSectionLength, float startOffset): 
            base(tunnelStart, tunnelEnd, lengthInSection, length, trackcircuitSectionLength, startOffset)
        {
            NumberPaths = numberPaths;
        }
    }

    internal class TroughInfoData : SectionInfoBase
    {
        public TroughInfoData(float tunnelStart, float tunnelEnd, float lengthInSection, float length, float trackcircuitSectionLength, float startOffset) :
            base(tunnelStart, tunnelEnd, lengthInSection, length, trackcircuitSectionLength, startOffset)
        {
        }
    }
}
