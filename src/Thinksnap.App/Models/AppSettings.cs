using Thinksnap.Core.Updates;

namespace Thinksnap.App.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";

    public string ThemePreset { get; set; } = "Midnight";

    public string AccentColor { get; set; } = "#2F80ED";

    public string CaptureHotkey { get; set; } = "PrintScreen";

    public bool ShowFloatingCaptureButton { get; set; } = true;

    public bool MinimizeToTrayOnClose { get; set; } = true;

    public double CaptureButtonSize { get; set; } = 54;

    public double OverlayToolbarButtonSize { get; set; } = 44;

    public double? FloatingCaptureLeft { get; set; }

    public double? FloatingCaptureTop { get; set; }

    public int CaptureDelayMilliseconds { get; set; } = 150;

    public bool ShowSelectionInstructions { get; set; } = true;

    public double OverlayDimOpacity { get; set; } = 0.55;

    public string DefaultRedactionStyle { get; set; } = "Pixelate";

    public double DefaultStrokeThickness { get; set; } = 3;

    public double DefaultTextFontSize { get; set; } = 18;

    public string DefaultTextColor { get; set; } = "#ff0000";

    public bool DefaultTextBold { get; set; }

    public string DefaultTextAlignment { get; set; } = "Left";

    public bool DefaultTextBackgroundEnabled { get; set; }

    public string DefaultTextBackgroundColor { get; set; } = "#d2000000";

    public bool ShowTextMoveHandles { get; set; } = true;

    public string DefaultFileNamePattern { get; set; } = "thinksnap-{yyyyMMdd-HHmmss}.png";

    public bool CopyAfterSave { get; set; }

    public string UpdateUrl { get; set; } = UpdateTarget.DefaultUrl;

    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Stable;

    public bool AutomaticallyCheckForUpdates { get; set; } = true;

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Language = Language,
            ThemePreset = ThemePreset,
            AccentColor = AccentColor,
            CaptureHotkey = CaptureHotkey,
            ShowFloatingCaptureButton = ShowFloatingCaptureButton,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            CaptureButtonSize = CaptureButtonSize,
            OverlayToolbarButtonSize = OverlayToolbarButtonSize,
            FloatingCaptureLeft = FloatingCaptureLeft,
            FloatingCaptureTop = FloatingCaptureTop,
            CaptureDelayMilliseconds = CaptureDelayMilliseconds,
            ShowSelectionInstructions = ShowSelectionInstructions,
            OverlayDimOpacity = OverlayDimOpacity,
            DefaultRedactionStyle = DefaultRedactionStyle,
            DefaultStrokeThickness = DefaultStrokeThickness,
            DefaultTextFontSize = DefaultTextFontSize,
            DefaultTextColor = DefaultTextColor,
            DefaultTextBold = DefaultTextBold,
            DefaultTextAlignment = DefaultTextAlignment,
            DefaultTextBackgroundEnabled = DefaultTextBackgroundEnabled,
            DefaultTextBackgroundColor = DefaultTextBackgroundColor,
            ShowTextMoveHandles = ShowTextMoveHandles,
            DefaultFileNamePattern = DefaultFileNamePattern,
            CopyAfterSave = CopyAfterSave,
            UpdateUrl = UpdateUrl,
            UpdateChannel = UpdateChannel,
            AutomaticallyCheckForUpdates = AutomaticallyCheckForUpdates,
            LastUpdateCheckUtc = LastUpdateCheckUtc
        };
    }
}
