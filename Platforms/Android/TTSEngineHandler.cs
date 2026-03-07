using Android.Icu.Util;
using global::Android.Content;
using global::Android.Speech.Tts;
using Java.Util;

using TextToSpeech = global::Android.Speech.Tts.TextToSpeech;

namespace MauiAppMain
{
    public class Language
    {
        public string country { get; set; } = "US";
        public string language { get; set; } = "en";

    }
        


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

            foreach (var voice in voices)
            {
                System.Diagnostics.Debug.WriteLine(voice.Locale);
            }

            if (tts == null)
                return;

            if (queueCount >= MAX_QUEUE)
                return;

            tts.SetPitch(1.0f);
            tts.SetSpeechRate(0.8f);
            //tts.SetLanguage(Java.Util.Locale.VietNamese);

            queueCount++;

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