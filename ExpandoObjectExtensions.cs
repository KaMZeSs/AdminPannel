using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdminPannel
{
    public static class ExpandoObjectExtensions
    {
        public static ExpandoObject Copy(this ExpandoObject original)
        {
            dynamic expando = new ExpandoObject();
            var expandoDict = (IDictionary<string, object>)expando;
            foreach (var property in original)
            {
                expandoDict[property.Key] = property.Value;
            }

            return expando;
        }
    }
}
