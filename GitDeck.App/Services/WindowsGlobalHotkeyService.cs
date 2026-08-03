using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace GitDeck.App.Services;

/// <summary>
/// Registers a system-wide hotkey through Win32 <c>RegisterHotKey</c>.
/// </summary>
/// <remarks>
/// The registration lives on a dedicated thread with its own message loop. <c>RegisterHotKey</c>
/// with a null window delivers <c>WM_HOTKEY</c> to the message queue of the thread that registered
/// it, and Avalonia's own loop does not surface those messages — so GitDeck runs a small loop of its
/// own and marshals presses back to the UI thread.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const uint WmHotkey = 0x0312;
    private const uint WmApp = 0x8000;
    private const uint WmApply = WmApp + 1;
    private const uint WmStop = WmApp + 2;

    private const uint PmNoRemove = 0x0000;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    // Without this, holding the combination down repeats it as fast as the key repeat rate.
    private const uint ModNoRepeat = 0x4000;

    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private const int HotkeyId = 1;

    private static readonly TimeSpan ApplyTimeout = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly ManualResetEventSlim _threadReady = new(false);

    private Thread? _thread;
    private uint _threadId;
    private ApplyRequest? _pending;
    private bool _isDisposed;

    public event EventHandler? Pressed;

    public KeyGesture? Current { get; private set; }

    public HotkeyRegistration LastResult { get; private set; } = HotkeyRegistration.None;

    public HotkeyRegistration Apply(KeyGesture? hotkey)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            // Keep the configured gesture even when registering it fails, so Settings still shows
            // what the user chose alongside the reason it is not active.
            Current = hotkey;
            LastResult = ApplyCore(hotkey);

            return LastResult;
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
                PostThreadMessage(_threadId, WmStop, UIntPtr.Zero, IntPtr.Zero);
                _thread.Join(ApplyTimeout);
                _thread = null;
            }
        }

        _threadReady.Dispose();
    }

    private HotkeyRegistration ApplyCore(KeyGesture? hotkey)
    {
        if (hotkey is null)
        {
            // Nothing to register, but a previous registration still has to be released.
            if (_thread is not null)
            {
                Send(new ApplyRequest(0, 0));
            }

            return HotkeyRegistration.None;
        }

        if (!Hotkeys.IsValid(hotkey))
        {
            return HotkeyRegistration.Failed(Hotkeys.ModifierRequiredMessage);
        }

        if (ToVirtualKey(hotkey.Key) is not { } virtualKey)
        {
            return HotkeyRegistration.Failed($"The key \"{hotkey.Key}\" cannot be used in a global hotkey.");
        }

        var request = Send(new ApplyRequest(ToModifiers(hotkey.KeyModifiers) | ModNoRepeat, virtualKey));

        if (request is null)
        {
            return HotkeyRegistration.Failed("The hotkey could not be registered: GitDeck's hotkey thread did not respond.");
        }

        return request.IsRegistered
            ? HotkeyRegistration.Registered
            : HotkeyRegistration.Failed(Describe(request.ErrorCode, hotkey));
    }

    /// <summary>
    /// Hands a request to the message-loop thread and waits for it, since only that thread may own
    /// the registration. Returns null if the thread never answered.
    /// </summary>
    private ApplyRequest? Send(ApplyRequest request)
    {
        EnsureThread();

        _pending = request;

        if (!PostThreadMessage(_threadId, WmApply, UIntPtr.Zero, IntPtr.Zero))
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
            Name = "GitDeck global hotkey",
            IsBackground = true,
        };

        _thread.Start();
        _threadReady.Wait();
    }

    private void RunMessageLoop()
    {
        _threadId = GetCurrentThreadId();

        // Force the message queue into existence before anyone posts to it.
        PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);

        _threadReady.Set();

        try
        {
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                switch (message.Value)
                {
                    case WmHotkey:
                        Dispatcher.UIThread.Post(() => Pressed?.Invoke(this, EventArgs.Empty));
                        break;

                    case WmApply:
                        ApplyPending();
                        break;

                    case WmStop:
                        return;
                }
            }
        }
        finally
        {
            UnregisterHotKey(IntPtr.Zero, HotkeyId);
        }
    }

    private void ApplyPending()
    {
        if (_pending is not { } request)
        {
            return;
        }

        _pending = null;

        UnregisterHotKey(IntPtr.Zero, HotkeyId);

        if (request.VirtualKey == 0)
        {
            // Unregister-only.
            request.IsRegistered = false;
            request.Completed.Set();
            return;
        }

        request.IsRegistered = RegisterHotKey(IntPtr.Zero, HotkeyId, request.Modifiers, request.VirtualKey);
        request.ErrorCode = request.IsRegistered ? 0 : Marshal.GetLastWin32Error();
        request.Completed.Set();
    }

    private static string Describe(int errorCode, KeyGesture hotkey) => errorCode == ErrorHotkeyAlreadyRegistered
        ? $"{hotkey} is already in use by another application. Pick a different combination."
        : $"Windows refused the hotkey {hotkey} (error {errorCode}).";

    private static uint ToModifiers(KeyModifiers modifiers)
    {
        var result = 0u;

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            result |= ModControl;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result |= ModAlt;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result |= ModShift;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            result |= ModWin;
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

    private sealed class ApplyRequest(uint modifiers, uint virtualKey)
    {
        public uint Modifiers { get; } = modifiers;

        public uint VirtualKey { get; } = virtualKey;

        public ManualResetEventSlim Completed { get; } = new(false);

        public bool IsRegistered { get; set; }

        public int ErrorCode { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Value;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out NativeMessage message, IntPtr hWnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr hWnd, uint filterMin, uint filterMax, uint removeMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
