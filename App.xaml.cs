using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using ScreenGrab.Services;
using ScreenGrab.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ScreenGrab;

public partial class App : Application
{
    private HotkeyService? _hotkeyService;
    private OcrService? _ocrService;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private Window? _hiddenWindow;
    private bool _isCapturing;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // Set up tray icon first so user sees the app is running
        SetupTrayIcon();

        _hiddenWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };
        _hiddenWindow.Show();
        _hiddenWindow.Hide();

        var handle = new WindowInteropHelper(_hiddenWindow).Handle;

        _hotkeyService = new HotkeyService();
        try
        {
            _hotkeyService.Register(handle);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "ScreenGrab", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Initialize OCR engine in background (takes a few seconds)
        _trayIcon!.ShowBalloonTip(3000, "ScreenGrab", "Loading OCR engine...",
            System.Windows.Forms.ToolTipIcon.Info);

        try
        {
            _ocrService = await Task.Run(() => new OcrService());
            _trayIcon.ShowBalloonTip(2000, "ScreenGrab", "Ready! Press Ctrl+Shift+T to capture text.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start OCR engine: {ex.Message}\n\nMake sure Python and easyocr are installed.",
                "ScreenGrab", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "ScreenGrab - Ctrl+Shift+T to capture",
            Visible = true
        };

        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(0, 120, 215));
            using var font = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            g.DrawString("T", font, brush, 4, 2);
        }
        _trayIcon.Icon = Icon.FromHandle(bmp.GetHicon());

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Capture (Ctrl+Shift+T)", null, (_, _) => OnHotkeyPressed());
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (_, _) => OnHotkeyPressed();
    }

    private async void OnHotkeyPressed()
    {
        if (_isCapturing || _ocrService == null) return;
        _isCapturing = true;

        try
        {
            var captureService = new CaptureService();
            var capture = captureService.CaptureCurrentMonitor();

            var ocrResult = await _ocrService.RecognizeAsync(capture.Bitmap);

            if (ocrResult.Words.Count == 0)
            {
                capture.Bitmap.Dispose();
                return;
            }

            var overlay = new OverlayWindow();
            overlay.Setup(capture, ocrResult);
            overlay.Closed += (_, _) => capture.Bitmap.Dispose();
            overlay.Show();
            overlay.Activate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Capture failed: {ex.Message}", "ScreenGrab",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isCapturing = false;
        }
    }

    private void ExitApplication()
    {
        _ocrService?.Dispose();
        _hotkeyService?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _hiddenWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _ocrService?.Dispose();
        _hotkeyService?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnExit(e);
    }
}
