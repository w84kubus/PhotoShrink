#!/usr/bin/env bash
# Buduje samodzielny, jednoplikowy PhotoShrink.exe dla Windows (x64) z macOS/Linux
# — nie wymaga maszyny Windows ani CI. Wynik: dist/win-x64/PhotoShrink.exe
#
# Użycie:  ./publish-windows.sh
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

OUT_DIR="dist/win-x64"
rm -rf "$OUT_DIR"

dotnet publish src/PhotoShrink.App/PhotoShrink.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none \
  -o "$OUT_DIR"

# Natywne pliki .pdb dociągane przez pakiety SkiaSharp.NativeAssets.* nie są potrzebne
# do działania (to tylko symbole debugowania bibliotek natywnych) — usuwamy je,
# żeby w dist/win-x64 został tylko sam PhotoShrink.exe.
find "$OUT_DIR" -name "*.pdb" -delete

echo ""
echo "Gotowe: $OUT_DIR/PhotoShrink.exe"
ls -lh "$OUT_DIR"
