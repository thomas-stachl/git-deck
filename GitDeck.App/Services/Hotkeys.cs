using Avalonia.Input;
using System;

namespace GitDeck.App.Services;

public static class Hotkeys
{
    public const string ModifierRequiredMessage =
        "A hotkey needs at least one modifier (Ctrl, Alt, Shift or Win) together with another key.";

    /// <summary>
    /// Whether a gesture is usable as a global hotkey. A bare key is rejected: registering one
    /// system-wide would take it away from every other application.
    /// </summary>
    public static bool IsValid(KeyGesture? gesture) =>
        gesture is not null
        && gesture.KeyModifiers != KeyModifiers.None
        && gesture.Key != Key.None
        && !IsModifier(gesture.Key);

    /// <summary>Whether a key is only a modifier, and so cannot complete a gesture on its own.</summary>
    public static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin
        or Key.System;

    /// <summary>Parses a stored gesture such as "Ctrl+Alt+G", returning null if it is unusable.</summary>
    public static KeyGesture? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var gesture = KeyGesture.Parse(text);
            return IsValid(gesture) ? gesture : null;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }
}
