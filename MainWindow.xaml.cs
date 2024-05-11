using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.DirectoryServices;
using System.Dynamic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shell;
using System.Windows.Threading;

using Dapper;

using MoreLinq;
using MoreLinq.Extensions;

using Npgsql;

namespace AdminPannel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();

            EnvLoader.LoadFromResource();

            DataContext = this;

            onGridChange = new Dictionary<Grid, Func<Task>>()
            {
                { this.Products_Grid, this.OnProductSelected }
            };

            _new_category = String.Empty;
            ProductsFilter = new ExpandoObject();
        }

        #region Service Things

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TextBoxInt_PreviewTextChanged(object sender, TextChangedEventArgs e)
        {
            var tbox = sender as TextBox;
            if (tbox is null)
                return;

            var maxIntLen = 10;

            var ss = tbox.SelectionStart;

            String cleanText = new (tbox.Text.Where(Char.IsDigit).Take(maxIntLen).ToArray());
            if (Int32.TryParse(cleanText, out var data))
            {
                tbox.Text = data.ToString();
            }
            else
            {
                String vs = new (Enumerable.SkipLast(cleanText, 1).ToArray());
                tbox.Text = vs;
            }
            tbox.SelectionStart = ss;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var vs = onGridChange.First().Key;
            this.ChangeGrid(vs);

            
        }

        #endregion

        #region Menu_Grid

        Grid? currentGrid;
        Grid? CurrentGrid
        {
            get
            {
                return currentGrid;
            }

            set
            {
                currentGrid = value;
                if (value is not null)
                {
                    try
                    {
                        this.onGridChange[value].Invoke();
                    }
                    catch { }
                }
            }
        }

        Dictionary<Grid, Func<Task>> onGridChange;

        private Grid? GridByName(string name)
        {

            switch (name)
            {
                case "Заказы":
                    return this.Orders_Grid;
                case "Список товаров":
                    return this.Products_Grid;
                case "Акции":
                    return this.SpecialOffers_Grid;
                case "Пункты выдачи":
                    return this.PickupPoints_Grid;
                case "Новости":
                    return this.News_Grid;
            }

            return null;
        }

        private void ChangeGrid(Grid? grid)
        {
            if (grid is null)
                return;

            if (currentGrid is not null)
                currentGrid.Visibility = Visibility.Collapsed;
            CurrentGrid = grid;
            CurrentGrid.Visibility = Visibility.Visible;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;
            if (item is null)
                return;

            var grid = this.GridByName(item.Header as string ?? "");

            ChangeGrid(grid);
        }

        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            if (currentGrid is not null)
                await this.onGridChange[currentGrid].Invoke();
        }


        #endregion

        #region Products Grid
        
        #region Categories

        public ObservableCollection<object> _categories = new();
        public ObservableCollection<object> Categories
        {
            get { return _categories; }
            set
            {
                _categories.Clear();
                _categories = value;
                OnPropertyChanged("Categories");
            }
        }

        private async Task OnProductSelected()
        {
            await UpdateCategories();

            if (ProductsFilter is null)
                return;
        }

        private async Task UpdateCategories()
        {
            var current_id = -1;
            if (CurrentCategory is not null)
            {
                current_id = CurrentCategory.id;
            }

            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var categories = await conn.QueryAsExpandoAsync("SELECT * FROM categories");

                dynamic allCategory = new ExpandoObject();
                allCategory.id = -1;
                allCategory.name = "Все";

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

        private async void CategoriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCategory = (sender as ListView)?.SelectedItem;
            if (selectedCategory != null)
            { 
                await UpdateProducts();
            }
        }

        private dynamic? _current_category;
        public dynamic? CurrentCategory
        {
            get { return _current_category; }
            set
            {
                _current_category = value;
                OnPropertyChanged("CurrentCategory");
            }
        }

        private string _new_category;
        public string NewCategory
        {
            get { return _new_category; }
            set
            {
                _new_category = value;
                OnPropertyChanged("NewCategory");
            }
        }

        private async void DeleteCategory_Button_Click(object sender, RoutedEventArgs e)
        {
            // Получаем ссылку на модель данных
            var category = (sender as FrameworkElement)?.DataContext as dynamic;

            if (category is null)
                return;

            if (MessageBox.Show($"Вы точно уверены, что хотите удалить категорию \"{category.name}\"", 
                "Подтвердите действие", MessageBoxButton.OKCancel, MessageBoxImage.Warning) is not MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "DELETE FROM categories WHERE id = @id";

                    var data = new
                    {
                        id = category.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    if (rowsAffected > 0)
                    {
                        this.Categories.Remove(category);
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Невозможно удалить категорию, к которой привязаны товары",
                    "Ошибка удаления категории", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCategory_Button_Click(object sender, RoutedEventArgs e)
        {
            // Получаем ссылку на модель данных
            var category = (sender as FrameworkElement)?.DataContext as dynamic;
            if (category is null)
                return;

            category.originalName = category.name;

            // Устана вливаем режим редактирования
            category.isEditing = true;
        }

        private void CancelEditCategory_Button_Click(object sender, RoutedEventArgs e)
        {
            // Получаем ссылку на модель данных
            var category = (sender as FrameworkElement)?.DataContext as dynamic;
            if (category is null)
                return;

            category.name = category.originalName;

            // Устана вливаем режим редактирования
            category.isEditing = false;
        }

        private async void ConfirmEditCategory_Button_Click(object sender, RoutedEventArgs e)
        {
            // Получаем ссылку на модель данных
            var category = (sender as FrameworkElement)?.DataContext as dynamic;
            if (category is null)
                return;

            var forbidden = this.Categories.Where(x => (x as dynamic).id is -1)
                .Select(x => (x as dynamic).name).ToList();

            var unique_check = this.Categories.Where(x => (x as dynamic).id != category.id)
                .Select(x => (x as dynamic).name).ToList();

            if (forbidden.Contains(category.name))
            {
                MessageBox.Show("Невозможно назвать категорию данным именем",
                    "Запрещенное название", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            if (unique_check.Contains(category.name))
            {
                MessageBox.Show("Категория с данным названием уже существует",
                    "Повторяющееся название", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE categories SET name = @new_name WHERE id = @id";

                    var data = new
                    {
                        new_name = category.name,
                        id = category.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Категория с данным названием уже существует на сервере. Обновите список",
                    "Повторяющееся название", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            // Устана вливаем режим редактирования
            category.isEditing = false;
        }

        private async void CreateNewCategory_Click(object sender, RoutedEventArgs e)
        {
            var forbidden = this.Categories.Where(x => (x as dynamic).id is -1)
                .Select(x => (x as dynamic).name).ToList();

            var unique_check = this.Categories.Select(x => (x as dynamic).name).ToList();

            if (forbidden.Contains(NewCategory))
            {
                MessageBox.Show("Невозможно назвать категорию данным именем",
                    "Запрещенное название", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            if (unique_check.Contains(NewCategory))
            {
                MessageBox.Show("Категория с данным названием уже существует",
                    "Повторяющееся название", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "INSERT INTO categories (name) VALUES (@name) RETURNING id";

                    var data = new
                    {
                        name = NewCategory
                    };

                    int newCategoryId = await conn.QuerySingleAsync<int>(sql, data);

                    dynamic created_category = new ExpandoObject();
                    created_category.id = newCategoryId;
                    created_category.name = NewCategory;

                    this.Categories.Add(created_category);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Категория с данным названием уже существует на сервере. Обновите список",
                    "Повторяющееся название", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        #endregion

        #region Products

        private dynamic? _products_filter;
        public dynamic? ProductsFilter
        {
            get { return _products_filter; }
            set
            {
                _products_filter = value;
                OnPropertyChanged("ProductsFilter");
            }
        }

        public ObservableCollection<object> _products = new();
        public ObservableCollection<object> Products
        {
            get { return _products; }
            set
            {
                _products = value;
                OnPropertyChanged("Products");
            }
        }
        private (String sql, object? data) CreateProductSQL(bool ProductsShouldBeFiltered = false)
        {
            var selected_category_id = CurrentCategory?.id ?? -1;

            var sql =
                        @"SELECT
	                        p.id,
	                        p.name,
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
                        ) oi ON p.id = oi.product_id";

            bool isWhere = false;

            if (selected_category_id is not -1)
            {
                sql += "\nWHERE p.category_id = @category_id";
                isWhere = true;
            }

            if (ProductsShouldBeFiltered & ProductsFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)ProductsFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    switch (kvp.Key)
                    {
                        case "articul":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.id = @articul";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.id = @articul";
                            }
                            break;
                        }
                        case "name":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.name LIKE @name";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.name LIKE @name";
                            }
                            break;
                        }
                        case "current_price_from":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE COALESCE(so.new_price, p.price) >= @current_price_from";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND COALESCE(so.new_price, p.price) >= @current_price_from";
                            }
                            break;
                        }
                        case "current_price_to":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE COALESCE(so.new_price, p.price) <= @current_price_to";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND COALESCE(so.new_price, p.price) >= @current_price_to";
                            }
                            break;
                        }
                        case "default_price_from":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.price >= @default_price_from";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.price >= @default_price_from";
                            }
                            break;
                        }
                        case "default_price_to":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.price <= @default_price_to";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.price <= @default_price_to";
                            }
                            break;
                        }
                        case "available_quantity_from":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.quantity >= @available_quantity_from";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.quantity >= @available_quantity_from";
                            }
                            break;
                        }
                        case "available_quantity_to":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.quantity <= @available_quantity_to";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.quantity <= @available_quantity_to";
                            }
                            break;
                        }
                        case "total_quantity_from":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.quantity + COALESCE(oi.total_ordered, 0) >= @total_quantity_from";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.quantity + COALESCE(oi.total_ordered, 0) >= @total_quantity_from";
                            }
                            break;
                        }
                        case "total_quantity_to":
                        {
                            if (!isWhere)
                            {
                                sql += "\nWHERE p.quantity + COALESCE(oi.total_ordered, 0) <= @total_quantity_to";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND p.quantity + COALESCE(oi.total_ordered, 0) <= @total_quantity_to";
                            }
                            break;
                        }
                    }
                }
            }

            sql += "\nORDER BY p.id";

            if (ProductsFilter is not null)
            {
                var exp = (ExpandoObject)ProductsFilter;
                dynamic data = exp.Copy();
                data.category_id = selected_category_id;

                return (sql, data);
            }

            return (sql, new { category_id = selected_category_id });
        }

        private async Task UpdateProducts(bool ProductsShouldBeFiltered = false)
        {
            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var cmd = this.CreateProductSQL(ProductsShouldBeFiltered);

                var products = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                Products = new ObservableCollection<object>(products);
            }
        }

        private async void DeleteSelectedProducts_Button_Click(object sender, RoutedEventArgs e)
        {
            var selected_products = Products_DataGrid.SelectedItems.Cast<dynamic>();

            if (selected_products is null)
                return;

            if (selected_products.Count() is 0)
                return;

            var count = selected_products.Count();

            var to_view = String.Join('\n', selected_products.Take(10).Select(x => $"[{x.id}] {x.name}"));
            if (count > 10)
                to_view += "\n...";

            if (MessageBox.Show($"Вы точно уверены, что хотите удалить данны{(count is 1 ? "й" : "е")} товар{(count is 1 ? "" : "ы")}?\n\n{to_view}",
                                "Подтвердите действие", MessageBoxButton.OKCancel, MessageBoxImage.Warning) is not MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "DELETE FROM products WHERE id = ANY(@ids)";

                    var data = new
                    {
                        ids = selected_products.Select(x => (int)x.id).ToArray()
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    foreach (var item in selected_products)
                    {
                        Products.Remove(item);
                    }
                }
            }
            catch (Exception)
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "SELECT product_id FROM order_items WHERE product_id = ANY(@ids) GROUP BY product_id";

                    var data = new
                    {
                        ids = selected_products.Select(x => (int)x.id).ToArray()
                    };

                    var ids = await conn.QueryAsync<int>(sql, data);

                    var failed = selected_products.Where(x => ids.Contains((int)x.id)).ToList();

                    to_view = String.Join('\n', failed.Take(10).Select(x => $"[{x.id}] {x.name}"));
                    if (count > 10)
                        to_view += "\n...";

                    MessageBox.Show($"Невозможно удалить товар{(ids.Count() is 1 ? "" : "ы")}, к которому привязаны заказы:\n\n{to_view}", 
                        $"Ошибка удаления товар{(ids.Count() is 1 ? "а" : "ов")}", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                
            }
        }

        private void ViewSelectedProduct_Button_Click(object sender, RoutedEventArgs e)
        {
            dynamic selected_product = Products_DataGrid.SelectedItem;
            if (selected_product is null)
                return;
            var window = new ProductInfoWindow(selected_product, false);

            window.ShowDialog();
        }

        private void CreateNewProduct_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ProductsFilter_Button_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsFilter is null)
                return;

            await UpdateProducts(ProductsShouldBeFiltered: true);
        }

        #endregion


        #endregion
    }
}