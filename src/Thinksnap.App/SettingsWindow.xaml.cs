using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Thinksnap.App.Models;
using Thinksnap.Core.Hotkeys;

namespace Thinksnap.App;

public partial class SettingsWindow : Window
{
    private readonly Func<AppSettings, string?> applySettings;
    private readonly Func<string?, Window?, bool> openUpdatePage;
    private AppSettings settings;
    private System.Windows.Controls.Button? activeCategoryButton;
    private bool isLoading = true;
    private bool isRecordingHotkey;

    public SettingsWindow(
        AppSettings settings,
        Func<AppSettings, string?> applySettings,
        Func<string?, Window?, bool> openUpdatePage)
    {
        this.settings = settings;
        this.applySettings = applySettings;
        this.openUpdatePage = openUpdatePage;
        InitializeComponent();
        LoadSettingsIntoControls();
        SetActiveCategoryButton(GeneralCategoryButton);
        isLoading = false;
    }

    private void LoadSettingsIntoControls()
    {
        SelectThemePreset(settings.ThemePreset);
        SelectComboItem(DefaultRedactionBox, settings.DefaultRedactionStyle);
        SelectComboItem(TextAlignmentBox, settings.DefaultTextAlignment);
        FloatingCaptureButtonCheckBox.IsChecked = settings.ShowFloatingCaptureButton;
        MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTrayOnClose;
        SelectionInstructionsCheckBox.IsChecked = settings.ShowSelectionInstructions;
        TextMoveHandlesCheckBox.IsChecked = settings.ShowTextMoveHandles;
        TextBoldCheckBox.IsChecked = settings.DefaultTextBold;
        TextBackgroundCheckBox.IsChecked = settings.DefaultTextBackgroundEnabled;
        CopyAfterSaveCheckBox.IsChecked = settings.CopyAfterSave;
        SetSliderValue(CaptureButtonSizeSlider, settings.CaptureButtonSize, 42, 72);
        SetSliderValue(ToolbarButtonSizeSlider, settings.OverlayToolbarButtonSize, 36, 56);
        SetSliderValue(CaptureDelaySlider, settings.CaptureDelayMilliseconds, 0, 1000);
        SetSliderValue(OverlayDimSlider, settings.OverlayDimOpacity * 100, 20, 85);
        SetSliderValue(StrokeThicknessSlider, settings.DefaultStrokeThickness, 1, 12);
        SetSliderValue(TextFontSizeSlider, settings.DefaultTextFontSize, 10, 72);
        FileNamePatternBox.Text = settings.DefaultFileNamePattern;
        UpdateUrlBox.Text = settings.UpdateUrl;
        UpdateHotkeyDisplay();
        UpdateRangeLabels();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: FrameworkElement section } button)
        {
            return;
        }

        SetActiveCategoryButton(button);

        try
        {
            var position = section.TransformToAncestor(SettingsScrollViewer).Transform(new System.Windows.Point(0, 0));
            SettingsScrollViewer.ScrollToVerticalOffset(SettingsScrollViewer.VerticalOffset + position.Y);
        }
        catch (InvalidOperationException)
        {
            section.BringIntoView();
        }
    }

    private void SetActiveCategoryButton(System.Windows.Controls.Button button)
    {
        if (activeCategoryButton is not null)
        {
            activeCategoryButton.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            activeCategoryButton.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
        }

        activeCategoryButton = button;
        if (System.Windows.Application.Current.Resources["AccentBrush"] is System.Windows.Media.Brush accentBrush)
        {
            button.Background = accentBrush;
            button.BorderBrush = accentBrush;
        }
    }

    private void ResetAllSettings_Click(object sender, RoutedEventArgs e)
    {
        isLoading = true;
        settings = new AppSettings();
        LoadSettingsIntoControls();
        isLoading = false;
        ApplyCurrentSettings();
    }

    private void OpenUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        openUpdatePage(settings.UpdateUrl, this);
    }

    private void ThemePresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || ThemePresetBox.SelectedItem is not ComboBoxItem item || item.Content is not string preset)
        {
            return;
        }

        settings.ThemePreset = preset;
        ApplyCurrentSettings();
    }

    private void AccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string accent)
        {
            return;
        }

        settings.AccentColor = accent;
        ApplyCurrentSettings();
    }

    private void FloatingCaptureButtonCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.ShowFloatingCaptureButton = FloatingCaptureButtonCheckBox.IsChecked == true;
        ApplyCurrentSettings();
    }

    private void MinimizeToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.MinimizeToTrayOnClose = MinimizeToTrayCheckBox.IsChecked == true;
        ApplyCurrentSettings();
    }

    private void ResetCaptureButtonPosition_Click(object sender, RoutedEventArgs e)
    {
        settings.FloatingCaptureLeft = null;
        settings.FloatingCaptureTop = null;
        settings.ShowFloatingCaptureButton = true;
        FloatingCaptureButtonCheckBox.IsChecked = true;
        ApplyCurrentSettings();
    }

    private void HotkeyRecordButton_Click(object sender, RoutedEventArgs e)
    {
        isRecordingHotkey = true;
        HotkeyRecordButton.Content = "Press keys...";
        StatusText.Text = "Press a key combination for capture.";
        HotkeyRecordButton.Focus();
        Keyboard.Focus(HotkeyRecordButton);
    }

    private void HotkeyRecordButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (isRecordingHotkey is false)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        var keyToken = ToHotkeyToken(key);
        if (keyToken is null)
        {
            StatusText.Text = "This key is not supported for global shortcuts.";
            return;
        }

        var candidate = BuildHotkeyText(keyToken, Keyboard.Modifiers);
        var previous = settings.CaptureHotkey;
        settings.CaptureHotkey = candidate;
        var error = ApplyCurrentSettings();
        if (error is not null)
        {
            settings.CaptureHotkey = previous;
            UpdateHotkeyDisplay();
        }

        isRecordingHotkey = false;
        HotkeyRecordButton.Content = "Record";
    }

    private void HotkeyResetButton_Click(object sender, RoutedEventArgs e)
    {
        var previous = settings.CaptureHotkey;
        settings.CaptureHotkey = HotkeyGesture.DefaultCaptureHotkey;
        var error = ApplyCurrentSettings();
        if (error is not null)
        {
            settings.CaptureHotkey = previous;
        }

        UpdateHotkeyDisplay();
    }

    private void CaptureDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isLoading)
        {
            return;
        }

        settings.CaptureDelayMilliseconds = (int)Math.Round(CaptureDelaySlider.Value);
        UpdateRangeLabels();
        ApplyCurrentSettings();
    }

    private void OverlayDimSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isLoading)
        {
            return;
        }

        settings.OverlayDimOpacity = Math.Round(OverlayDimSlider.Value) / 100;
        UpdateRangeLabels();
        ApplyCurrentSettings();
    }

    private void SelectionInstructionsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.ShowSelectionInstructions = SelectionInstructionsCheckBox.IsChecked == true;
        ApplyCurrentSettings();
    }

    private void CaptureButtonSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isLoading)
        {
            return;
        }

        settings.CaptureButtonSize = Math.Round(CaptureButtonSizeSlider.Value);
        UpdateRangeLabels();
        ApplyCurrentSettings();
    }

    private void ToolbarButtonSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isLoading)
        {
            return;
        }

        settings.OverlayToolbarButtonSize = Math.Round(ToolbarButtonSizeSlider.Value);
        UpdateRangeLabels();
        ApplyCurrentSettings();
    }

    private void DefaultRedactionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || DefaultRedactionBox.SelectedItem is not ComboBoxItem item || item.Content is not string value)
        {
            return;
        }

        settings.DefaultRedactionStyle = value;
        ApplyCurrentSettings();
    }

    private void StrokeThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isLoading)
        {
            return;
        }

        settings.DefaultStrokeThickness = Math.Round(StrokeThicknessSlider.Value);
        UpdateRangeLabels();
        ApplyCurrentSettings();
    }

    private void TextFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isLoading)
        {
            return;
        }

        settings.DefaultTextFontSize = Math.Round(TextFontSizeSlider.Value);
        UpdateRangeLabels();
        ApplyCurrentSettings();
    }

    private void TextMoveHandlesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.ShowTextMoveHandles = TextMoveHandlesCheckBox.IsChecked == true;
        ApplyCurrentSettings();
    }

    private void TextBoldCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.DefaultTextBold = TextBoldCheckBox.IsChecked == true;
        ApplyCurrentSettings();
    }

    private void TextAlignmentBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || TextAlignmentBox.SelectedItem is not ComboBoxItem item || item.Content is not string value)
        {
            return;
        }

        settings.DefaultTextAlignment = value;
        ApplyCurrentSettings();
    }

    private void TextColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string color)
        {
            return;
        }

        settings.DefaultTextColor = color;
        ApplyCurrentSettings();
    }

    private void TextBackgroundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.DefaultTextBackgroundEnabled = TextBackgroundCheckBox.IsChecked == true;
        ApplyCurrentSettings();
    }

    private void TextBackgroundColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string color)
        {
            return;
        }

        settings.DefaultTextBackgroundColor = color;
        ApplyCurrentSettings();
    }

    private void FileNamePatternBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.DefaultFileNamePattern = string.IsNullOrWhiteSpace(FileNamePatternBox.Text)
            ? "thinksnap-{yyyyMMdd-HHmmss}.png"
            : FileNamePatternBox.Text;
        ApplyCurrentSettings();
    }

    private void UpdateUrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.UpdateUrl = UpdateUrlBox.Text;
        ApplyCurrentSettings();
    }

    private void CopyAfterSaveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        settings.CopyAfterSave = CopyAfterSaveCheckBox.IsChecked == true;
        ApplyCurrentSettings();
    }

    private string? ApplyCurrentSettings()
    {
        NormalizeSettings();
        var error = applySettings(settings.Clone());
        if (error is null)
        {
            StatusText.Text = "Changes saved.";
            UpdateHotkeyDisplay();
        }
        else
        {
            StatusText.Text = error;
        }

        return error;
    }

    private void NormalizeSettings()
    {
        settings.CaptureButtonSize = Math.Clamp(settings.CaptureButtonSize, 42, 72);
        settings.OverlayToolbarButtonSize = Math.Clamp(settings.OverlayToolbarButtonSize, 36, 56);
        settings.CaptureDelayMilliseconds = Math.Clamp(settings.CaptureDelayMilliseconds, 0, 1000);
        settings.OverlayDimOpacity = Math.Clamp(settings.OverlayDimOpacity, 0.2, 0.85);
        settings.DefaultStrokeThickness = Math.Clamp(settings.DefaultStrokeThickness, 1, 12);
        settings.DefaultTextFontSize = Math.Clamp(settings.DefaultTextFontSize, 10, 72);
    }

    private void UpdateHotkeyDisplay()
    {
        var gesture = HotkeyGesture.ParseOrDefault(settings.CaptureHotkey);
        settings.CaptureHotkey = gesture.DisplayText;
        HotkeyDisplayText.Text = gesture.DisplayText;
    }

    private void UpdateRangeLabels()
    {
        CaptureDelayValueText.Text = $"Capture delay: {Math.Round(CaptureDelaySlider.Value)} ms";
        OverlayDimValueText.Text = $"Overlay dim: {Math.Round(OverlayDimSlider.Value)}%";
        CaptureButtonSizeValueText.Text = $"Floating capture button: {Math.Round(CaptureButtonSizeSlider.Value)} px";
        ToolbarButtonSizeValueText.Text = $"Overlay toolbar buttons: {Math.Round(ToolbarButtonSizeSlider.Value)} px";
        StrokeThicknessValueText.Text = $"Drawing thickness: {Math.Round(StrokeThicknessSlider.Value)} px";
        TextFontSizeValueText.Text = $"Text size: {Math.Round(TextFontSizeSlider.Value)} px";
    }

    private void SelectThemePreset(string preset)
    {
        SelectComboItem(ThemePresetBox, preset);
    }

    private static void SelectComboItem(System.Windows.Controls.ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static void SetSliderValue(Slider slider, double value, double minimum, double maximum)
    {
        slider.Value = Math.Clamp(value, minimum, maximum);
    }

    private static string BuildHotkeyText(string keyToken, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(keyToken);
        return string.Join("+", parts);
    }

    private static string? ToHotkeyToken(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return key.ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return key.ToString();
        }

        return key switch
        {
            Key.PrintScreen or Key.Snapshot => "PrintScreen",
            _ => null
        };
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or
            Key.LWin or Key.RWin;
    }
}
