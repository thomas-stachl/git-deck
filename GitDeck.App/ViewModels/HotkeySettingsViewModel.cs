using Avalonia.Input;
using GitDeck.App.Design;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using System.Collections.Generic;

namespace GitDeck.App.ViewModels;

/// <summary>The hotkeys section of Settings: one editor row per action, plus their persistence.</summary>
public sealed class HotkeySettingsViewModel
{
    private readonly ISettingsService _settingsService;

    public HotkeySettingsViewModel(ISettingsService settingsService, IGlobalHotkeyService hotkeyService)
    {
        _settingsService = settingsService;

        // The hotkeys are registered at startup, so the editors start from what the hotkey service
        // actually holds rather than re-reading the settings.
        Editors =
        [
            CreateEditor(hotkeyService, HotkeyAction.Branches, "Switch branches"),
            CreateEditor(hotkeyService, HotkeyAction.Commit, "Commit changes"),
        ];
    }

    // Parameterless constructor required by the Avalonia XAML previewer/designer.
    public HotkeySettingsViewModel() : this(new DesignSettingsService(), new DesignGlobalHotkeyService())
    {
    }

    public IReadOnlyList<HotkeyEditorViewModel> Editors { get; }

    private HotkeyEditorViewModel CreateEditor(IGlobalHotkeyService hotkeyService, HotkeyAction action, string label) =>
        new(label,
            hotkeyService.GetHotkey(action),
            hotkeyService.GetLastResult(action),
            gesture =>
            {
                Persist(action, gesture);
                return hotkeyService.Apply(action, gesture);
            });

    private void Persist(HotkeyAction action, KeyGesture? gesture)
    {
        // The stored form is KeyGesture.ToString(), which is what Hotkeys.TryParse reads back.
        var stored = gesture?.ToString();

        switch (action)
        {
            case HotkeyAction.Branches:
                _settingsService.Settings.BranchHotkey = stored;
                break;

            case HotkeyAction.Commit:
                _settingsService.Settings.CommitHotkey = stored;
                break;
        }

        _settingsService.Save();
    }
}
