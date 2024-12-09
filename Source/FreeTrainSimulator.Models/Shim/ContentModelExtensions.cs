using System;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Settings;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ContentModelExtensions
    {
        public static T GetByName<T>(this FrozenSet<T> models, string name) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public static T GetByNameOrFirstByName<T>(this FrozenSet<T> models, string name) where T : ModelBase
        {
            return models.GetByName(name) ?? models.OrderBy(m => m.Name).FirstOrDefault();
        }

        public static T GetById<T>(this FrozenSet<T> models, string id) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public static void Log<T>(this T model) where T : ProfileSettingsModelBase
        {
            if (model == null)
            {
                return;
            }
            PropertyInfo[] properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            foreach (PropertyInfo property in properties.OrderBy(p => p.Name))
            {
                if (string.Equals(property.Name, "Parent", StringComparison.OrdinalIgnoreCase))
                    continue;
                dynamic value = property.GetValue(model, null);

                if (property.PropertyType == typeof(int[]))  //int array
                {
                    value = string.Join(", ", (int[])value);
                }
                else if (property.PropertyType == typeof(string[]))  //string array
                {
                    value = string.Join(", ", (string[])value);
                }
                else if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition())
                {
                    value = value.ToString();
                }
                Trace.WriteLine($"{property.Name[..Math.Min(30, property.Name.Length)],-30} = {value?.ToString().Replace(Environment.UserName, "********") ?? "<null>"}");
            }
        }
    }
}
