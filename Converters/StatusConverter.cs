using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace AdminPannel.Converters
{
    internal class StatusConverter : IValueConverter
    {
        public static Dictionary<string, string> Statuses;

        static StatusConverter()
        {
            if (System.Windows.Application.Current.Resources["Statuses_List"] is IEnumerable<KVPair> kVPairs)
            {
                Statuses = new Dictionary<string, string>(kVPairs.Select(x => x.ToKeyValuePair()));
            }
            else
            {
                Statuses = new Dictionary<string, string>();
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is String status)
            {
                if (Statuses.TryGetValue(status, out var statusValue))
                    return statusValue;
                return status;
            }

            return String.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
