using Avalonia.Input;
using System;
using System.Collections.Generic;

namespace GitDeck.App.Services;

/// <summary>
/// Stands in on platforms where GitDeck has no global hotkey implementation yet. It remembers the
/// configured gestures so Settings still round-trips them, but never registers anything.
/// </summary>
public sealed class UnsupportedGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly Dictionary<HotkeyAction, KeyGesture?> _hotkeys = [];
    private readonly Dictionary<HotkeyAction, HotkeyRegistration> _results = [];

    /// <summary>Never raised: nothing is listening to the keyboard on this platform.</summary>
#pragma warning disable CS0067
    public event EventHandler<HotkeyPressedEventArgs>? Pressed;
#pragma warning restore CS0067

    public KeyGesture? GetHotkey(HotkeyAction action) => _hotkeys.GetValueOrDefault(action);

    public HotkeyRegistration GetLastResult(HotkeyAction action) =>
        _results.GetValueOrDefault(action, HotkeyRegistration.None);

    public HotkeyRegistration Apply(HotkeyAction action, KeyGesture? hotkey)
    {
        _hotkeys[action] = hotkey;
        _results[action] = hotkey is null
            ? HotkeyRegistration.None
            : HotkeyRegistration.Failed($"Global hotkeys are not supported on {Environment.OSVersion.Platform} yet.");

        return _results[action];
    }
}
