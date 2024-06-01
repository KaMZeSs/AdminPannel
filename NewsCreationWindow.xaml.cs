using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

using AdminPannel.Converters;

using Dapper;

using Npgsql;

namespace AdminPannel
{
    /// <summary>
    /// Логика взаимодействия для NewsCreationWindow.xaml
    /// </summary>
    public partial class NewsCreationWindow : Window, INotifyPropertyChanged
    {
        public NewsCreationWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        String _title = String.Empty;
        public String NewsTitle
        {
            get { return _title; }
            set
            {
                _title = value;
                OnPropertyChanged(nameof(NewsTitle));
            }
        }

        String _content = String.Empty;
        public String NewsContent
        {
            get { return _content; }
            set
            {
                _content = value;
                OnPropertyChanged(nameof(NewsContent));
            }
        }


        #region Image Changers

        private ObservableCollection<byte[]> _images = new();
        public ObservableCollection<byte[]> Images
        {
            get => _images;
            set
            {
                _images = value;
                OnPropertyChanged(nameof(Images));
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not byte[] image)
                return;

            if (new ByteArrayToImageConverter().Convert(image, typeof(BitmapImage), new(), CultureInfo.CurrentCulture) is BitmapImage imageSource)
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
            if ((sender as FrameworkElement)?.DataContext is not byte[] image)
                return;

            var openFileDialog = new Microsoft.Win32.SaveFileDialog();
            openFileDialog.Filter = "PNG (*.png)|*.png|JPG (*.jpg)|*.jpg|BMP (*.bmp)|*.bmp";
            bool? result = openFileDialog.ShowDialog();

            if (result is not true)
                return;

            string filePath = openFileDialog.FileName;

            try
            {
                using (MemoryStream ms = new MemoryStream(image))
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

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not byte[] image)
                return;

            Images.Remove(image);
        }

        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
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

            Images.Add(imageBytes);
        }

        #endregion

        private dynamic news_data = new ExpandoObject();
        public dynamic NewsData
        {
            get { return news_data; }
        }

        private readonly List<dynamic> news_images = [];
        public IEnumerable<dynamic> NewsImages
        {
            get { return news_images; }
        }

        private async void Create_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_title.Trim().Length is 0)
            {
                MessageBox.Show("Отсутстсвует тема новости",
                   "Недостаточно данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (_content.Trim().Length is 0)
            {
                MessageBox.Show("Отсутстсвует тело новости",
                   "Недостаточно данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            NpgsqlTransaction? tx = null;
            try
            {
                await using (var conn = await NpgsqlConnectionManager.Instance.GetConnectionAsync())
                {
                    tx = await conn.BeginTransactionAsync();

                    var sql = @$"INSERT INTO news
                                 (title, content) 
                                 VALUES (@title, @content)
                                 RETURNING id, creation_time";

                    var data = new
                    {
                        title = _title,
                        content = _content
                    };

                    var vs = await conn.QueryFirstAsExpandoAsync(sql, data);

                    news_data.id = vs.id;
                    news_data.title = _title;
                    news_data.content = _content;
                    news_data.creation_time = vs.creation_time;
                    news_data.notified = false;

                    foreach (var image in _images)
                    {
                        var image_sql = @$"INSERT INTO news_images
                                           (news_id, image) 
                                           VALUES (@news_id, @image)
                                           RETURNING id";

                        var image_data = new
                        {
                            news_id = vs.id,
                            image
                        };

                        var image_id = await conn.QueryFirstAsync<int>(image_sql, image_data);

                        dynamic expando = new ExpandoObject();
                        expando.id = image_id;
                        expando.news_id = news_data.id;
                        expando.image = image;

                        news_images.Add(expando);
                    }

                    await tx.CommitAsync();
                }
            }
            catch (Exception)
            {
                if (tx is not null)
                    await tx.RollbackAsync();

                MessageBox.Show("Непредвиденная ошибка при создании данных. Повторите попытку позже",
                    "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }



    }
}
