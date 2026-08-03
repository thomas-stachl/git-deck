using Avalonia.Input;
using System;

namespace GitDeck.App.Services;

/// <summary>What a global hotkey opens the run window for.</summary>
public enum HotkeyAction
{
    Branches,
    Commit,
}

public sealed class HotkeyPressedEventArgs(HotkeyAction action) : EventArgs
{
    public HotkeyAction Action { get; } = action;
}

/// <param name="ErrorMessage">
/// Why the hotkey is not active. Null together with <c>IsRegistered: false</c> means no hotkey is
/// configured, which is not an error.
/// </param>
public sealed record HotkeyRegistration(bool IsRegistered, string? ErrorMessage)
{
    public static readonly HotkeyRegistration None = new(false, null);

    public static readonly HotkeyRegistration Registered = new(true, null);

    public static HotkeyRegistration Failed(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Owns GitDeck's system-wide hotkeys, one per <see cref="HotkeyAction"/>.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>Raised on the UI thread when one of the hotkeys is pressed.</summary>
    event EventHandler<HotkeyPressedEventArgs>? Pressed;

    /// <summary>
    /// The gesture configured for an action, which is set even when registering it failed.
    /// </summary>
    KeyGesture? GetHotkey(HotkeyAction action);

    /// <summary>The outcome of the most recent <see cref="Apply"/> for an action.</summary>
    HotkeyRegistration GetLastResult(HotkeyAction action);

    /// <summary>
    /// Registers <paramref name="hotkey"/> for <paramref name="action"/>, replacing that action's
    /// previous registration. Passing <c>null</c> leaves the action without a hotkey.
    /// </summary>
    HotkeyRegistration Apply(HotkeyAction action, KeyGesture? hotkey);
}
