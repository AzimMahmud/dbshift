#!/usr/bin/env bash
set -euo pipefail

RUNTIME="${1:-linux-x64}"
FRAMEWORK="${FRAMEWORK:-net8.0}"
OUTPUT="${2:-$(dirname "$0")/dist}"
EXE_NAME="dbshift"

case "$RUNTIME" in
  win-*) EXE_NAME="dbshift.exe" ;;
esac

echo "Publishing dbshift ($RUNTIME, $FRAMEWORK)..."

dotnet publish "$(dirname "$0")/src/DbShift.CLI/DbShift.CLI.csproj" \
    --configuration Release \
    --framework "$FRAMEWORK" \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$OUTPUT" \
    -p:PublishSingleFile=true

echo ""
echo "Binary created: $OUTPUT/$EXE_NAME"
SIZE=$(du -h "$OUTPUT/$EXE_NAME" 2>/dev/null | cut -f1 || stat -c%s "$OUTPUT/$EXE_NAME" 2>/dev/null)
echo "Size: $SIZE"
echo ""
echo "To use:"
echo "  $OUTPUT/$EXE_NAME --help"
echo ""
echo "Example:"
echo "  $OUTPUT/$EXE_NAME new -n MyApp -p postgresql --json"

# Supported runtimes:
#   win-x64, win-arm64
#   linux-x64, linux-arm64, linux-musl-x64
#   osx-x64, osx-arm64
#
# Override the bundled framework (default net8.0 LTS):
#   FRAMEWORK=net10.0 ./publish.sh linux-x64
