using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Settings
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DefaultAttribute : Attribute
    {
        public readonly object Value;
        public DefaultAttribute(object value)
        {
            Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DoNotSaveAttribute : Attribute
    {
    }
}
