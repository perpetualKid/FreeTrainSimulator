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
using System.Collections.Immutable;

using FreeTrainSimulator.Common;

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

    internal class TunnelInfoData : SectionInfoBase
    {
        private static readonly float singleTunnelArea;
        private static readonly float singleTunnelPerimeter;
        private static readonly float doubleTunnelArea;
        private static readonly float doubleTunnelPerimeter;

        static TunnelInfoData()
        {
            ImmutableDictionary<string, string> settings = Simulator.Instance?.RouteModel?.Settings;
            ArgumentNullException.ThrowIfNull(settings, nameof(settings));

            if (settings.TryGetValue("SingleTunnelArea", out string settingValue))
                _ = float.TryParse(settingValue, out singleTunnelArea);
            if (settings.TryGetValue("SingleTunnelPerimeter", out settingValue))
                _ = float.TryParse(settingValue, out singleTunnelPerimeter);
            if (settings.TryGetValue("DoubleTunnelArea", out settingValue))
                _ = float.TryParse(settingValue, out doubleTunnelArea);
            if (settings.TryGetValue("DoubleTunnelPerimeter", out settingValue))
                _ = float.TryParse(settingValue, out doubleTunnelPerimeter);

            float speedLimit = Simulator.Instance.RouteModel.SpeedRestrictions[SpeedRestrictionType.Route];
            // if no values are in TRK file, calculate default values.
            // Single track Tunnels

            if (singleTunnelArea == 0)
            {

                if (speedLimit >= 97.22) // if route speed greater then 350km/h
                {
                    singleTunnelArea = 70.0f;
                    singleTunnelPerimeter = 32.0f;
                }
                else if (speedLimit >= 69.4 && speedLimit < 97.22) // Route speed greater then 250km/h and less then 350km/h
                {
                    singleTunnelArea = 70.0f;
                    singleTunnelPerimeter = 32.0f;
                }
                else if (speedLimit >= 55.5 && speedLimit < 69.4) // Route speed greater then 200km/h and less then 250km/h
                {
                    singleTunnelArea = 58.0f;
                    singleTunnelPerimeter = 28.0f;
                }
                else if (speedLimit >= 44.4 && speedLimit < 55.5) // Route speed greater then 160km/h and less then 200km/h
                {
                    singleTunnelArea = 50.0f;
                    singleTunnelPerimeter = 25.5f;
                }
                else if (speedLimit >= 33.3 && speedLimit < 44.4) // Route speed greater then 120km/h and less then 160km/h
                {
                    singleTunnelArea = 42.0f;
                    singleTunnelPerimeter = 22.5f;
                }
                else       // Route speed less then 120km/h
                {
                    singleTunnelArea = 21.0f;  // Typically older slower speed designed tunnels
                    singleTunnelPerimeter = 17.8f;
                }
            }

            // Double track Tunnels

            if (doubleTunnelArea == 0)
            {

                if (speedLimit >= 97.22) // if route speed greater then 350km/h
                {
                    doubleTunnelArea = 100.0f;
                    doubleTunnelPerimeter = 37.5f;
                }
                else if (speedLimit >= 69.4 && speedLimit < 97.22) // Route speed greater then 250km/h and less then 350km/h
                {
                    doubleTunnelArea = 100.0f;
                    doubleTunnelPerimeter = 37.5f;
                }
                else if (speedLimit >= 55.5 && speedLimit < 69.4) // Route speed greater then 200km/h and less then 250km/h
                {
                    doubleTunnelArea = 90.0f;
                    doubleTunnelPerimeter = 35.0f;
                }
                else if (speedLimit >= 44.4 && speedLimit < 55.5) // Route speed greater then 160km/h and less then 200km/h
                {
                    doubleTunnelArea = 80.0f;
                    doubleTunnelPerimeter = 34.5f;
                }
                else if (speedLimit >= 33.3 && speedLimit < 44.4) // Route speed greater then 120km/h and less then 160km/h
                {
                    doubleTunnelArea = 76.0f;
                    doubleTunnelPerimeter = 31.0f;
                }
                else       // Route speed less then 120km/h
                {
                    doubleTunnelArea = 41.8f;  // Typically older slower speed designed tunnels
                    doubleTunnelPerimeter = 25.01f;
                }
            }
        }

        public int NumberPaths { get; } // number of paths through this item

        public float CrossSectionArea => NumberPaths > 1 ? doubleTunnelArea : singleTunnelArea;
        public float Perimeter => NumberPaths > 1 ? doubleTunnelPerimeter : singleTunnelPerimeter;


        public TunnelInfoData(int numberPaths, float tunnelStart, float tunnelEnd, float lengthInSection, float length, float trackcircuitSectionLength, float startOffset) :
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
