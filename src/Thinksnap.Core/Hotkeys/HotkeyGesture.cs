namespace Thinksnap.Core.Hotkeys;

[Flags]
public enum HotkeyModifier
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed record HotkeyGesture(string Key, uint VirtualKey, HotkeyModifier Modifiers)
{
    public const string DefaultCaptureHotkey = "PrintScreen";

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(HotkeyModifier.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(HotkeyModifier.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(HotkeyModifier.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(HotkeyModifier.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(Key);
            return string.Join("+", parts);
        }
    }

    public uint WindowsModifierFlags => (uint)Modifiers;

    public override string ToString() => DisplayText;

    public static HotkeyGesture DefaultCapture() => new("PrintScreen", 0x2C, HotkeyModifier.None);

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = DefaultCapture();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var modifiers = HotkeyModifier.None;
        string? keyToken = null;
        foreach (var rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (rawPart.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= HotkeyModifier.Control;
                    break;
                case "ALT":
                    modifiers |= HotkeyModifier.Alt;
                    break;
                case "SHIFT":
                    modifiers |= HotkeyModifier.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= HotkeyModifier.Windows;
                    break;
                default:
                    if (keyToken is not null)
                    {
                        return false;
                    }

                    keyToken = rawPart;
                    break;
            }
        }

        if (keyToken is null || TryNormalizeKey(keyToken, out var key, out var virtualKey) is false)
        {
            return false;
        }

        gesture = new HotkeyGesture(key, virtualKey, modifiers);
        return true;
    }

    public static HotkeyGesture ParseOrDefault(string? value)
    {
        return TryParse(value, out var gesture) ? gesture : DefaultCapture();
    }

    private static bool TryNormalizeKey(string token, out string key, out uint virtualKey)
    {
        key = string.Empty;
        virtualKey = 0;

        var normalized = token.Trim();
        if (normalized.Length == 1)
        {
            var character = char.ToUpperInvariant(normalized[0]);
            if (character is >= 'A' and <= 'Z')
            {
                key = character.ToString();
                virtualKey = character;
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                key = character.ToString();
                virtualKey = character;
                return true;
            }
        }

        var upper = normalized.ToUpperInvariant();
        if (upper is "PRINTSCREEN" or "PRTSC" or "PRTSCN" or "SNAPSHOT")
        {
            key = "PrintScreen";
            virtualKey = 0x2C;
            return true;
        }

        if (upper.StartsWith('F') &&
            int.TryParse(upper[1..], out var functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            key = $"F{functionKey}";
            virtualKey = (uint)(0x70 + functionKey - 1);
            return true;
        }

        return false;
    }
}
