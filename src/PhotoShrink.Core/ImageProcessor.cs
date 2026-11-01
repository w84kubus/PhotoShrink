using SkiaSharp;

namespace PhotoShrink.Core;

/// <summary>
/// Główna logika aplikacji: wyszukiwanie zdjęć, pomniejszanie i kompresja do JPEG.
/// Czysta logika, bez zależności od GUI — łatwa do testowania jednostkowego.
/// Oryginalne pliki są tylko odczytywane, nigdy nie są nadpisywane ani usuwane.
/// </summary>
public static class ImageProcessor
{
    private static readonly string[] SupportedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif",
    };

    public static bool IsSupportedImage(string path) =>
        Array.IndexOf(SupportedExtensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;

    /// <summary>
    /// Zwraca pliki ze zdjęciami leżące bezpośrednio w <paramref name="sourceFolder"/> (bez podfolderów),
    /// z wyłączeniem samego podfolderu wyjściowego — dzięki temu ponowne odpalenie nigdy nie
    /// przetwarza już pomniejszonych kopii.
    /// </summary>
    public static IReadOnlyList<string> FindSourceImages(string sourceFolder, string outputFolderName)
    {
        if (!Directory.Exists(sourceFolder))
        {
            return Array.Empty<string>();
        }

        var outputFull = Path.GetFullPath(Path.Combine(sourceFolder, outputFolderName));

        return Directory.EnumerateFiles(sourceFolder)
            .Where(IsSupportedImage)
            .Where(f => !string.Equals(
                Path.GetFullPath(Path.GetDirectoryName(f) ?? ""),
                outputFull,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Przetwarza jedno zdjęcie: wczytuje, pomniejsza (jeśli większe niż MaxDimension,
    /// bez powiększania mniejszych), zapisuje kopię jako JPEG w <paramref name="destFolder"/>.
    /// </summary>
    public static async Task<ProcessResult> ProcessImageAsync(
        string sourcePath,
        string destFolder,
        ProcessOptions options,
        CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(sourcePath);

        try
        {
            var originalBytes = new FileInfo(sourcePath).Length;
            Directory.CreateDirectory(destFolder);

            var destFileName = Path.GetFileNameWithoutExtension(fileName) + ".jpg";
            var destPath = Path.Combine(destFolder, destFileName);

            await Task.Run(() => ResizeAndEncode(sourcePath, destPath, options), ct);

            var outputBytes = new FileInfo(destPath).Length;
            return new ProcessResult(fileName, ProcessStatus.Processed, originalBytes, outputBytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ProcessResult(fileName, ProcessStatus.Failed, 0, 0, ex.Message);
        }
    }

    private static void ResizeAndEncode(string sourcePath, string destPath, ProcessOptions options)
    {
        using var original = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidDataException("Nieobsługiwany lub uszkodzony plik obrazu.");

        var width = original.Width;
        var height = original.Height;
        var longestSide = Math.Max(width, height);

        SKBitmap? resized = null;
        try
        {
            SKBitmap toEncode = original;

            if (longestSide > options.MaxDimension && options.MaxDimension > 0)
            {
                var scale = (double)options.MaxDimension / longestSide;
                var newWidth = Math.Max(1, (int)Math.Round(width * scale));
                var newHeight = Math.Max(1, (int)Math.Round(height * scale));

                var info = new SKImageInfo(newWidth, newHeight, original.ColorType, original.AlphaType);
                resized = original.Resize(info, SKSamplingOptions.Default);
                toEncode = resized ?? original;
            }

            using var image = SKImage.FromBitmap(toEncode);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(options.JpegQuality, 1, 100));
            using var outStream = File.Create(destPath);
            data.SaveTo(outStream);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    /// <summary>
    /// Przetwarza wszystkie wspierane zdjęcia znalezione w <paramref name="sourceFolder"/>,
    /// zapisując kopie do podfolderu <see cref="ProcessOptions.OutputFolderName"/>.
    /// </summary>
    public static async Task<IReadOnlyList<ProcessResult>> ProcessFolderAsync(
        string sourceFolder,
        ProcessOptions options,
        IProgress<ProcessResult>? progress = null,
        CancellationToken ct = default)
    {
        var destFolder = Path.Combine(sourceFolder, options.OutputFolderName);
        var files = FindSourceImages(sourceFolder, options.OutputFolderName);
        var results = new List<ProcessResult>(files.Count);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ProcessImageAsync(file, destFolder, options, ct);
            results.Add(result);
            progress?.Report(result);
        }

        return results;
    }
}
