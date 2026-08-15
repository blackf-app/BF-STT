using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace BFSTT.Droid
{
    /// <summary>
    /// In-app usage guide. Detailed and tailored to the device's Android version. Localized in
    /// Vietnamese + English (other display languages fall back to English for the guide body).
    /// </summary>
    [Activity(
        Label = "BF-STT",
        Theme = "@android:style/Theme.Material.NoActionBar",
        ScreenOrientation = ScreenOrientation.Portrait)]
    public class GuideActivity : Activity
    {
        private static readonly Color TextHi = Color.ParseColor("#ECEFF4");
        private static readonly Color TextMid = Color.ParseColor("#98A2B3");
        private static readonly Color Primary = Color.ParseColor("#4C8DFF");
        private static readonly Color Warn = Color.ParseColor("#FBBF24");

        private LinearLayout _rootColumn = null!;

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
        }

        private void BuildUi()
        {
            var scroll = new ScrollView(this);
            scroll.SetBackgroundResource(Resource.Drawable.screen_bg);
            scroll.FillViewport = true;

            var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
            int pad = Dp(18);
            root.SetPadding(pad, Dp(20), pad, Dp(32));
            _rootColumn = root;

            root.AddView(BuildHeader());

            int sdk = (int)Build.VERSION.SdkInt;

            // ---- Your device (tailored checklist) ----
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("g_device_title")));
                body.AddView(BodyText(L.F("g_device_line", Build.VERSION.Release ?? "?", sdk)));
                body.AddView(BodyText(L.T("g_device_steps_intro")));
                foreach (var (text, warn) in TailoredSteps(sdk))
                    body.AddView(Bullet(text, warn));
            }

            // ---- Quick start ----
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("g_quick_title")));
                body.AddView(Step(1, L.T("g_quick_1")));
                body.AddView(Step(2, L.T("g_quick_2")));
                body.AddView(Step(3, L.T("g_quick_3")));
                body.AddView(Step(4, L.T("g_quick_4")));
                body.AddView(Step(5, L.T("g_quick_5")));
            }

            // ---- Bubble gestures ----
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("g_gestures_title")));
                body.AddView(Bullet(L.T("g_gesture_1")));
                body.AddView(Bullet(L.T("g_gesture_2")));
                body.AddView(Bullet(L.T("g_gesture_3")));
                body.AddView(Bullet(L.T("g_gesture_4")));
                body.AddView(Bullet(L.T("g_gesture_5")));
            }

            // ---- Permissions (with quick-open buttons) ----
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("g_perms_title")));
                body.AddView(Bullet(L.T("g_perm_1")));
                body.AddView(Bullet(L.T("g_perm_2")));
                body.AddView(Bullet(L.T("g_perm_3")));
                body.AddView(Bullet(L.T("g_perm_4")));
                body.AddView(Bullet(L.T("g_perm_5")));

                body.AddView(ActionButton(L.T("g_perm_btn_overlay"), OpenOverlaySettings));
                body.AddView(ActionButton(L.T("g_perm_btn_a11y"), OpenPasteAccessibilitySettings));
                body.AddView(ActionButton(L.T("g_perm_btn_appdetails"), OpenAppDetails));
            }

            // ---- Per-version guide ----
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("g_versions_title")));

                body.AddView(VersionTitle(L.T("g_v89_title"), sdk >= 26 && sdk <= 28));
                body.AddView(Bullet(L.T("g_v89_1")));
                body.AddView(Bullet(L.T("g_v89_2")));
                body.AddView(Bullet(L.T("g_v89_3")));

                body.AddView(VersionTitle(L.T("g_v1012_title"), sdk >= 29 && sdk <= 32));
                body.AddView(Bullet(L.T("g_v1012_1")));
                body.AddView(Bullet(L.T("g_v1012_2")));

                body.AddView(VersionTitle(L.T("g_v13_title"), sdk == 33));
                body.AddView(Bullet(L.T("g_v13_1")));
                body.AddView(Bullet(L.T("g_v13_2"), warn: true));

                body.AddView(VersionTitle(L.T("g_v14_title"), sdk == 34));
                body.AddView(Bullet(L.T("g_v14_1")));
                body.AddView(Bullet(L.T("g_v14_2"), warn: true));

                body.AddView(VersionTitle(L.T("g_v15_title"), sdk >= 35));
                body.AddView(Bullet(L.T("g_v15_1")));
                body.AddView(Bullet(L.T("g_v15_2")));

                body.AddView(VersionTitle(L.T("g_oem_title"), false));
                body.AddView(Bullet(L.T("g_oem_1")));
                body.AddView(Bullet(L.T("g_oem_2")));
                body.AddView(Bullet(L.T("g_oem_3")));
            }

            // ---- Troubleshooting ----
            {
                Card(out var body);
                body.AddView(SectionTitle(L.T("g_trouble_title")));
                body.AddView(Bullet(L.T("g_trouble_1")));
                body.AddView(Bullet(L.T("g_trouble_2")));
                body.AddView(Bullet(L.T("g_trouble_3")));
                body.AddView(Bullet(L.T("g_trouble_4")));
                body.AddView(Bullet(L.T("g_trouble_5")));
            }

            scroll.AddView(root);
            SetContentView(scroll);
        }

        private static List<(string Text, bool Warn)> TailoredSteps(int sdk)
        {
            var s = new List<(string, bool)>
            {
                (L.T("g_step_mic"), false),
                (L.T("g_step_overlay"), false),
            };
            if (sdk >= 33) s.Add((L.T("g_step_notif"), false));
            if (sdk >= 33) s.Add((L.T("g_step_a11y_restricted"), true));
            else s.Add((L.T("g_step_a11y_direct"), false));
            if (sdk >= 29) s.Add((L.T("g_step_battery"), false));
            return s;
        }

        // ---------- system settings shortcuts ----------

        private void OpenOverlaySettings()
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                    StartActivity(new Intent(
                        Settings.ActionManageOverlayPermission,
                        Android.Net.Uri.Parse("package:" + PackageName)));
            }
            catch { Toast.MakeText(this, L.T("g_overlay_fail"), ToastLength.Short)!.Show(); }
        }

        private void OpenPasteAccessibilitySettings()
        {
            string flat = new ComponentName(
                this, Java.Lang.Class.FromType(typeof(PasteAccessibilityService))).FlattenToString()!;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                try
                {
                    var intent = new Intent("android.settings.ACCESSIBILITY_DETAILS_SETTINGS");
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
            try { StartActivity(new Intent(Settings.ActionAccessibilitySettings)); }
            catch { Toast.MakeText(this, L.T("a11y_open_fail"), ToastLength.Short)!.Show(); }
        }

        private void OpenAppDetails()
        {
            try
            {
                StartActivity(new Intent(
                    Settings.ActionApplicationDetailsSettings,
                    Android.Net.Uri.Parse("package:" + PackageName)));
                Toast.MakeText(this, L.T("g_appdetails_toast"), ToastLength.Long)!.Show();
            }
            catch { Toast.MakeText(this, L.T("g_overlay_fail"), ToastLength.Short)!.Show(); }
        }

        // ---------- view helpers ----------

        private View BuildHeader()
        {
            var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
            col.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { BottomMargin = Dp(14) };

            var back = new TextView(this) { Text = L.T("guide_back") };
            back.SetTextColor(Primary);
            back.SetTextSize(ComplexUnitType.Sp, 15);
            back.SetTypeface(null, TypefaceStyle.Bold);
            back.Clickable = true;
            back.SetPadding(0, Dp(4), 0, Dp(12));
            back.Click += (s, e) => Finish();
            col.AddView(back);

            var title = new TextView(this) { Text = L.T("guide_title") };
            title.SetTextColor(TextHi);
            title.SetTextSize(ComplexUnitType.Sp, 24);
            title.SetTypeface(null, TypefaceStyle.Bold);
            col.AddView(title);

            var sub = new TextView(this) { Text = L.T("guide_subtitle") };
            sub.SetTextColor(TextMid);
            sub.SetTextSize(ComplexUnitType.Sp, 13);
            col.AddView(sub);

            return col;
        }

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
            _rootColumn.AddView(card);
            body = card;
        }

        private TextView SectionTitle(string text)
        {
            var tv = new TextView(this) { Text = text };
            tv.SetTextColor(TextHi);
            tv.SetTextSize(ComplexUnitType.Sp, 17);
            tv.SetTypeface(null, TypefaceStyle.Bold);
            tv.SetPadding(0, 0, 0, Dp(4));
            return tv;
        }

        private TextView VersionTitle(string text, bool current)
        {
            var tv = new TextView(this) { Text = current ? text + L.T("g_current_marker") : text };
            tv.SetTextColor(current ? Primary : TextHi);
            tv.SetTextSize(ComplexUnitType.Sp, 15);
            tv.SetTypeface(null, TypefaceStyle.Bold);
            tv.SetPadding(0, Dp(14), 0, Dp(2));
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

        private TextView Bullet(string text, bool warn = false)
        {
            var tv = new TextView(this) { Text = "•  " + text };
            tv.SetTextColor(warn ? Warn : TextMid);
            tv.SetTextSize(ComplexUnitType.Sp, 13);
            tv.SetLineSpacing(Dp(3), 1f);
            tv.SetPadding(Dp(2), Dp(6), 0, 0);
            return tv;
        }

        private TextView Step(int n, string text)
        {
            var tv = new TextView(this) { Text = n + ".  " + text };
            tv.SetTextColor(TextMid);
            tv.SetTextSize(ComplexUnitType.Sp, 13);
            tv.SetLineSpacing(Dp(3), 1f);
            tv.SetPadding(0, Dp(8), 0, 0);
            return tv;
        }

        private Button ActionButton(string text, System.Action onClick)
        {
            var b = new Button(this) { Text = text };
            b.SetAllCaps(false);
            b.SetTextColor(TextHi);
            b.SetTextSize(ComplexUnitType.Sp, 14);
            b.SetTypeface(null, TypefaceStyle.Bold);
            b.SetBackgroundResource(Resource.Drawable.btn_tonal);
            b.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
            b.StateListAnimator = null;
            b.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) };
            b.Click += (s, e) => onClick();
            return b;
        }

        private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);
    }
}
