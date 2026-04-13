using MauiAppMain.Models; // Đảm bảo có dòng này để nhận diện PointOfInterest
using MauiAppMain.Services;
using System.Threading; // Cần thêm thư viện này

namespace MauiAppMain;

public partial class SearchPage : ContentPage
{
    private readonly DatabaseService _database;

    // 1. KHAI BÁO BIẾN NÀY ĐỂ HẾT LỖI 'OnPoiSelected'
    public Action<PointOfInterest> OnPoiSelected { get; set; }

    // Biến dùng để hủy tác vụ tìm kiếm cũ khi có từ khóa mới
    private CancellationTokenSource _cts;

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

        // 1. Hủy bỏ tác vụ tìm kiếm đang chạy trước đó (nếu có)
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            resultList.ItemsSource = null;
            return;
        }

        try
        {
            // 2. Chờ 300ms trước khi thực hiện tìm kiếm (Debounce)
            // Nếu người dùng gõ tiếp, dòng này sẽ bị hủy ở bước 1
            await Task.Delay(300, token);

            // 3. Gọi hàm search trong Database với Token bảo vệ
            var results = await _database.SearchPoiAsync(keyword);

            // Kiểm tra lại lần nữa nếu token chưa bị hủy thì mới cập nhật UI
            if (!token.IsCancellationRequested)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    resultList.ItemsSource = results;
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Bỏ qua lỗi khi tác vụ bị hủy, đây là hoạt động bình thường
        }
        catch (Exception ex)
        {
            // Ghi log nếu có lỗi khác phát sinh để dễ debug
            System.Diagnostics.Debug.WriteLine($"Search Error: {ex.Message}");
        }
    }

    // 2. HÀM XỬ LÝ KHI CHỌN ITEM (Hết lỗi PointOfInterest)
    private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PointOfInterest selectedPoi)
        {
            OnPoiSelected?.Invoke(selectedPoi);
            await Navigation.PopAsync();
        }

        if (sender is CollectionView cv) cv.SelectedItem = null;
    }
}