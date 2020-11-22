using System.IO;

namespace Orts.Simulation.Track
{
    /// Reversal information class
    internal class TrackCircuitReversalInfo
    {
        public bool Valid { get; private set; }
        public int LastDivergeIndex { get; set; }
        public int FirstDivergeIndex { get; set; }
        public int DivergeSectorIndex { get; set; }
        public float DivergeOffset { get; set; }
        public bool SignalAvailable { get; set; }
        public bool SignalUsed { get; set; }
        public int LastSignalIndex { get; set; }
        public int FirstSignalIndex { get; set; }
        public int SignalSectorIndex { get; set; }
        public float SignalOffset { get; set; }
        public float ReverseReversalOffset { get; set; }
        public int ReversalIndex { get; set; }
        public int ReversalSectionIndex { get; set; }
        public bool ReversalActionInserted { get; set; }

        /// Constructor (from route path details)
        public TrackCircuitReversalInfo(TrackCircuitPartialPathRoute lastRoute, int prevReversalIndex, TrackCircuitPartialPathRoute firstRoute, float reverseReversalOffset, int reversalIndex, int reversalSectionIndex)
        {
            // preset values
            Valid = false;
            LastDivergeIndex = -1;
            FirstDivergeIndex = -1;
            LastSignalIndex = -1;
            FirstSignalIndex = -1;
            SignalAvailable = false;
            SignalUsed = false;
            ReverseReversalOffset = reverseReversalOffset;
            ReversalIndex = reversalIndex;
            ReversalSectionIndex = reversalSectionIndex;
            ReversalActionInserted = false;

            // search for first common section in last and first

            int lastIndex = lastRoute.Count - 1;
            int firstIndex = 0;

            int lastCommonSection = -1;
            int firstCommonSection = -1;

            bool commonFound = false;
            bool validDivPoint = false;

            while (!commonFound && lastIndex >= 0)
            {
                TrackCircuitRouteElement lastElement = lastRoute[lastIndex];

                while (!commonFound && firstIndex <= firstRoute.Count - 1)
                {
                    TrackCircuitRouteElement firstElement = firstRoute[firstIndex];
                    if (lastElement.TrackCircuitSection.Index == firstElement.TrackCircuitSection.Index)
                    {
                        commonFound = true;
                        lastCommonSection = lastIndex;
                        firstCommonSection = firstIndex;

                        Valid = (lastElement.Direction != firstElement.Direction);
                    }
                    else
                    {
                        firstIndex++;
                    }
                }
                lastIndex--;
                firstIndex = 0;
            }

            // search for last common section going backward along route
            // do not go back on last route beyond previous reversal point to prevent fall through of reversals
            if (Valid)
            {
                Valid = false;

                lastIndex = lastCommonSection;
                firstIndex = firstCommonSection;

                int endLastIndex = (prevReversalIndex > 0 && prevReversalIndex < lastCommonSection &&
                    Simulator.Instance.TimetableMode) ? prevReversalIndex : 0;

                while (lastIndex >= endLastIndex && firstIndex <= (firstRoute.Count - 1) && lastRoute[lastIndex].TrackCircuitSection.Index == firstRoute[firstIndex].TrackCircuitSection.Index)
                {
                    LastDivergeIndex = lastIndex;
                    FirstDivergeIndex = firstIndex;
                    DivergeSectorIndex = lastRoute[lastIndex].TrackCircuitSection.Index;

                    lastIndex--;
                    firstIndex++;
                }

                // if next route ends within last one, last diverge index can be set to endLastIndex
                if (firstIndex > firstRoute.Count - 1)
                {
                    LastDivergeIndex = endLastIndex;
                    DivergeSectorIndex = lastRoute[endLastIndex].TrackCircuitSection.Index;
                }

                Valid = LastDivergeIndex >= 0; // it is a reversal
                validDivPoint = true;
                if (Simulator.Instance.TimetableMode)
                    validDivPoint = LastDivergeIndex > 0 && FirstDivergeIndex < (firstRoute.Count - 1); // valid reversal point
                if (lastRoute.Count == 1 && FirstDivergeIndex < (firstRoute.Count - 1)) validDivPoint = true; // valid reversal point in first and only section
            }

            // determine offset

            if (validDivPoint)
            {
                DivergeOffset = 0.0f;
                for (int iSection = LastDivergeIndex; iSection < lastRoute.Count; iSection++)
                {
                    TrackCircuitSection thisSection = lastRoute[iSection].TrackCircuitSection;
                    DivergeOffset += thisSection.Length;
                }

                // find last signal furthest away from diverging point

                bool signalFound = false;
                int startSection = 0;

                if (!Simulator.Instance.TimetableMode)
                // In activity mode test starts only after reverse point.
                {
                    for (int iSection = 0; iSection < firstRoute.Count; iSection++)
                    {
                        if (firstRoute[iSection].TrackCircuitSection.Index == ReversalSectionIndex)
                        {
                            startSection = iSection;
                            break;
                        }
                    }
                    for (int iSection = startSection; iSection <= FirstDivergeIndex && !signalFound; iSection++)
                    {
                        TrackCircuitSection thisSection = firstRoute[iSection].TrackCircuitSection;
                        if (thisSection.EndSignals[firstRoute[iSection].Direction] != null)   // signal in required direction
                        {
                            signalFound = true;
                            FirstSignalIndex = iSection;
                            SignalSectorIndex = thisSection.Index;
                        }
                    }
                }
                // in timetable mode, search for first signal beyond diverging point
                else
                {
                    for (int iSection = FirstDivergeIndex; iSection >= startSection && !signalFound; iSection--)
                    {
                        TrackCircuitSection thisSection = firstRoute[iSection].TrackCircuitSection;
                        if (thisSection.EndSignals[firstRoute[iSection].Direction] != null)   // signal in required direction
                        {
                            signalFound = true;
                            FirstSignalIndex = iSection;
                            SignalSectorIndex = thisSection.Index;
                        }
                    }
                }

                // signal found
                if (signalFound)
                {
                    LastSignalIndex = lastRoute.GetRouteIndex(SignalSectorIndex, LastDivergeIndex);
                    if (LastSignalIndex > 0)
                    {
                        SignalAvailable = true;

                        SignalOffset = 0.0f;
                        for (int iSection = LastSignalIndex; iSection < lastRoute.Count; iSection++)
                        {
                            TrackCircuitSection thisSection = lastRoute[iSection].TrackCircuitSection;
                            SignalOffset += thisSection.Length;
                        }
                    }
                }
            }
            else
            {
                FirstDivergeIndex = -1;
                LastDivergeIndex = -1;
            }

        }//constructor

        /// Constructor (from copy)
        public TrackCircuitReversalInfo(TrackCircuitReversalInfo otherInfo)
        {
            Valid = otherInfo.Valid;

            LastDivergeIndex = otherInfo.LastDivergeIndex;
            FirstDivergeIndex = otherInfo.FirstDivergeIndex;
            DivergeSectorIndex = otherInfo.DivergeSectorIndex;
            DivergeOffset = otherInfo.DivergeOffset;

            SignalAvailable = otherInfo.SignalAvailable;
            SignalUsed = otherInfo.SignalUsed;
            LastSignalIndex = otherInfo.LastSignalIndex;
            FirstSignalIndex = otherInfo.FirstSignalIndex;
            SignalSectorIndex = otherInfo.SignalSectorIndex;
            SignalOffset = otherInfo.SignalOffset;
            ReverseReversalOffset = otherInfo.ReverseReversalOffset;
            ReversalIndex = otherInfo.ReversalIndex;
            ReversalSectionIndex = otherInfo.ReversalSectionIndex;
            ReversalActionInserted = false;
        }

        /// Constructor (for invalid item)
        public TrackCircuitReversalInfo()
        {
            // preset values
            Valid = false;

            LastDivergeIndex = -1;
            FirstDivergeIndex = -1;
            DivergeSectorIndex = -1;
            DivergeOffset = 0.0f;

            LastSignalIndex = -1;
            FirstSignalIndex = -1;
            SignalSectorIndex = -1;
            SignalOffset = 0.0f;

            SignalAvailable = false;
            SignalUsed = false;
            ReverseReversalOffset = 0.0f;
            ReversalIndex = -1;
            ReversalSectionIndex = -1;
            ReversalActionInserted = false;
        }

        /// Constructor for Restore
        public TrackCircuitReversalInfo(BinaryReader inf)
        {
            Valid = inf.ReadBoolean();
            LastDivergeIndex = inf.ReadInt32();
            FirstDivergeIndex = inf.ReadInt32();
            DivergeSectorIndex = inf.ReadInt32();
            DivergeOffset = inf.ReadSingle();

            SignalAvailable = inf.ReadBoolean();
            SignalUsed = inf.ReadBoolean();
            LastSignalIndex = inf.ReadInt32();
            FirstSignalIndex = inf.ReadInt32();
            SignalSectorIndex = inf.ReadInt32();
            SignalOffset = inf.ReadSingle();
            ReverseReversalOffset = inf.ReadSingle();
            ReversalIndex = inf.ReadInt32();
            ReversalSectionIndex = inf.ReadInt32();
            ReversalActionInserted = inf.ReadBoolean();
        }

        /// Save
        public void Save(BinaryWriter outf)
        {
            outf.Write(Valid);
            outf.Write(LastDivergeIndex);
            outf.Write(FirstDivergeIndex);
            outf.Write(DivergeSectorIndex);
            outf.Write(DivergeOffset);
            outf.Write(SignalAvailable);
            outf.Write(SignalUsed);
            outf.Write(LastSignalIndex);
            outf.Write(FirstSignalIndex);
            outf.Write(SignalSectorIndex);
            outf.Write(SignalOffset);
            outf.Write(ReverseReversalOffset);
            outf.Write(ReversalIndex);
            outf.Write(ReversalSectionIndex);
            outf.Write(ReversalActionInserted);
        }

    }//TCReversalInfo

    /// <summary>
    /// Rough Reversal information class, used only during route building.
    /// </summary>
    internal class RoughReversalInfo
    {
        public int SubPathIndex { get; set; }
        public float ReverseReversalOffset { get; set; }
        public int ReversalSectionIndex { get; set; }

        /// Constructor (from route path details)
        public RoughReversalInfo(int subPathIndex, float reverseReversalOffset, int reversalSectionIndex)
        {
            SubPathIndex = subPathIndex;
            ReverseReversalOffset = reverseReversalOffset;
            ReversalSectionIndex = reversalSectionIndex;
        }
    }
}
