using OpenCvSharp;
using OpenCvSharp.Extensions;
using ScreenCapture.NET;
using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;

#pragma warning disable

public static class CaptureManager
{
    private static DX11ScreenCaptureService? _screenCaptureService;
    private static DX11ScreenCapture? _screenCapture;
    private static readonly Dictionary<string, ICaptureZone> _captureZones = [];

    // Multi-thread safe — can be used freely across any number of threads.
    // Recommended for low-frequency captures (UI scan, OCR, logs, etc.).
    public static Mat GDICapture(Rectangle region)
    {
        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
        return BitmapConverter.ToMat(bitmap);
    }

    private static SKBitmap? _reusableBitmap; // ← Variável única reutilizável
    // Not thread-safe — must be used by a SINGLE dedicated thread only.
    // Designed for high-frequency capture (e.g., YOLO or AI inference loop).
    // DirectX 11 contexts and zones are not safe for concurrent access.
    public static SKBitmap? DX11Capture(Rectangle region)
    {
        if (_screenCaptureService == null)
        {
            _screenCaptureService = new DX11ScreenCaptureService();
            var graphicsCards = _screenCaptureService.GetGraphicsCards();
            var displays = _screenCaptureService.GetDisplays(graphicsCards.First());
            _screenCapture = _screenCaptureService.GetScreenCapture(displays.First());
            _screenCapture.Timeout = 1000;
        }

        string zoneKey = $"{region.X}_{region.Y}_{region.Width}_{region.Height}";
        if (!_captureZones.TryGetValue(zoneKey, out ICaptureZone? captureZone))
        {
            try
            {
                captureZone = _screenCapture!.RegisterCaptureZone(
                    region.X,
                    region.Y,
                    region.Width,
                    region.Height
                );

                _captureZones[zoneKey] = captureZone;
            }
            catch
            {
                Console.WriteLine($"Failed to register capture zone: {zoneKey} ):");
            }
        }

        if (!_screenCapture!.CaptureScreen())
            return null;

        if (captureZone == null)
            return null;

        // Converte para Mat do OpenCV
        using (captureZone.Lock())
        {
            ReadOnlySpan<byte> rawData = captureZone.RawBuffer;
            // Cria bitmap apenas na primeira vez
            if (_reusableBitmap == null)
            {
                _reusableBitmap = new SKBitmap(region.Width, region.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            }

            rawData.CopyTo(_reusableBitmap.GetPixelSpan());
            return _reusableBitmap;
        }
    }

    public static void DisposeDX11Resources()
    {
        _captureZones.Clear();
        _screenCapture?.Dispose();
        _screenCaptureService?.Dispose();
        _screenCapture = null;
        _screenCaptureService = null;
    }
}