using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace AdminPannel.Converters
{
    public class MultiObjectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length == 2)
            {
                object value1 = values[0];
                object value2 = values[1];

                if (parameter?.Equals("NotUnset") ?? false)
                {
                    if (value1.Equals(DependencyProperty.UnsetValue))
                    {
                        return false;
                    }
                    if (value2.Equals(DependencyProperty.UnsetValue))
                    {
                        return false;
                    }
                }
                
 
                return !Equals(value1, value2);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
