using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GitDeck.App.Services;

/// <summary>
/// Registers GitDeck's system-wide hotkeys through Win32 <c>RegisterHotKey</c>.
/// </summary>
/// <remarks>
/// The registrations live on a dedicated thread with its own message loop. <c>RegisterHotKey</c>
/// with a null window delivers <c>WM_HOTKEY</c> to the message queue of the thread that registered
/// it, and Avalonia's own loop does not surface those messages — so GitDeck runs a small loop of its
/// own and marshals presses back to the UI thread.
/// </remarks>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const uint WmApply = PInvoke.WM_APP + 1;
    private const uint WmStop = PInvoke.WM_APP + 2;

    // Without this, holding the combination down repeats it as fast as the key repeat rate.
    private const HOT_KEY_MODIFIERS ModNoRepeat = (HOT_KEY_MODIFIERS)0x4000;

    private static readonly TimeSpan ApplyTimeout = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly ManualResetEventSlim _threadReady = new(false);
    private readonly Dictionary<HotkeyAction, KeyGesture?> _hotkeys = [];
    private readonly Dictionary<HotkeyAction, HotkeyRegistration> _results = [];

    private Thread? _thread;
    private uint _threadId;
    private ApplyRequest? _pending;
    private bool _isDisposed;

    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    public KeyGesture? GetHotkey(HotkeyAction action)
    {
        lock (_gate)
        {
            return _hotkeys.GetValueOrDefault(action);
        }
    }

    public HotkeyRegistration GetLastResult(HotkeyAction action)
    {
        lock (_gate)
        {
            return _results.GetValueOrDefault(action, HotkeyRegistration.None);
        }
    }

    public HotkeyRegistration Apply(HotkeyAction action, KeyGesture? hotkey)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            // Keep the configured gesture even when registering it fails, so Settings still shows
            // what the user chose alongside the reason it is not active.
            _hotkeys[action] = hotkey;

            var result = ApplyCore(action, hotkey);
            _results[action] = result;

            return result;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_thread is not null)
            {
                PInvoke.PostThreadMessage(_threadId, WmStop, default, default);
                _thread.Join(ApplyTimeout);
                _thread = null;
            }
        }

        _threadReady.Dispose();
    }

    private HotkeyRegistration ApplyCore(HotkeyAction action, KeyGesture? hotkey)
    {
        var hotkeyId = ToHotkeyId(action);

        if (hotkey is null)
        {
            // Nothing to register, but a previous registration still has to be released.
            if (_thread is not null)
            {
                Send(new ApplyRequest(hotkeyId, 0, 0));
            }

            return HotkeyRegistration.None;
        }

        if (!Hotkeys.IsValid(hotkey))
        {
            return HotkeyRegistration.Failed(Hotkeys.ModifierRequiredMessage);
        }

        // Windows would reject the duplicate anyway, but with a message blaming another application.
        if (FindConflict(action, hotkey) is { } conflict)
        {
            return HotkeyRegistration.Failed(
                $"{Hotkeys.Format(hotkey)} is already used for {Hotkeys.Describe(conflict)}.");
        }

        if (ToVirtualKey(hotkey.Key) is not { } virtualKey)
        {
            return HotkeyRegistration.Failed($"The key \"{hotkey.Key}\" cannot be used in a global hotkey.");
        }

        var request = Send(new ApplyRequest(hotkeyId, ToModifiers(hotkey.KeyModifiers) | ModNoRepeat, virtualKey));

        if (request is null)
        {
            return HotkeyRegistration.Failed("The hotkey could not be registered: GitDeck's hotkey thread did not respond.");
        }

        return request.IsRegistered
            ? HotkeyRegistration.Registered
            : HotkeyRegistration.Failed(Describe(request.Error, hotkey));
    }

    private HotkeyAction? FindConflict(HotkeyAction action, KeyGesture hotkey) => _hotkeys
        .Where(entry => entry.Key != action && hotkey.Equals(entry.Value))
        .Select(entry => (HotkeyAction?)entry.Key)
        .FirstOrDefault();

    /// <summary>
    /// Hands a request to the message-loop thread and waits for it, since only that thread may own
    /// the registration. Returns null if the thread never answered.
    /// </summary>
    private ApplyRequest? Send(ApplyRequest request)
    {
        EnsureThread();

        _pending = request;

        if (!PInvoke.PostThreadMessage(_threadId, WmApply, default, default))
        {
            return null;
        }

        return request.Completed.Wait(ApplyTimeout) ? request : null;
    }

    private void EnsureThread()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(RunMessageLoop)
        {
            Name = "GitDeck global hotkeys",
            IsBackground = true,
        };

        _thread.Start();
        _threadReady.Wait();
    }

    private void RunMessageLoop()
    {
        _threadId = PInvoke.GetCurrentThreadId();

        // Force the message queue into existence before anyone posts to it.
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);

        _threadReady.Set();

        var registeredIds = new List<int>();

        try
        {
            // GetMessage returns -1 on failure, so the raw value has to be compared, not the BOOL.
            while (PInvoke.GetMessage(out var native, HWND.Null, 0, 0).Value > 0)
            {
                switch (native.message)
                {
                    case PInvoke.WM_HOTKEY:
                        // WM_HOTKEY carries the id that was registered in wParam.
                        OnHotkeyMessage((int)native.wParam.Value);
                        break;

                    case WmApply:
                        ApplyPending(registeredIds);
                        break;

                    case WmStop:
                        return;
                }
            }
        }
        finally
        {
            foreach (var hotkeyId in registeredIds)
            {
                PInvoke.UnregisterHotKey(HWND.Null, hotkeyId);
            }
        }
    }

    /// <remarks>
    /// Deliberately takes no lock. This runs on the message-loop thread, which is the same thread
    /// <see cref="Apply"/> is blocked waiting on while holding <c>_gate</c> — reaching for the lock
    /// here would stall that registration until it times out.
    /// </remarks>
    private void OnHotkeyMessage(int hotkeyId)
    {
        if (FromHotkeyId(hotkeyId) is not { } action)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => Pressed?.Invoke(this, new HotkeyPressedEventArgs(action)));
    }

    private void ApplyPending(List<int> registeredIds)
    {
        if (_pending is not { } request)
        {
            return;
        }

        _pending = null;

        PInvoke.UnregisterHotKey(HWND.Null, request.HotkeyId);
        registeredIds.Remove(request.HotkeyId);

        if (request.VirtualKey == 0)
        {
            // Unregister-only.
            request.IsRegistered = false;
            request.Completed.Set();
            return;
        }

        request.IsRegistered = PInvoke.RegisterHotKey(HWND.Null, request.HotkeyId, request.Modifiers, request.VirtualKey);
        request.Error = request.IsRegistered ? WIN32_ERROR.NO_ERROR : (WIN32_ERROR)Marshal.GetLastPInvokeError();

        if (request.IsRegistered)
        {
            registeredIds.Add(request.HotkeyId);
        }

        request.Completed.Set();
    }

    // Win32 hotkey ids are per-thread and arbitrary; one per action keeps them independent, and
    // deriving them means a WM_HOTKEY can be mapped back without consulting shared state.
    private static int ToHotkeyId(HotkeyAction action) => 1 + (int)action;

    private static HotkeyAction? FromHotkeyId(int hotkeyId)
    {
        var action = (HotkeyAction)(hotkeyId - 1);

        return Enum.IsDefined(action) ? action : null;
    }

    private static string Describe(WIN32_ERROR error, KeyGesture hotkey) => error == WIN32_ERROR.ERROR_HOTKEY_ALREADY_REGISTERED
        ? $"{Hotkeys.Format(hotkey)} is already in use by another application. Pick a different combination."
        : $"Windows refused the hotkey {Hotkeys.Format(hotkey)} ({error}).";

    private static HOT_KEY_MODIFIERS ToModifiers(KeyModifiers modifiers)
    {
        var result = default(HOT_KEY_MODIFIERS);

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            result |= HOT_KEY_MODIFIERS.MOD_CONTROL;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result |= HOT_KEY_MODIFIERS.MOD_ALT;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result |= HOT_KEY_MODIFIERS.MOD_SHIFT;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            result |= HOT_KEY_MODIFIERS.MOD_WIN;
        }

        return result;
    }

    /// <summary>
    /// Translates an Avalonia key into a Win32 virtual-key code, or null for keys that have none.
    /// The ranges below are contiguous in both enums, which is what makes the arithmetic valid.
    /// </summary>
    private static uint? ToVirtualKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => 0x41u + (uint)(key - Key.A),
        >= Key.D0 and <= Key.D9 => 0x30u + (uint)(key - Key.D0),
        >= Key.NumPad0 and <= Key.NumPad9 => 0x60u + (uint)(key - Key.NumPad0),
        >= Key.F1 and <= Key.F24 => 0x70u + (uint)(key - Key.F1),

        Key.Back => 0x08,
        Key.Tab => 0x09,
        Key.Return => 0x0D,
        Key.Escape => 0x1B,
        Key.Space => 0x20,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.End => 0x23,
        Key.Home => 0x24,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,

        Key.Multiply => 0x6A,
        Key.Add => 0x6B,
        Key.Subtract => 0x6D,
        Key.Decimal => 0x6E,
        Key.Divide => 0x6F,

        Key.OemSemicolon => 0xBA,
        Key.OemPlus => 0xBB,
        Key.OemComma => 0xBC,
        Key.OemMinus => 0xBD,
        Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF,
        Key.OemTilde => 0xC0,
        Key.OemOpenBrackets => 0xDB,
        Key.OemPipe => 0xDC,
        Key.OemCloseBrackets => 0xDD,
        Key.OemQuotes => 0xDE,
        Key.OemBackslash => 0xE2,

        _ => null,
    };

    private sealed class ApplyRequest(int hotkeyId, HOT_KEY_MODIFIERS modifiers, uint virtualKey)
    {
        public int HotkeyId { get; } = hotkeyId;

        public HOT_KEY_MODIFIERS Modifiers { get; } = modifiers;

        public uint VirtualKey { get; } = virtualKey;

        public ManualResetEventSlim Completed { get; } = new(false);

        public bool IsRegistered { get; set; }

        public WIN32_ERROR Error { get; set; }
    }
}
