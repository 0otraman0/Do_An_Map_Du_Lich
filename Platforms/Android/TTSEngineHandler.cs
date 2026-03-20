using Android.Icu.Util;
using global::Android.Content;
using global::Android.Speech.Tts;
using Java.Util;
using Locale = Java.Util.Locale;
using TextToSpeech = global::Android.Speech.Tts.TextToSpeech;

namespace MauiAppMain
{

    public static class AndroidTtsService
    {
        static TextToSpeech? tts;
        static int queueCount = 0;
        const int MAX_QUEUE = 3;

        public static void Init(Context context)
        {
            if (tts != null)
                return;

            tts = new TextToSpeech(context, new TtsInitListener());
            tts.SetOnUtteranceProgressListener(new MyListener());
        }

        public static void Speak(string text)
        {
            var voices = tts.Voices;
            var lang = Preferences.Get("App_language", "vi");
            Locale locale;

            switch (lang)
            {
                case "vi":
                    locale = new Locale("vi", "VN");
                    break;

                case "ja":
                    locale = new Locale("ja", "JP");
                    break;

                case "en":
                default:
                    locale = Locale.Us;
                    break;
            }
            var result = tts.SetLanguage(locale);

            

            if (tts == null)
                return;

            if (queueCount >= MAX_QUEUE)
                return;

            tts.SetPitch(1.0f);
            tts.SetSpeechRate(0.8f);
            //tts.SetLanguage(Java.Util.Locale.VietNamese);

            queueCount++;
            Console.WriteLine(locale);
            tts.Speak(text, QueueMode.Add, null, "poiSpeech");
        }

        public static void Stop()
        {
            tts?.Stop();
            queueCount = 0;
        }

        class TtsInitListener : Java.Lang.Object, TextToSpeech.IOnInitListener
        {
            public void OnInit(OperationResult status)
            {
            }
        }

        class MyListener : UtteranceProgressListener
        {
            public override void OnDone(string utteranceId)
            {
                queueCount--;
            }

            public override void OnError(string utteranceId)
            {
                queueCount--;
            }

            public override void OnStart(string utteranceId) { }
        }
    }
}