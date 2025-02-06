using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    public abstract record ProfileSettingsModelBase : ModelBase
    {
        [MemoryPackIgnore]
        public override ProfileSettingsModelBase Parent => _parent as ProfileSettingsModelBase;

        protected ProfileSettingsModelBase() : base()
        { }

        protected ProfileSettingsModelBase(string name, ProfileSettingsModelBase parent) : base(name, parent)
        { }

        public void Log()
        {
            string modelTypeName = GetType().Name.Replace("Model", string.Empty);
            Trace.WriteLine(modelTypeName);
            Trace.WriteLine(new string('=', modelTypeName.Length));
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            foreach (PropertyInfo property in properties.OrderBy(p => p.Name))
            {
                if (string.Equals(property.Name, "Parent", StringComparison.OrdinalIgnoreCase))
                    continue;
                dynamic value = property.GetValue(this, null);

                if (property.PropertyType == typeof(int[]))  //int array
                {
                    value = string.Join(",", (int[])value);
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
            Trace.WriteLine(LoggingUtil.SeparatorLine);
        }
    }
}
