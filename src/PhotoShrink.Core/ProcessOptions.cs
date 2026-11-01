namespace PhotoShrink.Core;

/// <summary>
/// Parametry jednego przebiegu pomniejszania/kompresji zdjęć.
/// </summary>
public sealed record ProcessOptions
{
    /// <summary>Maksymalna długość dłuższej krawędzi obrazu (px). Mniejsze zdjęcia nie są powiększane.</summary>
    public int MaxDimension { get; init; } = 1920;

    /// <summary>
    /// Docelowy maksymalny rozmiar pliku wynikowego (bajty). Jakość JPEG jest dobierana
    /// automatycznie (jak najwyższa, jaka się jeszcze mieści w limicie) — użytkownik
    /// podaje tylko wagę, nie musi znać pojęcia "jakości JPEG".
    /// </summary>
    public long MaxFileSizeBytes { get; init; } = 1_000_000; // ~1 MB — rozsądny domyślny limit na załącznik

    /// <summary>
    /// Nazwa podfolderu (tworzonego wewnątrz folderu źródłowego), do którego trafiają kopie.
    /// Oryginalne pliki nigdy nie są modyfikowane.
    /// </summary>
    public string OutputFolderName { get; init; } = "do_wyslania_mailem";
}
