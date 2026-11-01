using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PhotoShrink.App.ViewModels;
using PhotoShrink.App.Views;

namespace PhotoShrink.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            // Gdy aplikacja jest odpalona z menu "Pomniejsz zdjęcia" w Eksploratorze (prawy klik
            // na zaznaczonych plikach), Windows przekazuje ścieżki jako argumenty wiersza poleceń —
            // ładujemy je od razu na listę, zamiast pokazywać pustą stronę startową.
            var launchPaths = (desktop.Args ?? Array.Empty<string>())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

            if (launchPaths.Count > 0)
            {
                viewModel.AddPaths(launchPaths);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}