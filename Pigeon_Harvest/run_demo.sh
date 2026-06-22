#!/bin/bash
# ==========================================
# MoonHarvest YOLO Detection Demo
# Detect objects in derr.mp4 using YOLO models
# Run from Pigeon_Harvest directory
# ==========================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Default video path (from project root)
DEFAULT_VIDEO="../derr.mp4"

# Model paths
DETECTION_MODEL="vision_trial/models/yolov8n-crop-weed-416.onnx"
HEALTH_MODEL="../Pigeon_Harvest/HarvestmoonGCS/Assets/models/moonharvest-health-cls.onnx"

# --- Colors ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}=========================================${NC}"
echo -e "${BLUE}  MoonHarvest YOLO Detection Demo${NC}"
echo -e "${BLUE}=========================================${NC}"

# Activate virtual environment
if [ -f ".venv-yolo/bin/activate" ]; then
    echo -e "${GREEN}✓ Activating virtual environment...${NC}"
    source .venv-yolo/bin/activate
else
    echo -e "${RED}Virtual environment not found. Run setup first.${NC}"
    echo "  python3 -m venv .venv-yolo"
    echo "  source .venv-yolo/bin/activate"
    echo "  pip install -U ultralytics onnx onnxruntime opencv-python"
    exit 1
fi

# Check models
if [ ! -f "$DETECTION_MODEL" ]; then
    echo -e "${YELLOW}⚠ Detection model not found: $DETECTION_MODEL${NC}"
    echo -e "${YELLOW}  Will use YOLO classification model instead${NC}"
    USE_DETECTION=false
else
    echo -e "${GREEN}✓ Detection model found${NC}"
    USE_DETECTION=true
fi

if [ ! -f "$HEALTH_MODEL" ]; then
    echo -e "${YELLOW}⚠ Health model not found: $HEALTH_MODEL${NC}"
    USE_HEALTH=false
else
    echo -e "${GREEN}✓ Health classification model found${NC}"
    USE_HEALTH=true
fi

# Video path
VIDEO="${1:-$DEFAULT_VIDEO}"
if [ ! -f "$VIDEO" ]; then
    echo -e "${RED}Video not found: $VIDEO${NC}"
    echo "Usage: $0 [video_path]"
    echo "Default: $DEFAULT_VIDEO"
    exit 1
fi

echo -e "${GREEN}✓ Video: $VIDEO${NC}"
echo ""

# Detect video size
VIDEO_SIZE=$(du -h "$VIDEO" | cut -f1)
echo -e "Video size: ${VIDEO_SIZE}"

echo ""
echo "Starting detection demo in 2 seconds..."
echo "Press 'q' or ESC to stop preview"
sleep 2

if [ "$USE_DETECTION" = true ]; then
    echo -e "\n${BLUE}Running YOLO detection (crop/weed bounding boxes)...${NC}"
    echo -e "${BLUE}Model: $DETECTION_MODEL${NC}"
    python3 vision_trial/trial_yolo_video.py \
        "$VIDEO" \
        --model "$DETECTION_MODEL" \
        --output "runs/demo/detection_output.mp4" \
        --csv "runs/demo/detection_output.csv" \
        --imgsz 416 \
        --conf 0.35 \
        --iou 0.70 \
        --max-det 300 \
        --show \
        --window-width 1280
else
    echo -e "\n${YELLOW}Running YOLO health classification (grid-based)...${NC}"
    python3 scripts/run_detection_video.py \
        "$VIDEO" \
        --model "$HEALTH_MODEL" \
        --output "runs/demo/health_output.mp4" \
        --show \
        --grid-rows 4 \
        --grid-cols 6 \
        --min-conf 0.4
fi

echo -e "\n${GREEN}=========================================${NC}"
echo -e "${GREEN}  Demo Complete!${NC}"
echo -e "${GREEN}=========================================${NC}"
echo ""
echo "Output files saved to: runs/demo/"
echo ""
echo "Quick view:"
echo "  vlc runs/demo/detection_output.mp4"