# PhotoShrink

Desktopowa aplikacja (Windows) do pomniejszania i kompresowania zdjęć przed wysyłką mailem.
Wybierasz folder ze zdjęciami, maksymalny wymiar (domyślnie 1920 px) i jakość JPEG
(domyślnie 80) — aplikacja zapisuje pomniejszone kopie do podfolderu
`do_wyslania_mailem`, **nie dotykając oryginałów**.

Następca skryptu PowerShell `zmniejsz-zdjecia.ps1` — ta sama idea, ale jako
prawdziwa aplikacja desktopowa z GUI, zamiast skryptu do odpalania z terminala.

## Stack

- **.NET 10** (C#)
- **Avalonia UI 12** — cross-platformowy framework XAML (ten sam kod da się
  budować i odpalać na macOS/Linux, nie tylko na Windows — stąd możliwość
  developmentu i testów na tym komputerze)
- **CommunityToolkit.Mvvm** — source-generated MVVM (`[ObservableProperty]`, `[RelayCommand]`)
- **SkiaSharp** — resize + kodowanie JPEG (licencja MIT, bez kosztów i bez
  kluczy licencyjnych — patrz sekcja "Dlaczego SkiaSharp, a nie ImageSharp")
- **xUnit** — testy logiki w `PhotoShrink.Core`

## Struktura

```
src/PhotoShrink.Core/   — logika (resize, kompresja, wyszukiwanie plików) — bez zależności od GUI
src/PhotoShrink.App/    — GUI (Avalonia, MVVM)
tests/PhotoShrink.Core.Tests/ — testy jednostkowe logiki
publish-windows.sh      — budowa samodzielnego PhotoShrink.exe dla Windows
```

## Rozwój / testy (na tym komputerze, macOS)

```bash
# jednorazowo, jeśli dotnet nie jest jeszcze w PATH w nowej sesji terminala:
export PATH="$HOME/.dotnet:$PATH"

dotnet build            # budowa całego solution
dotnet test             # testy logiki PhotoShrink.Core
dotnet run --project src/PhotoShrink.App   # odpala GUI lokalnie (na macOS też działa — Avalonia)
```

## Budowa Windows .exe

```bash
./publish-windows.sh
```

Tworzy **samodzielny, jednoplikowy** `dist/win-x64/PhotoShrink.exe` (~100 MB —
zawiera cały runtime .NET, więc na komputerze docelowym **nie trzeba niczego
instalować**, wystarczy skopiować i odpalić plik). Budowane bezpośrednio z
macOS, bez potrzeby posiadania Windows czy CI — `dotnet publish -r win-x64`
potrafi cross-kompilować.

Plik `.exe` nie jest podpisany certyfikatem (code signing) — Windows
SmartScreen przy pierwszym uruchomieniu prawdopodobnie pokaże ostrzeżenie
"Windows ochronił Twój komputer" (typowe dla niepodpisanych .exe od małych
projektów). Trzeba kliknąć "Więcej informacji" → "Uruchom mimo to".

## Dlaczego SkiaSharp, a nie ImageSharp

Pierwsza wersja używała `SixLabors.ImageSharp` (popularna biblioteka do
obrazów w .NET). W trakcie budowy okazało się, że SixLabors zmienił politykę
licencyjną: **nawet darmowa linia 3.x wymaga teraz klucza licencyjnego do
buildów Release** (ostrzeżenie `No Six Labors license found` + link do
`sixlabors.com/pricing`). Żeby nie obciążać Cię koniecznością rejestracji
konta/klucza do prostego narzędzia na własne potrzeby, projekt przełączono na
**SkiaSharp** (ten sam silnik graficzny co w Chrome/Android, licencja MIT,
zero kosztów, zero kluczy).

## Co zrobione

- [x] Logika: wyszukiwanie zdjęć w folderze, resize (bez powiększania mniejszych
      zdjęć), kompresja JPEG z ustawialną jakością, zapis do podfolderu,
      oryginały bez zmian.
- [x] GUI: wybór folderu, maks. wymiar, jakość JPEG (slider), nazwa podfolderu,
      przycisk start, progress bar, log wyników.
- [x] 8 testów jednostkowych logiki (resize, brak powiększania, nienaruszone
      oryginały, wpływ jakości na rozmiar pliku, obsługa błędów, pomijanie
      podfolderu wyjściowego przy ponownym uruchomieniu).
- [x] Build + testy + start GUI zweryfikowane lokalnie na macOS.
- [x] Cross-kompilacja do natywnego Windows `.exe` zweryfikowana (prawdziwy
      plik PE32+ dla Windows, zbudowany na macOS).

## Co zostało do dopracowania

- **Nie przetestowano realnie na Windows** (ten komputer to Mac — build jest
  poprawny i plik to prawdziwy Windows PE, ale faktyczne odpalenie `.exe` i
  kliknięcie w prawdziwym Windows GUI wymaga przetestowania na Twojej maszynie
  z Windows).
- Rozmiar `.exe` (~100 MB) można zmniejszyć (trimming / ReadyToRun), ale to
  wymaga więcej testów zgodności z Avalonia/Skia — na razie priorytetem było
  "działa na pewno", nie "jak najmniejsze".
- Brak własnej ikony aplikacji (używa domyślnego logo Avalonii) — do wymiany
  na coś własnego, jeśli chcesz.
- Brak instalatora (.msi/setup) — na razie to gotowy do uruchomienia plik
  `.exe` ("portable"). Jeśli chcesz prawdziwy installer z ikoną w Menu Start,
  można dodać np. Inno Setup lub WiX.
- Plik nie jest podpisany certyfikatem code-signing (patrz uwaga o
  SmartScreen wyżej).
- Repozytorium git zostało zainicjalizowane i wszystko dodane do staging
  (`git add -A`), ale **nic nie zostało jeszcze zacommitowane** — zrób to
  sam, kiedy uznasz, że jesteś zadowolony z zawartości.
