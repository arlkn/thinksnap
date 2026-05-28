using Thinksnap.Core.Hotkeys;
using Xunit;

namespace Thinksnap.Core.Tests;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("PrintScreen", "PrintScreen", 0x2C, HotkeyModifier.None)]
    [InlineData("PrtSc", "PrintScreen", 0x2C, HotkeyModifier.None)]
    [InlineData("Ctrl+PrintScreen", "Ctrl+PrintScreen", 0x2C, HotkeyModifier.Control)]
    [InlineData("Ctrl+Shift+S", "Ctrl+Shift+S", 0x53, HotkeyModifier.Control | HotkeyModifier.Shift)]
    [InlineData("Alt+F12", "Alt+F12", 0x7B, HotkeyModifier.Alt)]
    public void TryParseNormalizesSupportedHotkeys(string input, string display, uint virtualKey, HotkeyModifier modifiers)
    {
        var parsed = HotkeyGesture.TryParse(input, out var gesture);

        Assert.True(parsed);
        Assert.Equal(display, gesture.DisplayText);
        Assert.Equal(virtualKey, gesture.VirtualKey);
        Assert.Equal(modifiers, gesture.Modifiers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Mouse4")]
    [InlineData("Ctrl+A+B")]
    public void TryParseRejectsUnsupportedHotkeys(string input)
    {
        var parsed = HotkeyGesture.TryParse(input, out _);

        Assert.False(parsed);
    }
}
