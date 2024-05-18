using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace AdminPannel.Converters
{
    public class KVPairToDataConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is KVPair pair)
            {
                return parameter.ToString() switch
                {
                    "Key" => pair.Key,
                    "Value" => pair.Value,
                    _ => pair.Value,
                };
            }
            return value?.ToString() ?? "";
        }
    }
}
