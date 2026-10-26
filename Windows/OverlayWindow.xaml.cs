using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MeaningOverlay.Windows;

public partial class OverlayWindow : Window
{
    // Win32 for click-through/detection outside
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private bool _isClosing;

    public OverlayWindow()
    {
        InitializeComponent();

        // Position near mouse cursor
        PositionNearCursor();

        // Handle Escape key
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) CloseOverlay();
        };

        // Deactivate = click outside → close
        Deactivated += (_, _) =>
        {
            if (!_isClosing) CloseOverlay();
        };
    }

    private void PositionNearCursor()
    {
        GetCursorPos(out var pt);

        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // Default: appear to the right-bottom of cursor with offset
        double left = pt.X + 16;
        double top = pt.Y + 16;

        // Adjust if it would go off the right edge (width 480 + shadow padding 32)
        if (left + 512 > screenWidth) left = screenWidth - 516;
        if (left < 0) left = 8;

        // We don't know height yet (SizeToContent), rough estimate: 340px
        if (top + 340 > screenHeight) top = screenHeight - 350;
        if (top < 0) top = 8;

        Left = left;
        Top = top;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Adjust position now that we know actual height
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        if (Top + ActualHeight + 32 > screenHeight)
            Top = screenHeight - ActualHeight - 40;

        // Fade in
        var sb = (Storyboard)FindResource("FadeInStoryboard");
        sb.Begin(this);

        // Start spinner
        var spin = (Storyboard)FindResource("SpinStoryboard");
        spin.Begin(this);
    }

    public void ShowLoading(string clipboardText, bool detailedMode = false)
    {
        SnippetText.Text = clipboardText.Length > 120
            ? clipboardText[..120] + "…"
            : clipboardText;

        LoadingPanel.Visibility = Visibility.Visible;
        ResultText.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LoadingText.Text = "Asking Gemini…";

        // Update mode badge
        ModeBadgeText.Text = detailedMode ? "🔍 Detailed" : "⚡ Simple";
        ModeBadge.Background = detailedMode
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x20, 0x06, 0xB6, 0xD4))
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x20, 0xA8, 0x55, 0xF7));
        ModeBadgeText.Foreground = detailedMode
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x06, 0xB6, 0xD4))
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xA8, 0x55, 0xF7));
    }

    public void ShowRetrying(int secondsLeft)
    {
        LoadingPanel.Visibility = Visibility.Visible;
        ResultText.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LoadingText.Text = secondsLeft > 0
            ? $"Rate limited — retrying in {secondsLeft}s…"
            : "Retrying…";
    }

    public void ShowResult(string originalText, string explanation)
    {
        SnippetText.Text = originalText.Length > 120
            ? originalText[..120] + "…"
            : originalText;

        // Stop spinner
        var spin = (Storyboard)FindResource("SpinStoryboard");
        spin.Stop(this);

        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;

        ResultText.Text = explanation;
        ResultText.Visibility = Visibility.Visible;

        // Scroll back to the top in case user had scrolled
        ContentScroller.ScrollToTop();

        // Re-adjust vertical position now that height is known
        Dispatcher.InvokeAsync(() =>
        {
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            if (Top + ActualHeight + 16 > screenHeight)
                Top = screenHeight - ActualHeight - 16;
            if (Top < 0) Top = 8;
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void ShowError(string errorMessage)
    {
        var spin = (Storyboard)FindResource("SpinStoryboard");
        spin.Stop(this);

        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultText.Visibility = Visibility.Collapsed;

        ErrorText.Text = errorMessage;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    public void CloseOverlay()
    {
        if (_isClosing) return;
        _isClosing = true;

        var spin = (Storyboard)FindResource("SpinStoryboard");
        spin.Stop(this);

        var fadeOut = (Storyboard)FindResource("FadeOutStoryboard");
        fadeOut.Begin(this);
    }

    private void FadeOut_Completed(object sender, EventArgs e)
    {
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void ContentScroller_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        // Smoothly scroll the viewer instead of letting the event bubble up unhandled
        var scroller = (System.Windows.Controls.ScrollViewer)sender;
        scroller.ScrollToVerticalOffset(scroller.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
