using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using BFSTT.Droid.Audio;
using BFSTT.Droid.Stt;

namespace BFSTT.Droid
{
    /// <summary>
    /// Single-screen config + test harness. Also acts as the "settings" screen
    /// opened when the floating bubble is long-pressed.
    /// </summary>
    [Activity(
        Label = "BF-STT",
        MainLauncher = true,
        Theme = "@android:style/Theme.DeviceDefault.Light.DarkActionBar",
        ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : Activity
    {
        private const int ReqPermissions = 100;

        private Spinner _providerSpinner = null!;
        private EditText _apiKeyInput = null!;
        private EditText _languageInput = null!;
        private CheckBox _autoPasteCheck = null!;
        private EditText _resultBox = null!;
        private Button _recordButton = null!;
        private TextView _statusText = null!;

        private readonly AndroidAudioRecorder _recorder = new();
        private bool _recording;
        private bool _busy;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AppSettings.Init(this);

            BuildUi();
            LoadSettingsIntoUi();
            RequestRuntimePermissions();
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (!string.IsNullOrEmpty(BubbleService.LastResult))
            {
                _resultBox.Text = BubbleService.LastResult;
            }
        }

        // ---------- UI ----------

        private void BuildUi()
        {
            var scroll = new ScrollView(this);
            var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
            int pad = Dp(20);
            root.SetPadding(pad, pad, pad, pad);

            root.AddView(Header("BF-STT — Speech To Text"));
            root.AddView(Label("Chon nha cung cap, nhap API key roi bat icon noi. " +
                               "Cham icon de thu am, cham lan nua de dung va dan. Giu icon de mo cai dat."));

            root.AddView(Label("Nha cung cap (Provider)"));
            _providerSpinner = new Spinner(this);
            var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, AppSettings.Providers);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _providerSpinner.Adapter = adapter;
            _providerSpinner.ItemSelected += (s, e) =>
            {
                string provider = AppSettings.Providers[e.Position];
                _apiKeyInput.Text = AppSettings.ApiKeyFor(provider);
            };
            root.AddView(_providerSpinner);

            root.AddView(Label("API Key"));
            _apiKeyInput = new EditText(this) { Hint = "Dan API key vao day" };
            _apiKeyInput.SetSingleLine(true);
            root.AddView(_apiKeyInput);

            root.AddView(Label("Ngon ngu (vi, en, ...)"));
            _languageInput = new EditText(this) { Text = "vi" };
            _languageInput.SetSingleLine(true);
            root.AddView(_languageInput);

            _autoPasteCheck = new CheckBox(this) { Text = "Tu dong dan vao o nhap lieu (can bat Tro nang)" };
            root.AddView(_autoPasteCheck);

            var saveBtn = new Button(this) { Text = "Luu cai dat" };
            saveBtn.Click += (s, e) => SaveSettings();
            root.AddView(saveBtn);

            var bubbleBtn = new Button(this) { Text = "Bat icon noi" };
            bubbleBtn.Click += (s, e) => EnableBubble();
            root.AddView(bubbleBtn);

            var a11yBtn = new Button(this) { Text = "Mo cai dat Tro nang (de dan tu dong)" };
            a11yBtn.Click += (s, e) => StartActivity(new Intent(Settings.ActionAccessibilitySettings));
            root.AddView(a11yBtn);

            root.AddView(Divider());
            root.AddView(Label("Thu nghiem ngay trong app:"));

            _recordButton = new Button(this) { Text = "● Thu am" };
            _recordButton.Click += (s, e) => ToggleInAppRecording();
            root.AddView(_recordButton);

            _statusText = new TextView(this) { Text = "" };
            root.AddView(_statusText);

            root.AddView(Label("Ket qua:"));
            _resultBox = new EditText(this)
            {
                Hint = "Van ban nhan dang se hien o day"
            };
            _resultBox.SetMinLines(3);
            _resultBox.Gravity = GravityFlags.Top | GravityFlags.Start;
            root.AddView(_resultBox);

            scroll.AddView(root);
            SetContentView(scroll);
        }

        private void LoadSettingsIntoUi()
        {
            int idx = System.Array.IndexOf(AppSettings.Providers, AppSettings.Provider);
            if (idx < 0) idx = 0;
            _providerSpinner.SetSelection(idx);
            _apiKeyInput.Text = AppSettings.ApiKeyFor(AppSettings.Providers[idx]);
            _languageInput.Text = AppSettings.Language;
            _autoPasteCheck.Checked = AppSettings.AutoPaste;
        }

        private void SaveSettings()
        {
            string provider = AppSettings.Providers[_providerSpinner.SelectedItemPosition];
            AppSettings.Provider = provider;
            AppSettings.SetApiKey(provider, _apiKeyInput.Text ?? "");
            AppSettings.Language = string.IsNullOrWhiteSpace(_languageInput.Text) ? "vi" : _languageInput.Text!.Trim();
            AppSettings.AutoPaste = _autoPasteCheck.Checked;
            Toast.MakeText(this, "Da luu cai dat.", ToastLength.Short)!.Show();
        }

        // ---------- floating bubble ----------

        private void EnableBubble()
        {
            SaveSettings();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M && !Settings.CanDrawOverlays(this))
            {
                Toast.MakeText(this, "Hay cap quyen hien thi tren cac ung dung khac.", ToastLength.Long)!.Show();
                var intent = new Intent(
                    Settings.ActionManageOverlayPermission,
                    Android.Net.Uri.Parse("package:" + PackageName));
                StartActivity(intent);
                return;
            }

            var svc = new Intent(this, typeof(BubbleService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                StartForegroundService(svc);
            else
                StartService(svc);

            Toast.MakeText(this, "Icon noi da bat. Ban co the thu nho app.", ToastLength.Short)!.Show();
        }

        // ---------- in-app test recording ----------

        private void ToggleInAppRecording()
        {
            if (_busy) return;

            if (!_recording)
            {
                try
                {
                    _recorder.Start();
                    _recording = true;
                    _recordButton.Text = "■ Dung & nhan dang";
                    _statusText.Text = "Dang thu am...";
                }
                catch (System.Exception ex)
                {
                    _statusText.Text = "Loi micro: " + ex.Message;
                }
                return;
            }

            // stop + transcribe
            byte[] wav;
            try { wav = _recorder.Stop(); }
            catch (System.Exception ex) { _statusText.Text = "Loi dung thu: " + ex.Message; _recording = false; _recordButton.Text = "● Thu am"; return; }

            _recording = false;
            _busy = true;
            _recordButton.Text = "● Thu am";
            _statusText.Text = "Dang nhan dang...";

            string provider = AppSettings.Provider;
            string key = AppSettings.CurrentApiKey;
            string lang = AppSettings.Language;

            if (string.IsNullOrWhiteSpace(key))
            {
                _statusText.Text = "Chua nhap API key cho " + provider;
                _busy = false;
                return;
            }

            System.Threading.Tasks.Task.Run(async () =>
            {
                string text;
                try
                {
                    text = await TranscriptionEngine.TranscribeAsync(wav, provider, key, lang);
                }
                catch (System.Exception ex)
                {
                    text = "";
                    RunOnUiThread(() => _statusText.Text = "STT loi: " + ex.Message);
                }

                RunOnUiThread(() =>
                {
                    _busy = false;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _resultBox.Text = text;
                        _statusText.Text = "Xong.";
                        var cm = GetSystemService(ClipboardService)!.JavaCast<ClipboardManager>()!;
                        cm.PrimaryClip = ClipData.NewPlainText("bfstt", text);
                    }
                    else if (_statusText.Text == "Dang nhan dang...")
                    {
                        _statusText.Text = "Khong nhan duoc van ban.";
                    }
                });
            });
        }

        // ---------- permissions ----------

        private void RequestRuntimePermissions()
        {
            var perms = new System.Collections.Generic.List<string> { Android.Manifest.Permission.RecordAudio };
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                perms.Add(Android.Manifest.Permission.PostNotifications);

            RequestPermissions(perms.ToArray(), ReqPermissions);
        }

        // ---------- tiny view helpers ----------

        private TextView Header(string text)
        {
            var tv = new TextView(this) { Text = text, TextSize = 22 };
            tv.SetPadding(0, 0, 0, Dp(8));
            return tv;
        }

        private TextView Label(string text)
        {
            var tv = new TextView(this) { Text = text };
            tv.SetPadding(0, Dp(12), 0, Dp(4));
            return tv;
        }

        private View Divider()
        {
            var v = new View(this);
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(1));
            lp.TopMargin = Dp(16);
            lp.BottomMargin = Dp(8);
            v.LayoutParameters = lp;
            v.SetBackgroundColor(Android.Graphics.Color.LightGray);
            return v;
        }

        private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);
    }
}
