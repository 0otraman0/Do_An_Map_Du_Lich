using System;
using System.Globalization;
using MauiAppMain.Resources.Localization;

namespace MauiAppMain.Services
{
    internal class LanguageService
    {
        const string ReferencesName = "App_language";

        public static void SetLanguage(string languageCode)
        {
            var culture = new CultureInfo(languageCode);

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            AppResource.Culture = culture;

            // SAVE language
            Preferences.Set(ReferencesName, languageCode);
            // reload UI
            Application.Current.MainPage = new AppShell();
        }

        public static void LoadSavedLanguage()
        {
            var languageCode = Preferences.Get(ReferencesName, "en");

            var culture = new CultureInfo(languageCode);

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            AppResource.Culture = culture;
        }
    }
}
