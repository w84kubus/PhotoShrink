using System;
using System.IO;
using System.Text.Json;

namespace PhotoShrink.App.Models;

/// <summary>
/// Ustawienia użytkownika zapisywane między uruchomieniami (prosty plik JSON w folderze danych
/// aplikacji). Zapis/odczyt jest "najlepszym staraniem" — jeśli się nie uda (np. brak uprawnień,
/// uszkodzony plik), aplikacja po prostu wraca do wartości domyślnych zamiast się wywalać.
/// </summary>
public sealed class AppSettings
{
    public int MaxDimension { get; set; } = 1920;

    public double MaxFileSizeMb { get; set; } = 1.0;

    public string OutputFolderName { get; set; } = "do_wyslania_mailem";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoShrink",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var path = FilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Uszkodzony/nieodczytywalny plik ustawień — wracamy do domyślnych, bez wywalania aplikacji.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Zapis ustawień to tylko wygoda — jeśli się nie uda, aplikacja i tak działa dalej.
        }
    }
}
