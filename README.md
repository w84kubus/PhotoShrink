<p align="center">
  <img src="assets/icon.png" width="112" height="112" alt="PhotoShrink logo" />
</p>

<h1 align="center">PhotoShrink</h1>

<p align="center">
  Drag in your photos, drag out an email-ready copy — originals untouched.
  <br />
  🇬🇧 <strong>English</strong> · <a href="README.pl.md">🇵🇱 Czytaj po polsku</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Avalonia_UI-12-9B59B6?logo=csharp&logoColor=white" alt="Avalonia UI 12" />
  <img src="https://img.shields.io/badge/SkiaSharp-image_engine-4A9EFF" alt="SkiaSharp" />
  <img src="https://img.shields.io/badge/platform-Windows_11-0078D6?logo=windows11&logoColor=white" alt="Windows 11" />
  <img src="https://img.shields.io/badge/built_on-macOS-000000?logo=apple&logoColor=white" alt="Built on macOS" />
  <img src="https://img.shields.io/badge/tests-11-22C55E?logo=xunit&logoColor=white" alt="11 tests" />
</p>

---

## About

**PhotoShrink** is a small Windows desktop app that does one thing: take photos straight from
the camera (several MB each, way too large for an email) and turn them into copies small enough
to attach, without touching the originals.

It's built for people who are not technical — drag photos onto the window, watch a size and
percentage next to each one, click one button, done. No "JPEG quality" slider, no folder
hunting, no config to get right first.

1. 🖼️ **Drag in photos** (or a whole folder) — or pick them the normal way
2. 📏 **Set a target file size** (default 1 MB) — PhotoShrink finds the best JPEG quality for it
3. ✅ **Click once** — see a live before/after size and percentage saved, per photo
4. 📤 **Attach them** — open the output folder in one click, or drag a photo straight onto an
   open email window

## Features

### Input
- **Drag & drop** — individual photos or a whole folder, with a highlighted drop zone and a
  confirmation animation on drop
- **Folder picker** as a fallback for anyone who'd rather click than drag
- **Explorer context menu** — select photos in Windows Explorer, right-click, *"Pomniejsz
  zdjęcia (PhotoShrink)"* launches the app with them already loaded (toggle on/off from
  Settings — writes to `HKEY_CURRENT_USER`, no admin rights needed)
- Unsupported files (anything that isn't a photo) are skipped with a clear message instead of
  silently vanishing
- **HEIC/HEIF** (iPhone photos) decoded via Windows' own codecs (GDI+/WIC) whenever SkiaSharp
  can't read a file — no bundled HEVC decoder, no extra licensing on PhotoShrink's part

### Processing
- Resize to a maximum dimension (default 1920 px) — never upscales a smaller photo
- **Target file size instead of a quality number** — binary search finds the highest JPEG
  quality that still fits the limit, so nobody has to know what "quality 80" means
- Originals are only ever read, never modified or deleted
- Copies are written next to the originals, in a `do_wyslania_mailem` subfolder

### Output
- Thumbnail, filename and live size (before → after, % saved) for every photo in the list
- A summary banner with the total saved (*"Saved 18.3 MB (81%)"*) once a batch finishes
- Remove a single photo from the list before processing, or clear the whole list
- **Drag a finished photo straight out of the list** onto any other window — an open Outlook
  compose window, Teams, anywhere that accepts a dropped file — to attach it directly
- One click opens the destination folder in Explorer

### Housekeeping
- Settings (max dimension, max file size, output folder name) are remembered between launches
- A single ~100 MB self-contained `.exe` — nothing to install on the machine that runs it

## Tech stack

| Layer | Technology |
|---|---|
| UI framework | [Avalonia UI](https://avaloniaui.net/) 12 (cross-platform XAML — the same code builds and runs on macOS/Linux too) |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — source-generated `[ObservableProperty]` / `[RelayCommand]` |
| Image engine | [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT) — resize + JPEG encode |
| HEIC fallback | `System.Drawing.Common`, Windows-only, used only when SkiaSharp can't decode a file |
| Runtime | .NET 10, self-contained single-file `win-x64` publish |
| Tests | xUnit, 11 tests covering the resize/compress/thumbnail logic |

## Why SkiaSharp, not ImageSharp

The first version used `SixLabors.ImageSharp`. Partway through, it turned out SixLabors now
requires a license key for Release builds — even on the formerly-free 3.x line. Rather than
make anyone register an account for a small personal tool, the image engine was swapped to
**SkiaSharp** — the same graphics engine behind Chrome and Android, MIT-licensed, no cost, no
key.

## Architecture

```
src/PhotoShrink.Core/   — resize, compression, file discovery, thumbnails — no UI dependency
src/PhotoShrink.App/    — Avalonia UI, MVVM, Explorer integration, drag&drop
tests/PhotoShrink.Core.Tests/
publish-windows.sh      — builds the self-contained Windows .exe
```

`PhotoShrink.Core` has no reference to Avalonia or anything Windows-specific, so the actual
resize/compress logic is fully unit-testable and runs the same way on every platform.

## Building

PhotoShrink targets Windows, but every part of it — including the final `.exe` — can be built
from macOS or Linux, no Windows machine or CI required:

```bash
dotnet build              # whole solution
dotnet test                # PhotoShrink.Core unit tests
dotnet run --project src/PhotoShrink.App   # run the GUI locally (works on macOS too — Avalonia)

./publish-windows.sh       # self-contained win-x64 PhotoShrink.exe in dist/win-x64/
```

`dotnet publish -r win-x64 --self-contained` is what makes the cross-compilation possible —
the resulting `.exe` needs nothing installed on the Windows machine that runs it.

## Status

Core resize/compress/drag&drop flow is built and verified end-to-end on real Windows 11. HEIC
support, drag-out-to-attach and the Explorer context menu are newer and still pending a real
Windows test before they're fully trusted.
