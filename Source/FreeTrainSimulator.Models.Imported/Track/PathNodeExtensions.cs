using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public static class PathNodeExtensions
    {
        public static PathNode NodeOfType(this IList<PathNode> pathNodes, PathNodeType targetType)
        {
            ArgumentNullException.ThrowIfNull(pathNodes, nameof(pathNodes));

            if (targetType == PathNodeType.End)
            {
                for (int i = pathNodes.Count - 1; i >= 0; i--)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            else
            {
                for (int i = 0; i < pathNodes.Count; i++)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            return null;
        }

        public static TrainPathPointBase NodeOfType(this IList<TrainPathPointBase> pathNodes, PathNodeType targetType)
        {
            ArgumentNullException.ThrowIfNull(pathNodes, nameof(pathNodes));

            if (targetType == PathNodeType.End)
            {
                for (int i = pathNodes.Count - 1; i >= 0; i--)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            else
            {
                for (int i = 0; i < pathNodes.Count; i++)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            return null;
        }

        public static TrainPathPointBase NextPathPoint(this IList<TrainPathPointBase> pathPoints, TrainPathPointBase currentPathPoint, PathSectionType pathType)
        {
            ArgumentNullException.ThrowIfNull(pathPoints, nameof(pathPoints));
            ArgumentNullException.ThrowIfNull(currentPathPoint, nameof(currentPathPoint));

            return pathType switch
            {
                PathSectionType.MainPath => (currentPathPoint.NextMainNode > -1 && pathPoints.Count > currentPathPoint.NextMainNode) ?
                pathPoints[currentPathPoint.NextMainNode] : null,
                PathSectionType.PassingPath => (currentPathPoint.NextSidingNode > -1 && pathPoints.Count > currentPathPoint.NextSidingNode) ?
                    pathPoints[currentPathPoint.NextSidingNode] : null,
                _ => null,
            };
        }

        public static TrainPathPointBase PreviousPathPoint(this IList<TrainPathPointBase> pathPoints, TrainPathPointBase currentPathPoint, PathSectionType pathType)
        {
            ArgumentNullException.ThrowIfNull(pathPoints, nameof(pathPoints));
            ArgumentNullException.ThrowIfNull(currentPathPoint, nameof(currentPathPoint));

            int currentPathPointIndex = -1;
            for (int i = pathPoints.Count - 1; i >= 0; i--)
            {
                if (currentPathPointIndex == -1 && pathPoints[i] == currentPathPoint)
                {
                    currentPathPointIndex = i;
                }
                else
                {
                    if ((pathType == PathSectionType.PassingPath && pathPoints[i].NextSidingNode == currentPathPointIndex) ||
                        (pathType == PathSectionType.MainPath && pathPoints[i].NextMainNode == currentPathPointIndex))
                        return pathPoints[i];
                }
            }
            return null;
        }

    }
}
