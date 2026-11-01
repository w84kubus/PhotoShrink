namespace PhotoShrink.Core;

public enum ProcessStatus
{
    Processed,
    Failed,
}

/// <summary>Wynik przetworzenia jednego pliku.</summary>
public sealed record ProcessResult(
    string FileName,
    ProcessStatus Status,
    long OriginalBytes,
    long OutputBytes,
    string? Message = null)
{
    /// <summary>Procent, o jaki zmniejszył się rozmiar pliku (0 jeśli brak danych albo plik się powiększył).</summary>
    public double SavedPercent =>
        OriginalBytes > 0 && OutputBytes <= OriginalBytes
            ? 100.0 * (1 - (double)OutputBytes / OriginalBytes)
            : 0;
}
