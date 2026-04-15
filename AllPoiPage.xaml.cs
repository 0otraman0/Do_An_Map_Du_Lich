using PointOfInterest = MauiAppMain.Models.PointOfInterest;
using System.Collections.ObjectModel;

namespace MauiAppMain
{
    public partial class AllPoiPage : ContentPage
    {
        // Đổi tên thành PoiSelectedEvent để không trùng với hàm OnPoiSelected bên dưới
        public event Action<PointOfInterest>? PoiSelectedEvent;

        public AllPoiPage(ObservableCollection<PointOfInterest> data)
        {
            InitializeComponent();
            AllPoiListView.ItemsSource = data;
        }

        // Đây là hàm xử lý sự kiện Click trên UI
        private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PointOfInterest poi)
            {
                await Navigation.PopModalAsync();
                PoiSelectedEvent?.Invoke(poi); // Gọi event bằng tên mới
            }
            if (sender is CollectionView cv) cv.SelectedItem = null;
        }
    }
}