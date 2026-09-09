#!/usr/bin/env python3
"""Export the trained crop/weed YOLOv8n model to ONNX at 416x416, matching
the input size MoonHarvest's moonharvest-uav-det.onnx already uses."""
import sys
from pathlib import Path
from ultralytics import YOLO

RUN_DIR = Path("/home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest/training/runs/crop_weed_v1")
BEST_PT = RUN_DIR / "weights" / "best.pt"

if not BEST_PT.exists():
    print(f"ERROR: {BEST_PT} not found")
    sys.exit(1)

model = YOLO(str(BEST_PT))
export_path = model.export(format="onnx", imgsz=416, opset=12, simplify=True)
print("Exported ONNX:", export_path)
