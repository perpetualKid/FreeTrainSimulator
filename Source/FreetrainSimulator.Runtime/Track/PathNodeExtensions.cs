using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Extension methods to query and navigate train path node collections.
    /// </summary>
    public static class PathNodeExtensions
    {
        /// <summary>
        /// Returns the first node matching <paramref name="targetType"/>, searching backwards for
        /// <see cref="PathNodeType.End"/> and forwards for all other types.
        /// </summary>
        public static PathNode NodeOfType(this IList<PathNode> pathNodes, PathNodeType targetType) => NodeOfType(pathNodes, targetType, static node => node.NodeType);

        /// <summary>
        /// Returns the first path point matching <paramref name="targetType"/>, searching backwards for
        /// <see cref="PathNodeType.End"/> and forwards for all other types.
        /// </summary>
        public static TrainPathPointBase NodeOfType(this IList<TrainPathPointBase> pathNodes, PathNodeType targetType) => NodeOfType(pathNodes, targetType, static node => node.NodeType);

        private static T NodeOfType<T>(IList<T> pathNodes, PathNodeType targetType, Func<T, PathNodeType> nodeTypeSelector) where T : class
        {
            ArgumentNullException.ThrowIfNull(pathNodes, nameof(pathNodes));

            if (targetType == PathNodeType.End)
            {
                for (int i = pathNodes.Count - 1; i >= 0; i--)
                {
                    if (nodeTypeSelector(pathNodes[i]).Includes(targetType))
                        return pathNodes[i];
                }
            }
            else
            {
                for (int i = 0; i < pathNodes.Count; i++)
                {
                    if (nodeTypeSelector(pathNodes[i]).Includes(targetType))
                        return pathNodes[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the successor of <paramref name="currentPathPoint"/> along the requested path section,
        /// or <see langword="null"/> if there is none or the stored index is out of range.
        /// </summary>
        public static TrainPathPointBase NextPathPoint(this IList<TrainPathPointBase> pathPoints, TrainPathPointBase currentPathPoint, PathSectionType pathType)
        {
            ArgumentNullException.ThrowIfNull(pathPoints, nameof(pathPoints));
            ArgumentNullException.ThrowIfNull(currentPathPoint, nameof(currentPathPoint));

            int nextIndex = pathType switch
            {
                PathSectionType.MainPath => currentPathPoint.NextMainNode,
                PathSectionType.PassingPath => currentPathPoint.NextSidingNode,
                _ => -1,
            };

            return (uint)nextIndex < (uint)pathPoints.Count ? pathPoints[nextIndex] : null;
        }

        /// <summary>
        /// Gets the predecessor of <paramref name="currentPathPoint"/>, i.e. the point whose next-node index
        /// (main or siding) references the current point, or <see langword="null"/> if there is none.
        /// </summary>
        public static TrainPathPointBase PreviousPathPoint(this IList<TrainPathPointBase> pathPoints, TrainPathPointBase currentPathPoint, PathSectionType pathType)
        {
            ArgumentNullException.ThrowIfNull(pathPoints, nameof(pathPoints));
            ArgumentNullException.ThrowIfNull(currentPathPoint, nameof(currentPathPoint));

            if (pathType is not (PathSectionType.MainPath or PathSectionType.PassingPath))
                return null;

            int currentPathPointIndex = -1;
            for (int i = pathPoints.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(pathPoints[i], currentPathPoint))
                {
                    currentPathPointIndex = i;
                    break;
                }
            }

            if (currentPathPointIndex < 0)
                return null;

            // a predecessor always precedes the current point in the list
            for (int i = currentPathPointIndex - 1; i >= 0; i--)
            {
                int nextIndex = pathType == PathSectionType.MainPath ? pathPoints[i].NextMainNode : pathPoints[i].NextSidingNode;
                if (nextIndex == currentPathPointIndex)
                    return pathPoints[i];
            }
            return null;
        }
    }
}
