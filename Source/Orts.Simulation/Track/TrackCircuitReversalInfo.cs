using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Orts.Models.State;

namespace Orts.Simulation.Track
{
    /// Reversal information class
    internal class TrackCircuitReversalInfo : ISaveStateApi<TrackCircuitReversalInfoSaveState>
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

        public ValueTask<TrackCircuitReversalInfoSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TrackCircuitReversalInfoSaveState()
            { 
                Valid = Valid,
                LastDivergeIndex = LastDivergeIndex,
                FirstDivergeIndex= FirstDivergeIndex,
                DivergeSectorIndex = DivergeSectorIndex,
                DivergeOffset = DivergeOffset,
                SignalAvailable = SignalAvailable,
                SignalUsed = SignalUsed,
                LastSignalIndex = LastSignalIndex,
                FirstSignalIndex = FirstSignalIndex,
                SignalSectorIndex = SignalSectorIndex,
                SignalOffset = SignalOffset,
                ReverseReversalOffset = ReverseReversalOffset,
                ReversalIndex = ReversalIndex,
                ReversalSectionIndex = ReversalSectionIndex,
                ReversalActionInserted = ReversalActionInserted,
            });
        }

        public ValueTask Restore(TrackCircuitReversalInfoSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            Valid = saveState.Valid;
            LastDivergeIndex = saveState.LastDivergeIndex;
            FirstDivergeIndex = saveState.FirstDivergeIndex;
            DivergeSectorIndex = saveState.DivergeSectorIndex;
            DivergeOffset = saveState.DivergeOffset;

            SignalAvailable = saveState.SignalAvailable;
            SignalUsed = saveState.SignalUsed;
            LastSignalIndex = saveState.LastSignalIndex;
            FirstSignalIndex = saveState.FirstSignalIndex;
            SignalSectorIndex = saveState.SignalSectorIndex;
            SignalOffset = saveState.SignalOffset;
            ReverseReversalOffset = saveState.ReverseReversalOffset;
            ReversalIndex = saveState.ReversalIndex;
            ReversalSectionIndex = saveState.ReversalSectionIndex;
            ReversalActionInserted = saveState.ReversalActionInserted;

            return ValueTask.CompletedTask;
        }
    }

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
