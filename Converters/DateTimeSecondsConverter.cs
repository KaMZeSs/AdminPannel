using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace AdminPannel.Converters
{

    public class DateTimeSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
                return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 
                    dateTime.Hour, dateTime.Minute, parameter is int seconds ? seconds : 0);
            return value;
        }
    }
}
