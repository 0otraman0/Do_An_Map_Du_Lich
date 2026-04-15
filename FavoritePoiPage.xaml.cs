using PointOfInterest = MauiAppMain.Models.PointOfInterest;
using System.Collections.ObjectModel;

namespace MauiAppMain
{
    public partial class FavoritePoiPage : ContentPage
    {
        public event Action<PointOfInterest>? PoiSelectedEvent;

        public FavoritePoiPage(ObservableCollection<PointOfInterest> data)
        {
            InitializeComponent();
            FavListView.ItemsSource = data;
        }

        private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PointOfInterest poi)
            {
                await Navigation.PopModalAsync();
                PoiSelectedEvent?.Invoke(poi);
            }
            if (sender is CollectionView cv) cv.SelectedItem = null;
        }
    }
}