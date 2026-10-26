using System.IO;
using System.Windows;
using System.Windows.Threading;
using MeaningOverlay.Services;
using MeaningOverlay.Windows;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;

namespace MeaningOverlay;

public partial class App : Application
{
    private HotkeyManager? _hotkeyManager;
    private Forms.NotifyIcon? _trayIcon;
    private OverlayWindow? _overlayWindow;
    private readonly GeminiService _geminiService = new();
    private bool _overlayVisible = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure settings file exists
        SettingsManager.EnsureSettingsFile();

        // If no API key, open settings first
        if (string.IsNullOrWhiteSpace(SettingsManager.LoadSettings().ApiKey))
        {
            ShowSettings();
        }

        SetupTrayIcon();
        SetupHotkey();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Meaning Overlay — Ctrl+Shift+M",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        // Try to load icon from assets ICO file, fallback to system icon
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "tray.ico");
        var pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "tray.png");
        try
        {
            if (File.Exists(iconPath))
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);
            else if (File.Exists(pngPath))
                _trayIcon.Icon = System.Drawing.Icon.FromHandle(
                    new System.Drawing.Bitmap(pngPath).GetHicon());
            else
                _trayIcon.Icon = System.Drawing.SystemIcons.Information;
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Information;
        }

        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private Forms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        var settingsItem = new Forms.ToolStripMenuItem("⚙  Settings");
        settingsItem.Click += (_, _) => ShowSettings();

        var exitItem = new Forms.ToolStripMenuItem("✕  Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            _hotkeyManager?.Dispose();
            Shutdown();
        };

        menu.Items.Add(settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void SetupHotkey()
    {
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            // Toggle: if overlay is visible, close it
            if (_overlayVisible && _overlayWindow != null && _overlayWindow.IsVisible)
            {
                _overlayWindow.CloseOverlay();
                _overlayVisible = false;
                return;
            }

            var text = ClipboardHelper.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification("Clipboard is empty!", "Copy some text first, then press Ctrl+Shift+M.");
                return;
            }

            OpenOverlay(text);
        });
    }

    private void OpenOverlay(string text)
    {
        // Close any existing overlay
        _overlayWindow?.Close();

        _overlayWindow = new OverlayWindow();
        _overlayWindow.Closed += (_, _) => _overlayVisible = false;
        _overlayVisible = true;

        _overlayWindow.Show();
        _overlayWindow.ShowLoading(text, SettingsManager.LoadSettings().DetailedMode);

        // Call Gemini in background
        Task.Run(async () =>
        {
            try
            {
                var settings = SettingsManager.LoadSettings();
                if (string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    Dispatcher.Invoke(() =>
                    {
                        _overlayWindow?.ShowError("No API key set. Right-click the tray icon → Settings.");
                    });
                    return;
                }

                // Progress reporter: receives retry countdown seconds as strings
                var progress = new Progress<string>(secondsStr =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (int.TryParse(secondsStr, out var s))
                            _overlayWindow?.ShowRetrying(s);
                    });
                });

                var result = await _geminiService.ExplainAsync(text, settings.ApiKey, progress, settings.DetailedMode, settings.PrimaryModel);
                Dispatcher.Invoke(() => _overlayWindow?.ShowResult(text, result));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => _overlayWindow?.ShowError($"Error: {ex.Message}"));
            }
        });
    }

    private void ShowSettings()
    {
        var settings = new SettingsWindow();
        settings.Show();
        settings.Activate();
    }

    private void ShowNotification(string title, string message)
    {
        _trayIcon?.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _hotkeyManager?.Dispose();
        base.OnExit(e);
    }
}
