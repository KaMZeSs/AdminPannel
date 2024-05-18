using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AdminPannel.Converters
{
    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime t_stmp)
            {
                return String.Empty;
            }
            if (parameter is string s_stmp)
            {
                return s_stmp switch
                {
                    "View" => t_stmp.ToString("g"),
                    "Sort" => t_stmp.ToUniversalTime().ToString("s"),
                    _ => t_stmp.ToString("g")
                };
            }
            return t_stmp.ToString("g");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
