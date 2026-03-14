using MauiAppMain.Services;

namespace MauiAppMain;

public partial class SearchPage : ContentPage
{
	public SearchPage()
	{
		InitializeComponent();
	}
    DatabaseService _database = new DatabaseService();
    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string keyword = e.NewTextValue;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            resultList.ItemsSource = null;
            return;
        }

        var results = await _database.SearchPoiAsync(keyword);

        resultList.ItemsSource = results;
    }
}