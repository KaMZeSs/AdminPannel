using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.Windows;
using System.Windows.Controls;

using MoreLinq;

namespace AdminPannel
{
    /// <summary>
    /// Логика взаимодействия для SelectProductWindow.xaml
    /// </summary>
    public partial class SelectProductWindow : Window, INotifyPropertyChanged
    {
        public SelectProductWindow()
        {
            InitializeComponent();
            DataContext = this;
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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateCategories();
        }

        #endregion

        #region Menu_Grid

        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            await UpdateCategories();
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

            var sql = @"SELECT
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
            await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
            {
                var cmd = this.CreateProductSQL(ProductsShouldBeFiltered);

                var products = await conn.QueryAsExpandoAsync(cmd.sql, cmd.data);

                Products = new ObservableCollection<object>(products);

                is_products_filtered = ProductsShouldBeFiltered;
            }
        }

        private void ViewSelectedProduct_Button_Click(object sender, RoutedEventArgs e)
        {
            dynamic selected_product = Products_DataGrid.SelectedItem;
            if (selected_product is null)
                return;

            this.DialogResult = true;
        }

        public dynamic SelectedProduct
        {
            get { return Products_DataGrid.SelectedItem; }
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
