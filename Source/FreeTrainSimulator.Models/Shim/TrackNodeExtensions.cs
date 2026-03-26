using System.Collections.Generic;

using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Shim
{
    public static class TrackNodeExtensions
    {
        /// <summary>
        /// Enumerates each <see cref="VectorSectionNode"/> in <see cref="VectorSections"/> together with
        /// its zero-based array index, avoiding the need for a manual counter at every call site.
        /// </summary>
        public static IEnumerable<(VectorSectionNode Section, int Index)> IndexedSections(this VectorNode vectorNode)
        {
            for (int i = 0; i < vectorNode?.VectorSections.Length; i++)
            {
                yield return (vectorNode.VectorSections[i], i);
            }
        }
    }
}
