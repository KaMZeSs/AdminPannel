using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace AdminPannel
{
    public class DynamicObjectWrapper : INotifyPropertyChanged
    {
        private dynamic _wrappedObject;

        public DynamicObjectWrapper(dynamic obj)
        {
            _wrappedObject = obj;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public dynamic WrappedObject
        {
            get { return _wrappedObject; }
            set
            {
                _wrappedObject = value;
                OnPropertyChanged(nameof(WrappedObject));
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DynamicObjectWrapperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Преобразование из DynamicObjectWrapper в строку
            if (value is DynamicObjectWrapper wrapper)
            {
                return wrapper.ToString();
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Преобразование из строки в DynamicObjectWrapper
            if (value is string str)
            {
                return new AdminPannel.DynamicObjectWrapper(str);
            }
            return null;
        }
    }

}
