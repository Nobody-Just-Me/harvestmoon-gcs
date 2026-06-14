# 🧹 Cleanup Summary - MoonHarvest Project

**Date**: June 15, 2026  
**Action**: Removed unused and irrelevant files

---

## ✅ **FILES REMOVED**

### **1. WeedMap Dataset & Related Files** (❌ Not Usable - No Labels)
- ❌ `RedEdge/` folder (~1GB)
- ❌ `Sequoia/` folder (~1GB)
- ❌ `Pigeon_Harvest/scripts/prepare_weedmap_yolo_dataset.py`
- ❌ `Pigeon_Harvest/scripts/prepare_weedmap.sh`
- ❌ `Pigeon_Harvest/scripts/train_weedmap_yolo.sh`
- ❌ `Pigeon_Harvest/scripts/download_uav_over_60m_datasets.py`
- ❌ `Pigeon_Harvest/runs/weedmap/` - Failed training results
- ❌ `download_new_uav_datasets.py`

**Reason**: WeedMap dataset has no valid annotations/labels, cannot be used for training.

---

### **2. Unused Pretrained Models**
- ❌ `yolo26n.pt`
- ❌ `yolov8n.pt`
- ❌ `yolov8n-seg.pt`

**Kept**: 
- ✅ `yolov8n-cls.pt` (Still needed for transfer learning)

**Reason**: We have our own trained model now (98.8% accuracy).

---

### **3. Old Training Scripts**
- ❌ `Pigeon_Harvest/scripts/prepare_flat_yolo_dataset.py`
- ❌ `Pigeon_Harvest/scripts/prepare_moonharvest_uav_yolo_dataset.py`
- ❌ `Pigeon_Harvest/scripts/train_all_datasets.sh`
- ❌ `Pigeon_Harvest/scripts/train_moonharvest_all_vision.sh`
- ❌ `Pigeon_Harvest/scripts/train_moonharvest_balanced_vision.sh`
- ❌ `Pigeon_Harvest/scripts/train_yolov8n_vision.sh`
- ❌ `Pigeon_Harvest/scripts/trial_yolo_video.py` (duplicate)

**Reason**: Consolidated to single working training script.

---

### **4. Old Trial & Test Files**
- ❌ `Pigeon_Harvest/scripts/benchmark_yolo_camera.py`
- ❌ `Pigeon_Harvest/scripts/prepare_two_day_trial.sh`
- ❌ `Pigeon_Harvest/scripts/run_desktop_trial.sh`
- ❌ `Pigeon_Harvest/scripts/sitl_smoke.sh`
- ❌ `Pigeon_Harvest/vision_trial/compare_*.jpg` (4 files)
- ❌ `Pigeon_Harvest/vision_trial/compare_settings.py`
- ❌ `Pigeon_Harvest/vision_trial/analyze_detection_results.py`
- ❌ `Pigeon_Harvest/vision_trial/live_preview_*.py` (3 files)
- ❌ `Pigeon_Harvest/vision_trial/prepare_flat_yolo_dataset.py`
- ❌ `Pigeon_Harvest/vision_trial/run_detection_testkamera.py`
- ❌ `Pigeon_Harvest/vision_trial/trial_health_cls.py`
- ❌ `Pigeon_Harvest/vision_trial/*.sh` (5 shell scripts)
- ❌ `Pigeon_Harvest/vision_trial/OPTIMASI_FPS.md`
- ❌ `Pigeon_Harvest/vision_trial/PANDUAN_CEPAT.md`
- ❌ `Pigeon_Harvest/vision_trial/README_DETECTION.md`
- ❌ `Pigeon_Harvest/vision_trial/TUNING_PARAMETER.md`
- ❌ `Pigeon_Harvest/vision_trial/output/` folder

**Reason**: Old experiments and trials, replaced by production-ready scripts.

---

### **5. Old Training/Validation Outputs**
- ❌ `runs/classify/predict/` (100 test images)
- ❌ `runs/classify/predict-2/`
- ❌ `runs/classify/val/`
- ❌ `runs/classify/val-2/`
- ❌ `runs/detect/` - Failed WeedMap detection
- ❌ `runs/segment/` - Failed WeedMap segmentation
- ❌ `Pigeon_Harvest/runs/moonharvest_uav/`
- ❌ `Pigeon_Harvest/runs/moonharvest_health/`
- ❌ `Pigeon_Harvest/runs/detect/`

**Reason**: Temporary outputs from old experiments and failed training runs.

---

### **6. Python Cache & System Files**
- ❌ `__MACOSX/` folder
- ❌ `Pigeon_Harvest/scripts/__pycache__/`
- ❌ `Pigeon_Harvest/vision_trial/__pycache__/`

**Reason**: Automatically generated cache files, not needed in repo.

---

### **7. Replaced Scripts**
- ❌ `RUN_VIDEO_CLASSIFICATION.sh`

**Replaced by**: 
- ✅ `RUN_UAV_DETECTION.sh` (with bounding boxes)

**Reason**: User wanted bounding box detection, not just classification overlay.

---

## ✅ **FILES KEPT** (Essential & Active)

### **Root Directory**
```
✅ derr.mp4                                          (384MB - Test video)
✅ README.md                                         (12KB - Main docs)
✅ UAV_DETECTION_GUIDE.md                           (8KB - Detection guide)
✅ RUN_UAV_DETECTION.sh                             (1.3KB - Quick launcher)
✅ START_TRAINING_NOW.sh                            (1.2KB - Training launcher)
✅ PIGEON_HARVEST_FULL_IMPLEMENTATION_PLAN.md       (21KB - Full plan)
✅ TEKNOFEST_2026_MoonHarvest_Presentation_COMPLETE.md (50KB)
✅ TEKNOFEST_2026_Presentation_README.md            (5.5KB)
✅ yolov8n-cls.pt                                   (5.4MB - Pretrained base)
```

### **Scripts (Pigeon_Harvest/scripts/)**
```
✅ check_training_success.sh                - Verify training results
✅ prepare_moonharvest_health_cls_dataset.py - Dataset preparation
✅ run_detection_video.py                   - UAV detection with bounding boxes
✅ train_moonharvest_health_cls.sh          - Training script
```

### **Vision Trial (Pigeon_Harvest/vision_trial/)**
```
✅ trial_yolo_video.py         - Advanced YOLO video processing
✅ testkamera.mp4               - Test video
✅ README.md                    - Vision trial documentation
✅ configs/                     - Configuration files
✅ models/                      - YOLO models (ONNX)
```

### **Training Results (runs/)**
```
✅ runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/
   - weights/best.pt (2.9MB) - 98.8% accuracy model
   - All training metrics and plots
✅ runs/uav_detection/ - Detection outputs
```

---

## 📊 **SPACE SAVED**

| Category | Size Saved |
|----------|------------|
| WeedMap Dataset | ~2.2 GB |
| Old Models | ~15 MB |
| Old Training Outputs | ~50 MB |
| Old Scripts & Docs | ~5 MB |
| Cache Files | ~10 MB |
| **TOTAL** | **~2.28 GB** |

---

## 🎯 **CURRENT PROJECT STATUS**

### **Active Components**
1. ✅ **Trained Model**: `health_train_v1-2/weights/best.pt` (98.8% accuracy)
2. ✅ **UAV Detection**: `run_detection_video.py` with grid-based bounding boxes
3. ✅ **Dataset**: MoonHarvest Health Classification (5 classes)
4. ✅ **Documentation**: Clean and comprehensive README + guides

### **Working Commands**
```bash
# Run UAV detection with bounding boxes
./RUN_UAV_DETECTION.sh derr.mp4

# Train model
./START_TRAINING_NOW.sh

# Check training success
./Pigeon_Harvest/scripts/check_training_success.sh

# Advanced detection
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show --grid-rows 5 --grid-cols 8
```

---

## 📁 **CLEAN PROJECT STRUCTURE**

```
MoonHarvest/
├── derr.mp4                          # Test video
├── README.md                         # Main documentation
├── UAV_DETECTION_GUIDE.md           # Detection guide
├── RUN_UAV_DETECTION.sh             # Quick launcher
├── START_TRAINING_NOW.sh            # Training launcher
├── yolov8n-cls.pt                   # Pretrained base
│
├── Pigeon_Harvest/
│   ├── scripts/
│   │   ├── run_detection_video.py           ⭐ Main detection script
│   │   ├── train_moonharvest_health_cls.sh  ⭐ Training script
│   │   ├── prepare_moonharvest_health_cls_dataset.py
│   │   └── check_training_success.sh
│   │
│   ├── vision_trial/
│   │   ├── trial_yolo_video.py      # Advanced YOLO processing
│   │   ├── testkamera.mp4
│   │   ├── models/                  # ONNX models
│   │   └── configs/                 # Configuration files
│   │
│   └── runs/
│       ├── health_classification/   # Training results
│       └── classify/
│
└── runs/
    ├── classify/                    # Classification outputs (16MB)
    └── uav_detection/              # Detection outputs (18MB)
```

---

## ✅ **SUMMARY**

**Status**: ✅ **PROJECT CLEANED AND OPTIMIZED**

- ❌ Removed: **2.28 GB** of unused files
- ✅ Kept: All essential and working components
- 🎯 Focus: UAV crop health detection with bounding boxes
- 📊 Model: 98.8% accuracy, production-ready
- 📚 Docs: Clean, comprehensive, up-to-date

**Result**: Project sekarang lebih bersih, lebih cepat, dan lebih mudah dikelola!

---

**Last Updated**: June 15, 2026  
**Action**: Cleanup completed successfully ✅
