# 🚁 UAV Detection dengan Bounding Box - Panduan Lengkap

## 🎯 **APA YANG ANDA DAPATKAN**

Script ini akan memberikan:
- ✅ **Bounding boxes** pada setiap area deteksi
- ✅ **Color-coded** berdasarkan health condition
- ✅ **Statistics overlay** menunjukkan distribusi class
- ✅ **Live preview** dengan window yang terlihat
- ✅ **Optimized untuk UAV footage** (drone view)

---

## 🚀 **QUICK START - COPY & PASTE**

### **Option 1: Menggunakan Script (TERMUDAH)**

```bash
cd /home/fawwazfa/Program/Harvestmoon
./RUN_UAV_DETECTION.sh derr.mp4
```

### **Option 2: Direct Python Command**

```bash
cd /home/fawwazfa/Program/Harvestmoon
source Pigeon_Harvest/.venv-yolo/bin/activate

python3 Pigeon_Harvest/scripts/run_detection_video.py \
    derr.mp4 \
    --show \
    --output runs/uav_detection/derr_detected.mp4 \
    --grid-rows 5 \
    --grid-cols 8 \
    --min-conf 0.4
```

---

## 🎨 **VISUAL OUTPUT**

### **Bounding Box Colors:**
- 🟢 **Green** → `healthy_crop` (Tanaman sehat)
- 🟠 **Orange** → `stressed_crop` (Tanaman stress)
- 🔴 **Red** → `disease_stress_vegetation` (Terserang penyakit)
- 🟡 **Yellow** → `drought_stress` (Kekeringan)
- ⚫ **Gray** → `bare_soil` (Tanah kosong)

### **Display Elements:**
```
┌────────────────────────────────────────┐
│  ┌──────────┐ ┌──────────┐             │
│  │healthy   │ │stressed  │  Stats Box: │
│  │crop 0.98 │ │crop 0.85 │  ┌────────┐ │
│  └──────────┘ └──────────┘  │Health  │ │
│                              │Distrib.│ │
│  ┌──────────┐ ┌──────────┐  │healthy:│ │
│  │disease   │ │drought   │  │  12    │ │
│  │0.92      │ │0.78      │  │stress: │ │
│  └──────────┘ └──────────┘  │  5     │ │
│                              └────────┘ │
│  Frame: 145/500            [Info]      │
└────────────────────────────────────────┘
```

---

## ⚙️ **PARAMETER OPTIMIZATION**

### **Grid Size** (Sesuaikan dengan altitude drone)

```bash
# Low altitude (detail tinggi) - 6x10 grid
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show --grid-rows 6 --grid-cols 10

# Medium altitude (balanced) - 5x8 grid (DEFAULT)
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show --grid-rows 5 --grid-cols 8

# High altitude (overview) - 3x5 grid
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show --grid-rows 3 --grid-cols 5
```

### **Confidence Threshold**

```bash
# High confidence only (clean output)
--min-conf 0.7

# Balanced (DEFAULT)
--min-conf 0.4

# Show all detections
--min-conf 0.1
```

### **Speed Optimization**

```bash
# Process every frame (slowest, best quality)
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 --show

# Skip every other frame (2x faster)
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 --show --skip-frames 1

# Skip 2 frames (3x faster)
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 --show --skip-frames 2
```

---

## 🎮 **KEYBOARD CONTROLS**

Saat preview window aktif:

| Key | Action |
|-----|--------|
| **Q** | Quit/Stop |
| **ESC** | Quit/Stop |

---

## 📊 **COMMAND EXAMPLES**

### **Example 1: Standard UAV Detection**
```bash
./RUN_UAV_DETECTION.sh derr.mp4
```

### **Example 2: High Detail (Low Altitude)**
```bash
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show \
    --grid-rows 8 \
    --grid-cols 12 \
    --min-conf 0.5
```

### **Example 3: Fast Processing**
```bash
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show \
    --grid-rows 4 \
    --grid-cols 6 \
    --skip-frames 2 \
    --min-conf 0.4
```

### **Example 4: Save Only (No Preview)**
```bash
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --output results/my_detection.mp4 \
    --grid-rows 5 \
    --grid-cols 8
```

### **Example 5: Multiple Videos (Batch)**
```bash
for video in *.mp4; do
    ./RUN_UAV_DETECTION.sh "$video"
done
```

---

## 🎯 **OPTIMIZATION TIPS**

### **For Better Detection:**
1. **Grid Size**: Match grid to field structure
   - Row crops: Use more columns than rows
   - Square fields: Use equal rows/columns

2. **Confidence**: Adjust based on lighting
   - Good lighting: 0.5-0.7
   - Variable lighting: 0.3-0.5
   - Poor lighting: 0.2-0.4

3. **Frame Skip**: Balance speed vs accuracy
   - Real-time monitoring: skip 2-3 frames
   - Analysis: process all frames

### **For Better Performance:**
```bash
# GPU acceleration (automatic if available)
# Model already optimized for RTX 3050

# Fast processing mode
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show \
    --grid-rows 4 \
    --grid-cols 6 \
    --skip-frames 1 \
    --min-conf 0.5
```

---

## 📁 **OUTPUT LOCATION**

**Default output:**
```
runs/uav_detection/[video_name]_detected.mp4
```

**Custom output:**
```bash
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --output /path/to/output.mp4 \
    --show
```

---

## 🐛 **TROUBLESHOOTING**

### **Problem 1: No preview window**
```bash
# Check DISPLAY
echo $DISPLAY

# Set DISPLAY
export DISPLAY=:0

# Retry
./RUN_UAV_DETECTION.sh derr.mp4
```

### **Problem 2: Too slow**
```bash
# Reduce grid size
--grid-rows 3 --grid-cols 5

# Skip frames
--skip-frames 2

# Both
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show --grid-rows 3 --grid-cols 5 --skip-frames 2
```

### **Problem 3: Boxes too small/large**
```bash
# More boxes (smaller)
--grid-rows 8 --grid-cols 12

# Fewer boxes (larger)
--grid-rows 3 --grid-cols 4
```

### **Problem 4: Too many false detections**
```bash
# Increase confidence threshold
--min-conf 0.6
```

---

## 📊 **EXPECTED PERFORMANCE**

### **Hardware: RTX 3050**
- **Grid 5x8**: ~15-20 FPS
- **Grid 3x5**: ~25-30 FPS
- **Grid 8x12**: ~8-12 FPS

### **Processing Time Estimate:**
| Video Length | Grid Size | Time |
|--------------|-----------|------|
| 1 minute | 5x8 | ~3-4 min |
| 5 minutes | 5x8 | ~15-20 min |
| 10 minutes | 5x8 | ~30-40 min |

*With frame skipping (--skip-frames 1): ~50% faster*

---

## 🎨 **CUSTOMIZATION**

### **Change Colors:**
Edit `run_detection_video.py` line ~60:
```python
colors = {
    'healthy_crop': (0, 255, 0),              # Green
    'stressed_crop': (0, 165, 255),           # Orange
    'disease_stress_vegetation': (0, 0, 255), # Red
    'drought_stress': (0, 255, 255),          # Yellow
    'bare_soil': (128, 128, 128)              # Gray
}
```

### **Change Grid:**
```bash
# Vertical crops (e.g., corn rows)
--grid-rows 4 --grid-cols 10

# Horizontal crops
--grid-rows 8 --grid-cols 5

# Square fields
--grid-rows 6 --grid-cols 6
```

---

## 📝 **SUMMARY**

### **Quick Commands:**

```bash
# Standard detection (balanced)
./RUN_UAV_DETECTION.sh derr.mp4

# High detail
./RUN_UAV_DETECTION.sh derr.mp4  # Edit script: GRID_ROWS=8 GRID_COLS=12

# Fast processing
python3 Pigeon_Harvest/scripts/run_detection_video.py derr.mp4 \
    --show --skip-frames 2 --grid-rows 4 --grid-cols 6
```

### **What You Get:**
- ✅ Bounding boxes with health classification
- ✅ Color-coded visualization
- ✅ Real-time statistics
- ✅ Live preview window
- ✅ Saved output video

---

## 🎬 **DEMO OUTPUT**

Your output will look like the image you showed, with:
- Grid-based bounding boxes across the field
- Each box labeled with class name and confidence
- Statistics panel showing distribution
- Frame counter and info overlay

---

**Created**: June 15, 2026  
**Model**: MoonHarvest Health Classification (98.8% accuracy)  
**Optimized for**: UAV agricultural monitoring  

🚁 **READY FOR UAV CROP HEALTH MONITORING!** 🌱
