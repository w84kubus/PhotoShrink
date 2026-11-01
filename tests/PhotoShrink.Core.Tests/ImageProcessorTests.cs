using PhotoShrink.Core;
using SkiaSharp;

namespace PhotoShrink.Core.Tests;

/// <summary>
/// Każdy test tworzy własny, tymczasowy folder i czyści go na końcu,
/// więc testy mogą działać równolegle i nie zostawiają śmieci na dysku.
/// </summary>
public sealed class ImageProcessorTests : IDisposable
{
    private readonly string _tempDir;

    public ImageProcessorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PhotoShrinkTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }

    /// <summary>
    /// Tworzy testowy PNG wypełniony deterministycznym szumem (nie jednolitym kolorem),
    /// żeby kompresja JPEG przy różnych poziomach jakości dawała realnie różne rozmiary plików.
    /// </summary>
    private static string CreateTestImage(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888));
        var rnd = new Random(12345);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);

        return path;
    }

    /// <summary>
    /// Tworzy testowy PNG z płynnym gradientem + niewielkim szumem — dużo bardziej reprezentatywny
    /// dla prawdziwego zdjęcia (skompresowalny przez JPEG) niż czysty losowy szum z <see cref="CreateTestImage"/>,
    /// który jest praktycznie nieskompresowalny i zniekształcałby testy limitu wagi pliku.
    /// </summary>
    private static string CreateCompressibleTestImage(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888));
        var rnd = new Random(12345);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var baseR = x * 255 / width;
                var baseG = y * 255 / height;
                var baseB = (x + y) * 255 / (width + height);
                var noise = rnd.Next(-20, 21);

                bitmap.SetPixel(x, y, new SKColor(
                    (byte)Math.Clamp(baseR + noise, 0, 255),
                    (byte)Math.Clamp(baseG + noise, 0, 255),
                    (byte)Math.Clamp(baseB + noise, 0, 255)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);

        return path;
    }

    private static (int Width, int Height) GetImageSize(string path)
    {
        using var bitmap = SKBitmap.Decode(path);
        return (bitmap.Width, bitmap.Height);
    }

    [Fact]
    public void IsSupportedImage_RecognizesCommonExtensions()
    {
        Assert.True(ImageProcessor.IsSupportedImage("foo.jpg"));
        Assert.True(ImageProcessor.IsSupportedImage("FOO.JPEG"));
        Assert.True(ImageProcessor.IsSupportedImage("foo.png"));
        Assert.False(ImageProcessor.IsSupportedImage("foo.txt"));
        Assert.False(ImageProcessor.IsSupportedImage("foo.docx"));
    }

    [Fact]
    public void FindSourceImages_ExcludesOutputSubfolder_SoRerunDoesNotReprocess()
    {
        const string outputName = "do_wyslania_mailem";
        CreateTestImage(Path.Combine(_tempDir, "a.png"), 100, 100);
        CreateTestImage(Path.Combine(_tempDir, "b.png"), 100, 100);
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "not an image");

        var outputDir = Path.Combine(_tempDir, outputName);
        Directory.CreateDirectory(outputDir);
        CreateTestImage(Path.Combine(outputDir, "a.jpg"), 50, 50);

        var found = ImageProcessor.FindSourceImages(_tempDir, outputName);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, f => f.EndsWith("a.png"));
        Assert.Contains(found, f => f.EndsWith("b.png"));
        Assert.All(found, f => Assert.NotEqual(
            Path.GetFullPath(outputDir),
            Path.GetFullPath(Path.GetDirectoryName(f)!),
            StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProcessImageAsync_ResizesLargeImage_PreservingAspectRatio()
    {
        var source = CreateTestImage(Path.Combine(_tempDir, "big.png"), 4000, 3000);
        var destFolder = Path.Combine(_tempDir, "out");
        var options = new ProcessOptions { MaxDimension = 1920, MaxFileSizeBytes = 2_000_000 };

        var result = await ImageProcessor.ProcessImageAsync(source, destFolder, options);

        Assert.Equal(ProcessStatus.Processed, result.Status);

        var destPath = Path.Combine(destFolder, "big.jpg");
        Assert.True(File.Exists(destPath));

        var (w, h) = GetImageSize(destPath);
        Assert.True(w <= 1920 && h <= 1920);
        // 4000x3000 -> stosunek 4:3 powinien zostać zachowany
        var originalRatio = 4000.0 / 3000.0;
        var outputRatio = (double)w / h;
        Assert.InRange(outputRatio, originalRatio - 0.01, originalRatio + 0.01);
    }

    [Fact]
    public async Task ProcessImageAsync_DoesNotUpscale_SmallImage()
    {
        var source = CreateTestImage(Path.Combine(_tempDir, "small.png"), 400, 300);
        var destFolder = Path.Combine(_tempDir, "out");
        var options = new ProcessOptions { MaxDimension = 1920, MaxFileSizeBytes = 2_000_000 };

        await ImageProcessor.ProcessImageAsync(source, destFolder, options);

        var (w, h) = GetImageSize(Path.Combine(destFolder, "small.jpg"));
        Assert.Equal(400, w);
        Assert.Equal(300, h);
    }

    [Fact]
    public async Task ProcessImageAsync_NeverModifiesOriginalFile()
    {
        var source = CreateTestImage(Path.Combine(_tempDir, "original.png"), 2500, 2000);
        var originalBytesBefore = await File.ReadAllBytesAsync(source);

        await ImageProcessor.ProcessImageAsync(source, Path.Combine(_tempDir, "out"),
            new ProcessOptions { MaxDimension = 800, MaxFileSizeBytes = 300_000 });

        var originalBytesAfter = await File.ReadAllBytesAsync(source);
        Assert.Equal(originalBytesBefore, originalBytesAfter);
    }

    [Fact]
    public async Task ProcessImageAsync_RespectsMaxFileSize_WhenLimitIsTight()
    {
        var source = CreateCompressibleTestImage(Path.Combine(_tempDir, "sizelimit.png"), 2000, 1500);
        const long limit = 150_000; // 150 KB — mało, powinno wymusić niższą jakość

        var result = await ImageProcessor.ProcessImageAsync(
            source, Path.Combine(_tempDir, "out"),
            new ProcessOptions { MaxDimension = 1920, MaxFileSizeBytes = limit });

        Assert.Equal(ProcessStatus.Processed, result.Status);
        // Szukanie binarne jest przybliżone (krok co 1 w jakości) — dajemy mały margines.
        Assert.True(result.OutputBytes <= limit * 1.05,
            $"Oczekiwano pliku <= {limit} B (z marginesem), otrzymano {result.OutputBytes} B.");
    }

    [Fact]
    public async Task ProcessImageAsync_TighterLimit_ProducesSmallerOrEqualFile_ThanLooserLimit()
    {
        var source = CreateTestImage(Path.Combine(_tempDir, "quality.png"), 1200, 900);

        var loose = await ImageProcessor.ProcessImageAsync(
            source, Path.Combine(_tempDir, "out_loose"),
            new ProcessOptions { MaxDimension = 1920, MaxFileSizeBytes = 5_000_000 });

        var tight = await ImageProcessor.ProcessImageAsync(
            source, Path.Combine(_tempDir, "out_tight"),
            new ProcessOptions { MaxDimension = 1920, MaxFileSizeBytes = 80_000 });

        Assert.True(tight.OutputBytes <= loose.OutputBytes,
            $"Oczekiwano, że ciaśniejszy limit ({tight.OutputBytes} B) da plik mniejszy lub równy niż luźny limit ({loose.OutputBytes} B).");
    }

    [Fact]
    public async Task ProcessFolderAsync_ProcessesAllImages_AndReportsProgress()
    {
        CreateTestImage(Path.Combine(_tempDir, "p1.png"), 1000, 1000);
        CreateTestImage(Path.Combine(_tempDir, "p2.png"), 1000, 1000);
        CreateTestImage(Path.Combine(_tempDir, "p3.png"), 1000, 1000);

        var reported = new List<ProcessResult>();
        var progress = new Progress<ProcessResult>(r => reported.Add(r));

        var results = await ImageProcessor.ProcessFolderAsync(
            _tempDir, new ProcessOptions(), progress);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(ProcessStatus.Processed, r.Status));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "do_wyslania_mailem")));
    }

    [Fact]
    public void TryCreateThumbnail_ReturnsSmallJpeg_ForValidImage()
    {
        var source = CreateTestImage(Path.Combine(_tempDir, "thumb_source.png"), 3000, 2000);

        var bytes = ImageProcessor.TryCreateThumbnail(source, maxSize: 96);

        Assert.NotNull(bytes);
        using var ms = new MemoryStream(bytes!);
        using var thumb = SKBitmap.Decode(ms);
        Assert.True(thumb.Width <= 96 && thumb.Height <= 96);
        // 3000x2000 -> stosunek 3:2 powinien zostać zachowany
        Assert.InRange((double)thumb.Width / thumb.Height, 1.5 - 0.05, 1.5 + 0.05);
    }

    [Fact]
    public void TryCreateThumbnail_ReturnsNull_ForUnreadableFile()
    {
        var badFile = Path.Combine(_tempDir, "not_an_image.jpg");
        File.WriteAllText(badFile, "to nie jest obraz");

        var bytes = ImageProcessor.TryCreateThumbnail(badFile);

        Assert.Null(bytes);
    }

    [Fact]
    public async Task ProcessImageAsync_UnreadableFile_ReturnsFailedResult_DoesNotThrow()
    {
        var badFile = Path.Combine(_tempDir, "broken.jpg");
        await File.WriteAllTextAsync(badFile, "to nie jest prawdziwy plik JPEG");

        var result = await ImageProcessor.ProcessImageAsync(
            badFile, Path.Combine(_tempDir, "out"), new ProcessOptions());

        Assert.Equal(ProcessStatus.Failed, result.Status);
        Assert.NotNull(result.Message);
    }
}
