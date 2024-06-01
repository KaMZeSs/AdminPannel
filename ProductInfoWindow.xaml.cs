using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

using AdminPannel.Converters;

using Dapper;

using MoreLinq.Extensions;

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
                _product_info = new ExpandoObject();
                _product_copy = new ExpandoObject();
            }
            else
            {
                _product_info = ((ExpandoObject)product_info).Copy();
                _product_copy = product_info;
                this.Title = _product_copy.name;
            }

            IsCreation = isCreation;
        }

        dynamic? _product_copy;
        public dynamic? OriginalObject
        {
            get { return _product_copy; } 
            set
            {
                _product_copy = value;
                OnPropertyChanged(nameof(OriginalObject));
            }
        }

        dynamic? _product_info;
        public dynamic? ProductInfo
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
                if (ProductInfo is not null && _current_category is not null)
                    ProductInfo.category_id = _current_category.id;
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
                this.Height = _is_creation ? 230 : 720;
                this.Width = _is_creation ? 430 : 650;
                OnPropertyChanged(nameof(IsCreation));

                OnPropertyChanged(nameof(CreationVisibility));
                OnPropertyChanged(nameof(ViewingVisibility));
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


        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_is_creation)
            {
                await RefreshData();
            }
            else
            {
                await this.UpdateCategories();
            }
        }

        private async Task RefreshData()
        {
            await this.UpdateCategories();
            await this.UpdateProductInfo();
            await this.UpdateProductImages();
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
            try
            {
                if (ProductInfo is not null)
                {
                    current_id = _product_copy?.category_id ?? -1;
                }
            }
            catch { }

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
            if (_product_info is null)
                return;
            if (_product_copy is null)
                return;
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

                    if (product is null)
                        return;

                    var vs1 = (ExpandoObject)this._product_info;
                    var vs2 = (ExpandoObject)this._product_copy;

                    vs1.UpdateFrom((ExpandoObject)product);
                    vs2.UpdateFrom((ExpandoObject)product);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        #region Product Info Changers

        private async void Name_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_product_info is null)
                return;
            if (_product_copy is null)
                return;

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
            if (ProductInfo is null)
                return;
            if (OriginalObject is null)
                return;


            this.ProductInfo.name = this.OriginalObject.name;
        }

        private async void Price_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_product_info is null)
                return;
            if (_product_copy is null)
                return;

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
            if (ProductInfo is null)
                return;
            if (OriginalObject is null)
                return;

            this.ProductInfo.price = this.OriginalObject.price;
        }

        private async void AvailableQuantity_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_product_info is null)
                return;
            if (_product_copy is null)
                return;

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
            if (ProductInfo is null)
                return;
            if (OriginalObject is null)
                return;

            this.ProductInfo.available_quantity = this.OriginalObject.available_quantity;
        }

        private async void Category_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_product_info is null)
                return;
            if (_product_copy is null)
                return;

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
            if (_product_copy is null)
                return;

            if (ProductInfo is null)
                return;
            if (OriginalObject is null)
                return;

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
            if (_product_info is null)
                return;
            if (_product_copy is null)
                return;

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
            if (ProductInfo is null)
                return;
            if (OriginalObject is null)
                return;

            this.ProductInfo.description = this.OriginalObject.description;
        }

        #endregion

        #region Image Changers

        private ObservableCollection<dynamic> _images = new();
        public ObservableCollection<dynamic> Images
        {
            get => _images;
            set
            {
                _images = value;
                OnPropertyChanged(nameof(Images));
            }
        }

        private async Task UpdateProductImages()
        {
            if (_product_info is null)
                return;

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

                    Images.Clear();
                    foreach (var item in vs)
                    {
                        Images.Add(item);
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при получении изображения. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private dynamic? _selected_image;
        public dynamic? SelectedImage
        {
            get { return _selected_image; }
            set
            {
                _selected_image = value;
                OnPropertyChanged("SelectedImage");
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var image = (sender as FrameworkElement)?.DataContext as dynamic;

            if (image is null)
                return;

            var imageSource = (new ByteArrayToImageConverter()).Convert(image.image, typeof(BitmapImage), null, null) as BitmapImage;

            if (imageSource != null)
            {
                // Создаем новое окно
                var window = new Window
                {
                    Width = Math.Min(imageSource.PixelWidth / 1.3, 600),
                    Background = System.Windows.Media.Brushes.Black,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    Title = "Изображение " + this.Title,
                    Content = new System.Windows.Controls.Image
                    {
                        Source = imageSource,
                        Stretch = System.Windows.Media.Stretch.Uniform
                    }
                };

                // Рассчитываем высоту окна, чтобы сохранить соотношение сторон изображения
                double aspectRatio = (double)imageSource.PixelHeight / imageSource.PixelWidth;
                window.Height = window.Width * aspectRatio + 19;

                window.Show();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var image = (sender as FrameworkElement)?.DataContext as dynamic;

            if (image is null)
                return;

            if (image.image is null)
                return;

            var openFileDialog = new Microsoft.Win32.SaveFileDialog();
            openFileDialog.Filter = "PNG (*.png)|*.png|JPG (*.jpg)|*.jpg|BMP (*.bmp)|*.bmp";
            bool? result = openFileDialog.ShowDialog();

            if (result is not true)
                return;

            string filePath = openFileDialog.FileName;

            try
            {
                using (MemoryStream ms = new MemoryStream(image.image))
                {
                    using (Bitmap bitmap = new Bitmap(ms))
                    {
                        switch (filePath.Split('.').Last().ToUpper())
                        {
                            case "PNG":
                            {
                                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                                break;
                            }
                            case "JPG":
                            {
                                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                break;
                            }
                            case "BMP":
                            {
                                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                                break;
                            }
                            default:
                                break;
                        }
                        
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при сохранении изображения: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var image = (sender as FrameworkElement)?.DataContext as dynamic;
            
            if (image is null)
                return;

            var result = MessageBox.Show(
                "Вы точно уверены, что хотите удалить данное изорабрежение без возможности восстановления?",
                "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (result is not MessageBoxResult.OK)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "DELETE FROM product_images WHERE id = @image_id";

                    var data = new
                    {
                        image_id = image.id
                    };

                    await conn.ExecuteAsync(sql, data);

                    Images.Remove(image);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при удалении изображения. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private async void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_product_info is null)
                return;

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
            bool? result = openFileDialog.ShowDialog();

            if (result is not true)
                return;

            string filePath = openFileDialog.FileName;

            byte[] imageBytes;

            try
            {
                imageBytes = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при чтении файла изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "INSERT INTO product_images (product_id, image) VALUES (@product_id, @image) RETURNING id";

                    var data = new
                    {
                        product_id = _product_info.id,
                        image = imageBytes
                    };

                    int image_id = await conn.QueryFirstAsync<int>(sql, data);

                    dynamic expando = new ExpandoObject();
                    expando.id = image_id;
                    expando.product_id = _product_info.id;
                    expando.image = imageBytes;

                    Images.Add(expando);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        #endregion

        #region New Product

        private async void Creation_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_product_info is null)
                return;

            try
            {
                var data = (IDictionary<String, Object>)_product_info;
                if (data.ContainsKey("name") && _product_info.name.Length is 0)
                {
                    MessageBox.Show("Название не установлено!", "Недостаточно данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (data.ContainsKey("price") && _product_info.price is 0)
                {
                    MessageBox.Show("Цена не установлена!", "Недостаточно данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка", "Недостаточно данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "INSERT INTO products (name, price, quantity, category_id, description) VALUES (@name, @price, @quantity, @category_id, @description) RETURNING id";

                    var data = new
                    {
                        _product_info.name,
                        _product_info.price,
                        quantity = 0,
                        description = String.Empty,
                        category_id = _product_info.category_id is -1 ? null : _product_info.category_id
                    };

                    int id = await conn.QueryFirstAsync<int>(sql, data);

                    _product_info.id = id;
                    _product_info.current_price = _product_info.price;
                    _product_info.available_quantity = 0;
                    _product_info.total_quantity = 0;
                    _product_info.now_spec = false;
                    _product_info.description = String.Empty;

                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Npgsql.PostgresException ex)
            {
                if (ex.ConstraintName == "products_name_key")
                {
                    MessageBox.Show("Товар с данным название уже существует",
                        "Повторяющееся название", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Непредвиденная ошибка при создании товара. Повторите попытку позже",
                        "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch
            {
                MessageBox.Show("Непредвиденная ошибка при создании товара. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Creation_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion
    }
}
