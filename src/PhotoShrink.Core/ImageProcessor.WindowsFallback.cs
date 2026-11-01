using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace PhotoShrink.Core;

/// <summary>
/// Awaryjna ścieżka dekodowania obrazów przez wbudowane kodeki systemu Windows (GDI+/WIC),
/// używana tylko wtedy, gdy SkiaSharp nie potrafi odczytać pliku — w praktyce głównie HEIC/HEIF
/// ze zdjęć z iPhone'a. Celowo nie dokładamy tu własnego dekodera HEVC: korzystamy wyłącznie
/// z tego, co system już ma (np. darmowe "Rozszerzenia obrazów HEIF" ze Sklepu Microsoft) —
/// jeśli systemowy kodek nie jest zainstalowany, ta ścieżka po prostu się nie powiedzie
/// i użytkownik dostaje zwykły komunikat "nieobsługiwany plik", tak jak dla każdego innego
/// nieznanego formatu.
/// </summary>
public static partial class ImageProcessor
{
    [SupportedOSPlatform("windows")]
    private static bool TryResizeAndEncodeViaWindowsImaging(string sourcePath, string destPath, ProcessOptions options)
    {
        try
        {
            using var original = new Bitmap(sourcePath);
            var width = original.Width;
            var height = original.Height;
            var longestSide = Math.Max(width, height);

            Bitmap? resized = null;
            try
            {
                Bitmap toEncode = original;

                if (longestSide > options.MaxDimension && options.MaxDimension > 0)
                {
                    var scale = (double)options.MaxDimension / longestSide;
                    var newWidth = Math.Max(1, (int)Math.Round(width * scale));
                    var newHeight = Math.Max(1, (int)Math.Round(height * scale));

                    resized = DrawResized(original, newWidth, newHeight);
                    toEncode = resized;
                }

                var bytes = EncodeJpegToFitSize(toEncode, options.MaxFileSizeBytes);
                File.WriteAllBytes(destPath, bytes);
                return true;
            }
            finally
            {
                resized?.Dispose();
            }
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[]? TryCreateThumbnailViaWindowsImaging(string path, int maxSize)
    {
        try
        {
            using var original = new Bitmap(path);
            var longestSide = Math.Max(original.Width, original.Height);
            var scale = Math.Min(1.0, (double)maxSize / longestSide);
            var width = Math.Max(1, (int)Math.Round(original.Width * scale));
            var height = Math.Max(1, (int)Math.Round(original.Height * scale));

            using var resized = DrawResized(original, width, height);
            return EncodeJpeg(resized, 70);
        }
        catch
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Bitmap DrawResized(Bitmap source, int width, int height)
    {
        var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(resized);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.DrawImage(source, 0, 0, width, height);
        return resized;
    }

    /// <summary>
    /// Ten sam pomysł co <see cref="EncodeToFitSize"/> (szukanie binarne najwyższej jakości,
    /// która mieści się w limicie), tylko przez enkoder JPEG z GDI+ zamiast SkiaSharp.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static byte[] EncodeJpegToFitSize(Bitmap bitmap, long maxBytes)
    {
        var atMaxQuality = EncodeJpeg(bitmap, MaxAutoQuality);
        if (atMaxQuality.Length <= maxBytes)
        {
            return atMaxQuality;
        }

        int lo = MinAutoQuality, hi = MaxAutoQuality - 1;
        byte[]? bestFit = null;

        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var attempt = EncodeJpeg(bitmap, mid);

            if (attempt.Length <= maxBytes)
            {
                bestFit = attempt;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return bestFit ?? EncodeJpeg(bitmap, MinAutoQuality);
    }

    [SupportedOSPlatform("windows")]
    private static byte[] EncodeJpeg(Bitmap bitmap, int quality)
    {
        var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

        using var stream = new MemoryStream();
        bitmap.Save(stream, jpegEncoder, parameters);
        return stream.ToArray();
    }
}
