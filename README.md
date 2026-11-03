<p align="center">
  <img src="assets/icon.png" width="112" height="112" alt="PhotoShrink logo" />
</p>

<h1 align="center">PhotoShrink</h1>

<p align="center">
  Drag in your photos, drag out an email-ready copy — originals untouched.
  <br />
  🇬🇧 <strong>English</strong> · <a href="README.pl.md">🇵🇱 Czytaj po polsku</a>
  <br />
  <a href="https://github.com/w84kubus/PhotoShrink/releases/tag/v0.1.5"><strong>⬇️ Download for Windows</strong></a>
  ·
  <a href="https://github.com/w84kubus/PhotoShrink/releases/tag/v0.1.5"><strong>⬇️ Download for macOS</strong></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Avalonia_UI-12-9B59B6?logo=csharp&logoColor=white" alt="Avalonia UI 12" />
  <img src="https://img.shields.io/badge/SkiaSharp-image_engine-4A9EFF" alt="SkiaSharp" />
  <img src="https://img.shields.io/badge/platform-Windows_%7C_macOS-0078D6?logo=windows11&logoColor=white" alt="Windows and macOS" />
  <img src="https://img.shields.io/badge/tests-12-22C55E?logo=xunit&logoColor=white" alt="12 tests" />
</p>

---

## Installation

Neither build is signed with a paid developer certificate, so the OS will flag it on first run
— that's expected, the app is safe, nobody paid Microsoft/Apple for a certificate for a small
personal tool.

**Windows:** run `PhotoShrink.exe`. SmartScreen will likely say *"Windows protected your PC"* —
click **"More info"** → **"Run anyway"**.

**macOS:** open `PhotoShrink.dmg`, drag `PhotoShrink.app` into `Applications`. You'll see *"Apple
could not verify ... is free of malware"* with no inline way past it — and double-clicking any
helper script hits the exact same wall, since recent macOS blocks every downloaded, unsigned
file the same way regardless of what it is. The one thing that **does** work: open Terminal and
run
```bash
xattr -cr /Applications/PhotoShrink.app
```
then launch PhotoShrink normally. No Terminal? **System Settings → Privacy & Security**, scroll
down to the blocked-app notice → **"Open Anyway"** (try opening the app first so the notice
shows up).

## About

**PhotoShrink** is a small desktop app (Windows and macOS) that does one thing: take photos
straight from the camera (several MB each, way too large for an email) and turn them into
copies small enough to attach, without touching the originals.

It's built for people who are not technical — drag photos onto the window, watch a size and
percentage next to each one, click one button, done. No "JPEG quality" slider, no folder
hunting, no config to get right first. Light, card-based interface with a consistent look on
both Windows and macOS, regardless of the system's own light/dark setting.

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
- **Explorer context menu** (Windows only) — select photos in Windows Explorer, right-click,
  *"Pomniejsz zdjęcia (PhotoShrink)"* launches the app with them already loaded (toggle on/off
  from Settings — writes to `HKEY_CURRENT_USER`, no admin rights needed)
- Unsupported files (anything that isn't a photo) are skipped with a clear message instead of
  silently vanishing
- **HEIC/HEIF** (iPhone photos) decoded via Windows' own codecs (GDI+/WIC) whenever SkiaSharp
  can't read a file — no bundled HEVC decoder, no extra licensing on PhotoShrink's part

### Processing
- Resize to a maximum dimension (default 1920 px) — never upscales a smaller photo
- **Target file size instead of a quality number** — binary search finds the highest JPEG
  quality that still fits the limit, so nobody has to know what "quality 80" means
- Never makes an already-small JPEG *bigger* — if a photo already fits the size budget without
  resizing, it's copied through untouched instead of getting re-compressed upward
- Originals are only ever read, never modified or deleted
- Copies are written next to the originals, in a `do_wyslania_mailem` subfolder

### Output
- Thumbnail, filename and live size (before → after, % saved) for every photo in the list
- A summary banner with the total saved (*"Saved 18.3 MB (81%)"*) once a batch finishes
- Remove a single photo from the list before processing, or clear the whole list
- **Drag a finished photo straight out of the list** onto any other window — an open Outlook
  compose window, Teams, Mail.app, anywhere that accepts a dropped file — to attach it directly
- One click opens the destination folder (Explorer on Windows, Finder on macOS)

### Housekeeping
- Settings (max dimension, max file size, output folder name) are remembered between launches
- A single self-contained package for each platform — nothing to install on the machine that
  runs it (a `.exe` on Windows, a proper `.app` in a `.dmg` on macOS)

## Tech stack

| Layer | Technology |
|---|---|
| UI framework | [Avalonia UI](https://avaloniaui.net/) 12 (cross-platform XAML — the same code builds and runs on macOS/Linux too) |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — source-generated `[ObservableProperty]` / `[RelayCommand]` |
| Image engine | [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT) — resize + JPEG encode |
| HEIC fallback | `System.Drawing.Common`, Windows-only, used only when SkiaSharp can't decode a file |
| Runtime | .NET 10, self-contained publish — single-file `win-x64` .exe, `.app` bundle for `osx-arm64` |
| Tests | xUnit, 12 tests covering the resize/compress/thumbnail logic |

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
publish-macos.sh        — builds PhotoShrink.app + a .dmg (Apple Silicon by default)
```

`PhotoShrink.Core` has no reference to Avalonia or anything Windows-specific, so the actual
resize/compress logic is fully unit-testable and runs the same way on every platform.

## Building

Avalonia is cross-platform, so despite targeting Windows and macOS as end-user platforms, every
part of PhotoShrink — including both final packages — can be built from macOS or Linux, no
Windows machine or CI required:

```bash
dotnet build              # whole solution
dotnet test                # PhotoShrink.Core unit tests
dotnet run --project src/PhotoShrink.App   # run the GUI locally (works on macOS too — Avalonia)

./publish-windows.sh       # self-contained win-x64 PhotoShrink.exe in dist/win-x64/
./publish-macos.sh         # PhotoShrink.app + PhotoShrink.dmg in dist/osx-arm64/ (set RID=osx-x64 for Intel Macs)
```

`dotnet publish -r win-x64 --self-contained` is what makes the Windows cross-compilation
possible — the resulting `.exe` needs nothing installed on the machine that runs it. The macOS
`.app` is just the same self-contained publish output wrapped in a proper bundle, with an
`.icns` icon built from the same source artwork as the Windows `.ico`.

## Status

Core resize/compress/drag&drop flow is verified end-to-end on real Windows 11 and on macOS
(Apple Silicon, built and tested on this same machine via the app bundle above — including the
"never grow an already-small JPEG" fix). HEIC support, drag-out-to-attach and the Windows
Explorer context menu are newer, Windows-specific, and still pending a real Windows test before
they're fully trusted.
