using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;

namespace BFSTT.Droid
{
    /// <summary>
    /// Re-launches the floating bubble service after the device boots, so the overlay
    /// behaves like an always-on system helper. Only fires when the user had it enabled
    /// and the overlay permission is still granted.
    ///
    /// Note: on MIUI/HyperOS the BOOT_COMPLETED broadcast is only delivered to apps that
    /// have been granted the "Autostart" permission (see the button in the settings screen).
    /// </summary>
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[]
    {
        Intent.ActionBootCompleted,
        "android.intent.action.QUICKBOOT_POWERON",
        "com.htc.intent.action.QUICKBOOT_POWERON"
    })]
    public class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null) return;

            AppSettings.Init(context);
            if (!AppSettings.BubbleEnabled) return;

            // Without the overlay permission the bubble can't be shown; skip silently.
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M && !Settings.CanDrawOverlays(context))
                return;

            var svc = new Intent(context, typeof(BubbleService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(svc);
            else
                context.StartService(svc);
        }
    }
}
