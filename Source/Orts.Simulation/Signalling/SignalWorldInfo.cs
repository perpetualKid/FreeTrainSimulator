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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// class SignalWorldInfo
    ///
    /// </summary>
    //================================================================================================//
    internal class SignalWorldInfo
    {
        public Dictionary<int, int> HeadReference { get; }     // key=TDBIndex, value=headindex
        public BitArray HeadsSet { get; }                          // Flags heads which are set
        public BitArray FlagsSet { get; private set; }                          // Flags signal-flags which are set
        public BitArray FlagsSetBackfacing { get; }                // Flags signal-flags which are set
        //    for backfacing signal
        public List<int> Backfacing { get; } = new List<int>();   // Flags heads which are backfacing

        public string ShapeFileName { get; }

        public SignalWorldInfo(SignalObject signalWorldItem, SignalConfigurationFile signalConfig)
        {
            ArgumentNullException.ThrowIfNull(signalConfig);
            ArgumentNullException.ThrowIfNull(signalWorldItem);

            HeadReference = new Dictionary<int, int>();

            // set flags with length to number of possible SubObjects type

            FlagsSet = new BitArray(EnumExtension.GetLength<SignalSubType>());
            FlagsSetBackfacing = new BitArray(EnumExtension.GetLength<SignalSubType>());

            string fileName = Path.GetFileName(signalWorldItem.FileName);
            ShapeFileName = Path.GetFileNameWithoutExtension(fileName);

            // search defined shapes in SIGCFG to find signal definition

            if (signalConfig.SignalShapes.TryGetValue(fileName, out SignalShape thisCFGShape))
            {
                HeadsSet = new BitArray(thisCFGShape.SignalSubObjs.Count);

                // loop through all heads and check SubObj flag per bit to check if head is set
                uint mask = 1;

                for (int i = 0; i < thisCFGShape.SignalSubObjs.Count; i++)
                {
                    uint headSet = signalWorldItem.SignalSubObject & mask;
                    SignalShape.SignalSubObject signalSubObjects = thisCFGShape.SignalSubObjs[i];
                    if (headSet != 0)
                    {
                        // set head, and if head is flag, also set flag
                        HeadsSet[i] = true;

                        if (signalSubObjects.BackFacing)
                        {
                            Backfacing.Add(i);
                            if ((int)signalSubObjects.SignalSubType >= 1)
                            {
                                FlagsSetBackfacing[(int)signalSubObjects.SignalSubType] = true;
                            }
                        }
                        else if ((int)signalSubObjects.SignalSubType >= 1)
                        {
                            FlagsSet[(int)signalSubObjects.SignalSubType] = true;
                        }
                    }
                    mask <<= 1;
                }

                // get TDB and head reference from World file
                foreach (SignalUnit signalUnitInfo in signalWorldItem.SignalUnits)
                {
                    HeadReference.Add(signalUnitInfo.TrackItem, signalUnitInfo.SubObject);
                }
            }
            else
            {
                Trace.TraceWarning("Signal not found : {0} n", fileName);
            }
        }

        internal void UpdateFlags(BitArray source)
        {
            FlagsSet = new BitArray(source);
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for copy
        /// </summary>
        public SignalWorldInfo(SignalWorldInfo source)
        {
            ArgumentNullException.ThrowIfNull(source);

            Backfacing = source.Backfacing;

            FlagsSet = new BitArray(source.FlagsSet);
            FlagsSetBackfacing = new BitArray(source.FlagsSetBackfacing);
            HeadsSet = new BitArray(source.HeadsSet);

            HeadReference = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> sourceRef in source.HeadReference)
            {
                HeadReference.Add(sourceRef.Key, sourceRef.Value);
            }
        }

    }

}
