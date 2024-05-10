using System;
using System.Globalization;
using System.Windows.Data;

namespace AdminPannel
{
    public class TextToSerchableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Этот метод преобразует значение, хранящееся в объекте, в текст для отображения в TextBox.
            // В данном случае, мы просто возвращаем значение как есть.
            if (value is null)
            {
                return String.Empty;
            }

            var str = new String(value.ToString().Skip(1).SkipLast(1).ToArray());
            return str;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Этот метод преобразует вводимый текст обратно в формат, необходимый для вашего объекта.
            // Здесь мы добавляем символы '%' к введенному тексту для использования в SQL-запросе.
            if (value != null)
            {
                string text = value.ToString();
                return $"%{text}%";
            }
            return null;
        }
    }

}



