namespace PhotoShrink.Core;

/// <summary>
/// Parametry jednego przebiegu pomniejszania/kompresji zdjęć.
/// </summary>
public sealed record ProcessOptions
{
    /// <summary>Maksymalna długość dłuższej krawędzi obrazu (px). Mniejsze zdjęcia nie są powiększane.</summary>
    public int MaxDimension { get; init; } = 1920;

    /// <summary>Jakość zapisu JPEG, 1-100.</summary>
    public int JpegQuality { get; init; } = 80;

    /// <summary>
    /// Nazwa podfolderu (tworzonego wewnątrz folderu źródłowego), do którego trafiają kopie.
    /// Oryginalne pliki nigdy nie są modyfikowane.
    /// </summary>
    public string OutputFolderName { get; init; } = "do_wyslania_mailem";
}
