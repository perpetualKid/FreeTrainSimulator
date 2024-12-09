using FreeTrainSimulator.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Settings
{
    public abstract record ProfileSettingsModelBase : ModelBase
    {
        public override ProfileModel Parent => _parent as ProfileModel;

        public void Log()
        {
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            foreach (PropertyInfo property in properties.OrderBy(p => p.Name))
            {
                if (string.Equals(property.Name, "Parent", StringComparison.OrdinalIgnoreCase))
                    continue;
                dynamic value = property.GetValue(this, null);

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
