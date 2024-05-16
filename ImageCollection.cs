using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AdminPannel
{
    public class ImageCollection : ObservableCollection<byte[]>
    {
        private ObservableCollection<BitmapImage> _images;

        public ObservableCollection<BitmapImage> Images
        {
            get
            {
                if (_images == null)
                {
                    _images = new ObservableCollection<BitmapImage>(this.Select(ByteArrayToBitmapImage));
                }
                return _images;
            }
        }

        private BitmapImage ByteArrayToBitmapImage(byte[] imageData)
        {
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

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            if (e.NewItems != null)
            {
                foreach (byte[] imageData in e.NewItems.Cast<byte[]>())
                {
                    _images.Add(ByteArrayToBitmapImage(imageData));
                }
            }

            if (e.OldItems != null)
            {
                foreach (byte[] imageData in e.OldItems.Cast<byte[]>())
                {
                    _images.Remove(_images.First(i => i.StreamSource.Equals(new MemoryStream(imageData))));
                }
            }
        }
    }

}
