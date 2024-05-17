using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace AdminPannel.Converters
{
    public class TextBlockMaxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double actualWidth = (double)value;
            double buttonWidth = 60; // Ширина кнопки
            double margin = 5; // Отступ между TextBlock и кнопками

            if (actualWidth - buttonWidth - margin > 0)
            {
                return actualWidth - buttonWidth - margin;
            }
            else
                return actualWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
