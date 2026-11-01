<p align="center">
  <img src="assets/icon.png" width="112" height="112" alt="Logo PhotoShrink" />
</p>

<h1 align="center">PhotoShrink</h1>

<p align="center">
  Przeciągasz zdjęcia, wyciągasz gotowe do maila — oryginały bez zmian.
  <br />
  <a href="README.md">🇬🇧 Read in English</a> · 🇵🇱 <strong>Polski</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Avalonia_UI-12-9B59B6?logo=csharp&logoColor=white" alt="Avalonia UI 12" />
  <img src="https://img.shields.io/badge/SkiaSharp-silnik_graficzny-4A9EFF" alt="SkiaSharp" />
  <img src="https://img.shields.io/badge/platforma-Windows_11-0078D6?logo=windows11&logoColor=white" alt="Windows 11" />
  <img src="https://img.shields.io/badge/budowane_na-macOS-000000?logo=apple&logoColor=white" alt="Zbudowane na macOS" />
  <img src="https://img.shields.io/badge/testy-11-22C55E?logo=xunit&logoColor=white" alt="11 testów" />
</p>

---

## O aplikacji

**PhotoShrink** to mała desktopowa aplikacja na Windows, która robi jedną rzecz: bierze zdjęcia
prosto z aparatu (po kilka MB każde, zdecydowanie za dużo do maila) i robi z nich kopie na tyle
małe, żeby dało się je załączyć — bez dotykania oryginałów.

Zrobiona z myślą o osobach nietechnicznych — przeciągasz zdjęcia na okno, widzisz rozmiar i
procent oszczędności przy każdym z nich, klikasz jeden przycisk, gotowe. Żadnego suwaka
"jakość JPEG", szukania folderu ani konfiguracji do ogarnięcia na starcie.

1. 🖼️ **Przeciągnij zdjęcia** (albo cały folder) — albo wybierz je zwyczajnie, klikając
2. 📏 **Ustaw docelowy rozmiar pliku** (domyślnie 1 MB) — PhotoShrink sam dobiera najlepszą
   jakość JPEG, żeby się zmieścić
3. ✅ **Kliknij raz** — zobacz na żywo rozmiar przed/po i procent oszczędności przy każdym zdjęciu
4. 📤 **Załącz je** — otwórz folder wynikowy jednym kliknięciem, albo przeciągnij zdjęcie
   bezpośrednio na otwarte okno maila

## Funkcje

### Wejście
- **Przeciągnij i upuść** — pojedyncze zdjęcia albo cały folder, z podświetleniem strefy i
  animacją potwierdzającą po upuszczeniu
- **Wybór folderu** jako alternatywa dla kogoś, kto woli kliknąć niż przeciągać
- **Menu prawego przycisku myszy w Eksploratorze** — zaznacz zdjęcia w Eksploratorze, prawy
  klik, *"Pomniejsz zdjęcia (PhotoShrink)"* odpala aplikację od razu z tymi zdjęciami na liście
  (włącz/wyłącz w Ustawieniach — zapis w `HKEY_CURRENT_USER`, bez uprawnień administratora)
- Nieobsługiwane pliki (wszystko, co nie jest zdjęciem) są pomijane z czytelnym komunikatem
  zamiast znikać bez śladu
- **HEIC/HEIF** (zdjęcia z iPhone'a) dekodowane przez wbudowane w Windows kodeki (GDI+/WIC),
  gdy tylko SkiaSharp nie potrafi odczytać pliku — bez własnego dekodera HEVC, bez dodatkowej
  licencji po stronie PhotoShrink

### Przetwarzanie
- Pomniejszanie do maksymalnego wymiaru (domyślnie 1920 px) — nigdy nie powiększa mniejszego
  zdjęcia
- **Docelowy rozmiar pliku zamiast liczby "jakości"** — szukanie binarne dobiera najwyższą
  jakość JPEG, która jeszcze mieści się w limicie, więc nikt nie musi wiedzieć, co znaczy
  "jakość 80"
- Oryginały są tylko odczytywane, nigdy nie są modyfikowane ani usuwane
- Kopie trafiają obok oryginałów, do podfolderu `do_wyslania_mailem`

### Wyjście
- Miniaturka, nazwa i rozmiar na żywo (przed → po, % oszczędności) przy każdym zdjęciu na liście
- Baner podsumowania z łączną oszczędnością (*"Zaoszczędzono 18,3 MB (81%)"*) po zakończeniu
- Usunięcie pojedynczego zdjęcia z listy przed przetworzeniem albo wyczyszczenie całej listy
- **Przeciągnięcie gotowego zdjęcia prosto z listy** na dowolne inne okno — otwarte okno "Nowa
  wiadomość" w Outlooku, Teams, cokolwiek, co przyjmuje upuszczony plik — żeby się tam załączyło
- Jedno kliknięcie otwiera folder wynikowy w Eksploratorze

### Drobiazgi
- Ustawienia (maks. wymiar, maks. rozmiar pliku, nazwa folderu wyjściowego) są zapamiętywane
  między uruchomieniami
- Jeden plik `.exe` (~100 MB, samodzielny) — nic nie trzeba instalować na komputerze, na którym
  ma działać

## Stack technologiczny

| Warstwa | Technologia |
|---|---|
| Framework UI | [Avalonia UI](https://avaloniaui.net/) 12 (cross-platformowy XAML — ten sam kod buduje się i działa też na macOS/Linux) |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — `[ObservableProperty]` / `[RelayCommand]` generowane przy kompilacji |
| Silnik graficzny | [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT) — resize + kodowanie JPEG |
| Awaryjny dekoder HEIC | `System.Drawing.Common`, tylko Windows, używany wyłącznie gdy SkiaSharp nie potrafi odczytać pliku |
| Runtime | .NET 10, samodzielny, jednoplikowy publish `win-x64` |
| Testy | xUnit, 11 testów pokrywających logikę resize/kompresji/miniatur |

## Dlaczego SkiaSharp, a nie ImageSharp

Pierwsza wersja używała `SixLabors.ImageSharp`. W trakcie budowy okazało się, że SixLabors
wymaga teraz klucza licencyjnego do buildów Release — nawet w dawniej darmowej linii 3.x. Żeby
nie zmuszać nikogo do zakładania konta do prostego, osobistego narzędzia, silnik graficzny
przełączono na **SkiaSharp** — ten sam silnik, co w Chrome i Androidzie, licencja MIT, zero
kosztów, zero kluczy.

## Architektura

```
src/PhotoShrink.Core/   — resize, kompresja, wyszukiwanie plików, miniatury — bez zależności od GUI
src/PhotoShrink.App/    — GUI w Avalonii, MVVM, integracja z Eksploratorem, drag&drop
tests/PhotoShrink.Core.Tests/
publish-windows.sh      — buduje samodzielny plik .exe dla Windows
```

`PhotoShrink.Core` nie ma żadnej zależności od Avalonii ani niczego specyficznego dla Windows,
więc właściwa logika resize/kompresji jest w pełni testowalna jednostkowo i działa tak samo na
każdej platformie.

## Budowanie

PhotoShrink celuje w Windows, ale każdą jego część — łącznie z finalnym `.exe` — da się
zbudować z macOS albo Linuksa, bez maszyny Windows czy CI:

```bash
dotnet build               # całe solution
dotnet test                 # testy jednostkowe PhotoShrink.Core
dotnet run --project src/PhotoShrink.App   # odpala GUI lokalnie (działa też na macOS — Avalonia)

./publish-windows.sh        # samodzielny PhotoShrink.exe (win-x64) w dist/win-x64/
```

To `dotnet publish -r win-x64 --self-contained` umożliwia tę cross-kompilację — gotowy `.exe`
nie wymaga niczego zainstalowanego na komputerze z Windows, na którym ma działać.

## Status

Główny przepływ (resize/kompresja/drag&drop) jest zbudowany i zweryfikowany od początku do
końca na prawdziwym Windows 11. Obsługa HEIC, przeciąganie gotowych zdjęć do innych aplikacji
oraz menu Eksploratora są nowsze i wciąż czekają na realny test na Windows, zanim będzie można
im w pełni zaufać.
