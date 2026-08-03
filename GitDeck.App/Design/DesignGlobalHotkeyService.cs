using Avalonia.Input;
using GitDeck.App.Services;
using System;
using System.Collections.Generic;

namespace GitDeck.App.Design;

internal sealed class DesignGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly Dictionary<HotkeyAction, KeyGesture?> _hotkeys = new()
    {
        [HotkeyAction.Branches] = new KeyGesture(Key.G, KeyModifiers.Control | KeyModifiers.Alt),
        [HotkeyAction.Commit] = new KeyGesture(Key.C, KeyModifiers.Control | KeyModifiers.Alt),
    };

    public event EventHandler<HotkeyPressedEventArgs>? Pressed
    {
        add { }
        remove { }
    }

    public KeyGesture? GetHotkey(HotkeyAction action) => _hotkeys.GetValueOrDefault(action);

    public HotkeyRegistration GetLastResult(HotkeyAction action) =>
        _hotkeys.GetValueOrDefault(action) is null ? HotkeyRegistration.None : HotkeyRegistration.Registered;

    public HotkeyRegistration Apply(HotkeyAction action, KeyGesture? hotkey)
    {
        _hotkeys[action] = hotkey;

        return GetLastResult(action);
    }
}
