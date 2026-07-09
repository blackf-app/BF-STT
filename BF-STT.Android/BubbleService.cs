using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;

using BFSTT.Droid.Audio;
using BFSTT.Droid.Stt;

namespace BFSTT.Droid
{
    /// <summary>
    /// Foreground service that shows the floating microphone bubble over every app.
    ///   - single tap  -> start recording / stop + transcribe + paste
    ///   - long press  -> open settings (MainActivity)
    ///   - drag        -> move the bubble around
    /// </summary>
    [Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMicrophone)]
    public class BubbleService : Service
    {
        private const int NotifId = 1001;
        private const string ChannelId = "bfstt_bubble";

        private static readonly Color IdleColor = Color.ParseColor("#2D6CDF");
        private static readonly Color RecordingColor = Color.ParseColor("#E53935");
        private static readonly Color ProcessingColor = Color.ParseColor("#FB8C00");

        private IWindowManager _wm = null!;
        private ImageView _bubble = null!;
        private WindowManagerLayoutParams _params = null!;
        private GestureDetector _gesture = null!;
        private Handler _main = null!;

        private readonly AndroidAudioRecorder _recorder = new();

        private enum UiState { Idle, Recording, Processing }
        private UiState _state = UiState.Idle;

        private float _downRawX, _downRawY;
        private int _startX, _startY;
        private bool _dragging;
        private bool _longPressed;
        private int _slop;

        /// <summary>Last delivered transcription, so MainActivity can show it.</summary>
        public static string LastResult { get; private set; } = "";

        public override IBinder? OnBind(Intent? intent) => null;

        public override void OnCreate()
        {
            base.OnCreate();
            _main = new Handler(Looper.MainLooper!);

            CreateChannel();
            StartAsForeground();

            _wm = GetSystemService(WindowService)!.JavaCast<IWindowManager>()!;
            _slop = ViewConfiguration.Get(this)!.ScaledTouchSlop;

            AddBubble();
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
            => StartCommandResult.Sticky;

        public override void OnDestroy()
        {
            try
            {
                if (_bubble != null) _wm?.RemoveView(_bubble);
            }
            catch { /* ignore */ }
            base.OnDestroy();
        }

        // ---------- overlay setup ----------

        private void AddBubble()
        {
            _bubble = new ImageView(this);
            _bubble.SetImageResource(Resource.Drawable.ic_mic);
            _bubble.SetColorFilter(Color.White);
            int pad = Dp(12);
            _bubble.SetPadding(pad, pad, pad, pad);
            _bubble.Background = MakeCircle(IdleColor);

            var type = Build.VERSION.SdkInt >= BuildVersionCodes.O
                ? WindowManagerTypes.ApplicationOverlay
                : WindowManagerTypes.Phone;

            _params = new WindowManagerLayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent,
                type,
                WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchModal,
                Format.Translucent)
            {
                Gravity = GravityFlags.Top | GravityFlags.Left,
                X = Dp(16),
                Y = Dp(220)
            };

            int size = Dp(58);
            _params.Width = size;
            _params.Height = size;

            _gesture = new GestureDetector(this, new BubbleGestureListener(this));
            _bubble.Touch += OnBubbleTouch;

            _wm.AddView(_bubble, _params);
        }

        private void OnBubbleTouch(object? sender, View.TouchEventArgs e)
        {
            var ev = e.Event;
            if (ev == null) { e.Handled = false; return; }

            _gesture.OnTouchEvent(ev);

            switch (ev.ActionMasked)
            {
                case MotionEventActions.Down:
                    _downRawX = ev.RawX;
                    _downRawY = ev.RawY;
                    _startX = _params.X;
                    _startY = _params.Y;
                    _dragging = false;
                    _longPressed = false;
                    e.Handled = true;
                    return;

                case MotionEventActions.Move:
                    float dx = ev.RawX - _downRawX;
                    float dy = ev.RawY - _downRawY;
                    if (!_dragging && (System.Math.Abs(dx) > _slop || System.Math.Abs(dy) > _slop))
                        _dragging = true;
                    if (_dragging)
                    {
                        _params.X = _startX + (int)dx;
                        _params.Y = _startY + (int)dy;
                        _wm.UpdateViewLayout(_bubble, _params);
                    }
                    e.Handled = true;
                    return;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (!_dragging && !_longPressed)
                        ToggleRecording();
                    e.Handled = true;
                    return;
            }

            e.Handled = false;
        }

        // Called by the gesture listener.
        internal void OnLongPress()
        {
            _longPressed = true;
            Vibrate(40);
            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
        }

        // ---------- recording / transcription ----------

        private void ToggleRecording()
        {
            switch (_state)
            {
                case UiState.Idle:
                    StartRec();
                    break;
                case UiState.Recording:
                    StopRecAndTranscribe();
                    break;
                case UiState.Processing:
                    // busy – ignore taps
                    break;
            }
        }

        private void StartRec()
        {
            try
            {
                _recorder.Start();
                SetState(UiState.Recording);
                Vibrate(30);
            }
            catch (System.Exception ex)
            {
                ShowToast("Khong the thu am: " + ex.Message);
                SetState(UiState.Idle);
            }
        }

        private void StopRecAndTranscribe()
        {
            byte[] wav;
            try
            {
                wav = _recorder.Stop();
            }
            catch (System.Exception ex)
            {
                ShowToast("Loi dung thu: " + ex.Message);
                SetState(UiState.Idle);
                return;
            }

            SetState(UiState.Processing);
            Vibrate(20);

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    string provider = AppSettings.Provider;
                    string key = AppSettings.CurrentApiKey;

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        Post(() =>
                        {
                            ShowToast("Chua co API key. Giu icon de mo cai dat.");
                            SetState(UiState.Idle);
                        });
                        return;
                    }

                    string text = await TranscriptionEngine.TranscribeAsync(wav, provider, key, AppSettings.Language);

                    Post(() =>
                    {
                        if (string.IsNullOrWhiteSpace(text))
                            ShowToast("Khong nhan duoc van ban.");
                        else
                            DeliverText(text);
                        SetState(UiState.Idle);
                    });
                }
                catch (System.Exception ex)
                {
                    Post(() =>
                    {
                        ShowToast("STT loi: " + ex.Message);
                        SetState(UiState.Idle);
                    });
                }
            });
        }

        private void DeliverText(string text)
        {
            LastResult = text;

            // 1) Always put it on the clipboard.
            var cm = GetSystemService(ClipboardService)!.JavaCast<ClipboardManager>()!;
            cm.PrimaryClip = ClipData.NewPlainText("bfstt", text);

            // 2) If the accessibility service is on, paste into the focused field.
            bool pasted = false;
            if (AppSettings.AutoPaste && PasteAccessibilityService.IsReady)
            {
                pasted = PasteAccessibilityService.Instance!.TryPaste(text);
            }

            string msg;
            if (pasted)
                msg = "Da dan: " + Ellipsis(text);
            else if (AppSettings.AutoPaste && !PasteAccessibilityService.IsReady)
                msg = "Da copy (bat Tro nang de tu dan): " + Ellipsis(text);
            else
                msg = "Da copy: " + Ellipsis(text);

            ShowToast(msg);
        }

        // ---------- ui helpers ----------

        private void SetState(UiState s)
        {
            _state = s;
            Post(() =>
            {
                Color c = s switch
                {
                    UiState.Recording => RecordingColor,
                    UiState.Processing => ProcessingColor,
                    _ => IdleColor
                };
                if (_bubble != null) _bubble.Background = MakeCircle(c);
            });
        }

        private GradientDrawable MakeCircle(Color c)
        {
            var d = new GradientDrawable();
            d.SetShape(ShapeType.Oval);
            d.SetColor(c);
            return d;
        }

        private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);

        private void Post(System.Action action) => _main.Post(action);

        private void ShowToast(string msg)
            => Post(() => Toast.MakeText(this, msg, ToastLength.Short)!.Show());

        private void Vibrate(long ms)
        {
            try
            {
                var v = GetSystemService(VibratorService)!.JavaCast<Vibrator>()!;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    v.Vibrate(VibrationEffect.CreateOneShot(ms, VibrationEffect.DefaultAmplitude));
                else
#pragma warning disable CS0618
                    v.Vibrate(ms);
#pragma warning restore CS0618
            }
            catch { /* ignore */ }
        }

        private static string Ellipsis(string s)
            => s.Length <= 40 ? s : s.Substring(0, 40) + "...";

        // ---------- foreground service plumbing ----------

        private void CreateChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var ch = new NotificationChannel(ChannelId, "BF-STT", NotificationImportance.Low)
                {
                    Description = "Floating speech-to-text bubble"
                };
                var nm = GetSystemService(NotificationService)!.JavaCast<NotificationManager>()!;
                nm.CreateNotificationChannel(ch);
            }
        }

        private void StartAsForeground()
        {
            var notif = new Notification.Builder(this, ChannelId)
                .SetContentTitle("BF-STT dang chay")
                .SetContentText("Cham icon de thu am - Giu de mo cai dat")
                .SetSmallIcon(Resource.Drawable.ic_mic)
                .SetOngoing(true)
                .Build();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                StartForeground(NotifId, notif, ForegroundService.TypeMicrophone);
            else
                StartForeground(NotifId, notif);
        }

        private sealed class BubbleGestureListener : GestureDetector.SimpleOnGestureListener
        {
            private readonly BubbleService _svc;
            public BubbleGestureListener(BubbleService svc) => _svc = svc;

            public override void OnLongPress(MotionEvent? e) => _svc.OnLongPress();
        }
    }
}
