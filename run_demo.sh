#!/usr/bin/env bash
# =============================================================================
# MoonHarvest Demo Runner — satu perintah, siap presentasi
# Jalankan: bash run_demo.sh [mode] [video]
#
# Mode:
#   fusion   (default) — HSV + YOLO v1, output 3-panel
#   hsv                — HSV saja tanpa YOLO
#   grid               — YOLO grid v4 (run_detection_video.py)
#
# Contoh:
#   bash run_demo.sh
#   bash run_demo.sh fusion gabung.mp4
#   bash run_demo.sh hsv gabung.mp4
# =============================================================================

set -e
cd "$(dirname "$0")"

# ---------- Konfigurasi ----------
VENV="Pigeon_Harvest/.venv-yolo"
MODE="${1:-fusion}"
VIDEO="${2:-gabung.mp4}"
OUTDIR="demo_videos/out"
MODEL_FUSION="runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/weights/best.pt"
MODEL_GRID="runs/classify/health_train_v3-20260621/weights/best.pt"

# ---------- Warna terminal ----------
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║         MoonHarvest Demo Runner              ║"
echo "╚══════════════════════════════════════════════╝"
echo ""

# Cek venv
if [ ! -f "$VENV/bin/activate" ]; then
  echo -e "${RED}[ERROR] venv tidak ditemukan: $VENV${NC}"
  echo "Jalankan: python3 -m venv $VENV && source $VENV/bin/activate && pip install ultralytics opencv-python"
  exit 1
fi
source "$VENV/bin/activate"

# Cek video
if [ ! -f "$VIDEO" ]; then
  echo -e "${RED}[ERROR] Video tidak ditemukan: $VIDEO${NC}"
  echo "Letakkan file video di: $(pwd)/$VIDEO"
  exit 1
fi

mkdir -p "$OUTDIR"
echo -e "${GREEN}[INFO]${NC} Mode    : $MODE"
echo -e "${GREEN}[INFO]${NC} Video   : $VIDEO"
echo -e "${GREEN}[INFO]${NC} Output  : $OUTDIR/"
echo ""

# ---------- Jalankan ----------
case "$MODE" in

  fusion)
    # Cek model
    if [ ! -f "$MODEL_FUSION" ]; then
      echo -e "${RED}[ERROR] Model fusion tidak ditemukan: $MODEL_FUSION${NC}"
      exit 1
    fi
    echo -e "${YELLOW}[RUN]${NC} Fusion HSV + YOLO v1..."
    python3 moonharvest_detect.py video \
      -i "$VIDEO" \
      -o "$OUTDIR" \
      --weights "$MODEL_FUSION" \
      --fps 2 \
      --width 1280 \
      --no-display
    echo ""
    echo -e "${GREEN}[DONE]${NC} Output:"
    ls -lh "$OUTDIR"/*.mp4 "$OUTDIR"/*.json 2>/dev/null || true
    ;;

  hsv)
    echo -e "${YELLOW}[RUN]${NC} HSV saja..."
    python3 moonharvest_detect.py hsv \
      -i "$VIDEO" \
      -o "$OUTDIR" \
      --fps 2 \
      --width 1280 \
      --no-display
    echo ""
    echo -e "${GREEN}[DONE]${NC} Output:"
    ls -lh "$OUTDIR"/*.mp4 "$OUTDIR"/*.json 2>/dev/null || true
    ;;

  grid)
    if [ ! -f "$MODEL_GRID" ]; then
      echo -e "${RED}[ERROR] Model grid tidak ditemukan: $MODEL_GRID${NC}"
      exit 1
    fi
    echo -e "${YELLOW}[RUN]${NC} YOLO Grid v4..."
    BASE=$(basename "$VIDEO" .mp4)
    python3 Pigeon_Harvest/scripts/run_detection_video.py \
      -i "$VIDEO" \
      -o "$OUTDIR/${BASE}_grid.mp4" \
      --model "$MODEL_GRID" \
      --no-display
    echo ""
    echo -e "${GREEN}[DONE]${NC} Output:"
    ls -lh "$OUTDIR"/*.mp4 2>/dev/null || true
    ;;

  all)
    echo -e "${YELLOW}[RUN]${NC} Semua mode sekaligus..."
    bash "$0" fusion "$VIDEO"
    bash "$0" hsv    "$VIDEO"
    bash "$0" grid   "$VIDEO"
    ;;

  *)
    echo -e "${RED}[ERROR]${NC} Mode tidak dikenal: $MODE"
    echo "Pilihan: fusion | hsv | grid | all"
    exit 1
    ;;
esac

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║   Selesai. File tersimpan di: $OUTDIR/   ║"
echo "╚══════════════════════════════════════════════╝"
