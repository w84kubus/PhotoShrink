using SkiaSharp;

namespace PhotoShrink.Core;

/// <summary>
/// Główna logika aplikacji: wyszukiwanie zdjęć, pomniejszanie i kompresja do JPEG.
/// Czysta logika, bez zależności od GUI — łatwa do testowania jednostkowego.
/// Oryginalne pliki są tylko odczytywane, nigdy nie są nadpisywane ani usuwane.
/// </summary>
public static partial class ImageProcessor
{
    private static readonly string[] SupportedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif", ".heic", ".heif",
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
        catch (UnauthorizedAccessException)
        {
            return new ProcessResult(fileName, ProcessStatus.Failed, 0, 0,
                "brak uprawnień do zapisu w tym miejscu (np. płyta CD/pendrive tylko do odczytu) " +
                "— skopiuj zdjęcia na dysk (np. na Pulpit) i spróbuj ponownie");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ProcessResult(fileName, ProcessStatus.Failed, 0, 0, ex.Message);
        }
    }

    private static void ResizeAndEncode(string sourcePath, string destPath, ProcessOptions options)
    {
        var original = SKBitmap.Decode(sourcePath);
        if (original is not null)
        {
            using (original)
            {
                ResizeAndEncodeViaSkia(original, sourcePath, destPath, options);
            }

            return;
        }

        // SkiaSharp nie zna tego formatu (typowo: HEIC/HEIF ze zdjęć z iPhone'a) — na Windows
        // spróbuj jeszcze przez wbudowane w system kodeki (GDI+/WIC), zanim się poddamy.
        if (OperatingSystem.IsWindows() && TryResizeAndEncodeViaWindowsImaging(sourcePath, destPath, options))
        {
            return;
        }

        throw new InvalidDataException("Nieobsługiwany lub uszkodzony plik obrazu.");
    }

    private static void ResizeAndEncodeViaSkia(SKBitmap original, string sourcePath, string destPath, ProcessOptions options)
    {
        var width = original.Width;
        var height = original.Height;
        var longestSide = Math.Max(width, height);
        var willResize = longestSide > options.MaxDimension && options.MaxDimension > 0;

        SKBitmap? resized = null;
        try
        {
            SKBitmap toEncode = original;

            if (willResize)
            {
                var scale = (double)options.MaxDimension / longestSide;
                var newWidth = Math.Max(1, (int)Math.Round(width * scale));
                var newHeight = Math.Max(1, (int)Math.Round(height * scale));

                var info = new SKImageInfo(newWidth, newHeight, original.ColorType, original.AlphaType);
                resized = original.Resize(info, SKSamplingOptions.Default);
                toEncode = resized ?? original;
            }

            using var image = SKImage.FromBitmap(toEncode);

            // Celujemy nie tylko w limit z ustawień, ale też w rozmiar oryginału — inaczej
            // zdjęcie w formacie dużo bardziej wydajnym niż JPEG (typowo HEIC z iPhone'a)
            // mogłoby po "pomniejszeniu" wyjść WIĘKSZE niż było, jeśli tylko mieściłoby się
            // w hojnym limicie z ustawień. "Pomniejszacz" nie powinien nigdy powiększać.
            var originalFileSize = new FileInfo(sourcePath).Length;
            var effectiveMaxBytes = EffectiveMaxBytes(options.MaxFileSizeBytes, originalFileSize);
            var bytes = EncodeToFitSize(image, effectiveMaxBytes);

            // Dodatkowa siatka bezpieczeństwa dla najczęstszego przypadku (JPEG -> JPEG, bez
            // zmiany wymiarów): jeśli nawet przy najniższej akceptowalnej jakości wynik wciąż
            // wyszedłby większy niż oryginał, po prostu kopiujemy oryginał 1:1 zamiast go psuć.
            if (!willResize && IsJpegFile(sourcePath) && bytes.Length >= originalFileSize)
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                return;
            }

            File.WriteAllBytes(destPath, bytes);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static bool IsJpegFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg";

    /// <summary>
    /// Nigdy nie celujemy wyżej niż w rozmiar oryginału — limit z ustawień to tylko górny
    /// sufit, nie cel sam w sobie. Współdzielone przez ścieżkę SkiaSharp i awaryjną ścieżkę
    /// Windows (HEIC), żeby obie zachowywały się tak samo.
    /// </summary>
    private static long EffectiveMaxBytes(long configuredLimit, long originalFileSize) =>
        originalFileSize > 0 ? Math.Min(configuredLimit, originalFileSize) : configuredLimit;

    private const int MinAutoQuality = 20;
    private const int MaxAutoQuality = 95;

    /// <summary>
    /// Koduje obraz jako JPEG, dobierając automatycznie najwyższą jakość, która mieści się
    /// w <paramref name="maxBytes"/> (szukanie binarne — kilka prób zamiast sprawdzania
    /// każdej wartości jakości po kolei). Jeśli nawet najniższa dopuszczalna jakość
    /// (<see cref="MinAutoQuality"/>) nie wystarczy, zwraca mimo to wynik z tą jakością
    /// zamiast zgłaszać błąd — "jak najlepiej się da" jest lepsze niż brak pliku.
    /// </summary>
    private static byte[] EncodeToFitSize(SKImage image, long maxBytes)
    {
        var atMaxQuality = EncodeJpeg(image, MaxAutoQuality);
        if (atMaxQuality.Length <= maxBytes)
        {
            // Mieści się nawet przy najlepszej jakości — nie ma sensu pogarszać bardziej niż trzeba.
            return atMaxQuality;
        }

        int lo = MinAutoQuality, hi = MaxAutoQuality - 1;
        byte[]? bestFit = null;

        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var attempt = EncodeJpeg(image, mid);

            if (attempt.Length <= maxBytes)
            {
                bestFit = attempt;
                lo = mid + 1; // spróbuj jeszcze lepszej jakości, skoro ta się zmieściła
            }
            else
            {
                hi = mid - 1;
            }
        }

        return bestFit ?? EncodeJpeg(image, MinAutoQuality);
    }

    private static byte[] EncodeJpeg(SKImage image, int quality)
    {
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    /// <summary>
    /// Tworzy małą miniaturę (JPEG, dłuższy bok maks. <paramref name="maxSize"/> px) do podglądu
    /// w GUI. Zwraca <c>null</c>, jeśli pliku nie da się odczytać jako obraz — wołający powinien
    /// wtedy po prostu pokazać ikonę zastępczą zamiast miniatury.
    /// </summary>
    public static byte[]? TryCreateThumbnail(string path, int maxSize = 96)
    {
        var viaSkia = TryCreateThumbnailViaSkia(path, maxSize);
        if (viaSkia is not null)
        {
            return viaSkia;
        }

        return OperatingSystem.IsWindows() ? TryCreateThumbnailViaWindowsImaging(path, maxSize) : null;
    }

    private static byte[]? TryCreateThumbnailViaSkia(string path, int maxSize)
    {
        try
        {
            using var original = SKBitmap.Decode(path);
            return original is null ? null : CreateThumbnailBytes(original, maxSize);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CreateThumbnailBytes(SKBitmap original, int maxSize)
    {
        var longestSide = Math.Max(original.Width, original.Height);
        var scale = Math.Min(1.0, (double)maxSize / longestSide);
        var width = Math.Max(1, (int)Math.Round(original.Width * scale));
        var height = Math.Max(1, (int)Math.Round(original.Height * scale));

        SKBitmap? resized = null;
        try
        {
            resized = original.Resize(
                new SKImageInfo(width, height, original.ColorType, original.AlphaType),
                SKSamplingOptions.Default);
            var toEncode = resized ?? original;

            using var image = SKImage.FromBitmap(toEncode);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 70);
            return data.ToArray();
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
