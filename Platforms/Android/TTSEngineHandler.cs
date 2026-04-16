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
        const int MAX_QUEUE = 3;

        public static void Init(Context context)
        {
            if (tts != null)
                return;

            tts = new TextToSpeech(context, new TtsInitListener());
            tts.SetOnUtteranceProgressListener(new MyListener());
        }

        public static void Speak(string text, bool flushQueue = false)
        {
            // 1. Kiểm tra null ngay lập tức khi vào hàm
            if (tts == null) return;

            if (string.IsNullOrEmpty(text)) return;



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

                tts.SetLanguage(locale);

                if (queueCount >= MAX_QUEUE && !flushQueue)
                    return;

                if (flushQueue) queueCount = 0; // Nếu flush thì reset count

                tts.SetPitch(1.0f);
                tts.SetSpeechRate(0.8f);

                queueCount++;

                // Sử dụng ID duy nhất để Listener nhận diện được
                string utteranceId = Guid.NewGuid().ToString();
                tts.Speak(text, flushQueue ? QueueMode.Flush : QueueMode.Add, null, utteranceId);
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
                // Quan trọng: Phải chạy trên MainThread vì nó sẽ tác động đến giao diện
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnSpeechCompleted?.Invoke();
                });
            }

            public override void OnError(string utteranceId)
            {
                queueCount--;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnSpeechCompleted?.Invoke();
                });
            }

            public override void OnStart(string utteranceId) { }
        }
    }
}