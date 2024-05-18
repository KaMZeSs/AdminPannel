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
        static Dictionary<string, string> statuses;

        static StatusConverter()
        {
            if (System.Windows.Application.Current.Resources["Statuses_List"] is IEnumerable<KVPair> kVPairs)
            {
                statuses = new Dictionary<string, string>(kVPairs.Select(x => x.ToKeyValuePair()));
            }
            else
            {
                statuses = new Dictionary<string, string>();
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is String status)
            {
                if (statuses.TryGetValue(status, out var statusValue))
                    return statusValue;
                return status;

                //return status switch
                //{
                //    "created" => "Создан",
                //    "canceled" => "Отменён",
                //    "completed" => "Выдан",
                //    "submitted" => "Подтверждён",
                //    _ => status
                //};
            }

            return String.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
