using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoShrink.Core;

namespace PhotoShrink.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string? SourceFolder { get; set; }

    [ObservableProperty]
    public partial int MaxDimension { get; set; } = 1920;

    [ObservableProperty]
    public partial int JpegQuality { get; set; } = 80;

    [ObservableProperty]
    public partial string OutputFolderName { get; set; } = "do_wyslania_mailem";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Wybierz folder ze zdjęciami.";

    public ObservableCollection<string> LogLines { get; } = new();

    private CancellationTokenSource? _cts;

    private bool CanStart => !IsBusy && !string.IsNullOrWhiteSpace(SourceFolder);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceFolder))
        {
            return;
        }

        IsBusy = true;
        ProgressPercent = 0;
        LogLines.Clear();
        StatusText = "Przetwarzanie...";
        _cts = new CancellationTokenSource();

        try
        {
            var outputFolderName = string.IsNullOrWhiteSpace(OutputFolderName)
                ? "do_wyslania_mailem"
                : OutputFolderName;

            var options = new ProcessOptions
            {
                MaxDimension = MaxDimension,
                JpegQuality = JpegQuality,
                OutputFolderName = outputFolderName,
            };

            var files = ImageProcessor.FindSourceImages(SourceFolder, options.OutputFolderName);
            var total = files.Count;

            if (total == 0)
            {
                StatusText = "Nie znaleziono zdjęć w wybranym folderze.";
                return;
            }

            var done = 0;
            var progress = new Progress<ProcessResult>(r =>
            {
                done++;
                ProgressPercent = 100.0 * done / total;
                LogLines.Insert(0, FormatLine(r));
            });

            var results = await ImageProcessor.ProcessFolderAsync(SourceFolder, options, progress, _cts.Token);

            var ok = results.Count(r => r.Status == ProcessStatus.Processed);
            var failed = results.Count(r => r.Status == ProcessStatus.Failed);

            StatusText = failed == 0
                ? $"Gotowe: {ok} zdjęć zapisano w \"{options.OutputFolderName}\"."
                : $"Gotowe: {ok} zdjęć OK, {failed} błędów (patrz log poniżej).";
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

    private static string FormatLine(ProcessResult r) => r.Status switch
    {
        ProcessStatus.Processed =>
            $"OK   {r.FileName}   {FormatSize(r.OriginalBytes)} -> {FormatSize(r.OutputBytes)}  (-{r.SavedPercent:0}%)",
        ProcessStatus.Failed => $"X    {r.FileName}   — {r.Message}",
        _ => $"?    {r.FileName}",
    };

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.0} MB" : $"{bytes / 1024.0:0} KB";

    partial void OnIsBusyChanged(bool value) => StartCommand.NotifyCanExecuteChanged();

    partial void OnSourceFolderChanged(string? value) => StartCommand.NotifyCanExecuteChanged();
}
