using Android.Icu.Util;
using global::Android.Content;
using global::Android.Speech.Tts;
using Java.Util;
using static System.Net.Mime.MediaTypeNames;
using Locale = Java.Util.Locale;
using TextToSpeech = global::Android.Speech.Tts.TextToSpeech;

namespace MauiAppMain
{
    public static class AndroidTtsService
    {
        public static Action? OnSpeechCompleted;
        static TextToSpeech? tts;
        static int queueCount = 0;
        public static bool IsSpeaking => queueCount > 0;
        const int MAX_QUEUE = 3;

        public static void Init(Context context)
        {
            if (tts != null)
                return;

            tts = new TextToSpeech(context, new TtsInitListener());
            tts.SetOnUtteranceProgressListener(new MyListener());
        }

        public static void Speak(string text, bool flushQueue = true)
        {
            // 1. Kiểm tra null ngay lập tức khi vào hàm
            if (tts == null || string.IsNullOrWhiteSpace(text)) return;

            // 2. Chặn lỗi khi tts chưa sẵn sàng lấy Voices
            try
            {
                // Thiết lập ngôn ngữ
                var lang = Preferences.Get("App_language", "vi");
                Locale locale = lang switch
                {
                    "vi" => new Locale("vi", "VN"),
                    "ja" => new Locale("ja", "JP"),
                    _ => Locale.Us
                };

                var result = tts.SetLanguage(locale);
                if (result == LanguageAvailableResult.MissingData || result == LanguageAvailableResult.NotSupported)
                {
                    locale = new Locale(lang);
                    tts.SetLanguage(locale);
                }

                if (flushQueue) queueCount = 0; // Nếu flush thì reset count

                tts.SetPitch(1.0f);
                tts.SetSpeechRate(0.8f);

                global::Android.OS.Bundle bundle = new global::Android.OS.Bundle();
                bundle.PutInt(TextToSpeech.Engine.KeyParamStream, 3); // AudioManager.STREAM_MUSIC = 3
                bundle.PutFloat(TextToSpeech.Engine.KeyParamVolume, 1.0f);

                int maxLen = TextToSpeech.MaxSpeechInputLength;
                if (maxLen <= 0) maxLen = 3999;

                var chunks = new System.Collections.Generic.List<string>();
                int i = 0;
                while (i < text.Length)
                {
                    int len = Math.Min(maxLen, text.Length - i);
                    if (len < maxLen)
                    {
                        chunks.Add(text.Substring(i, len));
                        break;
                    }
                    int lastSpace = text.LastIndexOfAny(new[] { ' ', '.', ',', '\n' }, i + len - 1, len);
                    if (lastSpace > i) len = lastSpace - i + 1;
                    chunks.Add(text.Substring(i, len));
                    i += len;
                }

                for (int j = 0; j < chunks.Count; j++)
                {
                    queueCount++;
                    string utteranceId = Guid.NewGuid().ToString();
                    tts.Speak(chunks[j], (flushQueue && j == 0) ? QueueMode.Flush : QueueMode.Add, bundle, utteranceId);
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("TTS_ERROR", ex.Message);
            }
        }

        public static void Stop()
        {
            if (tts != null)
            {
                tts.Stop();        // Dừng ngay lập tức
                //tts.Shutdown();    // Giải phóng tài nguyên và xóa queue
                //tts = null;        // Reset đối tượng
            }
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
                if (queueCount <= 0)
                {
                    queueCount = 0;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OnSpeechCompleted?.Invoke();
                    });
                }
            }

            public override void OnError(string utteranceId)
            {
                queueCount--;
                if (queueCount <= 0)
                {
                    queueCount = 0;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OnSpeechCompleted?.Invoke();
                    });
                }
            }

            public override void OnStart(string utteranceId) { }
        }
    }
}