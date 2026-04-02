using MauiAppMain.Models; // Đảm bảo có dòng này để nhận diện PointOfInterest
using MauiAppMain.Services;

namespace MauiAppMain;

public partial class SearchPage : ContentPage
{
    private readonly DatabaseService _database;

    // 1. KHAI BÁO BIẾN NÀY ĐỂ HẾT LỖI 'OnPoiSelected'
    public Action<PointOfInterest> OnPoiSelected { get; set; }

    // Sửa Constructor để nhận database từ MainPage
    public SearchPage(DatabaseService database)
    {
        InitializeComponent();
        _database = database;

        // Tự động hiện bàn phím khi vào trang
        searchEntry.Focus();
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string keyword = e.NewTextValue;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            resultList.ItemsSource = null;
            return;
        }

        // Gọi hàm search trong Database
        var results = await _database.SearchPoiAsync(keyword);
        resultList.ItemsSource = results;
    }

    // 2. HÀM XỬ LÝ KHI CHỌN ITEM (Hết lỗi PointOfInterest)
    private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PointOfInterest selectedPoi)
        {
            // Gọi Action để báo cho MainPage biết điểm được chọn
            OnPoiSelected?.Invoke(selectedPoi);

            // Thoát trang tìm kiếm quay về bản đồ
            await Navigation.PopAsync();
        }

        // Reset lại để lần sau nhấn vẫn ăn
        if (sender is CollectionView cv) cv.SelectedItem = null;
    }
}