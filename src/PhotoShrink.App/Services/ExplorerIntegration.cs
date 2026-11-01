using System;
using Microsoft.Win32;

namespace PhotoShrink.App.Services;

/// <summary>
/// Dodaje/usuwa wpis "Pomniejsz zdjęcia (PhotoShrink)" w menu prawego przycisku myszy
/// w Eksploratorze Windows (zaznacz pliki → prawy klik → od razu odpala PhotoShrink z tymi
/// plikami). Działa wyłącznie w HKEY_CURRENT_USER — nie wymaga uprawnień administratora
/// i da się w każdej chwili cofnąć.
///
/// Każda metoda sama sprawdza <see cref="OperatingSystem.IsWindows"/> i na innych platformach
/// po prostu nic nie robi — dzięki temu reszta aplikacji (w tym testy) może wołać tę klasę
/// bez żadnych warunków, także podczas developmentu na macOS.
/// </summary>
public static class ExplorerIntegration
{
    private const string KeyPath = @"Software\Classes\*\shell\PhotoShrink";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryEnable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return false;
            }

            using var shellKey = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (shellKey is null)
            {
                return false;
            }

            shellKey.SetValue("", "Pomniejsz zdjęcia (PhotoShrink)");
            shellKey.SetValue("Icon", $"\"{exePath}\"");
            // "Player": Eksplorator odpala PhotoShrink.exe JEDEN raz z wszystkimi zaznaczonymi
            // plikami jako argumentami, zamiast osobnego uruchomienia na każdy plik z osobna.
            shellKey.SetValue("MultiSelectModel", "Player");

            using var commandKey = shellKey.CreateSubKey("command");
            commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryDisable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
