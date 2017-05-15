using System;
using System.Collections.Generic;

namespace KiCadDoxer.Renderer
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class EnumStringValueAttribute : Attribute
    {
        public EnumStringValueAttribute(params string[] values)
        {
            this.Values = values;
        }

        public IEnumerable<string> Values { get; }
    }
}