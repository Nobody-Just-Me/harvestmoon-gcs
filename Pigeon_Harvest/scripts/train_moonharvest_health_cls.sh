#!/bin/bash
# Train YOLO Classification model on MoonHarvest Health dataset
# Usage: ./train_moonharvest_health_cls.sh

set -e

# Activate virtual environment
source Pigeon_Harvest/.venv-yolo/bin/activate 2>/dev/null || true

# Configuration
DATASET_PATH="/home/fawwazfa/Program/datasheet/moonharvest_health_cls"
EPOCHS=80
IMG_SIZE=224
BATCH_SIZE=32
PATIENCE=20
MODEL="yolov8n-cls.pt"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --epochs=*)
            EPOCHS="${1#*=}"
            shift
            ;;
        --imgsz=*)
            IMG_SIZE="${1#*=}"
            shift
            ;;
        --batch=*)
            BATCH_SIZE="${1#*=}"
            shift
            ;;
        --patience=*)
            PATIENCE="${1#*=}"
            shift
            ;;
        --model=*)
            MODEL="${1#*=}"
            shift
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --epochs=N        Number of training epochs (default: 80)"
            echo "  --imgsz=N         Image size for training (default: 224)"
            echo "  --batch=N         Batch size (default: 32)"
            echo "  --patience=N      Early stopping patience (default: 20)"
            echo "  --model=MODEL     Model file (default: yolov8n-cls.pt)"
            echo "  --help            Show this help message"
            exit 0
            ;;
        *)
            shift
            ;;
    esac
done

# Check if dataset exists, if not prepare it
if [[ ! -d "$DATASET_PATH/train" ]]; then
    echo "Dataset not found. Preparing..."
    python3 Pigeon_Harvest/scripts/prepare_moonharvest_health_cls_dataset.py
fi

# Create output directory
OUTPUT_DIR="Pigeon_Harvest/runs/health_classification"
mkdir -p "$OUTPUT_DIR"

echo "========================================="
echo "MOONHARVEST HEALTH CLASSIFICATION"
echo "========================================="
echo "Dataset:      $DATASET_PATH"
echo "Model:        $MODEL"
echo "Epochs:       $EPOCHS"
echo "Image Size:   $IMG_SIZE"
echo "Batch Size:   $BATCH_SIZE"
echo "Patience:     $PATIENCE"
echo "Output:       $OUTPUT_DIR"
echo "========================================="

# Train YOLO classification model
echo ""
echo "Starting training..."
yolo classify train \
    data="$DATASET_PATH" \
    model="$MODEL" \
    epochs="$EPOCHS" \
    imgsz="$IMG_SIZE" \
    batch="$BATCH_SIZE" \
    patience="$PATIENCE" \
    project="$OUTPUT_DIR" \
    name="health_train_v1" \
    device=0 \
    verbose=true

echo ""
echo "========================================="
echo "TRAINING COMPLETED!"
echo "========================================="
echo "Results saved to: $OUTPUT_DIR/health_train_v1/"
echo ""
echo "Best model: $OUTPUT_DIR/health_train_v1/weights/best.pt"
echo ""
echo "To test on new images:"
echo "  yolo classify predict model=$OUTPUT_DIR/health_train_v1/weights/best.pt source=/path/to/images"
echo ""
echo "To validate:"
echo "  yolo classify val model=$OUTPUT_DIR/health_train_v1/weights/best.pt data=$DATASET_PATH"
echo ""
