using Avalonia.Input;
using System;

namespace GitDeck.App.Services;

/// <param name="ErrorMessage">
/// Why the hotkey is not active. Null together with <c>IsRegistered: false</c> means no hotkey is
/// configured, which is not an error.
/// </param>
public sealed record HotkeyRegistration(bool IsRegistered, string? ErrorMessage)
{
    public static readonly HotkeyRegistration None = new(false, null);

    public static HotkeyRegistration Failed(string errorMessage) => new(false, errorMessage);

    public static readonly HotkeyRegistration Registered = new(true, null);
}

/// <summary>
/// Owns the single system-wide hotkey that shows the run window.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>Raised on the UI thread when the hotkey is pressed.</summary>
    event EventHandler? Pressed;

    /// <summary>The configured hotkey, which is set even when registering it failed.</summary>
    KeyGesture? Current { get; }

    /// <summary>The outcome of the most recent <see cref="Apply"/>.</summary>
    HotkeyRegistration LastResult { get; }

    /// <summary>
    /// Registers <paramref name="hotkey"/>, replacing any previous registration. Passing
    /// <c>null</c> leaves no hotkey registered.
    /// </summary>
    HotkeyRegistration Apply(KeyGesture? hotkey);
}
