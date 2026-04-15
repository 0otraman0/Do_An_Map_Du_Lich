using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MauiAppMain.Models
{
    public class PointOfInterest : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        public double RadiusMeters { get; set; } = 0;
        public bool IsTriggered { get; set; } = false;
        public string ImageUrlsJson { get; set; } = string.Empty;
        public int priorityLevel { get; set; } = 1;
        public string Address { get; set; } = string.Empty;

        // Trường IsFavorite được viết lại để phát tín hiệu cho UI
        private bool _isFavorite = false;
        private List<string> _imageList;

        [Ignore]
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FavoriteIcon)); // Thông báo icon thay đổi
                    OnPropertyChanged(nameof(FavoriteColor)); // Thông báo màu thay đổi
                }
            }
        }

        public List<string> ImageList
        {
            get
            {
                if (_imageList == null)
                {
                    _imageList = string.IsNullOrEmpty(ImageUrlsJson)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(ImageUrlsJson) ?? new List<string>();
                }
                return _imageList;
            }
        }
        // Phần xử lý sự kiện thông báo thay đổi
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string FavoriteIcon => IsFavorite ? "❤️" : "🤍";
        public Color FavoriteColor => IsFavorite ? Colors.DeepSkyBlue : Colors.Gray;
    }
}