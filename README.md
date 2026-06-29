# 🌾 MoonHarvest - UAV Crop Health Monitoring System

**Intelligent crop health detection and monitoring system using deep learning and UAV imagery**

[![Model Accuracy](https://img.shields.io/badge/Accuracy-98.8%25-brightgreen)]()
[![YOLO](https://img.shields.io/badge/YOLO-v8-blue)]()
[![Python](https://img.shields.io/badge/Python-3.12-blue)]()
[![License](https://img.shields.io/badge/License-MIT-yellow)]()

---

## 📋 **Table of Contents**

- [Overview](#overview)
- [Features](#features)
- [Quick Start](#quick-start)
- [Model Performance](#model-performance)
- [UAV Detection](#uav-detection)
- [Training](#training)
- [Documentation](#documentation)
- [System Requirements](#system-requirements)

---

## 🎯 **Overview**

MoonHarvest adalah sistem monitoring kesehatan tanaman menggunakan AI yang dirancang untuk:
- ✅ Deteksi real-time kondisi kesehatan tanaman dari UAV/drone
- ✅ Klasifikasi 5 kondisi kesehatan: Healthy, Stressed, Disease, Drought, Bare Soil
- ✅ Grid-based detection dengan bounding boxes
- ✅ Optimized untuk agricultural monitoring

---

## ✨ **Features**

### 🚁 **UAV Video Detection**
- **Grid-based detection** dengan bounding boxes
- **Color-coded visualization** (Green=Healthy, Red=Disease, etc.)
- **Real-time statistics** overlay
- **Live preview** dengan adjustable grid size
- **Optimized** untuk drone footage

### 🤖 **AI Model**
- **98.8% accuracy** pada validation set
- **Fast inference**: 2.5ms per image
- **5 health classes**: healthy_crop, stressed_crop, disease_stress, drought_stress, bare_soil
- **Lightweight**: 2.9MB model size

### 📊 **Analysis Tools**
- Frame-by-frame classification
- Health distribution statistics
- Exportable results (video + CSV)
- Batch processing support

---

## 🚀 **Quick Start**

### **Deteksi v3 — HSV+ONNX Fusion (DIREKOMENDASIKAN)**

Program terbaru: `moonharvest_detect_v3.py`
- WB + CLAHE preprocessing
- HSV connected-component per-object bounding box
- ONNX batch inference (model v5: 82.2% aerial accuracy)
- Compatibility matrix HSV–ONNX (mencegah mislabel)
- FHI sidebar + EMA temporal smoothing
- CSV log otomatis

```bash
cd /home/fawwazfa/Program/Harvestmoon

# Simpan video output
CUDA_VISIBLE_DEVICES="" python moonharvest_detect_v3.py YDXJ.mp4

# Tampilkan window real-time
CUDA_VISIBLE_DEVICES="" DISPLAY=:1 python moonharvest_detect_v3.py YDXJ.mp4 --show

# Pilih model lain
CUDA_VISIBLE_DEVICES="" python moonharvest_detect_v3.py YDXJ.mp4 \
  --model runs/classify/health_train_v5-20260626/weights/best.pt \
  --output out/hasil.mp4 --skip 2 --scale 0.7
```

> Tekan **Q** atau **Esc** di window untuk berhenti.

### **Opsi moonharvest_detect_v3.py**

| Opsi | Default | Keterangan |
|------|---------|-----------|
| `--model` | model v5 | Path `.pt` (ONNX sidecar otomatis dipakai) |
| `--output` | `out/<nama>_v3.mp4` | Path video output |
| `--skip N` | `2` | Skip N frame antar inferensi (2 = 3× lebih cepat) |
| `--scale S` | `0.7` | Skala output (0.7 = 70% ukuran asli) |
| `--show` | off | Tampilkan window real-time |
| `--no-log` | off | Nonaktifkan CSV log |

---

### **Buka hasil video**

```bash
DISPLAY=:1 ffplay out/YDXJ_v3.mp4
# atau
vlc out/YDXJ_v3.mp4
```

---

### **Deteksi lama (HSV+YOLO — masih tersedia)**

```bash
CUDA_VISIBLE_DEVICES="" DISPLAY=:1 python run_detection_video.py \
  YDXJ.mp4 \
  --model runs/classify/health_train_v5-20260626/weights/best.pt \
  --output out/YDXJ_hsv_yolo.mp4 \
  --skip-frames 2 --output-scale 0.7 --demo --show
```

---

## 📊 **Model Performance**

### **Classification Metrics**

| Metric | Value | Grade |
|--------|-------|-------|
| **Top-1 Accuracy** | 98.8% | 🌟🌟🌟 EXCELLENT |
| **Top-5 Accuracy** | 100% | 🌟🌟🌟 PERFECT |
| **Inference Speed** | 2.5ms/image | ⚡ VERY FAST |
| **Model Size** | 2.9 MB | 💾 LIGHTWEIGHT |

### **Model Location**

```
runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/weights/best.pt
```

### **Classes Detected**

1. 🟢 **healthy_crop** - Tanaman sehat
2. 🟡 **stressed_crop** - Tanaman stress (non-specific)
3. 🔴 **disease_stress_vegetation** - Terserang penyakit
4. 🟠 **drought_stress** - Stress kekeringan
5. ⚫ **bare_soil** - Tanah kosong

---

## 🚁 **UAV Detection
### **Basic Usage**

```bash
# Standard detection (5x8 grid)
./RUN_UAV_DETECTION.sh video.mp4

# High detail (8x12 grid - for low altitude)
python3 Pigeon_Harvest/scripts/run_detection_video.py video.mp4 \
    --show --grid-rows 8 --grid-cols 12

# Fast processing (skip frames)
python3 Pigeon_Harvest/scripts/run_detection_video.py video.mp4 \
    --show --skip-frames 2 --grid-rows 4 --grid-cols 6
```

### **Parameters**

| Parameter | Description | Default |
|-----------|-------------|---------|
| `--grid-rows` | Number of grid rows | 5 |
| `--grid-cols` | Number of grid columns | 8 |
| `--min-conf` | Minimum confidence threshold | 0.4 |
| `--skip-frames` | Skip N frames (0=process all) | 0 |
| `--show` | Show live preview window | False |
| `--output` | Output video path | Auto |

### **Output**

```
runs/uav_detection/[video_name]_detected.mp4
```

**Preview window shows:**
- ✅ Bounding boxes with class labels
- ✅ Confidence scores
- ✅ Color-coded health status
- ✅ Statistics overlay (class distribution)
- ✅ Frame counter

### **Keyboard Controls**

- **Q** or **ESC** - Stop and exit

---

## 🎓 **Training**

### **Dataset Preparation**

```bash
# Prepare MoonHarvest Health Classification dataset
python3 Pigeon_Harvest/scripts/prepare_moonharvest_health_cls_dataset.py

# Output: /home/fawwazfa/Program/datasheet/moonharvest_health_cls
```

### **Train Model**

```bash
# Using script (recommended)
./Pigeon_Harvest/scripts/train_moonharvest_health_cls.sh

# Or direct YOLO command
yolo classify train \
    data=/home/fawwazfa/Program/datasheet/moonharvest_health_cls \
    model=yolov8n-cls.pt \
    epochs=80 \
    imgsz=224 \
    batch=32 \
    device=0
```

### **Check Training Success**

```bash
./Pigeon_Harvest/scripts/check_training_success.sh
```

**Expected output:**
```
✅ PASS: Model file exists (2.9MB)
✅ PASS: All output files present
✅ EXCELLENT: Top-1 Accuracy = 98.8%
✅ PASS: Prediction successful
```

### **Validation**

```bash
yolo classify val \
    model=runs/classify/.../weights/best.pt \
    data=/home/fawwazfa/Program/datasheet/moonharvest_health_cls
```

---

## 📚 **Documentation**

### **Available Guides**

1. **UAV_DETECTION_GUIDE.md** - Complete UAV detection documentation
2. **PIGEON_HARVEST_FULL_IMPLEMENTATION_PLAN.md** - Full system architecture
3. **TEKNOFEST_2026_MoonHarvest_Presentation_COMPLETE.md** - Presentation materials

### **Key Scripts**

| Script | Purpose |
|--------|---------|
| `RUN_UAV_DETECTION.sh` | Quick UAV video detection |
| `START_TRAINING_NOW.sh` | Start model training |
| `check_training_success.sh` | Verify training results |
| `prepare_moonharvest_health_cls_dataset.py` | Prepare dataset |
| `run_detection_video.py` | Advanced detection with options |
| `trial_yolo_video.py` | YOLO video processing trial |

---

## 💻 **System Requirements**

### **Minimum**
- **OS**: Linux (Ubuntu 20.04+)
- **Python**: 3.8+
- **RAM**: 8GB
- **Storage**: 10GB free space

### **Recommended**
- **OS**: Linux (Ubuntu 22.04+)
- **Python**: 3.12
- **GPU**: NVIDIA RTX 3050 or better
- **RAM**: 16GB
- **Storage**: 50GB free space

### **Dependencies**

```txt
ultralytics>=8.0.0
opencv-python>=4.8.0
numpy>=1.24.0
torch>=2.0.0
```

---

## 🎬 **Examples**

### **Example 1: Basic UAV Detection**

```bash
./RUN_UAV_DETECTION.sh derr.mp4
```

### **Example 2: High-Detail Analysis**

```bash
python3 Pigeon_Harvest/scripts/run_detection_video.py \
    field_survey.mp4 \
    --show \
    --grid-rows 10 \
    --grid-cols 15 \
    --min-conf 0.5 \
    --output results/detailed_analysis.mp4
```

### **Example 3: Fast Monitoring**

```bash
python3 Pigeon_Harvest/scripts/run_detection_video.py \
    monitoring_flight.mp4 \
    --show \
    --grid-rows 4 \
    --grid-cols 6 \
    --skip-frames 2 \
    --min-conf 0.4
```

### **Example 4: Batch Processing**

```bash
for video in videos/*.mp4; do
    ./RUN_UAV_DETECTION.sh "$video"
done
```

---

## 🎨 **Visual Output**

### **Bounding Box Colors**

- 🟢 **Green** - `healthy_crop` (Confidence: 0.XX)
- 🟠 **Orange** - `stressed_crop` (Confidence: 0.XX)
- 🔴 **Red** - `disease_stress_vegetation` (Confidence: 0.XX)
- 🟡 **Yellow** - `drought_stress` (Confidence: 0.XX)
- ⚫ **Gray** - `bare_soil` (Confidence: 0.XX)

### **Statistics Overlay**

Located at top-left corner:
```
Health Distribution
  healthy_crop: 25
  stressed_crop: 8
  disease_stress: 3
  drought_stress: 2
  bare_soil: 2
```

---

## 🔧 **Optimization Tips**

### **Grid Size Selection**

| Drone Altitude | Recommended Grid | Use Case |
|----------------|------------------|----------|
| Low (< 20m) | 8x12 or 10x15 | Detailed analysis |
| Medium (20-50m) | 5x8 or 6x10 | Balanced |
| High (> 50m) | 3x5 or 4x6 | Overview |

### **Confidence Threshold**

| Lighting | Recommended Conf | Notes |
|----------|------------------|-------|
| Good | 0.5 - 0.7 | Cleaner output |
| Variable | 0.3 - 0.5 | Balanced |
| Poor | 0.2 - 0.4 | More detections |

### **Performance**

**Frame skipping** for faster processing:
- `--skip-frames 0` - All frames (best quality)
- `--skip-frames 1` - Every 2nd frame (2x faster)
- `--skip-frames 2` - Every 3rd frame (3x faster)

---

## 📈 **Performance Benchmarks**

### **Hardware: RTX 3050 6GB**

| Grid Size | FPS | Quality |
|-----------|-----|---------|
| 3x5 | ~25-30 | Low |
| 5x8 | ~15-20 | Medium |
| 8x12 | ~8-12 | High |

### **Processing Time** (1 minute video)

| Configuration | Time |
|---------------|------|
| 5x8 grid, all frames | ~3-4 min |
| 5x8 grid, skip 1 frame | ~1.5-2 min |
| 3x5 grid, skip 2 frames | ~30-45 sec |

---

## 🐛 **Troubleshooting**

### **No Preview Window**

```bash
export DISPLAY=:0
./RUN_UAV_DETECTION.sh video.mp4
```

### **Out of Memory**

```bash
# Reduce grid size
--grid-rows 3 --grid-cols 5

# Skip more frames
--skip-frames 2
```

### **Slow Processing**

```bash
# Fast mode
python3 Pigeon_Harvest/scripts/run_detection_video.py video.mp4 \
    --show --skip-frames 2 --grid-rows 4 --grid-cols 6
```

---

## 📝 **Project Structure**

```
MoonHarvest/
├── derr.mp4                                 # Test video (384MB)
├── README.md                                # Main documentation
├── UAV_DETECTION_GUIDE.md                  # Detection guide
├── RUN_UAV_DETECTION.sh                    # Quick launcher
├── START_TRAINING_NOW.sh                   # Training launcher
├── CLEANUP_SUMMARY.md                      # Cleanup documentation
├── yolov8n-cls.pt                          # Pretrained base model
│
├── Pigeon_Harvest/
│   ├── scripts/
│   │   ├── run_detection_video.py           ⭐ Main detection script
│   │   ├── train_moonharvest_health_cls.sh  ⭐ Training script
│   │   ├── check_training_success.sh        # Validation script
│   │   └── prepare_moonharvest_health_cls_dataset.py
│   │
│   ├── vision_trial/
│   │   ├── trial_yolo_video.py              # Advanced YOLO processing
│   │   ├── testkamera.mp4                   # Test video
│   │   ├── models/                          # ONNX models
│   │   └── configs/                         # Configuration files
│   │
│   └── runs/
│       └── health_classification/           # Training results
│           └── health_train_v1-2/
│               └── weights/best.pt          # 98.8% accuracy model
│
└── runs/
    ├── classify/                            # Classification outputs
    └── uav_detection/                       # Detection video outputs
```

---

## 🎯 **Use Cases**

1. **Precision Agriculture**
   - Early disease detection
   - Stress monitoring
   - Yield prediction

2. **Farm Management**
   - Field health mapping
   - Irrigation planning
   - Fertilizer optimization

3. **Research**
   - Crop phenotyping
   - Growth monitoring
   - Environmental impact studies

---

## 🤝 **Contributing**

Feel free to:
- Report issues
- Suggest improvements
- Submit pull requests
- Share your results

---

## 📄 **License**

MIT License - See LICENSE file for details

---

## 👥 **Team**

**MoonHarvest Development Team**
- Agricultural AI Research
- UAV Vision Systems
- Precision Farming Solutions

---

## 📞 **Support**

For questions or issues:
1. Check `UAV_DETECTION_GUIDE.md`
2. Review training logs
3. Verify system requirements
4. Test with sample videos

---

## 🎉 **Quick Start Summary**

```bash
# 1. Deteksi dengan window (YDXJ.mp4)
CUDA_VISIBLE_DEVICES="" DISPLAY=:1 python run_detection_video.py \
  YDXJ.mp4 \
  --model runs/classify/health_train_v3-20260621/weights/best.pt \
  --output out/YDXJ_hsv_yolo.mp4 \
  --skip-frames 2 --output-scale 0.7 --demo --show

# 2. Deteksi video lain (simpan saja)
CUDA_VISIBLE_DEVICES="" python run_detection_video.py \
  VIDEO.mp4 \
  --model runs/classify/health_train_v3-20260621/weights/best.pt \
  --output out/hasil.mp4 \
  --skip-frames 2 --output-scale 0.7 --demo

# 3. Buka hasil
DISPLAY=:1 ffplay out/YDXJ_hsv_yolo.mp4
```

---

**Model Performance**: 98.8% Accuracy ✅  
**Inference Speed**: 2.5ms per image ⚡  
**Status**: Production Ready 🚀  

**Last Updated**: June 15, 2026  
**Version**: 1.0.0  

---

🌾 **MoonHarvest - Intelligent Crop Health Monitoring** 🚁
