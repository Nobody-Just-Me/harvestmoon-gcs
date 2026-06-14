#!/bin/bash
# Run UAV detection with bounding boxes on video
# Usage: ./RUN_UAV_DETECTION.sh [video_file]

set -e

# Configuration
VIDEO="${1:-derr.mp4}"
MODEL="runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/weights/best.pt"
OUTPUT="runs/uav_detection/$(basename ${VIDEO%.mp4})_detected.mp4"

# Grid configuration for UAV footage
GRID_ROWS=5
GRID_COLS=8
MIN_CONF=0.4

cd /home/fawwazfa/Program/Harvestmoon
source Pigeon_Harvest/.venv-yolo/bin/activate

echo "========================================="
echo "UAV DETECTION WITH BOUNDING BOXES"
echo "========================================="
echo "Video:      $VIDEO"
echo "Model:      $MODEL"
echo "Grid:       ${GRID_ROWS}x${GRID_COLS}"
echo "Min Conf:   $MIN_CONF"
echo "Output:     $OUTPUT"
echo ""
echo "Controls:"
echo "  Q or ESC - Stop"
echo ""
echo "Starting in 2 seconds..."
sleep 2

python3 Pigeon_Harvest/scripts/run_detection_video.py \
    "$VIDEO" \
    --model "$MODEL" \
    --output "$OUTPUT" \
    --show \
    --grid-rows $GRID_ROWS \
    --grid-cols $GRID_COLS \
    --min-conf $MIN_CONF

echo ""
echo "========================================="
echo "DETECTION COMPLETE!"
echo "========================================="
echo "Output saved to: $OUTPUT"
echo ""
echo "View result:"
echo "  vlc $OUTPUT"
echo ""
