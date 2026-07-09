using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views.Accessibility;

namespace BFSTT.Droid
{
    /// <summary>
    /// Optional accessibility service that inserts the transcribed text into the
    /// currently-focused editable field of whatever app is in the foreground.
    /// If the user has not enabled it, the app falls back to the clipboard.
    /// </summary>
    [Service(
        Label = "BF-STT Paste",
        Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE",
        Exported = true)]
    [IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
    [MetaData("android.accessibilityservice", Resource = "@xml/accessibility_service_config")]
    public class PasteAccessibilityService : AccessibilityService
    {
        public static PasteAccessibilityService? Instance { get; private set; }

        /// <summary>True when the service is connected and ready to paste.</summary>
        public static bool IsReady => Instance != null;

        protected override void OnServiceConnected()
        {
            base.OnServiceConnected();
            Instance = this;
        }

        public override void OnAccessibilityEvent(AccessibilityEvent? e) { }

        public override void OnInterrupt() { }

        public override bool OnUnbind(Intent? intent)
        {
            Instance = null;
            return base.OnUnbind(intent);
        }

        public override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }

        /// <summary>
        /// Inserts <paramref name="text"/> into the focused editable field.
        /// Tries ACTION_PASTE first (cursor-aware); if that is not honoured by the
        /// app, falls back to ACTION_SET_TEXT (append) which works in most fields.
        /// Returns true only if some insert action succeeded.
        /// </summary>
        public bool TryPaste(string text)
        {
            AccessibilityNodeInfo? root = RootInActiveWindow;
            if (root == null) return false;

            AccessibilityNodeInfo? node = FindEditable(root);
            if (node == null)
            {
                root.Recycle();
                return false;
            }

            bool ok = false;

            // 1) ACTION_PASTE — respects the caret, but not all apps advertise it.
            try { ok = node.PerformAction(Android.Views.Accessibility.Action.Paste); }
            catch { ok = false; }

            // 2) ACTION_SET_TEXT — replace with existing + new (append). Broadly supported.
            if (!ok && !string.IsNullOrEmpty(text))
            {
                try
                {
                    string existing = node.Text?.ToString() ?? "";
                    string combined = string.IsNullOrEmpty(existing) ? text : existing + " " + text;

                    using var args = new Bundle();
                    args.PutCharSequence(
                        AccessibilityNodeInfo.ActionArgumentSetTextCharsequence,
                        new Java.Lang.String(combined));

                    ok = node.PerformAction(Android.Views.Accessibility.Action.SetText, args);
                }
                catch { ok = false; }
            }

            node.Recycle();
            root.Recycle();
            return ok;
        }

        /// <summary>
        /// Finds the best editable target: the input-focused node if editable,
        /// otherwise a breadth-first search for a focused-editable node, else any editable node.
        /// </summary>
        private static AccessibilityNodeInfo? FindEditable(AccessibilityNodeInfo root)
        {
            var focused = root.FindFocus(NodeFocus.Input);
            if (focused != null && focused.Editable) return focused;
            focused?.Recycle();

            AccessibilityNodeInfo? anyEditable = null;
            var queue = new System.Collections.Generic.Queue<AccessibilityNodeInfo>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var n = queue.Dequeue();

                if (n.Editable)
                {
                    if (n.Focused) return n;      // strongest match
                    anyEditable ??= n;
                }

                int count = n.ChildCount;
                for (int i = 0; i < count; i++)
                {
                    var c = n.GetChild(i);
                    if (c != null) queue.Enqueue(c);
                }
            }

            return anyEditable;
        }
    }
}
