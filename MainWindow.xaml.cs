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

            refreshByGrid = new Dictionary<Grid, Func<Task>>()
            {
                { this.Products_Grid, this.OnProductSelected }
            };
            _new_category = String.Empty;
            ProductsFilter = new ExpandoObject();
        }

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
        Dictionary<Grid, Func<Task>> refreshByGrid;

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

        #region Products
        
        #region Categories

        public ObservableCollection<object> Categories { get; set; } = new();

        private async Task OnProductSelected()
        {
            await UpdateCategories();

            // if has filter attr
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

                var tempList = new List<dynamic>();

                tempList.Add(allCategory);

                tempList.AddRange(categories);

                foreach (var item in tempList)
                {
                    item.isEditing = false;
                }

                UpdateObservableCollection(Categories, tempList);

                CurrentCategory = Categories.First(x => (x as dynamic).id == current_id);
            }
        }

        private async void CategoriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCategory = (sender as ListView)?.SelectedItem;
            if (selectedCategory != null)
            {
                var id = (int)(selectedCategory as dynamic).id;
                await UpdateProducts(id);
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

        public ObservableCollection<object> Products { get; set; } = new();

        private (String sql, object? data) CreateProductSQL()
        {
            var selected_category_id = CurrentCategory?.id ?? -1;

            var sql =
                        @"SELECT
	                        p.id,
	                        p.name,
	                        COALESCE(so.new_price, p.price) current_price,
	                        p.price,
	                        so.new_price IS NOT NULL AS now_spec,
	                        p.quantity AS available_quantity,
                            p.quantity + COALESCE(oi.total_ordered, 0) AS total_quantity
                        FROM
	                        products p
                        LEFT JOIN special_offers so ON p.id = so.product_id AND LOCALTIMESTAMP BETWEEN so.start_datetime AND so.end_datetime
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

            if (selected_category_id is not -1)
            {
                sql += "\nWHERE p.category_id = @category_id";
            }

            var data = new
            {
                category_id = selected_category_id
            };

            return (sql, data);
        }

        private async Task UpdateProducts(int category_id)
        {
            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var cmd = this.CreateProductSQL();

                var products = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                UpdateObservableCollection(Products, products);
            }
        }

        private void DeleteSelectedProducts_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ViewSelectedProduct_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CreateNewProduct_Button_Click(object sender, RoutedEventArgs e)
        {

        }
        private void ProductsFilter_Button_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsFilter is null)
                return;

        }

        #endregion


        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateObservableCollection(ObservableCollection<object> collection, IEnumerable<object> items)
        {
            collection.Clear();
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var vs = onGridChange.First().Key;
            this.ChangeGrid(vs);

            // Получаем представление коллекции элементов
            ICollectionView view = CollectionViewSource.GetDefaultView(Products);
            // Устанавливаем сортировку по первому столбцу
            view.SortDescriptions.Add(new SortDescription("id", ListSortDirection.Ascending));
        }
    }
}