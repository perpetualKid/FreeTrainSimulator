using System;
using System.Collections.Frozen;
using System.Linq;
using System.Text;

using FreeTrainSimulator.Models.Independent.Base;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ContentModelExtensions
    {
        private const char separatorChar = '/';

        public static bool SetupRequired<T>(this ModelBase<T> model) where T : ModelBase<T> => model == null || model.RefreshRequired;

        public static string Hierarchy<T>(this ModelBase<T> model) where T : ModelBase<T>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            StringBuilder builder = new StringBuilder();

            BuildHiearchy(model, builder);

            return builder.ToString();
        }

        public static string Hierarchy<T>(this ModelBase<T> parent, string modelName) where T : ModelBase<T>
        {
            ArgumentNullException.ThrowIfNull(parent, nameof(parent));

            StringBuilder builder = new StringBuilder();

            BuildHiearchy(parent, builder);
            _ = builder.Append(separatorChar);
            _ = builder.Append(modelName);
            return builder.ToString();
        }

        private static void BuildHiearchy(IFileResolve model, StringBuilder builder)
        {
            if (model.Container is IFileResolve fileResolve)
            {
                BuildHiearchy(fileResolve, builder);
                _ = builder.Append(separatorChar);
            }
            _ = builder.Append(model.FileName);
        }

        public static T GetByName<T>(this FrozenSet<T> models, string name) where T : ModelBase<T>
        {
            return models.Where(m => m.Name == name).FirstOrDefault();
        }

        public static T GetByNameOrFirstByName<T>(this FrozenSet<T> models, string name) where T : ModelBase<T>
        {
            return models.Where(m => m.Name == name).FirstOrDefault() ?? models.OrderBy(m => m.Name).FirstOrDefault();
        }

    }
}
