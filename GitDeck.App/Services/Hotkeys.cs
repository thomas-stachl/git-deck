using Avalonia.Input;
using System;
using System.Collections.Generic;

namespace GitDeck.App.Services;

public static class Hotkeys
{
    /// <summary>
    /// Formats a gesture the way it is printed on a keyboard. This is for display only — the stored
    /// setting keeps <see cref="KeyGesture.ToString"/>, which is what <see cref="TryParse"/> reads.
    /// </summary>
    public static string Format(KeyGesture gesture)
    {
        var parts = new List<string>(5);

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        parts.Add(FormatKey(gesture.Key));

        return string.Join("+", parts);
    }

    private static string FormatKey(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Num {(char)('0' + (key - Key.NumPad0))}",

        Key.Return => "Enter",
        Key.Back => "Backspace",
        Key.Escape => "Esc",
        Key.PageUp => "Page Up",
        Key.PageDown => "Page Down",

        Key.Add => "Num +",
        Key.Subtract => "Num -",
        Key.Multiply => "Num *",
        Key.Divide => "Num /",
        Key.Decimal => "Num .",

        Key.OemSemicolon => ";",
        Key.OemPlus => "+",
        Key.OemComma => ",",
        Key.OemMinus => "-",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemTilde => "`",
        Key.OemOpenBrackets => "[",
        Key.OemPipe => "\\",
        Key.OemCloseBrackets => "]",
        Key.OemQuotes => "'",
        Key.OemBackslash => "\\",

        _ => key.ToString(),
    };

    public const string ModifierRequiredMessage =
        "A hotkey needs at least one modifier (Ctrl, Alt, Shift or Win) together with another key.";

    /// <summary>Names an action in messages the user reads.</summary>
    public static string Describe(HotkeyAction action) => action switch
    {
        HotkeyAction.Branches => "switching branches",
        HotkeyAction.Commit => "committing changes",
        _ => action.ToString(),
    };

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
