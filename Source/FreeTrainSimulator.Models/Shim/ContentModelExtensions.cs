using System;
using System.Collections.Frozen;
using System.Linq;

using FreeTrainSimulator.Models.Base;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ContentModelExtensions
    {
        public static T GetByName<T>(this FrozenSet<T> models, string name) where T : ModelBase<T>
        {
            return models.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public static T GetByNameOrFirstByName<T>(this FrozenSet<T> models, string name) where T : ModelBase<T>
        {
            return models.GetByName(name) ?? models.OrderBy(m => m.Name).FirstOrDefault();
        }

        public static T GetById<T>(this FrozenSet<T> models, string id) where T : ModelBase<T>
        {
            return models.Where(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

    }
}
