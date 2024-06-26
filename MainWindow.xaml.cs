﻿using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using System.Diagnostics;
using System.Data;
using System.Drawing;

using Dapper;

using MoreLinq;
using MoreLinq.Extensions;

using Npgsql;

using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

using ClosedXML.Excel;

using AdminPannel.Extensions;
using AdminPannel.Converters;

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

            gridByNames = new Dictionary<String, Grid>()
            {
                { "Список товаров", this.Products_Grid },
                { "Заказы", this.Orders_Grid },
                { "Акции", this.SpecialOffers_Grid },
                { "Пункты выдачи", this.PickupPoints_Grid },
                { "Новости", this.News_Grid },
                { "Объём продаж", this.SalesVolume_Grid },
                { "Отчёт по акциям", this.TopPromotions_Grid },
                { "Загрузка пунктов выдачи", this.PickupPointsLoad_Grid },
            };

            updatesByName = new Dictionary<String, Func<Task>>()
            {
                { "Список товаров", this.OnProductSelected },
                { "Заказы", this.OnOrdersSelected },
                { "Акции", this.OnSpecialOffersSelected },
                { "Пункты выдачи", this.OnPickupPointsSelected },
                { "Новости", this.OnNewsSelected },
                { "Объём продаж", this.OnSalesVolumeSelected },
                { "Отчёт по акциям", this.OnTopPromotionsSelected },
                { "Загрузка пунктов выдачи", this.OnPickupPointsLoadSelected },
            };

            _new_category = String.Empty;
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
            var menu = Grid_Menu.GetAllMenuItems().Where(x => x.Header.Equals("Заказы")).First();
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.CheckedEvent));
        }

        #endregion

        #region Menu_Grid

        Grid? currentGrid;

        readonly Dictionary<String, Func<Task>> updatesByName;
        readonly Dictionary<String, Grid> gridByNames;

        // Флаг для предотвращения бесконечной рекурсии в меню
        private bool _isHandlingClick;

        private async void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;
            if (item is null)
                return;

            this.gridByNames.TryGetValue(item.Header as string ?? "", out var grid);
            this.updatesByName.TryGetValue(item.Header as string ?? "", out var func);

            if (grid is null)
                return;

            if (currentGrid is not null)
                currentGrid.Visibility = Visibility.Collapsed;
            currentGrid = grid;
            currentGrid.Visibility = Visibility.Visible;

            if (func is null)
                return;

            await func.Invoke();
        }

        private void MenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (_isHandlingClick)
                return;
            if (sender is not MenuItem item)
                return;
            _isHandlingClick = true;

            Grid_Menu.UncheckAllMenuItems();
            item.CheckMenuItemAndParents();

            _isHandlingClick = false;
        }

        private void MenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isHandlingClick)
                return;

            if (sender is not MenuItem item)
                return;

            _isHandlingClick = true;
            item.IsChecked = true;
            _isHandlingClick = false;
        }

        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid_name = gridByNames.First(x => x.Value.Equals(currentGrid)).Key;
                var func = updatesByName[grid_name];
                await func.Invoke();
            }
            catch { }
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
            try
            {
                await UpdateCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            try
            {
                var selectedCategory = (sender as ListView)?.SelectedItem;
                if (selectedCategory != null)
                {
                    await UpdateProducts();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
        private int _categories_listview_width;
        public int Categories_ListView_Width
        {
            get { return _categories_listview_width; }
            set
            {
                _categories_listview_width = value;
                OnPropertyChanged("Categories_ListView_Width");
                
            }
        }

        private int _categories_listview_textbox_width;
        public int Categories_ListView_TextBox_Width
        {
            get { return _categories_listview_textbox_width; }
            set
            {
                _categories_listview_textbox_width = value;
                OnPropertyChanged("Categories_ListView_TextBox_Width");
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

        private dynamic? _products_filter = new ExpandoObject();
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

        private bool is_products_filtered = false;

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

            if (ProductsShouldBeFiltered && ProductsFilter is not null)
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
                                sql += "\nWHERE lower(p.name) LIKE lower(@name)";
                                isWhere = true;
                            }
                            else
                            {
                                sql += " AND lower(p.name) LIKE lower(@name)";
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
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var cmd = this.CreateProductSQL(ProductsShouldBeFiltered);

                    var products = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                    Products = new ObservableCollection<object>(products);

                    is_products_filtered = ProductsShouldBeFiltered;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteSelectedProducts_Button_Click(object sender, RoutedEventArgs e)
        {
            var selected_products = Products_DataGrid.SelectedItems.Cast<dynamic>().ToArray();

            if (selected_products is null)
                return;

            if (selected_products.Count() is 0)
                return;

            var count = selected_products.Count();

            var to_view = String.Join('\n', selected_products.Take(6).Select(x => $"[{x.id}] {x.name}"));
            if (count > 6)
                to_view += "\n...";

            if (MessageBox.Show($"Вы точно уверены, что хотите удалить данны{(count is 1 ? "й" : "е")} товар{(count is 1 ? "" : "ы")}? Вего удалить: {count}\n\n{to_view}",
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
            var window = new ProductInfoWindow(null, true);
            var is_created = window.ShowDialog() ?? false;

            var new_product = window.ProductInfo;

            if (!is_created || new_product is null)
                return;

            if (CurrentCategory is null)
                return;

            if ((CurrentCategory.id is -1 | CurrentCategory.id == new_product.category_id) && !is_products_filtered)
            {
                if (new_product.category_id is not -1)
                {
                    var found = Categories.First(x => ((dynamic)x).id == new_product.category_id);
                    if (found != null)
                    {
                        new_product.category_name = (found as dynamic).name;
                    }
                }
                Products.Add(new_product);
            }

            var window_view = new ProductInfoWindow(new_product, false)
            {
                Top = window.Top,
                Left = window.Left
            };
            window_view.ShowDialog();
        }

        private async void ProductsFilter_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ProductsFilter is null)
                    return;

                await UpdateProducts(ProductsShouldBeFiltered: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #endregion

        #region Orders Grid

        private void PickupPoint_Combobox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not ComboBox cbox)
                return;
            cbox.IsDropDownOpen = true;
        }
        private void test_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not ComboBox cbox)
                return;
            if (e.OriginalSource is not TextBox tb)
                return;

            if (cbox.ItemsSource.Cast<dynamic>().Select(x => x.address).Contains(cbox.Text))
            {
                cbox.Items.Filter = null;
                return;
            }

            cbox.Items.Filter = (obj) =>
            {
                var vs = (dynamic)obj;
                if (vs is null)
                    return false;
                if (vs.address is not String str)
                    return false;

                if (str.ToLower().Contains(tb.Text.ToLower()))
                {
                    return true;
                }

                return false;
            };
        }

        enum OrdersType
        {
            New,
            Current,
            Completed
        }

        private OrdersType current_orders_type;

        private async void Orders_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
                return;

            current_orders_type = item.Header switch
            {
                "Новые" => OrdersType.New,
                "Текущие" => OrdersType.Current,
                "Завершённые" => OrdersType.Completed,
                _ => OrdersType.New
            };

            await UpdateOrders();
        }
        private void Orders_MenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
                return;
            _isHandlingClick = true;
            MoreEnumerable.ForEach(Orders_Menu.Items.Cast<MenuItem>().Where(x => x != item), (x) =>
            {
                x.IsChecked = false;
            });
            item.IsChecked = true;
            _isHandlingClick = false;
        }
        private void Orders_MenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isHandlingClick)
                return;
            if (sender is not MenuItem item)
                return;

            _isHandlingClick = true;
            item.IsChecked = true;
            _isHandlingClick = false;
        }

        private async Task OnOrdersSelected()
        {
            try
            {
                await UpdateOrdersPickupPoints();
                var menu = Orders_Menu.Items.Cast<MenuItem>().Where(x => x.Header.Equals("Новые")).First();
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.CheckedEvent));
                current_orders_type = OrdersType.New;
                await UpdateOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ObservableCollection<object> _orders = new();
        public ObservableCollection<object> Orders
        {
            get { return _orders; }
            set
            {
                _orders.Clear();
                _orders = value;
                OnPropertyChanged(nameof(Orders));
            }
        }

        private async Task UpdateOrders(bool OrdersShouldBeFiltered = false)
        {
            var vs = Orders_Order_DateTime_From_Filter_TextBox.Text;
            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var cmd = this.CreateOrdersSQL(OrdersShouldBeFiltered);

                var orders = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                Orders = new ObservableCollection<object>(orders);
            }

            if (Orders.Any())
                this.Orders_DataGrid.SelectedItem = Orders.First();
        }

        private (String sql, object? data) CreateOrdersSQL(bool OrdersShouldBeFiltered = false)
        {
            var filter_string = String.Empty;

            if (OrdersShouldBeFiltered && OrdersFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)OrdersFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    filter_string += kvp.Key switch
                    {
                        "order_id" => " AND o.id = @order_id",
                        "telegram_id" => " AND us.telegram_id = @telegram_id",
                        "order_timestamp_from" => " AND o.order_timestamp >= @order_timestamp_from",
                        "order_timestamp_to" => " AND o.order_timestamp <= @order_timestamp_to",
                        "pickup_point_address" => " AND lower(pp.address) LIKE lower(@pickup_point_address)",
                        "status" => " AND o.status = @status",
                        _ => ""
                    };
                }
            }

            var sql_status = current_orders_type switch
            {
                OrdersType.New => "= 'created'",
                OrdersType.Current => "NOT IN ('created', 'completed', 'canceled')",
                OrdersType.Completed => "IN ('completed', 'canceled')",
                _ => "= 'created'",
            };

            var sql =
                        @$"SELECT
                            o.id AS order_id,
                            us.telegram_id,
                            us.name as user_name,
                            o.pickup_point_id,
                            pp.address AS pickup_point_address,
                            o.status,
                            o.order_timestamp,
                            o_sum.total_price,
                            o_sum.total_count
                        FROM orders o
                        JOIN pickup_points pp ON o.pickup_point_id = pp.id
                        JOIN users us ON o.user_id = us.id
                        JOIN (
                            SELECT 
                                order_id,
                                count(*) as total_count,
                                sum(price*quantity) as total_price
                            FROM order_items
                            GROUP BY order_id
                        ) o_sum ON o.id = o_sum.order_id
                        WHERE o.status {sql_status} {filter_string}
                        ORDER BY o.order_timestamp DESC";

            return (sql, _orders_filter);
        }
        private async void OrdersFilter_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateOrders(OrdersShouldBeFiltered: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private dynamic? _orders_filter = new ExpandoObject();
        public dynamic? OrdersFilter
        {
            get { return _orders_filter; }
            set
            {
                _orders_filter = value;
                OnPropertyChanged(nameof(OrdersFilter));
            }
        }

        private ObservableCollection<KeyValuePair<String, String>> statuses = new(StatusConverter.Statuses.ToList());
        public ObservableCollection<KeyValuePair<String, String>> Statuses
        {
            get { return statuses; }
            set
            {
                statuses.Clear();
                statuses = value;
                OnPropertyChanged(nameof(Statuses));
            }
        }


        private ObservableCollection<KeyValuePair<String, String>> statuses_wo_empty = new(StatusConverter.Statuses.Where(x => x.Key.Length is not 0).ToList());
        public ObservableCollection<KeyValuePair<String, String>> Statuses_WO_Empty
        {
            get { return new(statuses.Where(x => x.Key.Length is not 0).ToList()); }
        }

        public ObservableCollection<object> _orders_pickup_points = new();
        public ObservableCollection<object> Orders_PickupPoints
        {
            get { return _orders_pickup_points; }
            set
            {
                _orders_pickup_points.Clear();
                _orders_pickup_points = value;
                OnPropertyChanged(nameof(Orders_PickupPoints));
            }
        }

        private async Task UpdateOrdersPickupPoints()
        {
            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var p_points = await conn.QueryAsExpandoAsync("SELECT id, address FROM pickup_points WHERE is_receiving_orders = True AND is_works = True");
                Orders_PickupPoints = new ObservableCollection<object>(p_points);
            }
        }

        private dynamic? _orders_current_pickup_point;
        public dynamic? Orders_Current_PickupPoint
        {
            get { return _orders_current_pickup_point; }
            set
            {
                _orders_current_pickup_point = value;
                OnPropertyChanged(nameof(Orders_Current_PickupPoint));
            }
        }

        private dynamic? _orders_original_pickup_point;
        public dynamic? Orders_Original_PickupPoint
        {
            get { return _orders_original_pickup_point; }
            set
            {
                _orders_original_pickup_point = value;
                OnPropertyChanged(nameof(Orders_Original_PickupPoint));
            }
        }

        private dynamic? _selected_order;
        public dynamic? Selected_Order
        {
            get { return _selected_order; }
            set
            {
                _selected_order = value;
                OnPropertyChanged(nameof(Selected_Order));
            }
        }
        private async void Orders_DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Orders_PickupPoints is null)
                return;

            if (Selected_Order is null) 
                return;

            int pp_id = Selected_Order.pickup_point_id;

            var found = _orders_pickup_points.Cast<dynamic>().Where(x => x.id == pp_id);
            
            if (!found.Any())
            {
                Orders_Current_PickupPoint = new
                {
                    id = -1,
                    address = ""
                };

                Orders_Original_PickupPoint = new
                {
                    id = -1,
                    address = ""
                };

                return;
            }

            Orders_Current_PickupPoint = found.First();

            Orders_Original_PickupPoint = new
            {
                id = pp_id,
                address = Selected_Order.pickup_point_address
            };


            var status = Selected_Order.status;
            var found_status = Statuses.Where(x => x.Key.Equals(status)).ToList();
            if (!found_status.Any())
                return;

            Orders_Current_Status = found_status.First();

            Orders_Original_Status = new
            {
                Key = Selected_Order.status
            };

            OrderCanBeChanged = !"canceled".Equals(status);

            try
            {
                await UpdateCurrentOrderItems(Selected_Order.order_id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Orders_PickupPoint_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Orders_PickupPoints is null)
                return;

            if (Selected_Order is null)
                return;

            int pp_id = Selected_Order.pickup_point_id;

            var found = _orders_pickup_points.Cast<dynamic>().Where(x => x.id == pp_id);

            if (!found.Any())
            {
                Orders_Current_PickupPoint = new
                {
                    id = -1,
                    address = ""
                };

                Orders_Original_PickupPoint = new
                {
                    id = -1,
                    address = ""
                };

                return;
            }

            PickupPoint_PP_Combobox.SelectedItem = found.First();
            PickupPoint_PP_Combobox.Text = Selected_Order.pickup_point_address;

            Orders_Original_PickupPoint = new
            {
                id = pp_id,
                address = Selected_Order.pickup_point_address
            };
        }

        private async void Orders_PickupPoint_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_orders_current_pickup_point is null || _selected_order is null)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE orders SET pickup_point_id = @pickup_point_id WHERE id = @id";

                    var data = new
                    {
                        pickup_point_id = _orders_current_pickup_point.id,
                        id = _selected_order.order_id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    _selected_order.pickup_point_address = _orders_current_pickup_point.address;

                    if (_orders_current_pickup_point is ExpandoObject exp)
                    {
                        Orders_Original_PickupPoint = exp.Copy();
                    }

                    var mb_result = MessageBox.Show("Создать шаблон уведомления пользователя о смене места выдачи заказа?",
                        "Требуется подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (mb_result is MessageBoxResult.Yes)
                    {
                        OrderNotification = $"Уважаемый, {_selected_order.user_name}. Уведомляем Вас о смене пункта выдачи вашего заказа.\n" + 
                            $"Новый пункт выдачи: {_selected_order.pickup_point_address}.\n" +
                            $"Просим прощения за предоставленные неудобства.";
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private dynamic? _orders_current_status;
        public dynamic? Orders_Current_Status
        {
            get { return _orders_current_status; }
            set
            {
                _orders_current_status = value;
                OnPropertyChanged(nameof(Orders_Current_Status));
            }
        }

        private dynamic? _orders_original_status;
        public dynamic? Orders_Original_Status
        {
            get { return _orders_original_status; }
            set
            {
                _orders_original_status = value;
                OnPropertyChanged(nameof(Orders_Original_Status));
            }
        }

        private bool _order_can_be_changed = true;
        public bool OrderCanBeChanged
        {
            get => _order_can_be_changed;
            set
            {
                _order_can_be_changed= value;
                OnPropertyChanged(nameof(OrderCanBeChanged));
            }
        }

        private void Orders_Status_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Statuses is null)
                return;

            if (Selected_Order is null)
                return;

            var status = Selected_Order.status;
            var found_status = Statuses.Where(x => x.Key.Equals(status)).ToList();
            if (!found_status.Any())
                return;

            Orders_Current_Status = found_status.First();

            Orders_Original_Status = new
            {
                Key = Selected_Order.status
            };
        }

        private async Task<IEnumerable<dynamic>> GetOrderShortage(int order_id)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql =
                                @"SELECT
	                                o.id as order_id,
	                                oi.product_id, 
	                                oi.quantity user_quantity,
	                                pr.name,
	                                pr.quantity shop_quantity,
                                    oi.quantity - pr.quantity as shortage
                                FROM order_items oi
                                JOIN products pr ON oi.product_id = pr.id
                                JOIN orders o ON oi.order_id = o.id
                                WHERE o.id = @order_id AND oi.quantity > pr.quantity
                                ORDER BY product_id";

                    var data = new
                    {
                        order_id
                    };

                    return await conn.QueryAsExpandoAsync(sql, data);
                }
            }
            catch (Exception)
            {
                return [];
            }
        }

        private async void Orders_Status_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_orders_current_status is null || _selected_order is null)
                return;

            string before = _selected_order.status;

            if ("canceled".Equals(before))
            {
                var mb_result = MessageBox.Show("Вы точно уверены, что хотите восстановить отменённый заказ?",
                        "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (mb_result is not MessageBoxResult.OK)
                    return;
            }

            try
            {
                await using ( var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE orders SET status = @status WHERE id = @id";

                    var data = new
                    {
                        id = _selected_order.order_id,
                        status = _orders_current_status.Key
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    _selected_order.status = _orders_current_status.Key;

                    Orders_Original_Status = new
                    {
                        Key = _selected_order.status
                    };
                }
            }
            catch (Exception)
            {
                if (await this.GetOrderShortage(_selected_order.order_id) is not IEnumerable<dynamic> shortage || shortage.Count() is 0)
                {
                    MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                        "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                var to_view = String.Join('\n', shortage.Select(x => $"[{x.product_id}] {x.name} - {x.shortage} шт."));

                MessageBox.Show($"Недостаточное количество товаров на складе:\n{to_view}",
                        "Недостаток на складе", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            string after = _selected_order.status;

            if ("ready".Equals(after))
            {
                await Order_SendReadyNotification(_selected_order.order_id);
            }
        }

        private async Task Order_SendReadyNotification(int order_id)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql =
                                @"INSERT INTO order_notifications 
                                (order_id) VALUES (@order_id)";

                    var data = new
                    {
                        order_id = order_id
                    };

                    await conn.ExecuteAsync(sql, data);
                }
            }
            catch (Exception)
            {

            }
        }

        private String _order_notification = String.Empty;
        public String OrderNotification
        {
            get { return _order_notification; }
            set
            {
                _order_notification = value;
                OnPropertyChanged(nameof(OrderNotification));
            }
        }

        private async void Order_SendNotification_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_selected_order is null)
                return;
            if (_order_notification.Trim().Length is 0)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql =
                                @"INSERT INTO order_notifications 
                                (order_id, message) 
                                VALUES (@order_id, @message)";

                    var data = new
                    {
                        order_id = _selected_order.order_id,
                        message = _order_notification
                    };

                    await conn.ExecuteAsync(sql, data);

                    MessageBox.Show($"Уведомление успешно отправлено пользователю",
                        "Успешная отправка", MessageBoxButton.OK);
                }
            }
            catch (Exception)
            {
                
            }
        }

        private ObservableCollection<object> _current_order_items = new();
        public ObservableCollection<object> CurrentOrderItems
        {
            get { return _current_order_items; }
            set
            {
                _current_order_items.Clear();
                _current_order_items = value;
                OnPropertyChanged(nameof(CurrentOrderItems));
                OnPropertyChanged(nameof(CurrentOrderPrice));
            }
        }

        private async Task UpdateCurrentOrderItems(int order_id)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql =
                                @"SELECT
                                    oi.id,
                                    pr.id as product_id,
                                    pr.name as product_name,
                                    oi.quantity,
                                    oi.price,
                                    oi.price * oi.quantity AS total_price
                                FROM order_items oi
                                JOIN products pr ON pr.id = oi.product_id
                                WHERE oi.order_id = @order_id";

                    var data = new
                    {
                        order_id = order_id
                    };

                    var res = await conn.QueryAsExpandoAsync(sql, data);
                    MoreEnumerable.ForEach(res, (x) => x.changeable_quantity = x.quantity);
                    CurrentOrderItems = new(res);
                }
            }
            catch { }
        }

        private async Task UpdateCurrentOrderInfo(int order_id)
        {
            var vs = CreateOrdersSQL(false);
            vs.sql = vs.sql.Replace("WHERE o.status = 'created'", String.Empty).Replace("ORDER BY o.order_timestamp DESC", String.Empty);
            vs.sql += "Where o.id = @order_id";
            vs.data = new
            {
                order_id
            };

            dynamic order;

            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var data = await conn.QueryAsExpandoAsync(vs.sql, vs.data);
                if (data is null)
                    return;
                order = data.First() as dynamic;
            }

            var old = _orders.First(x => ((dynamic)x).order_id == order_id);

            var index = _orders.IndexOf(old);
            _orders[index] = order;
            Orders_DataGrid.SelectedItem = order;
        }

        public int CurrentOrderPrice
        {
            get
            {
                return _current_order_items.Sum(x => ((dynamic)x)?.total_price);
            }
        }

        private async void Order_Item_Update_Quantity_Button_Click(object sender, RoutedEventArgs e)
        {
            var order_item = (sender as FrameworkElement)?.DataContext as dynamic;

            if (order_item is null)
                return;

            if (MessageBox.Show($"Вы точно уверены, что хотите изменить количество данного товара в заказе?",
                    "Подтвердите действие", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                is not MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "UPDATE order_items SET quantity = @quantity WHERE id = @id";

                    var data = new
                    {
                        quantity = order_item.changeable_quantity,
                        id = order_item.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    order_item.quantity = order_item.changeable_quantity;
                }
                if (Selected_Order is not null)
                    await UpdateCurrentOrderInfo(Selected_Order.order_id);
            }
            catch (Npgsql.PostgresException ex)
            {
                if (ex.ConstraintName?.Contains("quantity") ?? true)
                {
                    MessageBox.Show("Недостаточно товаров на складе.",
                        "Ошибка изменения данных", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Order_Item_Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_current_order_items.Count is 1)
            {
                MessageBox.Show($"Нельзя оставлять заказ без товаров!",
                    "Подтвердите действие", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var order_item = (sender as FrameworkElement)?.DataContext as dynamic;

            if (order_item is null)
                return;

            if (MessageBox.Show($"Вы точно уверены, что хотите удалить данный товар из заказа без возможности восстановления?",
                    "Подтвердите действие", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                is not MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "DELETE FROM order_items WHERE id = @id";

                    var data = new
                    {
                        id = order_item.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);
                }

                if (Selected_Order is not null)
                    await UpdateCurrentOrderInfo(Selected_Order.order_id);
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Order_Item_Info_Button_Click(object sender, RoutedEventArgs e)
        {
            var order_item = (sender as FrameworkElement)?.DataContext as dynamic;

            if (order_item is null)
                return;

            var vs = CreateProductSQL(false);
            vs.sql = vs.sql.Replace("ORDER BY p.id", String.Empty).Replace("WHERE p.category_id = @category_id", String.Empty);
            vs.sql += "WHERE p.id = @product_id";
            vs.data = new
            {
                order_item.product_id
            };

            dynamic product;

            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var data = await conn.QueryAsExpandoAsync(vs.sql, vs.data);
                if (data is null)
                    return;
                product = data.First() as dynamic;
            }

            var window = new ProductInfoWindow(product, false);
            window.ShowDialog();
        }

        private void Order_CreatePDF_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_selected_order is null)
                return;

            var order = _selected_order;
            var order_items = _current_order_items.ToList();

            var path = CreatePdf(order, order_items);
            OpenPdfFile(path);
        }

        private void OpenPdfFile(string filePath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                var process = Process.Start(psi);

                if (process is null)
                {
                    Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
                    saveFileDialog.Filter = "PDF Files (*.pdf)|*.pdf";
                    bool? result = saveFileDialog.ShowDialog();

                    if (result is not true)
                        return;

                    if (!File.Exists(filePath))
                        return;

                    File.Copy(filePath, saveFileDialog.FileName, true);
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибки открытия файла
                Console.WriteLine($"Ошибка открытия файла: {ex.Message}");
            }
        }

        public static String CreatePdf(dynamic order_info, List<dynamic> order_items)
        {
            int text_size = 14;
            int table_size = 12;

            // Создание нового документа PDF
            var document = new MigraDoc.DocumentObjectModel.Document();
            document.Info.Title = $"Заказ №{order_info.order_id}";

            // Создание секции для заголовка
            var headerSection = document.AddSection();

            var paragraph = headerSection.AddParagraph($"Заказ №{order_info.order_id}");
            paragraph.Format.Font.Bold = true;
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            paragraph.Format.Font.Size = 25;

            headerSection.AddParagraph().Format.SpaceAfter = 10;

            paragraph = headerSection.AddParagraph($"Telegram идентификатор пользователя: {order_info.telegram_id}");
            paragraph.Format.Font.Size = text_size;
            paragraph.Format.SpaceAfter = 2;
            
            paragraph = headerSection.AddParagraph($"Дата заказа: {order_info.order_timestamp}");
            paragraph.Format.Font.Size = text_size;
            paragraph.Format.SpaceAfter = 2;

            paragraph = headerSection.AddParagraph($"Адрес пункта выдачи: {order_info.pickup_point_address}");
            paragraph.Format.Font.Size = text_size;
            paragraph.Format.SpaceAfter = 2;

            paragraph = headerSection.AddParagraph($"Общая стоимость: {order_info.total_price} р.");
            paragraph.Format.Font.Size = text_size;

            headerSection.AddParagraph().Format.SpaceAfter = 10;

            // Создание таблицы для товаров
            var table = headerSection.AddTable();
            table.Format.Font.Size = table_size;
            table.Style = "Table";
            table.Format.Alignment = ParagraphAlignment.Center;

            double width = 17;

            var width_part = width / (3 + 7 + 3 + 3 + 3);

            table.AddColumn(Unit.FromCentimeter(width_part * 3));
            table.AddColumn(Unit.FromCentimeter(width_part * 7));
            table.AddColumn(Unit.FromCentimeter(width_part * 3));
            table.AddColumn(Unit.FromCentimeter(width_part * 3));
            table.AddColumn(Unit.FromCentimeter(width_part * 3));

            var header_row = table.AddRow();
            header_row.Format.SpaceAfter = 2;
            var header_cells = header_row.Cells;
            header_cells[0].AddParagraph("Артикул");
            header_cells[1].AddParagraph("Название");
            header_cells[2].AddParagraph("Единиц");
            header_cells[3].AddParagraph("Стоимость единицы");
            header_cells[4].AddParagraph("Общая cтоимость");
            table.Borders.Width = 1;

            // Заполнение таблицы товарами
            foreach (var item in order_items)
            {
                var row = table.AddRow();
                var table_cells = row.Cells;
                if (table_cells is null)
                    continue;

                MigraDoc.DocumentObjectModel.Paragraph cell = table_cells[0].AddParagraph($"{item.product_id}");
                cell.Format.SpaceAfter = 2;

                cell = table_cells[1].AddParagraph($"{item.product_name}");
                cell.Format.SpaceAfter = 2;
                cell.Format.Alignment = ParagraphAlignment.Justify;

                cell = table_cells[2].AddParagraph($"{item.quantity}");
                cell.Format.SpaceAfter = 2;

                cell = table_cells[3].AddParagraph($"{item.price} р.");
                cell.Format.SpaceAfter = 2;
                
                cell = table_cells[4].AddParagraph($"{item.total_price} р.");
                cell.Format.SpaceAfter = 2;
            }

            headerSection.AddParagraph().Format.SpaceAfter = 20;
            var info_par = headerSection.AddParagraph("Возврат товаров по гарантии возможен только при наличии данного документа.");
            info_par.Format.SpaceAfter = 40;

            var signatureTable = headerSection.AddTable();
            signatureTable.Format.Alignment = ParagraphAlignment.Center;
            signatureTable.Format.Font.Size = text_size;
            signatureTable.AddColumn("5.5cm");
            signatureTable.AddColumn("6cm");
            signatureTable.AddColumn("5.5cm");

            var sig_row = signatureTable.AddRow();
            sig_row.Format.SpaceAfter = 15;
            var cells = sig_row.Cells;
            cells[0].AddParagraph("Подпись покупателя");
            cells[2].AddParagraph("Подпись выдающего");
            cells = signatureTable.AddRow().Cells;
            cells[0].AddParagraph("_____________________");
            cells[2].AddParagraph("_____________________");


            var pdfRenderer = new PdfDocumentRenderer();
            pdfRenderer.Document = document;
            pdfRenderer.RenderDocument();

            // Сохраните массив байтов в временный файл
            string tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");

            pdfRenderer.Save(tempFilePath);

            return tempFilePath;
        }

        #endregion

        #region SpecialOffers Grid

        enum SpecialOffersType
        {
            Current,
            Planned,
            Completed
        }
        private SpecialOffersType current_special_offer_type;

        public bool Discount_Сhangeability
        {
            get
            {
                if (_selected_special_offer is null)
                    return false;
                return _selected_special_offer.start_datetime > DateTime.Now;
            }
        }
        public bool Start_DateTime_Сhangeability
        {
            get
            {
                if (_selected_special_offer is null)
                    return false;
                return _selected_special_offer.start_datetime > DateTime.Now;
            }
        }
        public bool End_DateTime_Сhangeability
        {
            get
            {
                if (_selected_special_offer is null)
                    return false;
                return _selected_special_offer.end_datetime > DateTime.Now;
            }
        }

        private async void SpecialOffers_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
                return;

            current_special_offer_type = item.Header switch
            {
                "Текущие" => SpecialOffersType.Current,
                "Запланированные" => SpecialOffersType.Planned,
                "Завершённые" => SpecialOffersType.Completed,
                _ => SpecialOffersType.Current
            };

            await UpdateSpecialOffers();
        }
        private void SpecialOffers_MenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
                return;
            _isHandlingClick = true;
            MoreEnumerable.ForEach(SpecialOffers_Menu.Items.Cast<MenuItem>().Where(x => x != item), (x) =>
            {
                x.IsChecked = false;
            });
            item.IsChecked = true;
            _isHandlingClick = false;
        }
        private void SpecialOffers_MenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isHandlingClick)
                return;
            if (sender is not MenuItem item)
                return;

            _isHandlingClick = true;
            item.IsChecked = true;
            _isHandlingClick = false;
        }

        private async Task OnSpecialOffersSelected()
        {
            var menu = SpecialOffers_Menu.Items.Cast<MenuItem>().Where(x => x.Header.Equals("Текущие")).First();
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.CheckedEvent));
            current_special_offer_type = SpecialOffersType.Current;

            try
            {
                await UpdateSpecialOffers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ObservableCollection<object> _special_offers = new();
        public ObservableCollection<object> SpecialOffers
        {
            get { return _special_offers; }
            set
            {
                _special_offers.Clear();
                _special_offers = value;
                OnPropertyChanged(nameof(SpecialOffers));
            }
        }

        private async Task UpdateSpecialOffers(bool ShouldBeFiltered = false)
        {
            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var cmd = this.CreateSpecialOffersSQL(ShouldBeFiltered);

                var special_offers = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                MoreEnumerable.ForEach(special_offers, 
                    x => 
                    {
                        x.changeable_discount = x.discount;
                        x.changeable_start_datetime = x.start_datetime;
                        x.changeable_end_datetime = x.end_datetime;
                    });

                SpecialOffers = new ObservableCollection<object>(special_offers);
            }

            if (SpecialOffers.Any())
                this.SpecialOffers_DataGrid.SelectedItem = SpecialOffers.First();
        }

        private (String sql, object? data) CreateSpecialOffersSQL(bool ShouldBeFiltered = false)
        {
            var filter_string = String.Empty;

            if (ShouldBeFiltered && SpecialOffersFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)SpecialOffersFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    filter_string += kvp.Key switch
                    {
                        "so_id" => " AND so.id = @so_id",
                        "product_id" => " AND so.product_id = @product_id",
                        "discount_from" => " AND so.discount >= @discount_from",
                        "discount_to" => " AND so.discount <= @discount_to",
                        _ => ""
                    };
                }

                if (!expandoDict.ContainsKey("datetime_to"))
                    SpecialOffersFilter.datetime_to = null;
                if (!expandoDict.ContainsKey("datetime_from"))
                    SpecialOffersFilter.datetime_from = null;

                filter_string += " AND (so.start_datetime <= @datetime_to OR @datetime_to IS NULL) AND (so.end_datetime >= @datetime_from OR @datetime_from IS NULL)";
            }

            var sql_status = current_special_offer_type switch
            {
                SpecialOffersType.Current => "LOCALTIMESTAMP BETWEEN so.start_datetime AND so.end_datetime",
                SpecialOffersType.Planned => "so.start_datetime > LOCALTIMESTAMP",
                SpecialOffersType.Completed => "so.end_datetime < LOCALTIMESTAMP",
                _ => "LOCALTIMESTAMP BETWEEN so.start_datetime AND so.end_datetime",
            };

            var sql =
                        @$"
                        SELECT
                            so.id AS so_id,
                            so.product_id AS product_id,
                            pr.name AS product_name,
                            so.start_datetime,
                            so.end_datetime,
                            so.discount,
                            pr.price AS def_price,
                            (pr.price - pr.price * so.discount / 100)::integer AS new_price
                        FROM special_offers so
                        JOIN products pr ON so.product_id = pr.id
                        WHERE {sql_status} {filter_string}
                        ORDER BY so.id DESC";

            return (sql, _special_offers_filter);
        }
        
        private async void SpecialOffersFilter_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateSpecialOffers(ShouldBeFiltered: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private dynamic _special_offers_filter = new ExpandoObject();
        public dynamic SpecialOffersFilter
        {
            get { return _special_offers_filter; }
            set
            {
                _special_offers_filter = value;
                OnPropertyChanged(nameof(SpecialOffersFilter));
            }
        }
        
        private dynamic? _selected_special_offer;
        public dynamic? SelectedSpecialOffer
        {
            get { return _selected_special_offer; }
            set
            {
                _selected_special_offer = value;
                OnPropertyChanged(nameof(SelectedSpecialOffer));

                OnPropertyChanged(nameof(Discount_Сhangeability));
                OnPropertyChanged(nameof(Start_DateTime_Сhangeability));
                OnPropertyChanged(nameof(End_DateTime_Сhangeability));
                
                OnPropertyChanged(nameof(CancelButtonContent));
                OnPropertyChanged(nameof(CancelButtonVisibility));
            }
        }
        
        private void SpecialOffers_Find_Product_Filter_Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new SelectProductWindow();
            var res = window.ShowDialog();
            if (res == true)
            {
                var selected = window.SelectedProduct;
                SpecialOffersFilter.product_id = selected.id;
            }
        }

        private async Task UpdateSpecialOfferInfo(int so_id)
        {
            var vs = CreateSpecialOffersSQL(false);

            var where = vs.sql.LastIndexOf("WHERE");
            vs.sql = vs.sql.Substring(0, where);

            vs.sql += "WHERE so.id = @so_id";
            vs.data = new
            {
                so_id
            };

            dynamic special_offer;

            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var data = await conn.QueryAsExpandoAsync(vs.sql, vs.data);
                if (data is null)
                    return;
                special_offer = data.First();
            }

            var old = _special_offers.First(x => ((dynamic)x).so_id == so_id);

            special_offer.changeable_discount = special_offer.discount;
            special_offer.changeable_start_datetime = special_offer.start_datetime;
            special_offer.changeable_end_datetime = special_offer.end_datetime;


            var index = _special_offers.IndexOf(old);
            _special_offers[index] = special_offer;
            SpecialOffers_DataGrid.SelectedItem = special_offer;

            OnPropertyChanged(nameof(Discount_Сhangeability));
            OnPropertyChanged(nameof(Start_DateTime_Сhangeability));
            OnPropertyChanged(nameof(End_DateTime_Сhangeability));
        }

        private async void SpecialOffers_Discount_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSpecialOffer is null)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @"UPDATE special_offers SET discount = @discount WHERE id = @id";

                    var data = new
                    {
                        id = SelectedSpecialOffer.so_id,
                        discount = SelectedSpecialOffer.changeable_discount
                    };

                    await conn.ExecuteAsync(sql, data);

                    await UpdateSpecialOfferInfo(SelectedSpecialOffer.so_id);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }
        private void SpecialOffers_Discount_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSpecialOffer != null)
                SelectedSpecialOffer.changeable_discount = SelectedSpecialOffer.discount;
        }

        private async void SpecialOffers_Start_DateTime_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSpecialOffer is null)
                return;

            if (SelectedSpecialOffer.changeable_start_datetime < DateTime.Now)
            {
                var result = MessageBox.Show("Указанная дата меньше текущей. Бедет использована текущая дата",
                    "Дата меньше текущей", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (result is not MessageBoxResult.OK)
                    return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var dt = SelectedSpecialOffer.changeable_start_datetime < DateTime.Now ? "LOCALTIMESTAMP" : "@start_datetime";
                    var sql = @$"UPDATE special_offers SET start_datetime = {dt} WHERE id = @id";

                    var data = new
                    {
                        id = SelectedSpecialOffer.so_id,
                        start_datetime = SelectedSpecialOffer.changeable_start_datetime
                    };

                    await conn.ExecuteAsync(sql, data);

                    await UpdateSpecialOfferInfo(SelectedSpecialOffer.so_id);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }
        private void SpecialOffers_Start_DateTime_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSpecialOffer != null)
                SelectedSpecialOffer.changeable_start_datetime = SelectedSpecialOffer.start_datetime;
        }

        private async void SpecialOffers_End_DateTime_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSpecialOffer is null)
                return;

            if (SelectedSpecialOffer.changeable_end_datetime < DateTime.Now)
            {
                var result = MessageBox.Show("Указанная дата меньше текущей. Бедет использована текущая дата",
                    "Дата меньше текущей", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (result is not MessageBoxResult.OK)
                    return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var dt = SelectedSpecialOffer.changeable_end_datetime < DateTime.Now ? "LOCALTIMESTAMP" : "@end_datetime";
                    var sql = @$"UPDATE special_offers SET end_datetime = {dt} WHERE id = @id";

                    var data = new
                    {
                        id = SelectedSpecialOffer.so_id,
                        end_datetime = SelectedSpecialOffer.changeable_end_datetime
                    };

                    await conn.ExecuteAsync(sql, data);

                    await UpdateSpecialOfferInfo(SelectedSpecialOffer.so_id);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }
        private void SpecialOffers_End_DateTime_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSpecialOffer != null)
                SelectedSpecialOffer.changeable_end_datetime = SelectedSpecialOffer.end_datetime;
        }

        private dynamic _new_special_offer = new ExpandoObject();
        public dynamic NewSpecialOffer
        {
            get { return _new_special_offer; }
            set
            {
                _new_special_offer = value;
                OnPropertyChanged(nameof(NewSpecialOffer));
            }
        }

        private bool _is_special_offer_creation = false;
        public bool IsSpecialOfferCreation
        {
            get
            {
                return _is_special_offer_creation;
            }
            set
            {
                _is_special_offer_creation = value;
                OnPropertyChanged(nameof(IsSpecialOfferCreation));
            }
        }

        private void SpecialOffers_Find_Product_New_Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new SelectProductWindow();
            var res = window.ShowDialog();
            if (res == true)
            {
                var selected = window.SelectedProduct;
                NewSpecialOffer.product_id = selected.id;
            }
        }
        private void SpecialOffers_Start_Create_New_Button_Click(object sender, RoutedEventArgs e)
        {
            NewSpecialOffer = new ExpandoObject();
            IsSpecialOfferCreation = true;
        }
        private void SpecialOffers_Cancel_Create_New_Button_Click(object sender, RoutedEventArgs e)
        {
            IsSpecialOfferCreation = false;
        }

        private async void SpecialOffers_Create_New_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_new_special_offer is null)
                return;

            try
            {
                if (_new_special_offer.product_id.ToString().Trim().Length is 0)
                {
                    MessageBox.Show("Отсутсвует артикул товара",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Отсутсвует артикул товара",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                if (_new_special_offer.datetime_from.ToString().Trim().Length is 0)
                {
                    MessageBox.Show("Отсутсвует дата начала",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Отсутсвует дата начала",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                if (_new_special_offer.datetime_to.ToString().Trim().Length is 0)
                {
                    MessageBox.Show("Отсутсвует дата конца",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Отсутсвует дата конца",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                if (_new_special_offer.discount.ToString().Trim().Length is 0)
                {
                    MessageBox.Show("Отсутсвует размер скидки",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Отсутсвует размер скидки",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_new_special_offer.datetime_from > _new_special_offer.datetime_to)
            {
                MessageBox.Show("Указанная дата начала меньше указанной даты окончания",
                    "Ошибка дат", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_new_special_offer.datetime_from < DateTime.Now)
            {
                var result = MessageBox.Show("Указанная дата начала меньше текущей. Бедет использована текущая дата",
                    "Дата меньше текущей", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (result is not MessageBoxResult.OK)
                    return;
            }

            var dateTime = _new_special_offer.datetime_from < DateTime.Now ? "LOCALTIMESTAMP" : _new_special_offer.datetime_from;
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @$"INSERT INTO special_offers 
                                 (product_id, start_datetime, end_datetime, discount) 
                                 VALUES (@product_id, @start_datetime, @end_datetime, @discount)";

                    var data = new
                    {
                        product_id = _new_special_offer.product_id,
                        start_datetime = _new_special_offer.datetime_from,
                        end_datetime = _new_special_offer.datetime_to,
                        discount = _new_special_offer.discount
                    };

                    await conn.ExecuteAsync(sql, data);
                }

                var task = Task.Run(() =>
                {
                    MessageBox.Show("Новая акция была успешная создана", "Действие выполнено", MessageBoxButton.OK, MessageBoxImage.Information);
                });

                IsSpecialOfferCreation = false;

                await UpdateSpecialOffers();

                await task;
            }
            catch (PostgresException ex)
            {
                MessageBox.Show($"Ошибка при создании акции:\n{ex.MessageText}",
                    "Ошибка при создании", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при создании данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

        }

        public String CancelButtonContent
        {
            get
            {
                if (_selected_special_offer is null)
                    return String.Empty;
                if (_selected_special_offer.start_datetime > DateTime.Now)
                    return "Отменить акцию";
                if (_selected_special_offer.end_datetime > DateTime.Now)
                    return "Завершить акцию";
                return String.Empty;
            }
        }

        public Visibility CancelButtonVisibility
        {
            get
            {
                if (_selected_special_offer is null)
                    return Visibility.Collapsed;
                if (_selected_special_offer.start_datetime > DateTime.Now)
                    return Visibility.Visible;
                if (_selected_special_offer.end_datetime > DateTime.Now)
                    return Visibility.Visible;
                return Visibility.Collapsed;
            }
        }

        private async void SpecialOffers_Cancel_Offer_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSpecialOffer is null)
                return;

            String question;
            if (CancelButtonContent.StartsWith("Отменить"))
            {
                question = "Вы точно уверены, что хотите отменить запланированную акцию? Запланированная акция будет удалена без возможности восстановления";
            }
            else
            {
                question = "Вы точно уверены, что хотите завершить текущую акцию? Акция будет прервана без возможности возобновления";
            }

            var result = MessageBox.Show(question, "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information);

            if (result is not MessageBoxResult.OK)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @$"SELECT * FROM cancel_special_offer(@offer_id)";

                    var data = new
                    {
                        offer_id = SelectedSpecialOffer.so_id
                    };

                    var q_result = await conn.QueryFirstAsync<String>(sql, data);

                    if (q_result.Equals("Ended early"))
                    {
                        await UpdateSpecialOfferInfo(SelectedSpecialOffer.so_id);
                        MessageBox.Show("Акция успешно завершена. Данные обновлен", "Успешное выполнение", MessageBoxButton.OK, MessageBoxImage.Information); 
                    }
                    else if (q_result.Equals("Canceled"))
                    {
                        var to_delete = _special_offers.First(x => ((dynamic)x).so_id == SelectedSpecialOffer.so_id);
                        SpecialOffers.Remove(to_delete);
                        MessageBox.Show("Акция успешно отменена. Данные обновлен", "Успешное выполнение", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (q_result.Equals("Cannot cancel"))
                    {
                        await UpdateSpecialOfferInfo(SelectedSpecialOffer.so_id);
                        MessageBox.Show("Акция не может быть отменена. Данные обновлены", "Ошибка выполнения", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (q_result.Equals("Not found"))
                    {
                        var to_delete = _special_offers.First(x => ((dynamic)x).so_id == SelectedSpecialOffer.so_id);
                        SpecialOffers.Remove(to_delete);
                        MessageBox.Show("Акция не найдена. Данные обновлены", "Ошибка выполнения", MessageBoxButton.OK, MessageBoxImage.Error);
                    } 
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

        #region PickupPoints Grid

        private IEnumerable<KeyValuePair<String, String>> _pp_statuses = PP_StatusConverter.Statuses.ToList();
        public IEnumerable<KeyValuePair<String, String>> PPStatuses
        {
            get
            {
                return _pp_statuses.Skip(1);
            }
        }

        private async Task OnPickupPointsSelected()
        {
            try
            {
                await UpdatePickupPoints();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ObservableCollection<object> _pickup_points = new();
        public ObservableCollection<object> PickupPoints
        {
            get { return _pickup_points; }
            set
            {
                _pickup_points.Clear();
                _pickup_points = value;
                OnPropertyChanged(nameof(PickupPoints));
            }
        }
        private async Task UpdatePickupPoints(bool ShouldBeFiltered = false)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var cmd = this.CreatePickupPointsSQL(ShouldBeFiltered);

                    var pickup_points = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                    MoreEnumerable.ForEach(pickup_points,
                        x =>
                        {
                            x.changeable_address = x.address;
                            x.changeable_summary = x.summary;
                        });

                    PickupPoints = new ObservableCollection<object>(pickup_points);
                }
            }
            catch (Exception)
            {
            }

            if (PickupPoints.Any())
                this.PickupPoints_DataGrid.SelectedItem = PickupPoints.First();
        }

        private (String sql, object? data) CreatePickupPointsSQL(bool ShouldBeFiltered = false)
        {
            var filter_string = String.Empty;

            if (ShouldBeFiltered && PickupPointsFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)PickupPointsFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    filter_string += kvp.Key switch
                    {
                        "address" => " AND lower(address) LIKE lower(@address)",
                        "status" => " AND status = @status",
                        _ => ""
                    };
                }
            }

            var sql =
                        @$"
                        SELECT 
                            id,
                            address,
                            summary,
                            is_works,
                            is_receiving_orders,
                            status
                        FROM pickup_points_view pp
                        WHERE {filter_string}
                        ORDER BY pp.id ASC";

            sql = sql.Replace("WHERE \r\n                        ", "");
            sql = sql.Replace("WHERE  AND", "WHERE");

            return (sql, _pickup_points_filter);
        }

        private async void PickupPointsFilter_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdatePickupPoints(ShouldBeFiltered: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при получении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private dynamic _pickup_points_filter = new ExpandoObject();
        public dynamic PickupPointsFilter
        {
            get { return _pickup_points_filter; }
            set
            {
                _pickup_points_filter = value;
                OnPropertyChanged(nameof(PickupPointsFilter));
            }
        }

        private dynamic? _selected_pickup_point;
        public dynamic? SelectedPickupPoint
        {
            get { return _selected_pickup_point; }
            set
            {
                _selected_pickup_point = value;
                OnPropertyChanged(nameof(SelectedPickupPoint));
            }
        }

        private void PickupPoints_DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedPickupPoint is null)
                return;

            var status = SelectedPickupPoint.status;
            var found_status = PPStatuses.Where(x => x.Key.Equals(status)).ToList();
            if (found_status.Count == 0)
                return;

            PickupPoints_Current_Status = found_status.First();

            PickupPoints_Original_Status = new
            {
                Key = SelectedPickupPoint.status
            };
        }

        private async void PickupPoints_Address_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPickupPoint is null)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @"UPDATE pickup_points SET address = @address WHERE id = @id";

                    var data = new
                    {
                        id = SelectedPickupPoint.id,
                        address = SelectedPickupPoint.changeable_address
                    };

                    await conn.ExecuteAsync(sql, data);

                    SelectedPickupPoint.address = SelectedPickupPoint.changeable_address;
                }
            }
            catch (PostgresException ex)
            {
                if (ex.MessageText.Contains("pickup_points_address_key"))
                {
                    MessageBox.Show($"Пункт выдачи с данным адресом уже существует",
                        "Повторение данных", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }
        private void PickupPoints_Address_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPickupPoint != null)
                SelectedPickupPoint.changeable_address = SelectedPickupPoint.address;
        }

        private dynamic? _pickup_points_current_status;
        public dynamic? PickupPoints_Current_Status
        {
            get { return _pickup_points_current_status; }
            set
            {
                _pickup_points_current_status = value;
                OnPropertyChanged(nameof(PickupPoints_Current_Status));
            }
        }

        private dynamic? _pickup_point_original_status;
        public dynamic? PickupPoints_Original_Status
        {
            get { return _pickup_point_original_status; }
            set
            {
                _pickup_point_original_status = value;
                OnPropertyChanged(nameof(PickupPoints_Original_Status));
            }
        }

        private async void PickupPoints_Status_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (PickupPoints_Current_Status is null || SelectedPickupPoint is null)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @"UPDATE pickup_points SET ";

                    sql += PickupPoints_Current_Status.Key switch
                    {
                        "works" => "is_works = True, is_receiving_orders = True",
                        "paused_receiving_orders" => "is_works = True, is_receiving_orders = False",
                        "stopped" => "is_works = False, is_receiving_orders = False",
                        _ => ""
                    };

                    if (sql.EndsWith("SET "))
                        return;


                    sql += " WHERE id = @id";

                    var data = new
                    {
                        id = SelectedPickupPoint.id
                    };

                    await conn.ExecuteAsync(sql, data);

                    SelectedPickupPoint.status = PickupPoints_Current_Status.Key;
                    PickupPoints_Original_Status = new
                    {
                        Key = PickupPoints_Current_Status.Key
                    };
                }
            }
            catch (PostgresException ex)
            {
                if (ex.MessageText.Contains("active orders"))
                {
                    MessageBox.Show("Невозможно отключить пункт выдачи, в который направлены активные заказы",
                        "Есть активные заказы", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }
        private void PickupPoints_Status_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (PickupPoints_Current_Status is null || PickupPoints_Original_Status is null)
                return;

            PickupPoints_Current_Status = PPStatuses.First(x => x.Key.Equals(PickupPoints_Original_Status.Key));
        }

        private async void PickupPoints_Summary_Confirm_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPickupPoint is null)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @"UPDATE pickup_points SET summary = @summary WHERE id = @id";

                    var data = new
                    {
                        id = SelectedPickupPoint.id,
                        summary = SelectedPickupPoint.changeable_summary
                    };

                    await conn.ExecuteAsync(sql, data);

                    SelectedPickupPoint.summary = SelectedPickupPoint.changeable_summary;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при изменении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }
        private void PickupPoints_Summary_Decline_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPickupPoint != null)
                SelectedPickupPoint.changeable_summary = SelectedPickupPoint.summary;
        }

        private dynamic _new_pickup_point = new ExpandoObject();
        public dynamic NewPickupPoint
        {
            get { return _new_pickup_point; }
            set
            {
                _new_pickup_point = value;
                OnPropertyChanged(nameof(NewPickupPoint));
            }
        }

        private bool _is_pickup_point_creation = false;
        public bool IsPickupPointCreation
        {
            get
            {
                return _is_pickup_point_creation;
            }
            set
            {
                _is_pickup_point_creation = value;
                OnPropertyChanged(nameof(IsPickupPointCreation));
            }
        }

        private void PickupPoints_Start_Create_New_Button_Click(object sender, RoutedEventArgs e)
        {
            NewPickupPoint = new ExpandoObject();
            IsPickupPointCreation = true;
        }
        private void PickupPoints_Cancel_Create_New_Button_Click(object sender, RoutedEventArgs e)
        {
            IsPickupPointCreation = false;
        }

        private async void PickupPoints_Create_New_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_new_pickup_point is null)
                return;
            if (_pickup_points is null)
                return;

            try
            {
                if (_new_pickup_point.address.ToString().Trim().Length is 0)
                {
                    MessageBox.Show("Отсутсвует адрес пункта выдачи",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Отсутсвует адрес пункта выдачи",
                        "Недостаток данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_pickup_points.Count(x => ((dynamic)x).address.Equals(_new_pickup_point.address)) is not 0)
            {
                MessageBox.Show("Пункт выдачи с данным адресом уже существует",
                        "Повторение данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @$"INSERT INTO pickup_points 
                                 (address) 
                                 VALUES (@address)";

                    var data = new
                    {
                        address = _new_pickup_point.address
                    };

                    await conn.ExecuteAsync(sql, data);
                }

                var task = Task.Run(() =>
                {
                    MessageBox.Show("Новый пунтк выдачи был успешно создан", "Действие выполнено", MessageBoxButton.OK, MessageBoxImage.Information);
                });

                IsPickupPointCreation = false;

                await UpdatePickupPoints();

                await task;
            }
            catch (PostgresException ex)
            {
                if (ex.MessageText.Contains("pickup_points_address_key"))
                {
                    MessageBox.Show($"Пункт выдачи с данным адресом уже существует",
                        "Повторение данных", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка при создании пункта выдачи:\n{ex.MessageText}",
                        "Ошибка при создании", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                

                return;
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при создании данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

        }

        private async void DeleteSelectedPickupPoints_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_selected_pickup_point is null)
                return;

            if (MessageBox.Show($"Вы точно уверены, что хотите удалить пункт выдачи по адресу: {_selected_pickup_point.address}?",
                                "Подтвердите действие", MessageBoxButton.OKCancel, MessageBoxImage.Warning) is not MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "DELETE FROM pickup_Points WHERE id = @id";

                    var data = new
                    {
                        id = _selected_pickup_point.id
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    PickupPoints.Remove(_selected_pickup_point);
                }
            }
            catch (Exception)
            {

                MessageBox.Show($"Невозможно удалить пункт выдачи, к которому привязаны заказы",
                    $"Ошибка удаления пункта выдачи", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region News Grid

        private async Task OnNewsSelected()
        {
            await UpdateNews();
        }

        private ObservableCollection<object> _news = new();
        public ObservableCollection<object> News
        {
            get { return _news; }
            set
            {
                _news.Clear();
                _news = value;
                OnPropertyChanged(nameof(News));
            }
        }

        private async Task UpdateNews(bool ShouldBeFiltered = false)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var cmd = this.CreateNewsSQL(ShouldBeFiltered);

                    var news = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                    News = new ObservableCollection<object>(news);
                }
            }
            catch (Exception)
            {
            }

            if (News.Any())
                this.News_DataGrid.SelectedItem = News.First();
        }

        private (String sql, object? data) CreateNewsSQL(bool ShouldBeFiltered = false)
        {
            var filter_string = String.Empty;

            if (ShouldBeFiltered && NewsFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)NewsFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    filter_string += kvp.Key switch
                    {
                        "title" => " AND lower(title) LIKE lower(@title)",
                        "datetime_from" => " AND ns.creation_time >= @datetime_from",
                        "datetime_to" => " AND ns.creation_time <= @datetime_to",
                        _ => ""
                    };
                }
            }

            var sql =
                        @$"
                        SELECT 
                            id,
                            title,
                            content,
                            creation_time,
                            notified
                        FROM news ns
                        WHERE {filter_string}
                        ORDER BY ns.id DESC";

            sql = sql.Replace("WHERE \r\n                        ", "");
            sql = sql.Replace("WHERE  AND", "WHERE");

            return (sql, _news_filter);
        }

        private async void NewsFilter_Button_Click(object sender, RoutedEventArgs e)
        {
            await UpdateNews(ShouldBeFiltered: true);
        }

        private dynamic _news_filter = new ExpandoObject();
        public dynamic NewsFilter
        {
            get { return _news_filter; }
            set
            {
                _news_filter = value;
                OnPropertyChanged(nameof(NewsFilter));
            }
        }

        private dynamic? _selected_news;
        public dynamic? SelectedNews
        {
            get { return _selected_news; }
            set
            {
                _selected_news = value;
                OnPropertyChanged(nameof(SelectedNews));
            }
        }

        private ObservableCollection<object> _selected_news_images = new();
        public ObservableCollection<object> SelectedNewsImages
        {
            get { return _selected_news_images; }
            set
            {
                _selected_news_images.Clear();
                _selected_news_images = value;
                OnPropertyChanged(nameof(SelectedNewsImages));
            }
        }

        private bool _should_update_news_images = true;
        private async void News_DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_should_update_news_images is false)
            {
                _should_update_news_images = true;
                return;
            }

            if (_selected_news is null)
                return;

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var sql = @"SELECT * FROM news_images WHERE news_id = @news_id";

                    var data = new
                    {
                        news_id = _selected_news.id
                    };

                    var news_images = await conn.QueryAsExpandoAsync(sql, data);
                    SelectedNewsImages = new ObservableCollection<object>(news_images);
                }
            }
            catch (Exception)
            {
            }
        }
        
        private void News_Create_New_Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new NewsCreationWindow();
            var is_created = window.ShowDialog() ?? false;

            var news_data = window.NewsData;
            var news_images = window.NewsImages;

            if (is_created is false)
                return;

            if (news_data is null || news_images is null)
                return;

            _should_update_news_images = false;

            News.Insert(0, news_data);
            SelectedNewsImages = new ObservableCollection<object>(news_images);
            News_DataGrid.SelectedItem = news_data;
        }

        private async void News_Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            var selected_news = News_DataGrid.SelectedItems.Cast<dynamic>().ToArray();

            if (selected_news is null)
                return;

            if (selected_news.Count() is 0)
                return;

            var count = selected_news.Count();

            var to_view = String.Join('\n', selected_news.Take(6).Select(x => $"{x.title}"));
            if (count > 6)
                to_view += "\n...";

            if (MessageBox.Show($"Вы точно уверены, что хотите удалить данн{(count is 1 ? "ую" : "ые")} новост{(count is 1 ? "ь" : "и")}? Вего удалить: {count}\n\n{to_view}",
                                "Подтвердите действие", MessageBoxButton.OKCancel, MessageBoxImage.Warning) is not MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    string sql = "DELETE FROM news WHERE id = ANY(@ids)";

                    var data = new
                    {
                        ids = selected_news.Select(x => (int)x.id).ToArray()
                    };

                    int rowsAffected = await conn.ExecuteAsync(sql, data);

                    foreach (var item in selected_news)
                    {
                        News.Remove(item);
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка при удалении данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            dynamic? image = (sender as FrameworkElement)?.DataContext;
            if (image is null)
                return;

            if (new ByteArrayToImageConverter().Convert(image.image, typeof(BitmapImage), null, null) is BitmapImage imageSource)
            {
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

                double aspectRatio = (double)imageSource.PixelHeight / imageSource.PixelWidth;
                window.Height = window.Width * aspectRatio + 19;

                window.Show();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            dynamic? image = (sender as FrameworkElement)?.DataContext;
            if (image is null)
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


        #endregion

        #region Statistics

        private ObservableCollection<object> _statistics = new();
        public ObservableCollection<object> Statistics
        {
            get { return _statistics; }
            set
            {
                _statistics.Clear();
                _statistics = value;
                OnPropertyChanged(nameof(Statistics));
            }
        }

        private void SaveStatistics(DataGrid dataGrid)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "Excel (*.xlsx)|*.xlsx";
            bool? result = saveFileDialog.ShowDialog();

            if (result is not true)
                return;

            string filePath = saveFileDialog.FileName;
            try
            {
                var dt = dataGrid.GetDataTable();
                if (dt is null)
                    return;

                XLWorkbook wb = new XLWorkbook();
                var worksheet = wb.AddWorksheet(dt, "Статистика");

                wb.SaveAs(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непредвиденная ошибка при сохранении данных: {ex.Message}",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private DateTime FirstDateTimeOfMonth
        {
            get
            {
                return new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }
        }
        private DateTime LastDateTimeOfMonth
        {
            get
            {
                return FirstDateTimeOfMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            }
        }

        #region Sales Volume

        private async Task OnSalesVolumeSelected()
        {
            SalesVolumeFilter = new ExpandoObject();

            SalesVolumeFilter.datetime_from = FirstDateTimeOfMonth;
            SalesVolumeFilter.datetime_to = LastDateTimeOfMonth;

            await UpdateSalesVolume(true);
        }

        private async Task UpdateSalesVolume(bool ShouldBeFiltered = false)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var cmd = this.CreateSalesVolumeSQL(ShouldBeFiltered);

                    var statistics = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                    Statistics = new ObservableCollection<object>(statistics);
                }
            }
            catch (Exception)
            {
            }
        }

        private (String sql, object? data) CreateSalesVolumeSQL(bool ShouldBeFiltered = false)
        {
            var group_filter_string = String.Empty;
            var filter_string = String.Empty;

            if (ShouldBeFiltered && SalesVolumeFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)SalesVolumeFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    group_filter_string += kvp.Key switch
                    {
                        "datetime_from" => " AND o.order_timestamp >= @datetime_from",
                        "datetime_to" => " AND o.order_timestamp <= @datetime_to",
                        _ => ""
                    };
                }

                foreach (var kvp in filter)
                {
                    filter_string += kvp.Key switch
                    {
                        "total_quantity_from" => " AND od.total_quantity >= @total_quantity_from",
                        "total_quantity_to" => " AND od.total_quantity <= @total_quantity_to",

                        "promotion_quantity_from" => " AND od.promotion_quantity >= @promotion_quantity_from",
                        "promotion_quantity_to" => " AND od.promotion_quantity <= @promotion_quantity_to",

                        "regular_quantity_from" => " AND od.regular_quantity >= @regular_quantity_from",
                        "regular_quantity_to" => " AND od.regular_quantity <= @regular_quantity_to",

                        "remaining_stock_from" => " AND p.quantity >= @remaining_stock_from",
                        "remaining_stock_to" => " AND p.quantity <= @remaining_stock_to",

                        _ => ""
                    };
                }
            }

            var sql =
                        @$"
                        WITH orders_data AS (
                        SELECT
                            p.id AS product_id,
                            p.name AS product_name,
                            SUM(oi.quantity) AS total_quantity,
                            SUM(CASE WHEN so.id IS NOT NULL THEN oi.quantity ELSE 0 END) AS promotion_quantity,
                            SUM(CASE WHEN so.id IS NULL THEN oi.quantity ELSE 0 END) AS regular_quantity
                        FROM
                            orders o
                            JOIN order_items oi ON o.id = oi.order_id
                            JOIN products p ON oi.product_id = p.id
                            LEFT JOIN special_offers so ON oi.product_id = so.product_id
                              AND o.order_timestamp BETWEEN so.start_datetime AND so.end_datetime
                        WHERE {group_filter_string}
                        GROUP BY
                            p.id, p.name
                        )
                        SELECT
                            od.product_id,
                            od.product_name,
                            od.total_quantity,
                            od.promotion_quantity,
                            od.regular_quantity,
                            p.quantity AS remaining_stock
                        FROM
                            orders_data od
                            JOIN products p ON od.product_id = p.id
                        WHERE {filter_string}
                        ORDER BY
                            od.total_quantity DESC;";

            sql = sql.Replace("WHERE \r\n                        ", "");
            sql = sql.Replace("WHERE  AND", "WHERE");

            return (sql, _sales_volume_filter);
        }

        private async void Sales_Volume_Filter_Button_Click(object sender, RoutedEventArgs e)
        {
            await UpdateSalesVolume(ShouldBeFiltered: true);
        }

        private void SalesVolume_SaveStatistics_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveStatistics(SalesVolume_DataGrid);
        }

        private dynamic _sales_volume_filter = new ExpandoObject();
        public dynamic SalesVolumeFilter
        {
            get { return _sales_volume_filter; }
            set
            {
                _sales_volume_filter = value;
                OnPropertyChanged(nameof(SalesVolumeFilter));
            }
        }

        #endregion

        #region Top Promotions 

        private async Task OnTopPromotionsSelected()
        {
            TopPromotionsFilter = new ExpandoObject();

            TopPromotionsFilter.datetime_from = FirstDateTimeOfMonth;
            TopPromotionsFilter.datetime_to = LastDateTimeOfMonth;

            await UpdateTopPromotions(true);
        }

        private async Task UpdateTopPromotions(bool ShouldBeFiltered = false)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var cmd = this.CreateTopPromotionsSQL(ShouldBeFiltered);

                    var statistics = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                    Statistics = new ObservableCollection<object>(statistics);
                }
            }
            catch (Exception)
            {
            }
        }

        private (String sql, object? data) CreateTopPromotionsSQL(bool ShouldBeFiltered = false)
        {
            var filter_string = String.Empty;

            if (ShouldBeFiltered && TopPromotionsFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)TopPromotionsFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    filter_string += kvp.Key switch
                    {
                        "total_quantity_from" => " AND COALESCE(SUM(oi.quantity), 0) >= @total_quantity_from",
                        "total_quantity_to" => " AND COALESCE(SUM(oi.quantity), 0) <= @total_quantity_to",
                        "discount_from" => " AND so.discount >= @discount_from",
                        "discount_to" => " AND so.discount <= @discount_to",
                        "product_id" => " AND p.id = @product_id",
                        "product_name" => " AND lower(p.name) LIKE lower(@product_name)",
                        _ => ""
                    };
                }
            }

            var sql =
                        @$"
                        SELECT
                            p.id AS product_id,
                            p.name AS product_name,
                            so.discount,
                            so.start_datetime,
                            so.end_datetime,
                            COALESCE(SUM(oi.quantity), 0) AS total_quantity
                        FROM
                            special_offers so
                            LEFT JOIN order_items oi ON oi.product_id = so.product_id
                                AND oi.order_id IN (
                                    SELECT id
                                    FROM orders
                                    WHERE order_timestamp BETWEEN so.start_datetime AND so.end_datetime
                                        AND order_timestamp BETWEEN COALESCE(@datetime_from, so.start_datetime) AND COALESCE(@datetime_to, so.end_datetime)
                                )
                            JOIN products p ON p.id = so.product_id
                        WHERE
                            ((so.start_datetime BETWEEN COALESCE(@datetime_from, so.start_datetime) AND COALESCE(@datetime_to, so.end_datetime))
                            OR (so.end_datetime BETWEEN COALESCE(@datetime_from, so.start_datetime) AND COALESCE(@datetime_to, so.end_datetime)))
                        GROUP BY
                            so.id, p.id
                        HAVING {filter_string}
                        ORDER BY
                            total_quantity DESC;";

            sql = sql.Replace("WHERE \r\n                        ", "");
            sql = sql.Replace("WHERE  AND", "WHERE");
            sql = sql.Replace("HAVING \r\n                        ", "");
            sql = sql.Replace("HAVING  AND", "HAVING");

            return (sql, _top_promotions_filter);
        }

        private async void TopPromotions_Filter_Button_Click(object sender, RoutedEventArgs e)
        {
            await UpdateTopPromotions(ShouldBeFiltered: true);
        }

        private void TopPromotions_SaveStatistics_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveStatistics(TopPromotions_DataGrid);
        }

        private dynamic _top_promotions_filter = new ExpandoObject();
        public dynamic TopPromotionsFilter
        {
            get { return _top_promotions_filter; }
            set
            {
                _top_promotions_filter = value;
                OnPropertyChanged(nameof(TopPromotionsFilter));
            }
        }

        private void TopPromotions_Find_Product_Filter_Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new SelectProductWindow();
            var res = window.ShowDialog();
            if (res == true)
            {
                var selected = window.SelectedProduct;
                TopPromotionsFilter.product_id = selected.id;
            }
        }

        #endregion

        #region PickupPoints Load

        private async Task OnPickupPointsLoadSelected()
        {
            PickupPointsLoadFilter = new ExpandoObject();

            PickupPointsLoadFilter.datetime_from = FirstDateTimeOfMonth;
            PickupPointsLoadFilter.datetime_to = LastDateTimeOfMonth;

            await UpdatePickupPointsLoad(true);
        }

        private async Task UpdatePickupPointsLoad(bool ShouldBeFiltered = false)
        {
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    var cmd = this.CreatePickupPointsLoadSQL(ShouldBeFiltered);

                    var statistics = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                    Statistics = new ObservableCollection<object>(statistics);
                }
            }
            catch (Exception)
            {
            }
        }

        private (String sql, object? data) CreatePickupPointsLoadSQL(bool ShouldBeFiltered = false)
        {
            var filter_string = String.Empty;
            var having_string = String.Empty;

            if (ShouldBeFiltered && PickupPointsLoadFilter is not null)
            {
                var expandoDict = (IDictionary<string, object>)PickupPointsLoadFilter;
                var filter = expandoDict.Where(kvp => kvp.Value is not null && kvp.Value.ToString() != String.Empty);

                foreach (var kvp in filter)
                {
                    filter_string += kvp.Key switch
                    {
                        "datetime_from" => " AND o.order_timestamp >= @datetime_from",
                        "datetime_to" => " AND o.order_timestamp <= @datetime_to",
                        "address" => " AND lower(pp.address) LIKE lower(@address)",
                        _ => ""
                    };
                }

                foreach (var kvp in filter)
                {
                    having_string += kvp.Key switch
                    {
                        "total_quantity_from" => " AND COUNT(*) >= @total_quantity_from",
                        "total_quantity_to" => " AND COUNT(*) <= @total_quantity_to",
                        _ => ""
                    };
                }
            }

            var sql =
                        @$"
                        SELECT pp.address, COUNT(*) AS order_count
                        FROM orders o
                        JOIN pickup_points pp ON pp.id = o.pickup_point_id
                        WHERE {filter_string}
                        GROUP BY pp.address
                        HAVING {having_string}
                        ORDER BY order_count DESC;";

            sql = sql.Replace("WHERE \r\n                        ", "");
            sql = sql.Replace("WHERE  AND", "WHERE");
            sql = sql.Replace("HAVING \r\n                        ", "");
            sql = sql.Replace("HAVING  AND", "HAVING");

            return (sql, _pickup_points_load_filter);
        }

        private async void PickupPointsLoad_Filter_Button_Click(object sender, RoutedEventArgs e)
        {
            await UpdatePickupPointsLoad(ShouldBeFiltered: true);
        }

        private void PickupPointsLoad_SaveStatistics_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveStatistics(PickupPointsLoad_DataGrid);
        }

        private dynamic _pickup_points_load_filter = new ExpandoObject();
        public dynamic PickupPointsLoadFilter
        {
            get { return _pickup_points_load_filter; }
            set
            {
                _pickup_points_load_filter = value;
                OnPropertyChanged(nameof(PickupPointsLoadFilter));
            }
        }

        #endregion

        #endregion
    }
}