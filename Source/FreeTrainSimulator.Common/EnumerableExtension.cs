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
    }
}
