using System;
using System.Globalization;
using System.Windows.Data;

namespace AdminPannel.Converters
{
    public class StringToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Этот метод преобразует целое число в строку (не используется в нашем случае)
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Этот метод преобразует строку в целое число
            if (int.TryParse(value as string, out int result))
            {
                return result;
            }
            return value; // Возвращаем значение как есть, если не удалось преобразовать в целое число
        }
    }
}
