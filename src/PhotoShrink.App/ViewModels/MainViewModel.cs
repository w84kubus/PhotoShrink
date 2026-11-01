using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoShrink.App.Models;
using PhotoShrink.App.Services;
using PhotoShrink.Core;

namespace PhotoShrink.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const string DefaultOutputFolderName = "do_wyslania_mailem";
    private const string WelcomeMessage = "Przeciągnij tutaj zdjęcia albo cały folder, żeby je zmniejszyć przed wysłaniem mailem.";

    private readonly bool _settingsLoaded;

    public MainViewModel()
    {
        var settings = AppSettings.Load();
        MaxDimension = settings.MaxDimension;
        MaxFileSizeMb = settings.MaxFileSizeMb;
        OutputFolderName = settings.OutputFolderName;
        _settingsLoaded = true;

        IsExplorerIntegrationEnabled = ExplorerIntegration.IsEnabled();

        Photos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPhotos));
            StartCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<PhotoItem> Photos { get; } = new();

    public bool HasPhotos => Photos.Count > 0;

    [ObservableProperty]
    public partial int MaxDimension { get; set; } = 1920;

    [ObservableProperty]
    public partial double MaxFileSizeMb { get; set; } = 1.0;

    [ObservableProperty]
    public partial string OutputFolderName { get; set; } = DefaultOutputFolderName;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDragOver { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = WelcomeMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    public partial string? SummaryText { get; set; }

    public bool HasSummary => !string.IsNullOrEmpty(SummaryText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExplorerIntegrationButtonText))]
    public partial bool IsExplorerIntegrationEnabled { get; set; }

    public string ExplorerIntegrationButtonText => IsExplorerIntegrationEnabled
        ? "Usuń z menu prawego przycisku myszy"
        : "Dodaj do menu prawego przycisku myszy";

    public bool ShowExplorerIntegrationOption => OperatingSystem.IsWindows();

    private CancellationTokenSource? _cts;
    private List<string> _lastDestinationFolders = new();

    private bool CanStart => !IsBusy && Photos.Count > 0;

    /// <summary>
    /// Dodaje zdjęcia na listę — ścieżka może być pojedynczym plikiem albo folderem
    /// (wtedy wszystkie obsługiwane zdjęcia z tego folderu, bez podfolderów, zostają dodane).
    /// Duplikaty (ten sam plik dodany drugi raz) są pomijane; pliki, które nie są obsługiwanymi
    /// zdjęciami, są pomijane z informacją o tym w pasku statusu.
    /// </summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        var filesToAdd = new List<string>();
        var skippedUnsupported = 0;

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                filesToAdd.AddRange(ImageProcessor.FindSourceImages(path, OutputFolderName));
            }
            else if (File.Exists(path))
            {
                if (ImageProcessor.IsSupportedImage(path))
                {
                    filesToAdd.Add(path);
                }
                else
                {
                    skippedUnsupported++;
                }
            }
        }

        var existing = new HashSet<string>(Photos.Select(p => p.FilePath), StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var file in filesToAdd)
        {
            if (!existing.Add(file))
            {
                continue;
            }

            long size;
            try
            {
                size = new FileInfo(file).Length;
            }
            catch
            {
                continue;
            }

            var item = new PhotoItem(file, size);
            item.RemoveCommand = new RelayCommand(() => Photos.Remove(item));
            Photos.Add(item);
            added++;
            _ = LoadThumbnailAsync(item);
        }

        SummaryText = null;

        var messages = new List<string>();
        if (added > 0)
        {
            messages.Add($"Gotowe do zmniejszenia: {Photos.Count} {PolishWord(Photos.Count, "zdjęcie", "zdjęcia", "zdjęć")}.");
        }

        if (skippedUnsupported > 0)
        {
            messages.Add($"Pominięto {skippedUnsupported} {PolishWord(skippedUnsupported, "plik", "pliki", "plików")} (to nie było zdjęcie).");
        }

        if (messages.Count == 0 && Photos.Count == 0)
        {
            messages.Add("Nie znaleziono zdjęć w upuszczonych plikach / folderze.");
        }

        if (messages.Count > 0)
        {
            StatusText = string.Join(" ", messages);
        }
    }

    private static async Task LoadThumbnailAsync(PhotoItem item)
    {
        var bytes = await Task.Run(() => ImageProcessor.TryCreateThumbnail(item.FilePath));
        if (bytes is null)
        {
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            await Dispatcher.UIThread.InvokeAsync(() => item.Thumbnail = bitmap);
        }
        catch
        {
            // Miniatura to tylko podgląd — jak się nie uda, zostaje ikona zastępcza.
        }
    }

    [RelayCommand]
    private void ClearPhotos()
    {
        Photos.Clear();
        _lastDestinationFolders = new List<string>();
        OpenOutputFolderCommand.NotifyCanExecuteChanged();
        SummaryText = null;
        StatusText = WelcomeMessage;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (Photos.Count == 0)
        {
            return;
        }

        IsBusy = true;
        ProgressPercent = 0;
        SummaryText = null;
        StatusText = "Przetwarzanie…";
        _cts = new CancellationTokenSource();

        var outputFolderName = string.IsNullOrWhiteSpace(OutputFolderName)
            ? DefaultOutputFolderName
            : OutputFolderName;

        var options = new ProcessOptions
        {
            MaxDimension = MaxDimension,
            MaxFileSizeBytes = Math.Max(10_000, (long)(MaxFileSizeMb * 1024 * 1024)),
            OutputFolderName = outputFolderName,
        };

        var toProcess = Photos.ToList();
        var total = toProcess.Count;
        var done = 0;
        var ok = 0;
        var failed = 0;
        long totalOriginalBytes = 0;
        long totalOutputBytes = 0;
        var destinationFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var item in toProcess)
            {
                _cts.Token.ThrowIfCancellationRequested();
                item.Status = PhotoItemStatus.Processing;
                StatusText = $"Przetwarzanie {done + 1} z {total}: {item.FileName}";

                var destFolder = Path.Combine(
                    Path.GetDirectoryName(item.FilePath) ?? Environment.CurrentDirectory,
                    outputFolderName);

                var result = await ImageProcessor.ProcessImageAsync(item.FilePath, destFolder, options, _cts.Token);

                if (result.Status == ProcessStatus.Processed)
                {
                    item.Status = PhotoItemStatus.Done;
                    item.OutputBytes = result.OutputBytes;
                    item.DestinationFolder = destFolder;
                    destinationFolders.Add(destFolder);
                    totalOriginalBytes += item.OriginalBytes;
                    totalOutputBytes += result.OutputBytes;
                    ok++;
                }
                else
                {
                    item.Status = PhotoItemStatus.Failed;
                    item.ErrorMessage = result.Message;
                    failed++;
                }

                done++;
                ProgressPercent = 100.0 * done / total;
            }

            _lastDestinationFolders = destinationFolders.ToList();
            OpenOutputFolderCommand.NotifyCanExecuteChanged();

            StatusText = failed == 0
                ? $"Gotowe! Zmniejszono {ok} {PolishWord(ok, "zdjęcie", "zdjęcia", "zdjęć")}."
                : $"Gotowe: {ok} z {total} udało się, {failed} nie — sprawdź listę poniżej.";

            if (ok > 0 && totalOriginalBytes > 0)
            {
                var savedBytes = totalOriginalBytes - totalOutputBytes;
                var savedPercent = 100.0 * savedBytes / totalOriginalBytes;
                SummaryText = savedBytes > 0
                    ? $"Zaoszczędzono {PhotoItem.FormatSize(savedBytes)} ({savedPercent:0}%) — z {PhotoItem.FormatSize(totalOriginalBytes)} do {PhotoItem.FormatSize(totalOutputBytes)}."
                    : $"Gotowe — łącznie {PhotoItem.FormatSize(totalOutputBytes)}.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Przerwano.";
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }

    [RelayCommand]
    private void ToggleExplorerIntegration()
    {
        var wantEnabled = !IsExplorerIntegrationEnabled;
        var success = wantEnabled ? ExplorerIntegration.TryEnable() : ExplorerIntegration.TryDisable();

        if (success)
        {
            IsExplorerIntegrationEnabled = wantEnabled;
            StatusText = wantEnabled
                ? "Dodano do menu prawego przycisku myszy w Eksploratorze."
                : "Usunięto z menu prawego przycisku myszy.";
        }
        else
        {
            StatusText = "Nie udało się zmienić integracji z Eksploratorem.";
        }
    }

    private bool CanOpenOutputFolder => _lastDestinationFolders.Count > 0;

    [RelayCommand(CanExecute = nameof(CanOpenOutputFolder))]
    private void OpenOutputFolder()
    {
        foreach (var folder in _lastDestinationFolders)
        {
            try
            {
                OpenFolderInFileExplorer(folder);
            }
            catch
            {
                // Najwyżej user otworzy folder ręcznie — nie wywalamy przez to aplikacji.
            }
        }
    }

    private static void OpenFolderInFileExplorer(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folder}\"",
                UseShellExecute = true,
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo { FileName = "open", Arguments = $"\"{folder}\"", UseShellExecute = true });
        }
        else if (OperatingSystem.IsLinux())
        {
            Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = $"\"{folder}\"", UseShellExecute = true });
        }
    }

    private static string PolishWord(int count, string one, string few, string many)
    {
        if (count == 1)
        {
            return one;
        }

        var lastDigit = count % 10;
        var lastTwo = count % 100;

        if (lastDigit is >= 2 and <= 4 && (lastTwo < 12 || lastTwo > 14))
        {
            return few;
        }

        return many;
    }

    private void SaveSettings()
    {
        if (!_settingsLoaded)
        {
            return;
        }

        new AppSettings
        {
            MaxDimension = MaxDimension,
            MaxFileSizeMb = MaxFileSizeMb,
            OutputFolderName = string.IsNullOrWhiteSpace(OutputFolderName) ? DefaultOutputFolderName : OutputFolderName,
        }.Save();
    }

    partial void OnIsBusyChanged(bool value) => StartCommand.NotifyCanExecuteChanged();

    partial void OnMaxDimensionChanged(int value) => SaveSettings();

    partial void OnMaxFileSizeMbChanged(double value) => SaveSettings();

    partial void OnOutputFolderNameChanged(string value) => SaveSettings();
}
