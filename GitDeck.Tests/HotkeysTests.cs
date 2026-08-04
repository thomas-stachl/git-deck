using Avalonia.Input;
using GitDeck.App.Services;
using Xunit;

namespace GitDeck.Tests;

public class HotkeysTests
{
    [Fact]
    public void FormatUsesKeyboardNames()
    {
        var gesture = new KeyGesture(Key.G, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.Equal("Ctrl+Alt+G", Hotkeys.Format(gesture));
    }

    [Theory]
    [InlineData(Key.D5, "5")]
    [InlineData(Key.NumPad5, "Num 5")]
    [InlineData(Key.Return, "Enter")]
    [InlineData(Key.OemComma, ",")]
    public void FormatNamesSpecialKeys(Key key, string expected)
    {
        var gesture = new KeyGesture(key, KeyModifiers.Control);

        Assert.Equal($"Ctrl+{expected}", Hotkeys.Format(gesture));
    }

    [Fact]
    public void TryParseRoundTripsStoredForm()
    {
        // Settings store KeyGesture.ToString(); parsing it back must yield the same gesture.
        var gesture = new KeyGesture(Key.C, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.Equal(gesture, Hotkeys.TryParse(gesture.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a gesture +++")]
    [InlineData("G")] // a bare key must not become a global hotkey
    public void TryParseRejectsUnusableInput(string? text)
    {
        Assert.Null(Hotkeys.TryParse(text));
    }

    [Fact]
    public void IsValidRequiresModifierAndNonModifierKey()
    {
        Assert.True(Hotkeys.IsValid(new KeyGesture(Key.G, KeyModifiers.Control)));
        Assert.False(Hotkeys.IsValid(new KeyGesture(Key.G, KeyModifiers.None)));
        Assert.False(Hotkeys.IsValid(new KeyGesture(Key.LeftCtrl, KeyModifiers.Control)));
        Assert.False(Hotkeys.IsValid(null));
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.LWin)]
    public void IsModifierRecognisesModifierKeys(Key key)
    {
        Assert.True(Hotkeys.IsModifier(key));
    }

    [Fact]
    public void IsModifierRejectsOrdinaryKeys()
    {
        Assert.False(Hotkeys.IsModifier(Key.G));
    }
}
