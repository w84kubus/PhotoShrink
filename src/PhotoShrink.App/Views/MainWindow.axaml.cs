using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PhotoShrink.App.Models;
using PhotoShrink.App.ViewModels;

namespace PhotoShrink.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (Vm is not null)
        {
            Vm.IsDragOver = true;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (Vm is not null)
        {
            Vm.IsDragOver = false;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var vm = Vm;
        if (vm is null)
        {
            return;
        }

        vm.IsDragOver = false;

        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            return;
        }

        var items = e.DataTransfer.TryGetFiles();
        if (items is null)
        {
            return;
        }

        var paths = items
            .Select(i => i.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToList();

        if (paths.Count == 0)
        {
            return;
        }

        vm.AddPaths(paths);
        PulseDropZone();
    }

    private async void PulseDropZone()
    {
        DropZoneBorder.Classes.Add("pulse");
        await Task.Delay(260);
        DropZoneBorder.Classes.Remove("pulse");
    }

    private async void OnPickFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Wybierz folder ze zdjęciami",
            AllowMultiple = false,
        });

        var folder = folders.FirstOrDefault();
        var path = folder?.TryGetLocalPath();
        if (path is not null)
        {
            Vm?.AddPaths(new[] { path });
        }
    }

    /// <summary>
    /// Pozwala "wyciągnąć" zdjęcie z listy przeciągnięciem — np. prosto na okno "Nowa wiadomość"
    /// w Outlooku, żeby się tam załączyło. Przeciąga się gotową, pomniejszoną kopię, jeśli już
    /// istnieje; w przeciwnym razie oryginał (coś lepszego niż nic).
    /// </summary>
    private async void OnPhotoRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not PhotoItem item)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Nie zaczynaj przeciągania, jeśli kliknięcie trafiło w przycisk "✕" (usuń z listy) —
        // ten ma swoją własną obsługę kliknięcia.
        if (e.Source is Visual sourceVisual && sourceVisual.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var pathToDrag = item is { Status: PhotoItemStatus.Done, DestinationFolder: not null }
            ? Path.Combine(item.DestinationFolder, Path.GetFileNameWithoutExtension(item.FilePath) + ".jpg")
            : item.FilePath;

        if (!File.Exists(pathToDrag))
        {
            return;
        }

        var topLevel = GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var storageFile = await topLevel.StorageProvider.TryGetFileFromPathAsync(pathToDrag);
        if (storageFile is null)
        {
            return;
        }

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.CreateFile(storageFile));

        await DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Copy);
    }
}
