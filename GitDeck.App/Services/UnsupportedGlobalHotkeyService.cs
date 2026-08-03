using Avalonia.Input;
using System;

namespace GitDeck.App.Services;

/// <summary>
/// Stands in on platforms where GitDeck has no global hotkey implementation yet. It remembers the
/// configured gesture so Settings still round-trips it, but never registers anything.
/// </summary>
public sealed class UnsupportedGlobalHotkeyService : IGlobalHotkeyService
{
    /// <summary>Never raised: nothing is listening to the keyboard on this platform.</summary>
#pragma warning disable CS0067
    public event EventHandler? Pressed;
#pragma warning restore CS0067
    public KeyGesture? Current { get; private set; }

    public HotkeyRegistration LastResult { get; private set; } = HotkeyRegistration.None;

    public HotkeyRegistration Apply(KeyGesture? hotkey)
    {
        Current = hotkey;
        LastResult = hotkey is null
            ? HotkeyRegistration.None
            : HotkeyRegistration.Failed($"Global hotkeys are not supported on {Environment.OSVersion.Platform} yet.");

        return LastResult;
    }
}
