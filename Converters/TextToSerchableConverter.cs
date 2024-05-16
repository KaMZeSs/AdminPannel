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
            // В данном случае, мы просто возвращаем значение как есть.
            if (value is null)
            {
                return string.Empty;
            }

            var str = new string(value.ToString()?.Skip(1).SkipLast(1).ToArray());
            return str;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Этот метод преобразует вводимый текст обратно в формат, необходимый для вашего объекта.
            // Здесь мы добавляем символы '%' к введенному тексту для использования в SQL-запросе.
            if (value != null)
            {
                string? text = value.ToString();
                return $"%{text}%";
            }
            return string.Empty;
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

            return !Equals(editedObject, originalObject);
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
                return !Equals(value1, value2);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

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

    public class ByteArrayToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var imageData = value as byte[];
            if (imageData == null)
                return new();

            using (var stream = new MemoryStream(imageData))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                return image;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}



