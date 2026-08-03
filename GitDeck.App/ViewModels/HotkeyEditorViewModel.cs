using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Services;
using System;

namespace GitDeck.App.ViewModels;

/// <summary>
/// One hotkey row in Settings: the capture box, its label and the registration status underneath.
/// </summary>
public partial class HotkeyEditorViewModel : ObservableObject
{
    private readonly Func<KeyGesture?, HotkeyRegistration> _apply;

    public HotkeyEditorViewModel(
        string label,
        KeyGesture? hotkey,
        HotkeyRegistration registration,
        Func<KeyGesture?, HotkeyRegistration> apply)
    {
        Label = label;
        _apply = apply;

        Hotkey = hotkey;
        Status = Describe(registration);
    }

    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    public partial KeyGesture? Hotkey { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    public string Display => Hotkey is { } hotkey ? Hotkeys.Format(hotkey) : string.Empty;

    /// <summary>Called by the settings view when the user presses a combination in the capture box.</summary>
    public void Capture(KeyGesture gesture)
    {
        if (!Hotkeys.IsValid(gesture))
        {
            Status = Hotkeys.ModifierRequiredMessage;
            return;
        }

        Hotkey = gesture;

        // Re-pressing the gesture already configured leaves Hotkey unchanged and so raises no
        // change notification; refresh the status anyway so the box does not look inert.
        Status = Describe(_apply(gesture));
    }

    [RelayCommand]
    private void Clear()
    {
        Hotkey = null;
    }

    partial void OnHotkeyChanged(KeyGesture? value)
    {
        Status = Describe(_apply(value));
    }

    private static string Describe(HotkeyRegistration registration) => registration switch
    {
        { IsRegistered: true } => "Hotkey is active.",
        { ErrorMessage: { } error } => error,
        _ => "No hotkey set. Click the box and press a combination.",
    };
}
