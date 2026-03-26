using System.Collections.Generic;
using System.Linq;

namespace FreeTrainSimulator.Common
{
    public static class EnumerableExtension
    {
        public static IList<T> PresetCollection<T>(int count)
        {
            return Enumerable.Repeat(default(T), count).ToList();
        }

        /// <summary>
        /// Enumerates each item in IList collection together with
        /// its zero-based array index, avoiding the need for a manual counter at every call site.
        /// </summary>
        public static IEnumerable<(T Item, int Index)> IndexedSelect<T>(this IList<T> list)
        {
            if (list == null)
                yield break;

            for (int i = 0; i < list.Count; i++)
            {
                yield return (list[i], i);
            }
        }
    }
}
