using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace AdminPannel.Converters
{
    public class HeightConstraintConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3 || values.Any(value => value == DependencyProperty.UnsetValue))
                return Binding.DoNothing;

            double parentHeight = (double)values[0];
            double marginTop = (double)values[1];
            double marginBottom = (double)values[2];

            double maxHeight = parentHeight - marginTop - marginBottom;
            return maxHeight < 0 ? 0 : maxHeight;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
