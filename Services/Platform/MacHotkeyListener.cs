using BF_STT.Services.Infrastructure;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// macOS global hotkey listener using CGEventTap.
    ///
    /// IMPORTANT: To intercept system-wide key events, the app needs the
    /// "Accessibility" permission (System Settings &gt; Privacy &amp; Security &gt; Accessibility).
    /// The OS will prompt the user automatically on the first run.
    /// </summary>
    [SupportedOSPlatform("macos")]
    internal class MacHotkeyListener : IHotkeyListener
    {
        private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        // Maps Windows-style VK codes (which is what the settings store) to macOS kVK_* codes.
        // Only codes used in the SettingsWindow hotkey list are present here.
        private static readonly Dictionary<int, ushort> VkToMacKeyCode = new()
        {
            [HotkeyCodes.F1] = 0x7A,
            [HotkeyCodes.F2] = 0x78,
            [HotkeyCodes.F3] = 0x63,
            [HotkeyCodes.F4] = 0x76,
            [HotkeyCodes.F5] = 0x60,
            [HotkeyCodes.F6] = 0x61,
            [HotkeyCodes.F7] = 0x62,
            [HotkeyCodes.F8] = 0x64,
            [HotkeyCodes.F9] = 0x65,
            [HotkeyCodes.F10] = 0x6D,
            [HotkeyCodes.F11] = 0x67,
            [HotkeyCodes.F12] = 0x6F,
            [HotkeyCodes.Backtick] = 0x32,
        };

        private readonly SettingsService _settingsService;
        private readonly Action _onKeyDown;
        private readonly Action _onKeyUp;
        private readonly Action _onStopAndSendKeyDown;
        private readonly Action _onStopAndSendKeyUp;

        private IntPtr _eventTap;
        private IntPtr _runLoopSource;
        private Thread? _tapThread;
        private bool _disposed;
        private CGEventTapCallBack? _callbackDelegate;

        private bool _isHotkeyTracking;
        private bool _isStopAndSendHotkeyTracking;

        public MacHotkeyListener(SettingsService settingsService,
            Action onKeyDown, Action onKeyUp,
            Action onStopAndSendKeyDown, Action onStopAndSendKeyUp)
        {
            _settingsService = settingsService;
            _onKeyDown = onKeyDown;
            _onKeyUp = onKeyUp;
            _onStopAndSendKeyDown = onStopAndSendKeyDown;
            _onStopAndSendKeyUp = onStopAndSendKeyUp;

            _tapThread = new Thread(RunTapLoop)
            {
                IsBackground = true,
                Name = "BF-STT MacHotkeyListener"
            };
            _tapThread.Start();
        }

        private void RunTapLoop()
        {
            // kCGEventKeyDown = 10, kCGEventKeyUp = 11
            ulong mask = (1UL << 10) | (1UL << 11);
            _callbackDelegate = HandleEvent;
            // kCGSessionEventTap = 1, kCGHeadInsertEventTap = 0, kCGEventTapOptionDefault = 0
            _eventTap = CGEventTapCreate(1, 0, 0, mask, _callbackDelegate, IntPtr.Zero);

            if (_eventTap == IntPtr.Zero)
            {
                Serilog.Log.Warning("MacHotkeyListener: CGEventTapCreate returned null. " +
                    "Accessibility permission likely missing. Grant it in System Settings > Privacy & Security > Accessibility.");
                return;
            }

            _runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
            var runLoop = CFRunLoopGetCurrent();
            // kCFRunLoopCommonModes
            var commonModes = GetCFRunLoopCommonModes();
            CFRunLoopAddSource(runLoop, _runLoopSource, commonModes);
            CGEventTapEnable(_eventTap, true);
            CFRunLoopRun();
        }

        private IntPtr HandleEvent(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo)
        {
            // kCGEventKeyDown = 10, kCGEventKeyUp = 11
            if (type != 10 && type != 11) return eventRef;

            // kCGKeyboardEventKeycode = 9
            long keyCode = CGEventGetIntegerValueField(eventRef, 9);
            var settings = _settingsService.CurrentSettings;

            bool swallow = false;

            if (VkToMacKeyCode.TryGetValue(settings.HotkeyVirtualKeyCode, out ushort recordHotkey)
                && keyCode == recordHotkey)
            {
                swallow = true;
                if (type == 10) // keyDown
                {
                    if (!_isHotkeyTracking)
                    {
                        _isHotkeyTracking = true;
                        Avalonia.Threading.Dispatcher.UIThread.Post(_onKeyDown);
                    }
                }
                else // keyUp
                {
                    if (_isHotkeyTracking)
                    {
                        _isHotkeyTracking = false;
                        Avalonia.Threading.Dispatcher.UIThread.Post(_onKeyUp);
                    }
                }
            }
            else if (VkToMacKeyCode.TryGetValue(settings.StopAndSendHotkeyVirtualKeyCode, out ushort stopHotkey)
                && keyCode == stopHotkey)
            {
                swallow = true;
                if (type == 10) // keyDown
                {
                    if (!_isStopAndSendHotkeyTracking)
                    {
                        _isStopAndSendHotkeyTracking = true;
                        Avalonia.Threading.Dispatcher.UIThread.Post(_onStopAndSendKeyDown);
                    }
                }
                else // keyUp
                {
                    if (_isStopAndSendHotkeyTracking)
                    {
                        _isStopAndSendHotkeyTracking = false;
                        Avalonia.Threading.Dispatcher.UIThread.Post(_onStopAndSendKeyUp);
                    }
                }
            }

            // Return IntPtr.Zero to swallow the event, or the event itself to pass it through.
            return swallow ? IntPtr.Zero : eventRef;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_eventTap != IntPtr.Zero)
            {
                CGEventTapEnable(_eventTap, false);
            }
            // CFRunLoop will exit when the source is removed; for simplicity we let
            // the background thread die when the process exits (it's a daemon thread).
        }

        #region P/Invoke

        private delegate IntPtr CGEventTapCallBack(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo);

        [DllImport(CoreGraphics)]
        private static extern IntPtr CGEventTapCreate(uint tap, uint place, uint options, ulong eventsOfInterest,
            CGEventTapCallBack callback, IntPtr userInfo);

        [DllImport(CoreGraphics)]
        private static extern void CGEventTapEnable(IntPtr tap, bool enable);

        [DllImport(CoreGraphics)]
        private static extern long CGEventGetIntegerValueField(IntPtr eventRef, uint field);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, IntPtr order);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFRunLoopGetCurrent();

        [DllImport(CoreFoundation)]
        private static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

        [DllImport(CoreFoundation)]
        private static extern void CFRunLoopRun();

        [DllImport(CoreFoundation, EntryPoint = "kCFRunLoopCommonModes")]
        private static extern IntPtr kCFRunLoopCommonModes_Symbol();

        private static IntPtr GetCFRunLoopCommonModes()
        {
            // The symbol is a global CFStringRef. Read it via dlsym.
            var handle = NativeLibrary.Load(CoreFoundation);
            try
            {
                if (NativeLibrary.TryGetExport(handle, "kCFRunLoopCommonModes", out var addr))
                {
                    return Marshal.ReadIntPtr(addr);
                }
            }
            finally
            {
                NativeLibrary.Free(handle);
            }
            return IntPtr.Zero;
        }

        #endregion
    }
}
