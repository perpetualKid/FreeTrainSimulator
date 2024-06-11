using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Formats.Msts.Models;
using Orts.Models.State;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// TrackCircuit position class
    /// </summary>
    public class TrackCircuitPosition : ISaveStateApi<TrackCircuitPositionSaveState>
    {
        public int TrackCircuitSectionIndex { get; internal set; }
        public TrackDirection Direction { get; internal set; }
        public float Offset { get; internal set; }
        public int RouteListIndex { get; internal set; }
        public int TrackNode { get; private set; }
        public float DistanceTravelled { get; internal set; }

        /// <summary>
        /// constructor - creates empty item
        /// </summary>
        public TrackCircuitPosition()
        {
            TrackCircuitSectionIndex = -1;
            Direction = TrackDirection.Ahead;
            Offset = 0.0f;
            RouteListIndex = -1;
            TrackNode = -1;
            DistanceTravelled = 0.0f;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public TrackCircuitPosition(TrackCircuitPosition source, bool reverse = false)
        {
            ArgumentNullException.ThrowIfNull(source);

            TrackCircuitSectionIndex = source.TrackCircuitSectionIndex;
            Direction = reverse ? source.Direction.Reverse() : source.Direction;
            Offset = source.Offset;
            RouteListIndex = source.RouteListIndex;
            TrackNode = source.TrackNode;
            DistanceTravelled = source.DistanceTravelled;
        }

        public ValueTask<TrackCircuitPositionSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TrackCircuitPositionSaveState()
            {
                Direction = Direction,
                TrackCircuitSectionIndex = TrackCircuitSectionIndex,
                RouteListIndex = RouteListIndex,
                DistanceTravelled = DistanceTravelled,
                Offset = Offset,
                TrackNodeIndex = TrackNode
            });
        }

        public ValueTask Restore(TrackCircuitPositionSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            TrackCircuitSectionIndex = saveState.TrackCircuitSectionIndex;
            Direction = saveState.Direction;
            Offset = saveState.Offset;
            RouteListIndex = saveState.RouteListIndex;
            TrackNode = saveState.TrackNodeIndex;
            DistanceTravelled = saveState.DistanceTravelled;

            return ValueTask.CompletedTask;
        }

        // Update this instance based on another instance
        // this avoid allocation such as using Copy constructor
        internal void UpdateFrom(TrackCircuitPosition source)
        {
            TrackCircuitSectionIndex = source.TrackCircuitSectionIndex;
            Direction = source.Direction;
            Offset = source.Offset;
            RouteListIndex = source.RouteListIndex;
            TrackNode = source.TrackNode;
            DistanceTravelled = source.DistanceTravelled;
        }

        /// <summary>
        /// Reverse (or continue in same direction)
        /// <\summary>

        internal void Reverse(TrackDirection oldDirection, TrackCircuitPartialPathRoute route, float offset)
        {
            ArgumentNullException.ThrowIfNull(route);

            RouteListIndex = route.GetRouteIndex(TrackCircuitSectionIndex, 0);
            Direction = RouteListIndex >= 0 ? route[RouteListIndex].Direction : Direction = Direction.Reverse();

            TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[TrackCircuitSectionIndex];
            if (oldDirection != Direction)
                Offset = section.Length - Offset; // actual reversal so adjust offset

            DistanceTravelled = offset;
        }

        /// <summary>
        /// Set the position based on the trackcircuit section.
        /// </summary>
        /// <param name="trackCircuitCrossReferecenList">List of cross-references from tracknode to trackcircuitsection</param>
        /// <param name="offset">Offset along the tracknode</param>
        /// <param name="direction">direction along the tracknode (1 is forward)</param>
        internal void SetPosition(TrackCircuitCrossReferences trackCircuitCrossReferecenList, float offset, TrackDirection direction)
        {
            ArgumentNullException.ThrowIfNull(trackCircuitCrossReferecenList);

            int crossRefIndex = trackCircuitCrossReferecenList.GetCrossReferenceIndex(offset, direction);

            if (crossRefIndex < 0)
                return;

            TrackCircuitSectionCrossReference crossReference = trackCircuitCrossReferecenList[crossRefIndex];
            TrackCircuitSectionIndex = crossReference.Index;
            Direction = direction;
            Offset = offset - crossReference.OffsetLength[direction];
        }
    }

}
