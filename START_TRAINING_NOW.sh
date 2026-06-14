#!/bin/bash
# Start training immediately
# This is a one-time command to start training

set -e

cd /home/fawwazfa/Program/Harvestmoon
source Pigeon_Harvest/.venv-yolo/bin/activate

echo "========================================="
echo "STARTING MOONHARVEST HEALTH TRAINING"
echo "========================================="
echo ""
echo "Configuration:"
echo "  - Dataset: moonharvest_health_cls"
echo "  - Model: yolov8n-cls.pt"
echo "  - Epochs: 80"
echo "  - Batch: 32"
echo "  - Image Size: 224"
echo "  - Device: GPU (0)"
echo ""
echo "Estimated time: 30-60 minutes on RTX 3050"
echo ""
echo "Starting in 3 seconds..."
sleep 3

# Run training
yolo classify train \
    data=/home/fawwazfa/Program/datasheet/moonharvest_health_cls \
    model=yolov8n-cls.pt \
    epochs=80 \
    imgsz=224 \
    batch=32 \
    patience=20 \
    device=0 \
    project=Pigeon_Harvest/runs/health_classification \
    name=health_train_v1 \
    verbose=true

echo ""
echo "========================================="
echo "TRAINING COMPLETED!"
echo "========================================="
echo ""
echo "Check results:"
echo "  ./Pigeon_Harvest/scripts/check_training_success.sh"
echo ""
