using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenGrab.Services;

public class CaptureService
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int LOGPIXELSX = 88;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public record CaptureResult(
        Bitmap Bitmap,
        BitmapSource BitmapSource,
        int ScreenLeft,
        int ScreenTop,
        int ScreenWidth,
        int ScreenHeight,
        double DpiScale);

    public CaptureResult CaptureCurrentMonitor()
    {
        GetCursorPos(out POINT cursorPos);
        IntPtr hMonitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMonitor, ref monitorInfo);

        var bounds = monitorInfo.rcMonitor;
        int width = bounds.Right - bounds.Left;
        int height = bounds.Bottom - bounds.Top;

        // Get DPI scale
        IntPtr hdc = GetDC(IntPtr.Zero);
        int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
        ReleaseDC(IntPtr.Zero, hdc);
        double dpiScale = dpi / 96.0;

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(width, height));
        }

        var bitmapSource = ConvertToBitmapSource(bitmap);
        bitmapSource.Freeze();

        return new CaptureResult(bitmap, bitmapSource, bounds.Left, bounds.Top, width, height, dpiScale);
    }

    private static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }
}
