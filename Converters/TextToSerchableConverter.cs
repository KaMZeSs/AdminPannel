using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AdminPannel.Converters
{
    public class TextToSerchableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Этот метод преобразует значение, хранящееся в объекте, в текст для отображения в TextBox.
            if (value is null)
            {
                return string.Empty;
            }

            var str = new string(value.ToString()?.Skip(1).SkipLast(1).ToArray());
            return str;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Этот метод преобразует вводимый текст обратно в формат, необходимый для запроса.
            if (value != null)
            {
                string? text = value.ToString();
                return $"%{text}%";
            }
            return string.Empty;
        }
    }
}



