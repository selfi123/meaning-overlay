using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using MeaningOverlay.Services;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MeaningOverlay.Windows;

public partial class SettingsWindow : Window
{
    private bool _showingKey = false;
    private bool _detailedMode = false;
    private string _primaryModel = "pollinations";

    public SettingsWindow()
    {
        InitializeComponent();
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var settings = SettingsManager.LoadSettings();
        ApiKeyBox.Password = settings.ApiKey;
        ApiKeyVisible.Text = settings.ApiKey;

        _detailedMode = settings.DetailedMode;
        _primaryModel = settings.PrimaryModel ?? "pollinations";
        
        ApplyModeVisuals();
        ApplyProviderVisuals();
    }

    // ── Provider toggle ──────────────────────────────────────────────────────

    private void Pollinations_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _primaryModel = "pollinations";
        ApplyProviderVisuals();
    }

    private void Gemini_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _primaryModel = "gemini";
        ApplyProviderVisuals();
    }

    private void ApplyProviderVisuals()
    {
        if (_primaryModel == "pollinations")
        {
            PollinationsBtn.BorderBrush = BrushFromHex("#7C3AED");
            PollinationsBtn.Background = BrushFromHex("#1A1A30");
            SetTextColors(PollinationsBtn, "#F0F0FF", "#8080A0");

            GeminiBtn.BorderBrush = BrushFromHex("#3D3D5C");
            GeminiBtn.Background = BrushFromHex("#12121E");
            SetTextColors(GeminiBtn, "#606080", "#404060");
        }
        else
        {
            PollinationsBtn.BorderBrush = BrushFromHex("#3D3D5C");
            PollinationsBtn.Background = BrushFromHex("#12121E");
            SetTextColors(PollinationsBtn, "#606080", "#404060");

            GeminiBtn.BorderBrush = BrushFromHex("#7C3AED");
            GeminiBtn.Background = BrushFromHex("#1A1A30");
            SetTextColors(GeminiBtn, "#F0F0FF", "#8080A0");
        }
    }

    // ── Mode toggle ──────────────────────────────────────────────────────────

    private void SimpleMode_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _detailedMode = false;
        ApplyModeVisuals();
    }

    private void DetailedMode_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _detailedMode = true;
        ApplyModeVisuals();
    }

    private void ApplyModeVisuals()
    {
        // Active: purple border + light background + white text
        // Inactive: dim border + dark background + grey text

        if (_detailedMode)
        {
            // Simple = inactive
            SimpleModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x5C));
            SimpleModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1E));
            SetTextColors(SimpleModeBtn, "#606080", "#404060");

            // Detailed = active
            DetailedModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED));
            DetailedModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x30));
            SetTextColors(DetailedModeBtn, "#F0F0FF", "#8080A0");
        }
        else
        {
            // Simple = active
            SimpleModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED));
            SimpleModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x30));
            SetTextColors(SimpleModeBtn, "#F0F0FF", "#8080A0");

            // Detailed = inactive
            DetailedModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x5C));
            DetailedModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1E));
            SetTextColors(DetailedModeBtn, "#606080", "#404060");
        }
    }

    /// Sets the Foreground of first and second TextBlock inside a Border > StackPanel
    private static void SetTextColors(System.Windows.Controls.Border border, string titleHex, string descHex)
    {
        if (border.Child is System.Windows.Controls.StackPanel sp && sp.Children.Count >= 2)
        {
            if (sp.Children[0] is System.Windows.Controls.TextBlock t1)
                t1.Foreground = BrushFromHex(titleHex);
            if (sp.Children[1] is System.Windows.Controls.TextBlock t2)
                t2.Foreground = BrushFromHex(descHex);
        }
    }

    private static SolidColorBrush BrushFromHex(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex)!);

    // ── Save / Cancel ────────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var key = _showingKey ? ApiKeyVisible.Text.Trim() : ApiKeyBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(key))
        {
            ShowStatus("⚠ Please enter your Gemini API key.", "#FF6B6B");
            return;
        }

        SettingsManager.SaveSettings(new AppSettings(key, _detailedMode, _primaryModel));
        ShowStatus("✓ Settings saved!", "#4ADE80");

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        timer.Tick += (_, _) => { timer.Stop(); Close(); };
        timer.Start();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // ── Key show/hide ────────────────────────────────────────────────────────

    private void ToggleShowKey_Click(object sender, RoutedEventArgs e)
    {
        _showingKey = !_showingKey;
        if (_showingKey)
        {
            ApiKeyVisible.Text = ApiKeyBox.Password;
            ApiKeyBox.Visibility = Visibility.Collapsed;
            ApiKeyVisible.Visibility = Visibility.Visible;
            ApiKeyVisible.Focus();
        }
        else
        {
            ApiKeyBox.Password = ApiKeyVisible.Text;
            ApiKeyVisible.Visibility = Visibility.Collapsed;
            ApiKeyBox.Visibility = Visibility.Visible;
            ApiKeyBox.Focus();
        }
    }

    private void Link_Navigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void ShowStatus(string message, string colorHex)
    {
        StatusText.Text = message;
        StatusText.Foreground = BrushFromHex(colorHex);
        StatusText.Visibility = Visibility.Visible;
    }
}
