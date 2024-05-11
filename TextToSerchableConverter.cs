using System;
using System.Globalization;
using System.Windows;
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

    public class ObjectChangedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // В этом методе мы сравниваем редактируемый объект с копией
            // и возвращаем true, если они не равны
            var editedObject = value;
            var originalObject = parameter;

            return !object.Equals(editedObject, originalObject);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MultiObjectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length == 2)
            {
                object value1 = values[0];
                object value2 = values[1];

                // Проверяем, что оба значения не равны
                return !object.Equals(value1, value2);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}



