using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AdminPannel
{
    /// <summary>
    /// Логика взаимодействия для ProductInfoWindow.xaml
    /// </summary>
    public partial class ProductInfoWindow : Window, INotifyPropertyChanged
    {
        public ProductInfoWindow(dynamic? product_info, bool isCreation)
        {
            InitializeComponent();

            DataContext = this;

            if (product_info is null)
            {
                product_info = new ExpandoObject();
            }
            else
            {
                _product_info = ((ExpandoObject)product_info).Copy();
                _product_copy = product_info;
            }

            _is_creation = isCreation;
        }

        dynamic _product_copy;
        public dynamic OriginalObject
        {
            get { return _product_copy; } 
            set
            {
                _product_copy = value;
                OnPropertyChanged(nameof(OriginalObject));
            }
        }

        dynamic _product_info;
        public dynamic ProductInfo
        {
            get { return _product_info; }
            set
            {
                _product_info = value;
                OnPropertyChanged("ProductInfo");
            }
        }
        
        bool _is_creation;
        public bool IsCreation
        {
            get { return _is_creation; }
            set
            {
                _is_creation = value;
                OnPropertyChanged(nameof(IsCreation));
                OnPropertyChanged(nameof(CreationVisibility));
            }
        }

        public Visibility CreationVisibility
        {
            get
            {
                return _is_creation ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool IsChanged = false;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_is_creation)
            {
                return;
            }

            
        }

        private void Name_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Name_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            this.ProductInfo.name = this.OriginalObject.name;
        }
    }
}
