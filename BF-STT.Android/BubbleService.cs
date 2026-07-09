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
    [Service(
        Name = "vn.easygoing.bfstt.BubbleService",
        Exported = false,
        // The bubble is a long-lived overlay (specialUse) that only elevates to the
        // microphone type while actually recording. specialUse can be (re)started from
        // the background (boot / after being killed); a microphone-typed service cannot,
        // which is why the persistent type must NOT be microphone.
        ForegroundServiceType = ForegroundService.TypeSpecialUse | ForegroundService.TypeMicrophone)]
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

            // May be started from boot / an alarm restart with no Activity having run first,
            // so make sure the settings store is ready before we read it.
            AppSettings.Init(this);
            AppSettings.BubbleEnabled = true;

            CreateChannel();
            UpdateForeground(recording: false);

            _wm = GetSystemService(WindowService)!.JavaCast<IWindowManager>()!;
            _slop = ViewConfiguration.Get(this)!.ScaledTouchSlop;

            AddBubble();
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
            => StartCommandResult.Sticky;

        public override void OnTaskRemoved(Intent? rootIntent)
        {
            // Swiping the app card out of Recents (aggressive on MIUI/HyperOS) can kill the
            // whole process along with this service. If the user still wants the bubble,
            // schedule a near-immediate restart that survives process death via AlarmManager.
            if (AppSettings.BubbleEnabled)
            {
                try
                {
                    var restart = new Intent(this, typeof(BubbleService));
                    var pi = PendingIntent.GetForegroundService(
                        this, 1, restart,
                        PendingIntentFlags.OneShot | PendingIntentFlags.Immutable)!;
                    var am = GetSystemService(AlarmService)!.JavaCast<AlarmManager>()!;
                    am.SetAndAllowWhileIdle(
                        AlarmType.ElapsedRealtimeWakeup,
                        SystemClock.ElapsedRealtime() + 1000,
                        pi);
                }
                catch { /* ignore */ }
            }
            base.OnTaskRemoved(rootIntent);
        }

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
                // Add the microphone FGS type first so the OS doesn't mute background
                // capture, then start recording.
                UpdateForeground(recording: true);
                _recorder.Start();
                SetState(UiState.Recording);
                Vibrate(30);
            }
            catch (System.Exception ex)
            {
                UpdateForeground(recording: false);
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
                UpdateForeground(recording: false);
                ShowToast("Loi dung thu: " + ex.Message);
                SetState(UiState.Idle);
                return;
            }

            // Audio is captured; the mic is no longer needed during the HTTP upload, so
            // drop the microphone FGS type (and its privacy indicator) right away.
            UpdateForeground(recording: false);

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

        private Notification BuildNotification()
        {
            var tap = new Intent(this, typeof(MainActivity));
            tap.AddFlags(ActivityFlags.NewTask);
            var pi = PendingIntent.GetActivity(
                this, 0, tap,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            return new Notification.Builder(this, ChannelId)
                .SetContentTitle("BF-STT dang chay")
                .SetContentText("Cham icon de thu am - Giu de mo cai dat")
                .SetSmallIcon(Resource.Drawable.ic_mic)
                .SetContentIntent(pi)
                .SetOngoing(true)
                .Build();
        }

        /// <summary>
        /// (Re)promotes the service to the foreground with the right FGS type. When not
        /// recording it runs as <c>specialUse</c> (background-restartable); while recording
        /// it also carries the <c>microphone</c> type. specialUse only exists on API 34+, so
        /// older devices fall back to the microphone type (or no type before API 29).
        /// </summary>
        private void UpdateForeground(bool recording)
        {
            var notif = BuildNotification();
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake) // API 34+
                {
                    var type = ForegroundService.TypeSpecialUse;
                    if (recording) type |= ForegroundService.TypeMicrophone;
                    StartForeground(NotifId, notif, type);
                }
                else if (Build.VERSION.SdkInt >= BuildVersionCodes.Q) // API 29–33: no specialUse
                {
                    StartForeground(NotifId, notif, ForegroundService.TypeMicrophone);
                }
                else // API 26–28
                {
                    StartForeground(NotifId, notif);
                }
            }
            catch (System.Exception)
            {
                // Elevating to the microphone type can be refused if the system decides the
                // app isn't in a while-in-use state. Fall back to a plain foreground
                // notification so the bubble/service stays alive regardless.
                try { StartForeground(NotifId, notif); } catch { /* ignore */ }
            }
        }

        private sealed class BubbleGestureListener : GestureDetector.SimpleOnGestureListener
        {
            private readonly BubbleService _svc;
            public BubbleGestureListener(BubbleService svc) => _svc = svc;

            public override void OnLongPress(MotionEvent? e) => _svc.OnLongPress();
        }
    }
}
