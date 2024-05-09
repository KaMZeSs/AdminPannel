using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Dynamic;

namespace AdminPannel
{
    public static class ExpandoObjectExtensions
    {
        public static void AddProperty(this ExpandoObject expando, string propertyName, object value)
        {
            var expandoDict = (IDictionary<string, object>)expando;
            expandoDict[propertyName] = value;
        }
    }
}
