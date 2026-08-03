using Avalonia.Input;
using GitDeck.App.Services;
using System;

namespace GitDeck.App.Design;

internal sealed class DesignGlobalHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? Pressed
    {
        add { }
        remove { }
    }

    public KeyGesture? Current { get; private set; } = new(Key.G, KeyModifiers.Control | KeyModifiers.Alt);

    public HotkeyRegistration LastResult { get; private set; } = HotkeyRegistration.Registered;

    public HotkeyRegistration Apply(KeyGesture? hotkey)
    {
        Current = hotkey;
        LastResult = hotkey is null ? HotkeyRegistration.None : HotkeyRegistration.Registered;

        return LastResult;
    }
}
