using MauiAppMain.Resources.Localization;
using System.Globalization;
using MauiAppMain.Models;
using MauiAppMain.Services;

namespace MauiAppMain;

public partial class SettingPage : ContentPage
{
    DatabaseService database;

    public SettingPage()
    {
        InitializeComponent();
        database = new DatabaseService();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        bool playSound = Preferences.Get("SoundPlayWhenClickedPOI", false);
        soundCheckBox.IsChecked = playSound;

        var languages = await database.GetLanguagesAsync();

        // show current language
        var savedCode = Preferences.Get("App_language", "en");

        var current = languages.FirstOrDefault(l => l.Code == savedCode);

        if (current != null)
            SelectedLanguageLabel.Text = current.Language;

        LanguageList.ItemsSource = languages;
    }
    void OnLanguageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Language_option lang)
            return;

        // update button text
        SelectedLanguageLabel.Text = lang.Language;

        // hide list
        LanguageList.IsVisible = false;

        // change language
        LanguageService.SetLanguage(lang.Code);
    }


    //async void OnLanguageButtonTapped(object sender, EventArgs e)
    //{
    //    if (!LanguageList.IsVisible)
    //    {
    //        LanguageList.IsVisible = true;

    //        LanguageList.Opacity = 0;
    //        LanguageList.Scale = 0.95;

    //        await Task.WhenAll(
    //            LanguageList.FadeTo(1, 400, Easing.CubicOut),
    //            LanguageList.ScaleTo(1, 400, Easing.CubicOut)
    //        );
    //    }
    //    else
    //    {
    //        await Task.WhenAll(
    //            LanguageList.FadeTo(0, 450, Easing.CubicIn),
    //            LanguageList.ScaleTo(0.95, 650, Easing.CubicIn)
    //        );

    //        LanguageList.IsVisible = false;
    //    }
    //}
    async void OnLanguageButtonTapped(object sender, EventArgs e)
    {
        if (LanguageContainer.HeightRequest == 0)
        {
            int itemCount = ((IEnumerable<Language_option>)LanguageList.ItemsSource)?.Count() ?? 0;

            double targetHeight = itemCount * 40; // each item ~40px

            await LanguageContainer.HeightRequestTo(targetHeight, 300, Easing.CubicOut);
        }
        else
        {
            await LanguageContainer.HeightRequestTo(0, 250, Easing.CubicIn);
        }
    }
    public async void OnSoundCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Set("SoundPlayWhenClickedPOI", e.Value);
    }

}
public static class AnimationExtensions
{
    public static Task HeightRequestTo(this VisualElement view,
        double value, uint length, Easing easing)
    {
        var tcs = new TaskCompletionSource<bool>();

        var animation = new Animation(
            v => view.HeightRequest = v,
            view.HeightRequest,
            value);

        animation.Commit(view, "HeightAnim", 16, length, easing,
            (v, c) => tcs.SetResult(true));

        return tcs.Task;
    }
}