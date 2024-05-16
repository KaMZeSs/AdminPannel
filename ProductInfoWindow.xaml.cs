using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
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

using Dapper;

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

        public ObservableCollection<object> _categories = new();
        public ObservableCollection<object> Categories
        {
            get { return _categories; }
            set
            {
                _categories.Clear();
                foreach (var item in value)
                {
                    _categories.Add(item);
                }
                OnPropertyChanged("Categories");
            }
        }
        
        private dynamic? _current_category;
        public dynamic? CurrentCategory
        {
            get { return _current_category; }
            set
            {
                _current_category = value;
                this.ProductInfo.category_id = CurrentCategory.id;
                OnPropertyChanged("CurrentCategory");
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
                OnPropertyChanged(nameof(ViewingVisibility));

                OnPropertyChanged(nameof(WindowHeight));
                OnPropertyChanged(nameof(WindowWidth));
            }
        }

        public Visibility CreationVisibility
        {
            get
            {
                return _is_creation ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility ViewingVisibility
        {
            get
            {
                return _is_creation ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public int WindowHeight
        {
            get { return _is_creation ? 400 : 500; }
        }

        public int WindowWidth
        {
            get { return _is_creation ? 400 : 800; }
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

        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            await this.UpdateCategories();
            await this.UpdateProductInfo();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await this.UpdateCategories();
            await this.UpdateProductInfo();
            await this.UpdateProductImage();
        }

        private void TextBoxInt_PreviewTextChanged(object sender, TextChangedEventArgs e)
        {
            var tbox = sender as TextBox;
            if (tbox is null)
                return;

            var maxIntLen = 10;

            var ss = tbox.SelectionStart;

            String cleanText = new(tbox.Text.Where(Char.IsDigit).Take(maxIntLen).ToArray());
            if (Int32.TryParse(cleanText, out var data))
            {
                tbox.Text = data.ToString();
            }
            else
            {
                String vs = new(Enumerable.SkipLast(cleanText, 1).ToArray());
                tbox.Text = vs;
            }
            tbox.SelectionStart = ss;
        }

        private async Task UpdateCategories()
        {
            var current_id = -1;
            if (ProductInfo is not null)
            {
                current_id = _product_copy.category_id;
            }

            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var categories = await conn.QueryAsExpandoAsync("SELECT * FROM categories");

                dynamic allCategory = new ExpandoObject();
                allCategory.id = -1;
                allCategory.name = "Без категории";

                var tempList = new List<dynamic>
                {
                    allCategory
                };

                tempList.AddRange(categories);

                foreach (var item in tempList)
                {
                    item.isEditing = false;
                }

                Categories = new ObservableCollection<object>(tempList);

                if (Categories.Count(x => (x as dynamic).id == current_id) is not 0)
                    CurrentCategory = Categories.First(x => (x as dynamic).id == current_id);
            }
        }

        private async Task UpdateProductInfo()
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = @"SELECT
	                        p.id,
	                        p.name,
                            p.description,
                            COALESCE(ca.id, -1) as category_id,
                            COALESCE(ca.name, '') as category_name,
	                        COALESCE((p.price - p.price * so.discount / 100), p.price)::integer current_price,
	                        p.price,
	                        so.discount IS NOT NULL AS now_spec,
	                        p.quantity AS available_quantity,
                            p.quantity + COALESCE(oi.total_ordered, 0) AS total_quantity
                        FROM
	                        products p
                        LEFT JOIN special_offers so ON p.id = so.product_id AND LOCALTIMESTAMP BETWEEN so.start_datetime AND so.end_datetime
                        LEFT JOIN categories ca ON p.category_id = ca.id
                        LEFT JOIN (
	                        SELECT
		                        oi.product_id,
		                        SUM(oi.quantity) AS total_ordered
	                        FROM
		                        order_items oi
	                        JOIN orders o ON oi.order_id = o.id
	                        WHERE
		                        o.status NOT IN ('canceled', 'completed')
	                        GROUP BY
		                        oi.product_id
                        ) oi ON p.id = oi.product_id
                        WHERE p.id = @id";

                    var data = new
                    {
                        id = _product_info.id
                    };

                    var product = (await conn.QueryAsExpandoAsync(sql, data)).First();

                    var vs1 = (ExpandoObject)this._product_info;
                    var vs2 = (ExpandoObject)this._product_copy;

                    vs1.UpdateFrom(product as ExpandoObject);
                    vs2.UpdateFrom(product as ExpandoObject);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private async void Name_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE products SET name = @new_name WHERE id = @id";

                    var data = new
                    {
                        new_name = _product_info.name,
                        id = _product_info.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    _product_copy.name = _product_info.name;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Товар с данным названием уже существует на сервере.",
                    "Повторяющееся название", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private void Name_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            this.ProductInfo.name = this.OriginalObject.name;
        }

        private async void Price_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE products SET price = @new_price WHERE id = @id";

                    var data = new
                    {
                        new_price = _product_info.price,
                        id = _product_info.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    _product_copy.price = _product_info.price;

                    await UpdateProductInfo();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private void Price_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            this.ProductInfo.price = this.OriginalObject.price;
        }

        private async void AvailableQuantity_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE products SET quantity = @new_quantity WHERE id = @id";

                    var data = new
                    {
                        new_quantity = _product_info.available_quantity,
                        id = _product_info.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    _product_copy.available_quantity = _product_info.available_quantity;

                    await UpdateProductInfo();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private void AvailableQuantity_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            this.ProductInfo.available_quantity = this.OriginalObject.available_quantity;
        }

        private async void Category_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE products SET category_id = @new_category_id WHERE id = @id";

                    var data = new
                    {
                        new_category_id = _product_info.category_id is -1 ? null : _product_info.category_id,
                        id = _product_info.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    await UpdateProductInfo();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private void Category_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            this.ProductInfo.category_id = this.OriginalObject.category_id;
            
            var current_id = -1;
            if (ProductInfo is not null)
            {
                current_id = _product_copy.category_id;
            }

            if (Categories.Count(x => (x as dynamic).id == current_id) is not 0)
                CurrentCategory = Categories.First(x => (x as dynamic).id == current_id);
        }

        private async void Description_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE products SET description = @new_description WHERE id = @id";

                    var data = new
                    {
                        new_description = _product_info.description,
                        id = _product_info.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    _product_copy.description = _product_info.description;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private void Description_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            this.ProductInfo.description = this.OriginalObject.description;
        }

        private async void Image_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            //try
            //{
            //    await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            //    {
            //        string sql = "UPDATE products SET description = @new_description WHERE id = @id";

            //        var data = new
            //        {
            //            new_description = _product_info.description,
            //            id = _product_info.id
            //        };

            //        int rowsAffected = await conn.ExecuteAsync(sql, data);

            //        _product_copy.description = _product_info.description;
            //    }
            //}
            //catch (Exception)
            //{
            //    MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
            //        "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            //    return;
            //}
        }

        private void Image_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.ProductInfo.image = this.OriginalObject.image;
                this.ViewImage(_product_info.image);
            }
            catch (Exception)
            {
                var vs = this.ProductInfo as IDictionary<String, Object>;

                vs?.Remove("image");
                displayImage.Source = null;
            }


        }

        private async Task UpdateProductImage()
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "SELECT * FROM product_images WHERE product_id = @id";

                    var data = new
                    {
                        id = _product_info.id
                    };

                    var vs = await conn.QueryAsExpandoAsync(sql, data);

                    if (vs.Count() is 0)
                        return;

                    var imageData = (byte[])vs.First().image;

                    this.ViewImage(imageData);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при получении изображения. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private void ViewImage(byte[]? img)
        {
            if (img != null)
            {
                using (var stream = new MemoryStream(img))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();

                    if (image != null)
                    {
                        displayImage.Source = image;
                        this.ProductInfo.image = img;
                    }
                }
            }
        }

        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            // Создаем диалоговое окно для выбора файла
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            // Устанавливаем фильтр для файлов изображений
            openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";

            // Показываем диалоговое окно
            bool? result = openFileDialog.ShowDialog();

            // Если пользователь выбрал файл
            if (result == true)
            {
                // Получаем путь к выбранному файлу
                string filePath = openFileDialog.FileName;

                try
                {
                    // Считываем содержимое файла в массив байтов
                    byte[] imageBytes = File.ReadAllBytes(filePath);

                    this.ViewImage(imageBytes);

                }
                catch (Exception ex)
                {
                    // Обработка исключений
                    MessageBox.Show($"Ошибка при чтении файла изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
