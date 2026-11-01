#!/usr/bin/env bash
# Buduje prawdziwą aplikację macOS (PhotoShrink.app) z tego samego kodu co wersja Windows
# — Avalonia jest cross-platformowa, więc to nie jest osobny port, tylko inny sposób pakowania.
# Domyślnie na Apple Silicon (arm64); dla Intela ustaw RID=osx-x64.
#
# Użycie:  ./publish-macos.sh
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

RID="${RID:-osx-arm64}"
OUT_DIR="dist/$RID"
PUBLISH_DIR="$OUT_DIR/publish"
APP_DIR="$OUT_DIR/PhotoShrink.app"
ICONSET_DIR="$OUT_DIR/PhotoShrink.iconset"

rm -rf "$OUT_DIR"
mkdir -p "$PUBLISH_DIR"

echo "==> dotnet publish ($RID)"
dotnet publish src/PhotoShrink.App/PhotoShrink.App.csproj \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:DebugType=none \
  -o "$PUBLISH_DIR"

echo "==> ikona .icns"
mkdir -p "$ICONSET_DIR"
MASTER="src/PhotoShrink.App/Assets/icon-1024.png"
for sz in 16 32 128 256 512; do
  sips -z "$sz" "$sz" "$MASTER" --out "$ICONSET_DIR/icon_${sz}x${sz}.png" >/dev/null
done
sips -z 32 32   "$MASTER" --out "$ICONSET_DIR/icon_16x16@2x.png" >/dev/null
sips -z 64 64   "$MASTER" --out "$ICONSET_DIR/icon_32x32@2x.png" >/dev/null
sips -z 256 256 "$MASTER" --out "$ICONSET_DIR/icon_128x128@2x.png" >/dev/null
sips -z 512 512 "$MASTER" --out "$ICONSET_DIR/icon_256x256@2x.png" >/dev/null
sips -z 1024 1024 "$MASTER" --out "$ICONSET_DIR/icon_512x512@2x.png" >/dev/null
iconutil -c icns "$ICONSET_DIR" -o "$OUT_DIR/PhotoShrink.icns"

echo "==> składanie PhotoShrink.app"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp -R "$PUBLISH_DIR/." "$APP_DIR/Contents/MacOS/"
cp "$OUT_DIR/PhotoShrink.icns" "$APP_DIR/Contents/Resources/PhotoShrink.icns"
chmod +x "$APP_DIR/Contents/MacOS/PhotoShrink"

cat > "$APP_DIR/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>PhotoShrink</string>
  <key>CFBundleDisplayName</key><string>PhotoShrink</string>
  <key>CFBundleIdentifier</key><string>pl.w84kubus.photoshrink</string>
  <key>CFBundleVersion</key><string>0.1.0</string>
  <key>CFBundleShortVersionString</key><string>0.1.0</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleExecutable</key><string>PhotoShrink</string>
  <key>CFBundleIconFile</key><string>PhotoShrink.icns</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSHumanReadableCopyright</key><string>PhotoShrink</string>
</dict>
</plist>
PLIST

echo "==> podpis ad-hoc (żeby Gatekeeper nie uznał pliku za 'uszkodzony')"
codesign --force --deep -s - "$APP_DIR" 2>&1 | tail -5 || true

echo "==> pakowanie do .dmg (do instalacji: przeciągnij do Applications)"
DMG_STAGE="$OUT_DIR/dmg-stage"
rm -rf "$DMG_STAGE"
mkdir -p "$DMG_STAGE"
cp -R "$APP_DIR" "$DMG_STAGE/"
ln -s /Applications "$DMG_STAGE/Applications"

DMG_PATH="$OUT_DIR/PhotoShrink.dmg"
rm -f "$DMG_PATH"
hdiutil create -volname "PhotoShrink" -srcfolder "$DMG_STAGE" -ov -format UDZO "$DMG_PATH" >/dev/null

echo ""
echo "Gotowe: $APP_DIR"
du -sh "$APP_DIR"
echo "Gotowe: $DMG_PATH"
du -sh "$DMG_PATH"
