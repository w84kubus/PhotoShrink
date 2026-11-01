using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PhotoShrink.App.ViewModels;

namespace PhotoShrink.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
        if (folder is not null && DataContext is MainViewModel vm)
        {
            vm.SourceFolder = folder.TryGetLocalPath() ?? folder.Path.LocalPath;
        }
    }
}