using System.Collections.Generic;

using Android.Animation;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;

using BFSTT.Droid.Audio;
using BFSTT.Droid.Stt;

namespace BFSTT.Droid
{
    /// <summary>
    /// Single-screen config + test harness with a modern, animated dark UI. Fully localized
    /// (display language is user-selectable). Also the "settings" screen opened when the
    /// floating bubble is long-pressed.
    /// </summary>
    [Activity(
        Label = "BF-STT",
        MainLauncher = true,
        Theme = "@android:style/Theme.Material.NoActionBar",
        ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : Activity
    {
        private const int ReqPermissions = 100;
        private const string ActionA11yDetails = "android.settings.ACCESSIBILITY_DETAILS_SETTINGS";

        private static readonly Color TextHi = Color.ParseColor("#ECEFF4");
        private static readonly Color TextMid = Color.ParseColor("#98A2B3");
        private static readonly Color TextLo = Color.ParseColor("#5B6675");
        private static readonly Color Primary = Color.ParseColor("#4C8DFF");
        private static readonly Color Danger = Color.ParseColor("#FF5A5F");
        private static readonly Color Success = Color.ParseColor("#34D399");
        private static readonly Color Warn = Color.ParseColor("#FBBF24");

        /// <summary>Speech-recognition languages: code + native display name.
        /// "auto" is shown with the localized "Auto detect" label.</summary>
        private static readonly (string Code, string Name)[] RecLangs =
        {
            ("auto", ""),
            ("vi", "Tiếng Việt"), ("en", "English"), ("zh", "中文"), ("ja", "日本語"),
            ("ko", "한국어"), ("fr", "Français"), ("de", "Deutsch"), ("es", "Español"),
            ("th", "ไทย"), ("it", "Italiano"), ("pt", "Português"), ("ru", "Русский"),
            ("id", "Bahasa Indonesia"), ("hi", "हिन्दी"), ("ar", "العربية"),
        };

        private SegmentedControl _providerSeg = null!;
        private TextView _recLangValue = null!;
        private EditText _apiKeyInput = null!;
        private Switch _autoPasteSwitch = null!;
        private TextView _pasteStatusChip = null!;
        private EditText _resultBox = null!;
        private Button _recordButton = null!;
        private TextView _statusText = null!;
        private View _pulseDot = null!;

        private readonly List<View> _cards = new();
        private LinearLayout _root = null!;
        private ObjectAnimator? _pulse;

        private readonly AndroidAudioRecorder _recorder = new();
        private bool _recording;
        private bool _busy;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AppSettings.Init(this);
            L.Lang = AppSettings.UiLang;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Window!.SetStatusBarColor(Color.ParseColor("#141824"));
                Window!.SetNavigationBarColor(Color.ParseColor("#0C0E13"));
            }

            BuildUi();
            LoadSettingsIntoUi();
            RefreshPasteStatus();
            RequestRuntimePermissions();
        }

        protected override void OnResume()
        {
            base.OnResume();
            RefreshPasteStatus();
            if (!string.IsNullOrEmpty(BubbleService.LastResult))
                _resultBox.Text = BubbleService.LastResult;
        }

        // ================= UI =================

        private void BuildUi()
        {
            var scroll = new ScrollView(this);
            scroll.SetBackgroundResource(Resource.Drawable.screen_bg);
            scroll.FillViewport = true;

            var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
            int pad = Dp(18);
            root.SetPadding(pad, Dp(24), pad, Dp(32));
            _root = root;

            root.AddView(BuildHero());

            // 1) Display language
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("ui_lang_title")));
                body.AddView(BodyText(L.T("ui_lang_desc")));
                var field = DropdownField(
                    L.LangNames[UiLangIndex()],
                    L.T("picker_ui_lang_title"),
                    L.LangNames,
                    UiLangIndex(),
                    which =>
                    {
                        string code = L.Langs[which];
                        if (code == AppSettings.UiLang) return;
                        AppSettings.UiLang = code;
                        L.Lang = code;
                        Recreate();
                    },
                    out _);
                field.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
                body.AddView(field);
            }

            // 2) Recognition language
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("rec_lang_title")));
                body.AddView(BodyText(L.T("rec_lang_desc")));
                var recField = DropdownField(
                    RecLangName(RecLangIndex()),
                    L.T("picker_rec_lang_title"),
                    RecLangItems(),
                    RecLangIndex(),
                    which =>
                    {
                        AppSettings.Language = RecLangs[which].Code;
                        _recLangValue.Text = RecLangName(which);
                    },
                    out _recLangValue);
                recField.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
                body.AddView(recField);
            }

            // 3) Provider + API key
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("provider_title")));
                body.AddView(BodyText(L.T("provider_desc")));

                int provInit = System.Array.IndexOf(AppSettings.Providers, AppSettings.Provider);
                _providerSeg = new SegmentedControl(this, AppSettings.Providers, provInit < 0 ? 0 : provInit);
                _providerSeg.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, Dp(52)) { TopMargin = Dp(12) };
                _providerSeg.SelectionChanged = idx =>
                {
                    string provider = AppSettings.Providers[idx];
                    AppSettings.Provider = provider;
                    _apiKeyInput.Text = AppSettings.ApiKeyFor(provider);
                };
                body.AddView(_providerSeg);

                _apiKeyInput = Field(L.T("apikey_hint"), singleLine: true);
                _apiKeyInput.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
                body.AddView(_apiKeyInput);

                body.AddView(MakeButton(L.T("save_btn"), Resource.Drawable.btn_primary, TextHi, () => SaveSettings()));
            }

            // 4) Auto-paste + accessibility
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("autopaste_title")));
                body.AddView(BodyText(L.T("autopaste_desc")));

                var switchRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                switchRow.SetGravity(GravityFlags.CenterVertical);
                switchRow.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(6) };

                var switchLabel = BodyText(L.T("autopaste_switch"));
                switchLabel.SetTextColor(TextHi);
                switchLabel.LayoutParameters = new LinearLayout.LayoutParams(
                    0, ViewGroup.LayoutParams.WrapContent, 1f);
                switchRow.AddView(switchLabel);

                _autoPasteSwitch = new Switch(this);
                switchRow.AddView(_autoPasteSwitch);
                body.AddView(switchRow);

                var statusRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                statusRow.SetGravity(GravityFlags.CenterVertical);
                statusRow.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
                var statusLabel = BodyText(L.T("a11y_status_label"));
                statusLabel.SetPadding(0, 0, Dp(8), 0);
                statusRow.AddView(statusLabel);
                _pasteStatusChip = Chip(L.T("a11y_off"), Warn);
                statusRow.AddView(_pasteStatusChip);
                body.AddView(statusRow);

                body.AddView(MakeButton(L.T("a11y_open_btn"),
                    Resource.Drawable.btn_primary, TextHi, () => OpenPasteAccessibilitySettings()));
            }

            // 5) Floating bubble
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("bubble_title")));
                body.AddView(BodyText(L.T("bubble_desc")));

                var rowA = ButtonRow(
                    MakeButton(L.T("bubble_on_btn"), Resource.Drawable.btn_primary, TextHi, () => EnableBubble(), fullWidth: false),
                    MakeButton(L.T("bubble_off_btn"), Resource.Drawable.btn_outline, TextMid, () => DisableBubble(), fullWidth: false));
                body.AddView(rowA);

                body.AddView(Divider());
                body.AddView(BodyText(L.T("keepalive_desc")));
                body.AddView(MakeButton(L.T("battery_btn"), Resource.Drawable.btn_tonal, TextHi, () => RequestIgnoreBattery()));
                body.AddView(MakeButton(L.T("autostart_btn"), Resource.Drawable.btn_tonal, TextHi, () => OpenAutostartSettings()));
            }

            // 6) In-app test
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("test_title")));
                body.AddView(BodyText(L.T("test_desc")));

                _recordButton = MakeButton(L.T("record_btn"), Resource.Drawable.btn_primary, TextHi, () => ToggleInAppRecording());
                body.AddView(_recordButton);

                var statusRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                statusRow.SetGravity(GravityFlags.CenterVertical);
                statusRow.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };

                _pulseDot = new View(this);
                var dot = new GradientDrawable();
                dot.SetShape(ShapeType.Oval);
                dot.SetColor(Danger.ToArgb());
                _pulseDot.Background = dot;
                _pulseDot.LayoutParameters = new LinearLayout.LayoutParams(Dp(10), Dp(10)) { RightMargin = Dp(10) };
                _pulseDot.Visibility = ViewStates.Invisible;
                statusRow.AddView(_pulseDot);

                _statusText = BodyText(L.T("status_ready"));
                _statusText.SetPadding(0, 0, 0, 0);
                statusRow.AddView(_statusText);
                body.AddView(statusRow);

                _resultBox = Field(L.T("result_hint"), singleLine: false);
                _resultBox.SetMinLines(3);
                _resultBox.Gravity = GravityFlags.Top | GravityFlags.Start;
                _resultBox.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
                body.AddView(_resultBox);

                body.AddView(MakeButton(L.T("copy_btn"), Resource.Drawable.btn_tonal, TextHi, () => CopyResult()));
            }

            scroll.AddView(root);
            SetContentView(scroll);

            root.Post(AnimateIn);
        }

        private View BuildHero()
        {
            var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            row.SetGravity(GravityFlags.CenterVertical);
            row.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { BottomMargin = Dp(18) };

            var badge = new ImageView(this);
            badge.SetImageResource(Resource.Drawable.ic_mic);
            badge.SetColorFilter(Color.White);
            badge.SetBackgroundResource(Resource.Drawable.hero_badge);
            int bp = Dp(12);
            badge.SetPadding(bp, bp, bp, bp);
            badge.LayoutParameters = new LinearLayout.LayoutParams(Dp(52), Dp(52)) { RightMargin = Dp(14) };
            row.AddView(badge);

            var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
            col.LayoutParameters = new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WrapContent, 1f);
            var title = new TextView(this) { Text = "BF-STT" };
            title.SetTextColor(TextHi);
            title.SetTextSize(ComplexUnitType.Sp, 26);
            title.SetTypeface(null, TypefaceStyle.Bold);
            col.AddView(title);
            var sub = new TextView(this) { Text = L.T("app_subtitle") };
            sub.SetTextColor(TextMid);
            sub.SetTextSize(ComplexUnitType.Sp, 13);
            col.AddView(sub);
            row.AddView(col);

            var guideBtn = new Button(this) { Text = L.T("guide_btn") };
            guideBtn.SetAllCaps(false);
            guideBtn.SetTextColor(TextHi);
            guideBtn.SetTextSize(ComplexUnitType.Sp, 13);
            guideBtn.SetTypeface(null, TypefaceStyle.Bold);
            guideBtn.SetBackgroundResource(Resource.Drawable.btn_tonal);
            guideBtn.SetPadding(Dp(14), Dp(9), Dp(14), Dp(9));
            guideBtn.StateListAnimator = null;
            guideBtn.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { LeftMargin = Dp(10) };
            guideBtn.Click += (s, e) => StartActivity(new Intent(this, typeof(GuideActivity)));
            AddPress(guideBtn);
            row.AddView(guideBtn);

            return row;
        }

        // ---------- entrance animation ----------

        private void AnimateIn()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                c.Alpha = 0f;
                c.TranslationY = Dp(28);
                c.Animate()!
                    .Alpha(1f)
                    .TranslationY(0f)
                    .SetStartDelay(60 + i * 70)
                    .SetDuration(440)
                    .SetInterpolator(new DecelerateInterpolator())
                    .WithEndAction(new Java.Lang.Runnable(() =>
                    {
                        c.Alpha = 1f;
                        c.TranslationY = 0f;
                    }))
                    .Start();
            }

            // Safety net: a cancelled reveal (permission dialog steals focus, or animations
            // are disabled) must never leave cards stuck invisible.
            _root.PostDelayed(EnsureCardsVisible, 1400);
        }

        private void EnsureCardsVisible()
        {
            foreach (var c in _cards)
            {
                c.Animate()!.Cancel();
                c.Alpha = 1f;
                c.TranslationY = 0f;
            }
        }

        // ================= recognition / display language helpers =================

        private static int UiLangIndex()
        {
            int idx = System.Array.IndexOf(L.Langs, AppSettings.UiLang);
            return idx < 0 ? 0 : idx;
        }

        private static string RecLangName(int i)
            => RecLangs[i].Code == "auto" ? L.T("auto_detect") : RecLangs[i].Name;

        private static string[] RecLangItems()
        {
            var a = new string[RecLangs.Length];
            for (int i = 0; i < a.Length; i++) a[i] = RecLangName(i);
            return a;
        }

        private static int RecLangIndex()
        {
            int idx = System.Array.FindIndex(RecLangs, x => x.Code == AppSettings.Language);
            if (idx >= 0) return idx;
            int vi = System.Array.FindIndex(RecLangs, x => x.Code == "vi");
            return vi < 0 ? 0 : vi;
        }

        // ================= settings =================

        private void LoadSettingsIntoUi()
        {
            _apiKeyInput.Text = AppSettings.CurrentApiKey;
            _autoPasteSwitch.Checked = AppSettings.AutoPaste;
        }

        private void SaveSettings()
        {
            string provider = AppSettings.Providers[_providerSeg.SelectedIndex];
            AppSettings.Provider = provider;
            AppSettings.SetApiKey(provider, _apiKeyInput.Text ?? "");
            AppSettings.AutoPaste = _autoPasteSwitch.Checked;
            Toast.MakeText(this, L.T("saved_toast"), ToastLength.Short)!.Show();
        }

        // ================= floating bubble =================

        private void EnableBubble()
        {
            SaveSettings();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M && !Settings.CanDrawOverlays(this))
            {
                Toast.MakeText(this, L.T("overlay_needed"), ToastLength.Long)!.Show();
                var intent = new Intent(
                    Settings.ActionManageOverlayPermission,
                    Android.Net.Uri.Parse("package:" + PackageName));
                StartActivity(intent);
                return;
            }

            AppSettings.BubbleEnabled = true;

            var svc = new Intent(this, typeof(BubbleService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                StartForegroundService(svc);
            else
                StartService(svc);

            Toast.MakeText(this, L.T("bubble_enabled_toast"), ToastLength.Short)!.Show();
        }

        private void DisableBubble()
        {
            AppSettings.BubbleEnabled = false;
            StopService(new Intent(this, typeof(BubbleService)));
            Toast.MakeText(this, L.T("bubble_disabled_toast"), ToastLength.Short)!.Show();
        }

        // ================= accessibility (BF-STT Paste) =================

        private void OpenPasteAccessibilitySettings()
        {
            string flat = new ComponentName(
                this, Java.Lang.Class.FromType(typeof(PasteAccessibilityService))).FlattenToString()!;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                try
                {
                    var intent = new Intent(ActionA11yDetails);
                    var bundle = new Bundle();
                    bundle.PutString(":settings:fragment_args_key", flat);
                    intent.PutExtra(":settings:fragment_args_key", flat);
                    intent.PutExtra(":settings:show_fragment_args", bundle);
                    intent.AddFlags(ActivityFlags.NewTask);
                    StartActivity(intent);
                    Toast.MakeText(this, L.T("a11y_find_toast"), ToastLength.Long)!.Show();
                    return;
                }
                catch { /* fall through */ }
            }

            try
            {
                StartActivity(new Intent(Settings.ActionAccessibilitySettings));
                Toast.MakeText(this, L.T("a11y_find_list_toast"), ToastLength.Long)!.Show();
            }
            catch
            {
                Toast.MakeText(this, L.T("a11y_open_fail"), ToastLength.Short)!.Show();
            }
        }

        private void RefreshPasteStatus()
        {
            bool on = PasteAccessibilityService.IsReady;
            _pasteStatusChip.Text = on ? L.T("a11y_on") : L.T("a11y_off");
            _pasteStatusChip.SetTextColor(on ? Success : Warn);
        }

        // ================= OEM battery / autostart whitelisting =================

        private void RequestIgnoreBattery()
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    var intent = new Intent(
                        Settings.ActionRequestIgnoreBatteryOptimizations,
                        Android.Net.Uri.Parse("package:" + PackageName));
                    StartActivity(intent);
                }
            }
            catch
            {
                try { StartActivity(new Intent(Settings.ActionIgnoreBatteryOptimizationSettings)); }
                catch { Toast.MakeText(this, L.T("battery_open_fail"), ToastLength.Short)!.Show(); }
            }
        }

        private void OpenAutostartSettings()
        {
            var candidates = new[]
            {
                new ComponentName("com.miui.securitycenter", "com.miui.permcenter.autostart.AutoStartManagementActivity"),
                new ComponentName("com.letv.android.letvsafe", "com.letv.android.letvsafe.AutobootManageActivity"),
                new ComponentName("com.huawei.systemmanager", "com.huawei.systemmanager.startupmgr.ui.StartupNormalAppListActivity"),
                new ComponentName("com.coloros.safecenter", "com.coloros.safecenter.permission.startup.StartupAppListActivity"),
                new ComponentName("com.oppo.safe", "com.oppo.safe.permission.startup.StartupAppListActivity"),
                new ComponentName("com.vivo.permissionmanager", "com.vivo.permissionmanager.activity.BgStartUpManagerActivity"),
            };

            foreach (var c in candidates)
            {
                try
                {
                    var intent = new Intent();
                    intent.SetComponent(c);
                    intent.AddFlags(ActivityFlags.NewTask);
                    StartActivity(intent);
                    return;
                }
                catch { /* try next OEM */ }
            }

            try
            {
                var intent = new Intent(
                    Settings.ActionApplicationDetailsSettings,
                    Android.Net.Uri.Parse("package:" + PackageName));
                StartActivity(intent);
            }
            catch
            {
                Toast.MakeText(this, L.T("autostart_fail"), ToastLength.Long)!.Show();
            }
        }

        // ================= in-app test recording =================

        private void CopyResult()
        {
            string text = _resultBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                Toast.MakeText(this, L.T("copy_empty"), ToastLength.Short)!.Show();
                return;
            }
            var cm = GetSystemService(ClipboardService)!.JavaCast<ClipboardManager>()!;
            cm.PrimaryClip = ClipData.NewPlainText("bfstt", text);
            Toast.MakeText(this, L.T("copied_toast"), ToastLength.Short)!.Show();
        }

        private void ToggleInAppRecording()
        {
            if (_busy) return;

            if (!_recording)
            {
                try
                {
                    _recorder.Start();
                    _recording = true;
                    _recordButton.Text = L.T("record_stop_btn");
                    _recordButton.SetBackgroundResource(Resource.Drawable.btn_danger);
                    _statusText.Text = L.T("status_recording");
                    _statusText.SetTextColor(Danger);
                    StartPulse();
                }
                catch (System.Exception ex)
                {
                    _statusText.Text = L.F("status_mic_error", ex.Message);
                    _statusText.SetTextColor(Danger);
                }
                return;
            }

            byte[] wav;
            try { wav = _recorder.Stop(); }
            catch (System.Exception ex)
            {
                _statusText.Text = L.F("status_stop_error", ex.Message);
                _statusText.SetTextColor(Danger);
                ResetRecordButton();
                StopPulse();
                return;
            }

            _recording = false;
            _busy = true;
            ResetRecordButton();
            StopPulse();
            _statusText.Text = L.T("status_transcribing");
            _statusText.SetTextColor(Warn);

            string provider = AppSettings.Provider;
            string key = AppSettings.CurrentApiKey;
            string lang = AppSettings.Language;

            if (string.IsNullOrWhiteSpace(key))
            {
                _statusText.Text = L.F("status_no_key", provider);
                _statusText.SetTextColor(Danger);
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
                    RunOnUiThread(() =>
                    {
                        _statusText.Text = L.F("status_stt_error", ex.Message);
                        _statusText.SetTextColor(Danger);
                    });
                }

                RunOnUiThread(() =>
                {
                    _busy = false;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _resultBox.Text = text;
                        _statusText.Text = L.T("status_done");
                        _statusText.SetTextColor(Success);
                        var cm = GetSystemService(ClipboardService)!.JavaCast<ClipboardManager>()!;
                        cm.PrimaryClip = ClipData.NewPlainText("bfstt", text);
                    }
                    else if (_statusText.Text == L.T("status_transcribing"))
                    {
                        _statusText.Text = L.T("status_no_text");
                        _statusText.SetTextColor(Warn);
                    }
                });
            });
        }

        private void ResetRecordButton()
        {
            _recordButton.Text = L.T("record_btn");
            _recordButton.SetBackgroundResource(Resource.Drawable.btn_primary);
        }

        private void StartPulse()
        {
            _pulseDot.Visibility = ViewStates.Visible;
            _pulse?.Cancel();
            var a = ObjectAnimator.OfPropertyValuesHolder(_pulseDot,
                PropertyValuesHolder.OfFloat("scaleX", 1f, 1.8f)!,
                PropertyValuesHolder.OfFloat("scaleY", 1f, 1.8f)!,
                PropertyValuesHolder.OfFloat("alpha", 0.95f, 0.2f)!)!;
            a.SetDuration(720);
            a.RepeatCount = ValueAnimator.Infinite;
            a.RepeatMode = ValueAnimatorRepeatMode.Reverse;
            a.Start();
            _pulse = a;
        }

        private void StopPulse()
        {
            _pulse?.Cancel();
            _pulse = null;
            _pulseDot.ScaleX = 1f;
            _pulseDot.ScaleY = 1f;
            _pulseDot.Alpha = 1f;
            _pulseDot.Visibility = ViewStates.Invisible;
        }

        // ================= permissions =================

        private void RequestRuntimePermissions()
        {
            var perms = new List<string> { Android.Manifest.Permission.RecordAudio };
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                perms.Add(Android.Manifest.Permission.PostNotifications);

            RequestPermissions(perms.ToArray(), ReqPermissions);
        }

        // ================= view helpers =================

        private void Card(out LinearLayout body)
        {
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
            card.SetBackgroundResource(Resource.Drawable.card_bg);
            int p = Dp(18);
            card.SetPadding(p, p, p, p);
            card.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { BottomMargin = Dp(14) };
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                card.Elevation = Dp(2);

            _root.AddView(card);
            _cards.Add(card);
            body = card;
        }

        private TextView SectionTitle(string text)
        {
            var tv = new TextView(this) { Text = text };
            tv.SetTextColor(TextHi);
            tv.SetTextSize(ComplexUnitType.Sp, 17);
            tv.SetTypeface(null, TypefaceStyle.Bold);
            return tv;
        }

        private TextView BodyText(string text)
        {
            var tv = new TextView(this) { Text = text };
            tv.SetTextColor(TextMid);
            tv.SetTextSize(ComplexUnitType.Sp, 13);
            tv.SetLineSpacing(Dp(3), 1f);
            tv.SetPadding(0, Dp(4), 0, 0);
            return tv;
        }

        private TextView Chip(string text, Color textColor)
        {
            var tv = new TextView(this) { Text = text };
            tv.SetBackgroundResource(Resource.Drawable.chip_bg);
            tv.SetTextColor(textColor);
            tv.SetTextSize(ComplexUnitType.Sp, 12);
            tv.SetTypeface(null, TypefaceStyle.Bold);
            tv.SetPadding(Dp(12), Dp(5), Dp(12), Dp(5));
            return tv;
        }

        private EditText Field(string hint, bool singleLine)
        {
            var et = new EditText(this) { Hint = hint };
            et.SetBackgroundResource(Resource.Drawable.field_bg);
            et.SetTextColor(TextHi);
            et.SetHintTextColor(TextLo);
            et.SetTextSize(ComplexUnitType.Sp, 15);
            et.SetPadding(Dp(16), Dp(14), Dp(16), Dp(14));
            if (singleLine) et.SetSingleLine(true);
            return et;
        }

        /// <summary>A rounded field showing the current value on the left with a chevron
        /// pinned to the right, opening a single-choice dialog on tap. Used for both language
        /// pickers. The value <see cref="TextView"/> is returned via <paramref name="valueView"/>
        /// so callers can update it after a pick.</summary>
        private View DropdownField(string valueText, string dialogTitle, string[] items, int selected,
                                   System.Action<int> onPick, out TextView valueView)
        {
            var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            row.SetBackgroundResource(Resource.Drawable.field_bg);
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(16), Dp(14), Dp(16), Dp(14));
            row.Clickable = true;

            valueView = new TextView(this) { Text = valueText };
            valueView.SetTextColor(TextHi);
            valueView.SetTextSize(ComplexUnitType.Sp, 15);
            valueView.SetTypeface(null, TypefaceStyle.Bold);
            valueView.LayoutParameters = new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WrapContent, 1f);
            row.AddView(valueView);

            var arrow = new TextView(this) { Text = "▾" };
            arrow.SetTextColor(TextMid);
            arrow.SetTextSize(ComplexUnitType.Sp, 16);
            arrow.SetPadding(Dp(8), 0, 0, 0);
            row.AddView(arrow);

            AddPress(row);
            row.Click += (s, e) =>
            {
                var builder = new AlertDialog.Builder(this, Android.Resource.Style.ThemeMaterialDialogAlert);
                builder.SetTitle(dialogTitle);
                builder.SetSingleChoiceItems(items, selected, (ds, de) =>
                {
                    onPick(de.Which);
                    (ds as IDialogInterface)?.Dismiss();
                });
                builder.Show();
            };
            return row;
        }

        private View Divider()
        {
            var v = new View(this);
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(1))
            {
                TopMargin = Dp(16),
                BottomMargin = Dp(8)
            };
            v.LayoutParameters = lp;
            v.SetBackgroundColor(Color.ParseColor("#2A313E"));
            return v;
        }

        private Button MakeButton(string text, int bgRes, Color textColor, System.Action onClick, bool fullWidth = true)
        {
            var b = new Button(this) { Text = text };
            b.SetAllCaps(false);
            b.SetTextColor(textColor);
            b.SetTextSize(ComplexUnitType.Sp, 15);
            b.SetTypeface(null, TypefaceStyle.Bold);
            b.SetBackgroundResource(bgRes);
            b.SetPadding(Dp(18), Dp(14), Dp(18), Dp(14));
            b.StateListAnimator = null;
            if (fullWidth)
            {
                b.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
            }
            b.Click += (s, e) => onClick();
            AddPress(b);
            return b;
        }

        private View ButtonRow(Button left, Button right)
        {
            var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            row.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
            left.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                RightMargin = Dp(6)
            };
            right.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                LeftMargin = Dp(6)
            };
            row.AddView(left);
            row.AddView(right);
            return row;
        }

        private void AddPress(View v)
        {
            v.Touch += (s, e) =>
            {
                var ev = e.Event;
                if (ev != null)
                {
                    switch (ev.ActionMasked)
                    {
                        case MotionEventActions.Down:
                            v.Animate()!.ScaleX(0.96f).ScaleY(0.96f).SetDuration(90).Start();
                            break;
                        case MotionEventActions.Up:
                        case MotionEventActions.Cancel:
                            v.Animate()!.ScaleX(1f).ScaleY(1f).SetDuration(160)
                                .SetInterpolator(new OvershootInterpolator(2.2f)).Start();
                            break;
                    }
                }
                e.Handled = false;
            };
        }

        private int Dp(int value) => Dp(this, value);

        private static int Dp(Context ctx, int value)
            => (int)(value * ctx.Resources!.DisplayMetrics!.Density);

        // ================= segmented control (provider picker) =================

        private sealed class SegmentedControl : FrameLayout
        {
            private readonly View _thumb;
            private readonly TextView[] _labels;
            private readonly int _pad;
            private int _index;

            public System.Action<int>? SelectionChanged;
            public int SelectedIndex => _index;

            public SegmentedControl(Context ctx, string[] items, int initial) : base(ctx)
            {
                _pad = Dp(ctx, 5);
                SetBackgroundResource(Resource.Drawable.seg_track);
                SetPadding(_pad, _pad, _pad, _pad);

                _thumb = new View(ctx);
                _thumb.SetBackgroundResource(Resource.Drawable.seg_thumb);
                AddView(_thumb, new FrameLayout.LayoutParams(1, ViewGroup.LayoutParams.MatchParent));

                var row = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
                row.LayoutParameters = new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);

                _labels = new TextView[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    var tv = new TextView(ctx) { Text = items[i], Gravity = GravityFlags.Center };
                    tv.SetTextSize(ComplexUnitType.Sp, 15);
                    tv.SetTypeface(null, TypefaceStyle.Bold);
                    tv.Clickable = true;
                    tv.LayoutParameters = new LinearLayout.LayoutParams(
                        0, ViewGroup.LayoutParams.MatchParent, 1f);
                    int idx = i;
                    tv.Click += (s, e) => Select(idx, true);
                    _labels[i] = tv;
                    row.AddView(tv);
                }
                AddView(row);

                _index = (initial < 0 || initial >= items.Length) ? 0 : initial;
                ApplyColors();
            }

            protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
            {
                base.OnSizeChanged(w, h, oldw, oldh);
                Post(() => PositionThumb(false));
            }

            public void Select(int idx, bool notify)
            {
                if (idx < 0 || idx >= _labels.Length) return;
                bool changed = idx != _index;
                _index = idx;
                PositionThumb(true);
                ApplyColors();
                if (notify && changed) SelectionChanged?.Invoke(_index);
            }

            private void PositionThumb(bool animate)
            {
                int innerW = Width - _pad * 2;
                if (innerW <= 0 || _labels.Length == 0) return;
                int thumbW = innerW / _labels.Length;
                if (_thumb.Width != thumbW)
                {
                    _thumb.LayoutParameters = new FrameLayout.LayoutParams(
                        thumbW, ViewGroup.LayoutParams.MatchParent);
                }
                float target = _index * thumbW;
                if (animate)
                    _thumb.Animate()!.TranslationX(target).SetDuration(240)
                          .SetInterpolator(new DecelerateInterpolator()).Start();
                else
                    _thumb.TranslationX = target;
            }

            private void ApplyColors()
            {
                for (int i = 0; i < _labels.Length; i++)
                    _labels[i].SetTextColor(i == _index ? TextHi : TextMid);
            }
        }
    }
}
