#!/usr/bin/env python3
"""Build a lightweight MoonHarvest 5-class vegetation health classification dataset.

Output layout is compatible with Ultralytics YOLO classification:

  output/
    train/bare_soil/*.jpg
    val/bare_soil/*.jpg
    train/healthy_crop/*.jpg
    ...

Target classes:
  healthy_crop
  stressed_crop
  drought_stress
  bare_soil
  disease_stress_vegetation

The script uses the datasets already downloaded under /home/fawwazfa/Program/datasheet.
If no explicit soil/bare-soil folder is found, it creates a clearly documented
bare_soil proxy from crop/weed field images so the demo pipeline can still run.
Use --max-per-class 0 only when you really want every mapped image. The default
keeps training balanced enough for RTX 3050 trial and proposal validation.
"""

from __future__ import annotations

import argparse
import json
import os
import random
import shutil
from pathlib import Path


IMAGE_EXTS = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
CLASSES = [
    # Ultralytics classification uses sorted folder names for class IDs.
    # Keep this alphabetic so the generated classes file matches training order.
    "bare_soil",
    "disease_stress_vegetation",
    "drought_stress",
    "healthy_crop",
    "stressed_crop",
]


def iter_images(path: Path) -> list[Path]:
    if not path.exists():
        return []
    return sorted(
        p for p in path.rglob("*")
        if p.is_file() and p.suffix.lower() in IMAGE_EXTS
    )


def first_existing(root: Path, candidates: list[str]) -> list[Path]:
    found: list[Path] = []
    for rel in candidates:
        path = root / rel
        if path.exists():
            found.append(path)
    return found


def add_images_from_dirs(
    root: Path,
    candidates: list[str],
    target: list[Path],
    target_notes: list[str],
    note_prefix: str = "",
) -> None:
    for path in first_existing(root, candidates):
        images = iter_images(path)
        if images:
            target.extend(images)
            target_notes.append(f"{note_prefix}{path}")


def find_plant_disease_roots(root: Path) -> list[Path]:
    roots: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_dir():
            continue
        if path.name in {"train", "valid"} and "New Plant Diseases Dataset" in str(path):
            roots.append(path)
    return sorted(set(roots))


def find_soil_dirs(root: Path) -> list[Path]:
    names = ("soil", "bare", "barren", "background")
    out: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_dir():
            continue
        normalized = path.name.lower().replace("-", "_").replace(" ", "_")
        if any(name in normalized for name in names):
            out.append(path)
    return out


def collect_sources(root: Path) -> tuple[dict[str, list[Path]], dict[str, list[str]]]:
    sources: dict[str, list[Path]] = {name: [] for name in CLASSES}
    notes: dict[str, list[str]] = {name: [] for name in CLASSES}

    healthy_dirs = first_existing(root, [
        "archive (3)/plant-health/healthy",
        "archive (4)/Lettuce_disease_datasets/Healthy",
    ])
    unhealthy_dirs = first_existing(root, [
        "archive (3)/plant-health/unhealthy",
    ])
    drought_dirs = first_existing(root, [
        "archive (1)/water stress crop images/Sorghum images",
        "archive (1)/water stress crop images/wheat images",
        "archive (1)/water stress crop images/maize images",
        "archive (1)/water stress crop images/Rice plant image",
    ])
    disease_dirs = first_existing(root, [
        "archive (4)/Lettuce_disease_datasets/Downy_mildew_on_lettuce",
        "archive (4)/Lettuce_disease_datasets/Viral",
        "archive (4)/Lettuce_disease_datasets/Bacterial",
        "archive (4)/Lettuce_disease_datasets/Septoria_blight_on_lettuce",
        "archive (4)/Lettuce_disease_datasets/Powdery_mildew_on_lettuce",
        "archive (4)/Lettuce_disease_datasets/Wilt_and_leaf_blight_on_lettuce",
    ])

    for path in healthy_dirs:
        sources["healthy_crop"].extend(iter_images(path))
        notes["healthy_crop"].append(str(path))
    for path in unhealthy_dirs:
        sources["stressed_crop"].extend(iter_images(path))
        notes["stressed_crop"].append(str(path))
    for path in drought_dirs:
        sources["drought_stress"].extend(iter_images(path))
        notes["drought_stress"].append(str(path))
    for path in disease_dirs:
        sources["disease_stress_vegetation"].extend(iter_images(path))
        notes["disease_stress_vegetation"].append(str(path))

    # Extra datasets downloaded for UAV >60 m work. The entries are deliberately
    # conservative: clearly drought/stress folders go to drought/stressed classes,
    # and broad aerial crop images are used only as healthy/context support.
    add_images_from_dirs(root, [
        "uav_over_60m/29_uav_based_soybean_pod_images",
        "uav_over_60m/35_multimodal_deep_learning_rice/Rice Plant Image Dataset/Rice Plant Image Dataset/Images",
        "29_uav_based_soybean_pod_images",
        "35_multimodal_deep_learning_rice/Rice Plant Image Dataset/Rice Plant Image Dataset/Images",
    ], sources["healthy_crop"], notes["healthy_crop"], "UAV context: ")
    add_images_from_dirs(root, [
        "uav_over_60m/33_uav_maize_water_stress_common_rust/01_orthomosaics/01_orthomosaics/water_stress_2025",
        "uav_over_60m/33_uav_maize_water_stress_common_rust/02_processed_patches/02_processed_patches/images",
        "33_uav_maize_water_stress_common_rust/01_orthomosaics/01_orthomosaics/water_stress_2025",
        "33_uav_maize_water_stress_common_rust/02_processed_patches/02_processed_patches/images",
    ], sources["stressed_crop"], notes["stressed_crop"], "UAV stress: ")
    add_images_from_dirs(root, [
        "uav_over_60m/34_minimal_multimodal_wheat_drought/Wheat image data set/train",
        "uav_over_60m/34_minimal_multimodal_wheat_drought/Wheat image data set/test",
        "34_minimal_multimodal_wheat_drought/Wheat image data set/train",
        "34_minimal_multimodal_wheat_drought/Wheat image data set/test",
    ], sources["drought_stress"], notes["drought_stress"], "Wheat drought: ")
    add_images_from_dirs(root, [
        "uav_over_60m/33_uav_maize_water_stress_common_rust/01_orthomosaics/01_orthomosaics/common_rust_2025",
        "33_uav_maize_water_stress_common_rust/01_orthomosaics/01_orthomosaics/common_rust_2025",
    ], sources["disease_stress_vegetation"], notes["disease_stress_vegetation"], "UAV disease: ")

    # Add all images from the large PlantVillage-style dataset that map to
    # MoonHarvest health classes.
    for split_root in find_plant_disease_roots(root):
        for class_dir in sorted(p for p in split_root.iterdir() if p.is_dir()):
            lower = class_dir.name.lower()
            imgs = iter_images(class_dir)
            if not imgs:
                continue
            if "healthy" in lower:
                sources["healthy_crop"].extend(imgs)
                notes["healthy_crop"].append(str(class_dir))
            else:
                sources["disease_stress_vegetation"].extend(imgs)
                notes["disease_stress_vegetation"].append(str(class_dir))

    soil_dirs = find_soil_dirs(root)
    for path in soil_dirs:
        imgs = iter_images(path)
        if imgs:
            sources["bare_soil"].extend(imgs)
            notes["bare_soil"].append(str(path))

    if not sources["bare_soil"]:
        proxy_dirs = first_existing(root, [
            "yolo_crop_weed/images/train",
            "yolo_crop_weed/images/val",
            "archive/agri_data/data",
        ])
        for path in proxy_dirs:
            sources["bare_soil"].extend(iter_images(path))
            notes["bare_soil"].append(f"PROXY field/background images from {path}; replace with real soil class when available.")

    return sources, notes


def copy_or_link(src: Path, dst: Path, mode: str) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists() or dst.is_symlink():
        dst.unlink()
    if mode == "symlink":
        os.symlink(src, dst)
    else:
        shutil.copy2(src, dst)


def build_dataset(root: Path, output: Path, max_per_class: int, val_ratio: float, seed: int, mode: str) -> dict:
    rng = random.Random(seed)
    if output.exists():
        shutil.rmtree(output)

    sources, notes = collect_sources(root)
    manifest: dict = {
        "source_root": str(root),
        "output": str(output),
        "classes": CLASSES,
        "max_per_class": max_per_class,
        "val_ratio": val_ratio,
        "mode": mode,
        "counts": {},
        "sources": notes,
    }

    for class_name in CLASSES:
        images = list(dict.fromkeys(sources[class_name]))
        rng.shuffle(images)
        selected = images if max_per_class <= 0 else images[:max_per_class]
        if not selected:
            raise SystemExit(f"No images found for class '{class_name}'. Check dataset folder: {root}")

        val_count = max(1, int(round(len(selected) * val_ratio))) if len(selected) >= 5 else 1
        val_set = set(selected[:val_count])
        train_count = 0
        val_actual = 0

        for idx, src in enumerate(selected):
            split = "val" if src in val_set else "train"
            if split == "train":
                train_count += 1
            else:
                val_actual += 1
            safe_suffix = src.suffix.lower() if src.suffix else ".jpg"
            dst = output / split / class_name / f"{class_name}_{idx:05d}{safe_suffix}"
            copy_or_link(src, dst, mode)

        manifest["counts"][class_name] = {
            "total": len(selected),
            "train": train_count,
            "val": val_actual,
            "available_before_limit": len(images),
        }

    output.mkdir(parents=True, exist_ok=True)
    (output / "moonharvest_health_classes.txt").write_text("\n".join(CLASSES) + "\n", encoding="utf-8")
    (output / "README.md").write_text(build_readme(manifest), encoding="utf-8")
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return manifest


def build_readme(manifest: dict) -> str:
    lines = [
        "# MoonHarvest Health Classification Dataset",
        "",
        "Dataset ini dibuat otomatis dari folder Kaggle lokal untuk training `yolov8n-cls`.",
        "",
        "## Classes",
        "",
    ]
    for class_name in CLASSES:
        count = manifest["counts"].get(class_name, {})
        lines.append(
            f"- `{class_name}`: {count.get('train', 0)} train, "
            f"{count.get('val', 0)} val, {count.get('total', 0)} total"
        )

    lines.extend([
        "",
        "## Catatan Mapping",
        "",
        "- `healthy_crop`: plant-health healthy, lettuce healthy, dan subset plant disease healthy.",
        "- `stressed_crop`: plant-health unhealthy.",
        "- `drought_stress`: Agricultural Water Stress Image Dataset.",
        "- `disease_stress_vegetation`: lettuce disease dan subset plant disease non-healthy.",
        "- `bare_soil`: memakai folder soil/bare jika tersedia. Jika tidak ada, script membuat proxy dari field/background images agar pipeline demo tetap bisa dilatih. Untuk hasil final, ganti dengan dataset tanah kosong asli.",
        "",
        "## Training",
        "",
        "Jalankan dari root project `Pigeon_Harvest`:",
        "",
        "```bash",
        "DEVICE=0 EPOCHS=80 PATIENCE=20 BATCH=16 MAX_PER_CLASS=500 scripts/train_moonharvest_health_cls.sh",
        "```",
        "",
    ])
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default="/home/fawwazfa/Program/datasheet")
    parser.add_argument("--output", default="/home/fawwazfa/Program/datasheet/moonharvest_health_cls")
    parser.add_argument("--max-per-class", type=int, default=500, help="0 means use all mapped images")
    parser.add_argument("--val-ratio", type=float, default=0.2)
    parser.add_argument("--seed", type=int, default=26)
    parser.add_argument("--mode", choices=["copy", "symlink"], default="symlink")
    args = parser.parse_args()

    manifest = build_dataset(
        root=Path(args.source_root).expanduser(),
        output=Path(args.output).expanduser(),
        max_per_class=args.max_per_class,
        val_ratio=min(max(args.val_ratio, 0.05), 0.4),
        seed=args.seed,
        mode=args.mode,
    )

    print(json.dumps(manifest["counts"], indent=2))
    print(f"\nDataset ready: {manifest['output']}")
    print(f"Classes file: {manifest['output']}/moonharvest_health_classes.txt")
    print(f"Manifest: {manifest['output']}/manifest.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
