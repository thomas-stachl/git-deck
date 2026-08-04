using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Services;
using System;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

/// <summary>
/// One hotkey row in Settings: the capture box, its label and the registration status underneath.
/// </summary>
public partial class HotkeyEditorViewModel : ObservableObject
{
    private readonly Func<KeyGesture?, HotkeyRegistration> _apply;
    private bool _isSettingSilently;

    public HotkeyEditorViewModel(
        string label,
        KeyGesture? hotkey,
        HotkeyRegistration registration,
        Func<KeyGesture?, HotkeyRegistration> apply)
    {
        Label = label;
        _apply = apply;

        // The gesture arrives already registered (hotkeys are applied at startup), so setting it
        // here must not re-apply it.
        _isSettingSilently = true;
        Hotkey = hotkey;
        _isSettingSilently = false;

        Status = Describe(registration);
    }

    public string Label { get; }

    [ObservableProperty]
    public partial KeyGesture? Hotkey { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [RelayCommand]
    private void Clear()
    {
        Hotkey = null;
    }

    /// <summary>
    /// The single apply path: persisting and registering both hang off the property change, so a
    /// captured gesture can never be applied twice.
    /// </summary>
    partial void OnHotkeyChanged(KeyGesture? oldValue, KeyGesture? newValue)
    {
        if (_isSettingSilently)
        {
            return;
        }

        // The capture box hands over whatever was pressed; a gesture without a modifier must not
        // be registered system-wide, so it is bounced back to the previous one.
        if (newValue is not null && !Hotkeys.IsValid(newValue))
        {
            _isSettingSilently = true;
            Hotkey = oldValue;
            _isSettingSilently = false;

            Status = Hotkeys.ModifierRequiredMessage;
            return;
        }

        _ = ApplyAsync(newValue);
    }

    private async Task ApplyAsync(KeyGesture? gesture)
    {
        // Applying waits on the hotkey thread and can block up to its timeout; keep that wait off
        // the UI thread so the settings window stays responsive.
        Status = Describe(await Task.Run(() => _apply(gesture)));
    }

    private static string Describe(HotkeyRegistration registration) => registration switch
    {
        { IsRegistered: true } => "Hotkey is active.",
        { ErrorMessage: { } error } => error,
        _ => "No hotkey set. Click the box and press a combination.",
    };
}
