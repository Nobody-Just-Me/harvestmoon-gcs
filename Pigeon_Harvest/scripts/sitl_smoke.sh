#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "[SITL SMOKE] Build desktop target"
dotnet build "$ROOT_DIR/HarvestmoonGCS/HarvestmoonGCS.csproj" -f net9.0-desktop --nologo --verbosity:minimal

echo "[SITL SMOKE] Verify Python camera bridge"
if [[ -x "$ROOT_DIR/.venv-camera/bin/python" ]]; then
  "$ROOT_DIR/.venv-camera/bin/python" -c "import cv2; print('cv2', cv2.__version__)"
else
  echo "[WARN] .venv-camera not found; create it before camera field test"
fi

echo "[SITL SMOKE] Verify model assets"
test -f "$ROOT_DIR/HarvestmoonGCS/Assets/models/yolov8n.onnx"
test -f "$ROOT_DIR/HarvestmoonGCS/Assets/models/classes-yolov8n-coco.txt"

echo "[SITL SMOKE] Done"
