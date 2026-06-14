#!/bin/bash
# Check if training was successful
# Usage: ./check_training_success.sh [model_path]

set -e

# Default paths
DEFAULT_MODEL="Pigeon_Harvest/runs/health_classification/health_train_v1/weights/best.pt"
DEFAULT_DATA="/home/fawwazfa/Program/datasheet/moonharvest_health_cls"

MODEL_PATH="${1:-$DEFAULT_MODEL}"
DATA_PATH="${2:-$DEFAULT_DATA}"

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "========================================="
echo "TRAINING SUCCESS CHECKER"
echo "========================================="
echo "Model: $MODEL_PATH"
echo "Data:  $DATA_PATH"
echo ""

SUCCESS_COUNT=0
FAIL_COUNT=0

# Check 1: Model file exists
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "CHECK 1: Model File Exists"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
if [[ -f "$MODEL_PATH" ]]; then
    FILE_SIZE=$(stat -f%z "$MODEL_PATH" 2>/dev/null || stat -c%s "$MODEL_PATH")
    FILE_SIZE_MB=$((FILE_SIZE / 1024 / 1024))
    
    if [[ $FILE_SIZE_MB -gt 5 ]]; then
        echo -e "${GREEN}✅ PASS${NC}: Model file exists (${FILE_SIZE_MB}MB)"
        SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    else
        echo -e "${RED}❌ FAIL${NC}: Model file too small (${FILE_SIZE_MB}MB < 5MB)"
        FAIL_COUNT=$((FAIL_COUNT + 1))
    fi
else
    echo -e "${RED}❌ FAIL${NC}: Model file not found"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    echo ""
    echo "Model path expected: $MODEL_PATH"
    echo "Check if training completed successfully"
    exit 1
fi

# Check 2: Training output files
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "CHECK 2: Training Output Files"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
MODEL_DIR=$(dirname "$MODEL_PATH")
RESULTS_DIR=$(dirname "$MODEL_DIR")

REQUIRED_FILES=(
    "$RESULTS_DIR/results.png"
    "$RESULTS_DIR/confusion_matrix.png"
    "$RESULTS_DIR/results.csv"
)

MISSING_FILES=0
for FILE in "${REQUIRED_FILES[@]}"; do
    if [[ -f "$FILE" ]]; then
        echo -e "${GREEN}✅${NC} $(basename $FILE)"
    else
        echo -e "${RED}❌${NC} $(basename $FILE) - MISSING"
        MISSING_FILES=$((MISSING_FILES + 1))
    fi
done

if [[ $MISSING_FILES -eq 0 ]]; then
    echo -e "${GREEN}✅ PASS${NC}: All output files present"
    SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
else
    echo -e "${YELLOW}⚠️  WARN${NC}: $MISSING_FILES files missing"
fi

# Check 3: Validate model
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "CHECK 3: Model Validation"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [[ -d "$DATA_PATH" ]]; then
    # Activate venv
    source Pigeon_Harvest/.venv-yolo/bin/activate 2>/dev/null || true
    
    # Run validation
    VAL_OUTPUT=$(yolo classify val \
        model="$MODEL_PATH" \
        data="$DATA_PATH" \
        verbose=False 2>&1)
    
    # Extract accuracy (looking for "Top1_acc" or "accuracy")
    ACCURACY=$(echo "$VAL_OUTPUT" | grep -oP "(?<=all\s+)\d+\s+\d+\s+\K[\d\.]+" | head -1)
    
    if [[ -n "$ACCURACY" ]]; then
        ACCURACY_PERCENT=$(echo "$ACCURACY * 100" | bc -l | xargs printf "%.1f")
        
        if (( $(echo "$ACCURACY >= 0.80" | bc -l) )); then
            echo -e "${GREEN}✅ EXCELLENT${NC}: Top-1 Accuracy = ${ACCURACY_PERCENT}%"
            SUCCESS_COUNT=$((SUCCESS_COUNT + 2))
        elif (( $(echo "$ACCURACY >= 0.70" | bc -l) )); then
            echo -e "${GREEN}✅ GOOD${NC}: Top-1 Accuracy = ${ACCURACY_PERCENT}%"
            SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
        elif (( $(echo "$ACCURACY >= 0.50" | bc -l) )); then
            echo -e "${YELLOW}⚠️  ACCEPTABLE${NC}: Top-1 Accuracy = ${ACCURACY_PERCENT}%"
            echo "   Consider training longer or adjusting hyperparameters"
        else
            echo -e "${RED}❌ FAIL${NC}: Top-1 Accuracy = ${ACCURACY_PERCENT}% (< 50%)"
            FAIL_COUNT=$((FAIL_COUNT + 1))
        fi
    else
        echo -e "${YELLOW}⚠️  WARN${NC}: Could not extract accuracy from validation output"
        echo "Run manually: yolo classify val model=$MODEL_PATH data=$DATA_PATH"
    fi
else
    echo -e "${YELLOW}⚠️  SKIP${NC}: Data path not found, skipping validation"
fi

# Check 4: Test prediction
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "CHECK 4: Prediction Test"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [[ -d "$DATA_PATH/val" ]]; then
    # Find a test image
    TEST_IMAGE=$(find "$DATA_PATH/val" -name "*.jpg" -o -name "*.png" | head -1)
    
    if [[ -n "$TEST_IMAGE" ]]; then
        echo "Testing on: $(basename $TEST_IMAGE)"
        
        PRED_OUTPUT=$(yolo classify predict \
            model="$MODEL_PATH" \
            source="$TEST_IMAGE" \
            save=False \
            verbose=False 2>&1)
        
        if [[ $? -eq 0 ]]; then
            echo -e "${GREEN}✅ PASS${NC}: Prediction successful"
            SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
        else
            echo -e "${RED}❌ FAIL${NC}: Prediction failed"
            FAIL_COUNT=$((FAIL_COUNT + 1))
        fi
    else
        echo -e "${YELLOW}⚠️  SKIP${NC}: No test images found"
    fi
else
    echo -e "${YELLOW}⚠️  SKIP${NC}: Validation data not found"
fi

# Final summary
echo ""
echo "========================================="
echo "FINAL RESULT"
echo "========================================="
echo "Checks Passed: $SUCCESS_COUNT"
echo "Checks Failed: $FAIL_COUNT"
echo ""

if [[ $FAIL_COUNT -eq 0 && $SUCCESS_COUNT -ge 3 ]]; then
    echo -e "${GREEN}🎉 TRAINING SUCCESSFUL!${NC}"
    echo ""
    echo "Your model is ready to use:"
    echo "  Model: $MODEL_PATH"
    echo ""
    echo "Next steps:"
    echo "  1. View confusion matrix:"
    echo "     eog $RESULTS_DIR/confusion_matrix.png"
    echo ""
    echo "  2. View training curves:"
    echo "     eog $RESULTS_DIR/results.png"
    echo ""
    echo "  3. Test on your own images:"
    echo "     yolo classify predict model=$MODEL_PATH source=/path/to/images"
    echo ""
    exit 0
elif [[ $FAIL_COUNT -gt 0 ]]; then
    echo -e "${RED}❌ TRAINING FAILED OR INCOMPLETE${NC}"
    echo ""
    echo "Recommendations:"
    echo "  1. Check training logs for errors"
    echo "  2. Re-run training with adjusted parameters"
    echo "  3. Ensure dataset is properly prepared"
    echo ""
    exit 1
else
    echo -e "${YELLOW}⚠️  TRAINING MAY NEED IMPROVEMENT${NC}"
    echo ""
    echo "Model works but accuracy could be better."
    echo "Consider:"
    echo "  - Training for more epochs"
    echo "  - Using a larger model (yolov8s-cls.pt)"
    echo "  - Checking dataset quality"
    echo ""
    exit 0
fi
