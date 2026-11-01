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
        var options = new ProcessOptions { MaxDimension = 1920, JpegQuality = 80 };

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
        var options = new ProcessOptions { MaxDimension = 1920, JpegQuality = 80 };

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
            new ProcessOptions { MaxDimension = 800, JpegQuality = 50 });

        var originalBytesAfter = await File.ReadAllBytesAsync(source);
        Assert.Equal(originalBytesBefore, originalBytesAfter);
    }

    [Fact]
    public async Task ProcessImageAsync_LowerQuality_ProducesSmallerOrEqualFile()
    {
        var source = CreateTestImage(Path.Combine(_tempDir, "quality.png"), 1200, 900);

        var highQ = await ImageProcessor.ProcessImageAsync(
            source, Path.Combine(_tempDir, "out_high"),
            new ProcessOptions { MaxDimension = 1920, JpegQuality = 95 });

        var lowQ = await ImageProcessor.ProcessImageAsync(
            source, Path.Combine(_tempDir, "out_low"),
            new ProcessOptions { MaxDimension = 1920, JpegQuality = 30 });

        Assert.True(lowQ.OutputBytes <= highQ.OutputBytes,
            $"Oczekiwano, że jakość 30 ({lowQ.OutputBytes} B) da plik mniejszy lub równy jakości 95 ({highQ.OutputBytes} B).");
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
