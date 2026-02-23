using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RemoteAccess.Desktop.Services;

public class ScreenCaptureService : IDisposable
{
    private readonly ImageCodecInfo _jpegCodec;
    private readonly EncoderParameters _encoderParams;
    private int _quality = 40;
    private float _scale = 0.6f;

    public int Quality
    {
        get => _quality;
        set
        {
            _quality = Math.Clamp(value, 10, 100);
            _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_quality);
        }
    }

    public float Scale
    {
        get => _scale;
        set => _scale = Math.Clamp(value, 0.2f, 1.0f);
    }

    public ScreenCaptureService()
    {
        _jpegCodec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        _encoderParams = new EncoderParameters(1)
        {
            Param = { [0] = new EncoderParameter(Encoder.Quality, (long)_quality) }
        };
    }

    public (int Width, int Height) GetScreenSize()
    {
        var bounds = Screen.PrimaryScreen!.Bounds;
        return (bounds.Width, bounds.Height);
    }

    public byte[] CaptureScreen()
    {
        var bounds = Screen.PrimaryScreen!.Bounds;
        var scaledW = (int)(bounds.Width * _scale);
        var scaledH = (int)(bounds.Height * _scale);

        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);

            // Draw cursor
            DrawCursor(g, bounds);
        }

        // Scale down for bandwidth
        using var scaled = new Bitmap(scaledW, scaledH, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.DrawImage(bmp, 0, 0, scaledW, scaledH);
        }

        using var ms = new MemoryStream();
        scaled.Save(ms, _jpegCodec, _encoderParams);
        return ms.ToArray();
    }

    private static void DrawCursor(Graphics g, Rectangle bounds)
    {
        try
        {
            CURSORINFO cursorInfo = new() { cbSize = Marshal.SizeOf<CURSORINFO>() };
            if (GetCursorInfo(ref cursorInfo) && cursorInfo.flags == 0x00000001) // CURSOR_SHOWING
            {
                var hIcon = CopyIcon(cursorInfo.hCursor);
                if (hIcon != IntPtr.Zero && GetIconInfo(hIcon, out var iconInfo))
                {
                    var x = cursorInfo.ptScreenPos.X - iconInfo.xHotspot - bounds.X;
                    var y = cursorInfo.ptScreenPos.Y - iconInfo.yHotspot - bounds.Y;

                    using var icon = Icon.FromHandle(hIcon);
                    g.DrawIcon(icon, (int)x, (int)y);

                    if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
                    if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                    DestroyIcon(hIcon);
                }
            }
        }
        catch { /* cursor draw is optional */ }
    }

    public void Dispose()
    {
        _encoderParams.Dispose();
    }

    // ── P/Invoke ──────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CURSORINFO pci);
    [DllImport("user32.dll")] private static extern IntPtr CopyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public uint xHotspot, yHotspot;
        public IntPtr hbmMask, hbmColor;
    }
}
