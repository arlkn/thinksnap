using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Thinksnap.App.Models;
using Thinksnap.App.Services;
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
        this.settings.Language = LocalizationService.NormalizeLanguage(settings.Language);
        InitializeComponent();
        LoadSettingsIntoControls();
        ApplyLocalization();
        SetActiveCategoryButton(GeneralCategoryButton);
        isLoading = false;
    }

    private void LoadSettingsIntoControls()
    {
        SelectComboItemByTag(LanguageBox, settings.Language);
        SelectThemePreset(settings.ThemePreset);
        SelectComboItemByTag(DefaultRedactionBox, settings.DefaultRedactionStyle);
        SelectComboItemByTag(TextAlignmentBox, settings.DefaultTextAlignment);
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

    private void ApplyLocalization()
    {
        Title = T("Settings.WindowTitle");
        SettingsTitleText.Text = T("Settings.Title");
        StatusText.Text = string.IsNullOrWhiteSpace(StatusText.Text) || StatusText.Text == "Changes are saved automatically."
            ? T("Settings.AutoSaved")
            : StatusText.Text;

        GeneralCategoryButton.Content = T("Settings.CategoryGeneral");
        ShortcutsCategoryButton.Content = T("Settings.CategoryShortcuts");
        CaptureCategoryButton.Content = T("Settings.CategoryCapture");
        InterfaceCategoryButton.Content = T("Settings.CategoryInterface");
        EditorCategoryButton.Content = T("Settings.CategoryEditor");
        SaveCategoryButton.Content = T("Settings.CategorySave");
        UpdatesCategoryButton.Content = T("Settings.CategoryUpdates");
        ResetAllButton.Content = T("Settings.ResetAll");

        GeneralTitleText.Text = T("Settings.General");
        GeneralDescriptionText.Text = T("Settings.GeneralDescription");
        FloatingCaptureButtonCheckBox.Content = T("Settings.ShowFloatingButton");
        MinimizeToTrayCheckBox.Content = T("Settings.MinimizeToTray");
        ResetCaptureButtonPositionButton.Content = T("Settings.ResetCapturePosition");

        ShortcutsTitleText.Text = T("Settings.Shortcuts");
        ShortcutsDescriptionText.Text = T("Settings.ShortcutsDescription");
        HotkeyRecordButton.Content = isRecordingHotkey ? T("Settings.Recording") : T("Settings.Record");
        HotkeyResetButton.Content = T("Action.Reset");

        CaptureTitleText.Text = T("Settings.Capture");
        CaptureDescriptionText.Text = T("Settings.CaptureDescription");
        SelectionInstructionsCheckBox.Content = T("Settings.SelectionHint");

        InterfaceTitleText.Text = T("Settings.Interface");
        InterfaceDescriptionText.Text = T("Settings.InterfaceDescription");
        LanguageLabelText.Text = T("Settings.Language");
        ThemePresetLabelText.Text = T("Settings.ThemePreset");
        AccentLabelText.Text = T("Settings.Accent");

        EditorTitleText.Text = T("Settings.Editor");
        EditorDescriptionText.Text = T("Settings.EditorDescription");
        DefaultRedactionLabelText.Text = T("Settings.DefaultRedaction");
        SetComboItemContent(DefaultRedactionBox, "Pixelate", T("Settings.RedactionPixelate"));
        SetComboItemContent(DefaultRedactionBox, "Blackout", T("Settings.RedactionBlackout"));
        SetComboItemContent(DefaultRedactionBox, "Blur", T("Settings.RedactionBlur"));
        TextMoveHandlesCheckBox.Content = T("Settings.ShowTextMoveHandles");
        TextBoldCheckBox.Content = T("Settings.TextBold");
        TextAlignmentLabelText.Text = T("Settings.DefaultTextAlignment");
        SetComboItemContent(TextAlignmentBox, "Left", T("Settings.TextAlignmentLeft"));
        SetComboItemContent(TextAlignmentBox, "Center", T("Settings.TextAlignmentCenter"));
        SetComboItemContent(TextAlignmentBox, "Right", T("Settings.TextAlignmentRight"));
        TextColorLabelText.Text = T("Settings.TextColor");
        TextBackgroundCheckBox.Content = T("Settings.TextBackground");
        TextBackgroundColorLabelText.Text = T("Settings.TextBackgroundColor");

        SaveTitleText.Text = T("Settings.Save");
        SaveDescriptionText.Text = T("Settings.SaveDescription");
        FileNamePatternLabelText.Text = T("Settings.FileNamePattern");
        CopyAfterSaveCheckBox.Content = T("Settings.CopyAfterSave");

        UpdatesTitleText.Text = T("Settings.CategoryUpdates");
        UpdatesDescriptionText.Text = T("Settings.UpdateDescription");
        OpenUpdatesButton.Content = T("Action.Updates");
        UpdateUrlLabelText.Text = T("Settings.UpdateUrl");
        CloseButton.Content = T("Action.Close");

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

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || LanguageBox.SelectedItem is not ComboBoxItem item || item.Tag is not string language)
        {
            return;
        }

        settings.Language = LocalizationService.NormalizeLanguage(language);
        ApplyLocalization();
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
        HotkeyRecordButton.Content = T("Settings.Recording");
        StatusText.Text = T("Settings.HotkeyPrompt");
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
            StatusText.Text = T("Settings.UnsupportedKey");
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
        HotkeyRecordButton.Content = T("Settings.Record");
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
        if (isLoading || DefaultRedactionBox.SelectedItem is not ComboBoxItem item || item.Tag is not string value)
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
        if (isLoading || TextAlignmentBox.SelectedItem is not ComboBoxItem item || item.Tag is not string value)
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
            StatusText.Text = T("Settings.ChangesSaved");
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
        settings.Language = LocalizationService.NormalizeLanguage(settings.Language);
    }

    private void UpdateHotkeyDisplay()
    {
        var gesture = HotkeyGesture.ParseOrDefault(settings.CaptureHotkey);
        settings.CaptureHotkey = gesture.DisplayText;
        HotkeyDisplayText.Text = gesture.DisplayText;
    }

    private void UpdateRangeLabels()
    {
        CaptureDelayValueText.Text = L("Settings.CaptureDelay", Math.Round(CaptureDelaySlider.Value));
        OverlayDimValueText.Text = L("Settings.OverlayDim", Math.Round(OverlayDimSlider.Value));
        CaptureButtonSizeValueText.Text = L("Settings.CaptureButtonSize", Math.Round(CaptureButtonSizeSlider.Value));
        ToolbarButtonSizeValueText.Text = L("Settings.ToolbarButtonSize", Math.Round(ToolbarButtonSizeSlider.Value));
        StrokeThicknessValueText.Text = L("Settings.StrokeThickness", Math.Round(StrokeThicknessSlider.Value));
        TextFontSizeValueText.Text = L("Settings.TextSize", Math.Round(TextFontSizeSlider.Value));
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

    private static void SelectComboItemByTag(System.Windows.Controls.ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        SelectComboItem(comboBox, value);
    }

    private static void SetComboItemContent(System.Windows.Controls.ComboBox comboBox, string tag, string content)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                item.Content = content;
                return;
            }
        }
    }

    private string T(string key) => LocalizationService.Text(settings, key);

    private string L(string key, params object[] values) => LocalizationService.Format(settings, key, values);

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
