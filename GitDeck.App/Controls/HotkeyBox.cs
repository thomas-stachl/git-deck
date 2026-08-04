using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using GitDeck.App.Services;
using System;

namespace GitDeck.App.Controls;

/// <summary>
/// A capture field for global hotkeys: while it has focus, key presses set <see cref="Gesture"/>
/// instead of doing what they normally would. The shown text always comes from the gesture, so the
/// box itself is read-only.
/// </summary>
public class HotkeyBox : TextBox
{
    public static readonly StyledProperty<KeyGesture?> GestureProperty =
        AvaloniaProperty.Register<HotkeyBox, KeyGesture?>(nameof(Gesture), defaultBindingMode: BindingMode.TwoWay);

    public HotkeyBox()
    {
        IsReadOnly = true;
    }

    public KeyGesture? Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    // Reuse TextBox's template and theme rather than requiring one of our own.
    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Leave keyboard navigation intact, so the box can still be tabbed out of.
        if (e.Key is Key.Tab && e.KeyModifiers is KeyModifiers.None)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;

        // A modifier on its own is the start of a combination, not a combination.
        if (Hotkeys.IsModifier(e.Key))
        {
            return;
        }

        // Validity (a bare key must not become a system-wide hotkey) is the view model's rule;
        // it bounces invalid gestures back through the two-way binding.
        Gesture = new KeyGesture(e.Key, e.KeyModifiers);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GestureProperty)
        {
            Text = change.GetNewValue<KeyGesture?>() is { } gesture ? Hotkeys.Format(gesture) : string.Empty;
        }
    }
}
