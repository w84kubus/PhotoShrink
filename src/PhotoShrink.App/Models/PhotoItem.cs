using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PhotoShrink.App.Models;

public enum PhotoItemStatus
{
    Pending,
    Processing,
    Done,
    Failed,
}

/// <summary>
/// Jedno zdjęcie na liście w GUI — od upuszczenia/wybrania, przez przetwarzanie, po wynik.
/// Trzyma tylko stan potrzebny do wyświetlenia; samo przetwarzanie robi PhotoShrink.Core.
/// </summary>
public partial class PhotoItem : ObservableObject
{
    public PhotoItem(string filePath, long originalBytes)
    {
        FilePath = filePath;
        OriginalBytes = originalBytes;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public long OriginalBytes { get; }

    public string OriginalSizeText => FormatSize(OriginalBytes);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(OutputSizeText))]
    [NotifyPropertyChangedFor(nameof(SavedText))]
    public partial PhotoItemStatus Status { get; set; } = PhotoItemStatus.Pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputSizeText))]
    [NotifyPropertyChangedFor(nameof(SavedText))]
    public partial long OutputBytes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    /// <summary>Folder, do którego trafiła pomniejszona kopia (ustawiany po udanym przetworzeniu).</summary>
    public string? DestinationFolder { get; set; }

    /// <summary>Ustawiane przez MainViewModel przy tworzeniu elementu — usuwa ten element z listy.</summary>
    public IRelayCommand? RemoveCommand { get; set; }

    public string OutputSizeText => Status switch
    {
        PhotoItemStatus.Done => FormatSize(OutputBytes),
        PhotoItemStatus.Failed => "błąd",
        PhotoItemStatus.Processing => "…",
        _ => "—",
    };

    public string SavedText => Status == PhotoItemStatus.Done && OriginalBytes > 0 && OutputBytes <= OriginalBytes
        ? $"-{100.0 * (1 - (double)OutputBytes / OriginalBytes):0}%"
        : "";

    public string StatusText => Status switch
    {
        PhotoItemStatus.Pending => "Gotowe do zmniejszenia",
        PhotoItemStatus.Processing => "Przetwarzanie…",
        PhotoItemStatus.Done => "Gotowe",
        PhotoItemStatus.Failed => ErrorMessage is null ? "Nie udało się" : $"Nie udało się: {ErrorMessage}",
        _ => "",
    };

    public static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.0} MB" : $"{bytes / 1024.0:0} KB";
}
