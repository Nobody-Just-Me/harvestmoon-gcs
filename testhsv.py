import argparse
import csv
import json
import os
import time
from collections import deque

import cv2
import numpy as np


DEFAULT_CONFIG = {
    "preprocess": {
        "gray_world": True,
        "clahe": True,
        "clahe_clip": 2.0,
        "blur": 5,
        "resize_width": 960
    },
    "grid": {
        "rows": 6,
        "cols": 6,
        "active_cell_threshold": 0.12
    },
    "temporal": {
        "window": 5
    },
    "display": {
        "overlay_alpha": 0.35,
        "show_grid": True,
        "show_mask": True
    },
    "classes": {
        "healthy_green": {
            "lower": [35, 45, 40],
            "upper": [95, 255, 255],
            "open": 3,
            "close": 5,
            "min_area": 250,
            "threshold": 0.08,
            "area_weight": 0.65,
            "cell_weight": 0.35,
            "color": [0, 220, 0]
        },
        "yellow_stress": {
            "lower": [18, 45, 60],
            "upper": [38, 255, 255],
            "open": 3,
            "close": 5,
            "min_area": 180,
            "threshold": 0.05,
            "area_weight": 0.6,
            "cell_weight": 0.4,
            "color": [0, 220, 220]
        },
        "brown_dry": {
            "lower": [5, 35, 35],
            "upper": [22, 255, 220],
            "open": 3,
            "close": 5,
            "min_area": 180,
            "threshold": 0.05,
            "area_weight": 0.6,
            "cell_weight": 0.4,
            "color": [30, 90, 200]
        },
        "soil_background": {
            "lower": [8, 20, 20],
            "upper": [30, 180, 180],
            "open": 3,
            "close": 5,
            "min_area": 300,
            "threshold": 0.12,
            "area_weight": 0.55,
            "cell_weight": 0.45,
            "color": [120, 120, 120]
        }
    }
}


def ensure_config(path):
    if not os.path.exists(path):
        save_config(path, DEFAULT_CONFIG)
    return load_config(path)


def load_config(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_config(path, cfg):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(cfg, f, indent=2)


def clamp_odd(value, minimum=1):
    value = max(minimum, int(value))
    return value if value % 2 == 1 else value + 1


def resize_keep_aspect(frame, width):
    if width <= 0 or frame.shape[1] <= width:
        return frame
    ratio = width / frame.shape[1]
    height = int(frame.shape[0] * ratio)
    return cv2.resize(frame, (width, height), interpolation=cv2.INTER_AREA)


def gray_world_balance(frame):
    img = frame.astype(np.float32)
    avg_b, avg_g, avg_r = np.mean(img[:, :, 0]), np.mean(img[:, :, 1]), np.mean(img[:, :, 2])
    avg_gray = (avg_b + avg_g + avg_r) / 3.0
    gain_b = avg_gray / (avg_b + 1e-6)
    gain_g = avg_gray / (avg_g + 1e-6)
    gain_r = avg_gray / (avg_r + 1e-6)
    img[:, :, 0] *= gain_b
    img[:, :, 1] *= gain_g
    img[:, :, 2] *= gain_r
    return np.clip(img, 0, 255).astype(np.uint8)


def preprocess_frame(frame, cfg):
    frame = resize_keep_aspect(frame, cfg["preprocess"].get("resize_width", 960))
    if cfg["preprocess"].get("gray_world", True):
        frame = gray_world_balance(frame)
    if cfg["preprocess"].get("clahe", True):
        lab = cv2.cvtColor(frame, cv2.COLOR_BGR2LAB)
        l, a, b = cv2.split(lab)
        clahe = cv2.createCLAHE(clipLimit=float(cfg["preprocess"].get("clahe_clip", 2.0)), tileGridSize=(8, 8))
        l = clahe.apply(l)
        lab = cv2.merge([l, a, b])
        frame = cv2.cvtColor(lab, cv2.COLOR_LAB2BGR)
    blur_k = clamp_odd(cfg["preprocess"].get("blur", 5), 1)
    if blur_k > 1:
        frame = cv2.GaussianBlur(frame, (blur_k, blur_k), 0)
    return frame


def apply_hsv_mask(hsv, class_cfg):
    lower = np.array(class_cfg["lower"], dtype=np.uint8)
    upper = np.array(class_cfg["upper"], dtype=np.uint8)
    mask = cv2.inRange(hsv, lower, upper)

    open_k = clamp_odd(class_cfg.get("open", 3), 1)
    close_k = clamp_odd(class_cfg.get("close", 5), 1)
    kernel_open = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (open_k, open_k))
    kernel_close = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (close_k, close_k))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel_open)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel_close)

    num_labels, labels, stats, _ = cv2.connectedComponentsWithStats(mask, connectivity=8)
    cleaned = np.zeros_like(mask)
    min_area = int(class_cfg.get("min_area", 100))
    for i in range(1, num_labels):
        area = stats[i, cv2.CC_STAT_AREA]
        if area >= min_area:
            cleaned[labels == i] = 255
    return cleaned


def grid_metrics(mask, rows, cols, active_threshold):
    h, w = mask.shape[:2]
    cell_h = h // rows
    cell_w = w // cols
    densities = []
    active_cells = 0
    for r in range(rows):
        for c in range(cols):
            y1 = r * cell_h
            y2 = h if r == rows - 1 else (r + 1) * cell_h
            x1 = c * cell_w
            x2 = w if c == cols - 1 else (c + 1) * cell_w
            cell = mask[y1:y2, x1:x2]
            density = float(np.count_nonzero(cell)) / float(cell.size + 1e-6)
            densities.append(density)
            if density >= active_threshold:
                active_cells += 1
    return {
        "density_mean": float(np.mean(densities)) if densities else 0.0,
        "density_max": float(np.max(densities)) if densities else 0.0,
        "active_cells": active_cells,
        "total_cells": rows * cols,
        "active_ratio": active_cells / float(rows * cols + 1e-6)
    }


def contour_metrics(mask):
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    count = len(contours)
    largest = 0.0
    bbox = None
    if contours:
        largest_cnt = max(contours, key=cv2.contourArea)
        largest = float(cv2.contourArea(largest_cnt))
        x, y, w, h = cv2.boundingRect(largest_cnt)
        bbox = [int(x), int(y), int(w), int(h)]
    return {
        "contour_count": count,
        "largest_area": largest,
        "largest_bbox": bbox
    }


def analyze_classes(frame, cfg):
    proc = preprocess_frame(frame, cfg)
    hsv = cv2.cvtColor(proc, cv2.COLOR_BGR2HSV)
    rows = int(cfg["grid"].get("rows", 6))
    cols = int(cfg["grid"].get("cols", 6))
    active_thr = float(cfg["grid"].get("active_cell_threshold", 0.12))

    results = {}
    for name, class_cfg in cfg["classes"].items():
        mask = apply_hsv_mask(hsv, class_cfg)
        area_ratio = float(np.count_nonzero(mask)) / float(mask.size + 1e-6)
        g = grid_metrics(mask, rows, cols, active_thr)
        c = contour_metrics(mask)
        score = (
            area_ratio * float(class_cfg.get("area_weight", 0.65))
            + g["active_ratio"] * float(class_cfg.get("cell_weight", 0.35))
        )
        detected = score >= float(class_cfg.get("threshold", 0.05))
        results[name] = {
            "mask": mask,
            "area_ratio": area_ratio,
            "score": score,
            "detected": detected,
            **g,
            **c
        }
    return proc, results


def smooth_results(results, history, cfg):
    snapshot = {k: {"score": v["score"], "area_ratio": v["area_ratio"]} for k, v in results.items()}
    history.append(snapshot)
    smoothed = {}
    for cls in results:
        scores = [item[cls]["score"] for item in history if cls in item]
        areas = [item[cls]["area_ratio"] for item in history if cls in item]
        out = results[cls].copy()
        out["score_smooth"] = float(np.mean(scores)) if scores else out["score"]
        out["area_ratio_smooth"] = float(np.mean(areas)) if areas else out["area_ratio"]
        out["detected"] = out["score_smooth"] >= float(cfg["classes"][cls].get("threshold", 0.05))
        smoothed[cls] = out
    return smoothed


def dominant_class(results):
    if not results:
        return None, 0.0
    best_name = max(results, key=lambda k: results[k].get("score_smooth", results[k]["score"]))
    best_score = results[best_name].get("score_smooth", results[best_name]["score"])
    return best_name, best_score


def draw_grid(frame, rows, cols, color=(80, 80, 80)):
    h, w = frame.shape[:2]
    for r in range(1, rows):
        y = int(r * h / rows)
        cv2.line(frame, (0, y), (w, y), color, 1)
    for c in range(1, cols):
        x = int(c * w / cols)
        cv2.line(frame, (x, 0), (x, h), color, 1)
    return frame


def overlay_results(frame, results, cfg):
    out = frame.copy()
    alpha = float(cfg["display"].get("overlay_alpha", 0.35))
    if cfg["display"].get("show_mask", True):
        for cls, res in results.items():
            if not res.get("detected", False):
                continue
            color = tuple(int(c) for c in cfg["classes"][cls].get("color", [0, 255, 0]))
            overlay = np.zeros_like(out)
            overlay[res["mask"] > 0] = color
            out = cv2.addWeighted(out, 1.0, overlay, alpha, 0)
            if res.get("largest_bbox"):
                x, y, w, h = res["largest_bbox"]
                cv2.rectangle(out, (x, y), (x + w, y + h), color, 2)

    if cfg["display"].get("show_grid", True):
        out = draw_grid(out, int(cfg["grid"].get("rows", 6)), int(cfg["grid"].get("cols", 6)))

    winner, win_score = dominant_class(results)
    y = 25
    label = f"DOMINAN: {winner if winner else '-'} | skor={win_score:.3f}"
    cv2.putText(out, label, (12, y), cv2.FONT_HERSHEY_SIMPLEX, 0.68, (255, 255, 255), 2, cv2.LINE_AA)
    y += 28
    for cls, res in sorted(results.items(), key=lambda x: x[1].get("score_smooth", x[1]["score"]), reverse=True):
        color = tuple(int(c) for c in cfg["classes"][cls].get("color", [255, 255, 255]))
        text = (
            f"{cls}: score={res.get('score_smooth', res['score']):.3f} "
            f"area={100*res.get('area_ratio_smooth', res['area_ratio']):.1f}% "
            f"cell={res['active_cells']}/{res['total_cells']} cnt={res['contour_count']}"
        )
        cv2.putText(out, text, (12, y), cv2.FONT_HERSHEY_SIMPLEX, 0.53, color, 2, cv2.LINE_AA)
        y += 24
    return out


def open_source(source):
    if isinstance(source, str) and source.isdigit():
        return cv2.VideoCapture(int(source)), "camera"
    if isinstance(source, str) and source.lower().endswith((".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff")):
        return source, "image"
    return cv2.VideoCapture(source), "video"


def init_csv(csv_path, class_names):
    if not csv_path:
        return None, None
    f = open(csv_path, "w", newline="", encoding="utf-8")
    writer = csv.writer(f)
    header = ["frame", "time_sec", "dominant_class", "dominant_score"]
    for cls in class_names:
        header += [f"{cls}_score", f"{cls}_area_ratio", f"{cls}_active_ratio", f"{cls}_largest_area"]
    writer.writerow(header)
    return f, writer


def write_csv(writer, frame_idx, timestamp, results):
    winner, score = dominant_class(results)
    row = [frame_idx, round(timestamp, 3), winner, round(score, 5)]
    for cls in results:
        res = results[cls]
        row += [
            round(res.get("score_smooth", res["score"]), 5),
            round(res.get("area_ratio_smooth", res["area_ratio"]), 5),
            round(res["active_ratio"], 5),
            round(res["largest_area"], 2)
        ]
    writer.writerow(row)


def run_pipeline(args):
    cfg = ensure_config(args.config)
    history = deque(maxlen=int(cfg["temporal"].get("window", 5)))
    src, src_type = open_source(args.source)
    csv_file, csv_writer = init_csv(args.csv, list(cfg["classes"].keys()))
    writer = None
    frame_idx = 0

    if src_type == "image":
        frame = cv2.imread(src)
        if frame is None:
            raise FileNotFoundError(f"Gagal membaca image: {src}")
        proc, results = analyze_classes(frame, cfg)
        results = smooth_results(results, history, cfg)
        vis = overlay_results(proc, results, cfg)
        if args.output:
            cv2.imwrite(args.output, vis)
        print(json.dumps({k: {kk: vv for kk, vv in v.items() if kk != "mask"} for k, v in results.items()}, indent=2))
        cv2.imshow("MoonHarvest HSV", vis)
        cv2.waitKey(0)
        cv2.destroyAllWindows()
        return

    if not src.isOpened():
        raise RuntimeError("Gagal membuka source video/kamera")

    fps = src.get(cv2.CAP_PROP_FPS)
    if fps <= 0:
        fps = 20.0

    while True:
        ok, frame = src.read()
        if not ok:
            break
        frame_idx += 1
        proc, results = analyze_classes(frame, cfg)
        results = smooth_results(results, history, cfg)
        vis = overlay_results(proc, results, cfg)

        if args.output and writer is None:
            h, w = vis.shape[:2]
            fourcc = cv2.VideoWriter_fourcc(*"mp4v")
            writer = cv2.VideoWriter(args.output, fourcc, fps, (w, h))
        if writer is not None:
            writer.write(vis)
        if csv_writer is not None:
            timestamp = src.get(cv2.CAP_PROP_POS_MSEC) / 1000.0
            write_csv(csv_writer, frame_idx, timestamp, results)

        cv2.imshow("MoonHarvest HSV", vis)
        key = cv2.waitKey(1) & 0xFF
        if key == ord('q'):
            break
        if key == ord('p'):
            cv2.waitKey(0)

    src.release()
    if writer is not None:
        writer.release()
    if csv_file is not None:
        csv_file.close()
    cv2.destroyAllWindows()


def create_calibration_window(class_cfg):
    cv2.namedWindow("Calibrate", cv2.WINDOW_NORMAL)
    values = {
        "LH": int(class_cfg["lower"][0]),
        "LS": int(class_cfg["lower"][1]),
        "LV": int(class_cfg["lower"][2]),
        "UH": int(class_cfg["upper"][0]),
        "US": int(class_cfg["upper"][1]),
        "UV": int(class_cfg["upper"][2]),
        "OPEN": int(class_cfg.get("open", 3)),
        "CLOSE": int(class_cfg.get("close", 5)),
        "AREA": int(class_cfg.get("min_area", 250)),
        "THR": int(float(class_cfg.get("threshold", 0.08)) * 100)
    }
    limits = {"LH": 179, "LS": 255, "LV": 255, "UH": 179, "US": 255, "UV": 255, "OPEN": 21, "CLOSE": 21, "AREA": 5000, "THR": 100}
    for k, v in values.items():
        cv2.createTrackbar(k, "Calibrate", v, limits[k], lambda x: None)


def read_calibration_values(base_cfg):
    cfg = dict(base_cfg)
    cfg["lower"] = [
        cv2.getTrackbarPos("LH", "Calibrate"),
        cv2.getTrackbarPos("LS", "Calibrate"),
        cv2.getTrackbarPos("LV", "Calibrate")
    ]
    cfg["upper"] = [
        cv2.getTrackbarPos("UH", "Calibrate"),
        cv2.getTrackbarPos("US", "Calibrate"),
        cv2.getTrackbarPos("UV", "Calibrate")
    ]
    cfg["open"] = clamp_odd(cv2.getTrackbarPos("OPEN", "Calibrate"), 1)
    cfg["close"] = clamp_odd(cv2.getTrackbarPos("CLOSE", "Calibrate"), 1)
    cfg["min_area"] = int(cv2.getTrackbarPos("AREA", "Calibrate"))
    cfg["threshold"] = cv2.getTrackbarPos("THR", "Calibrate") / 100.0
    return cfg


def get_first_frame(source):
    src, src_type = open_source(source)
    if src_type == "image":
        frame = cv2.imread(src)
        if frame is None:
            raise FileNotFoundError(f"Gagal membaca image: {src}")
        return frame, None, src_type
    if not src.isOpened():
        raise RuntimeError("Gagal membuka source")
    ok, frame = src.read()
    if not ok:
        src.release()
        raise RuntimeError("Tidak ada frame yang bisa dibaca")
    return frame, src, src_type


def calibrate_class(args):
    cfg = ensure_config(args.config)
    if args.class_name not in cfg["classes"]:
        raise KeyError(f"Class tidak ditemukan: {args.class_name}")

    frame, stream, src_type = get_first_frame(args.source)
    base = cfg["classes"][args.class_name].copy()
    create_calibration_window(base)
    print("[CALIBRATE] q=keluar | s=simpan | n=frame berikutnya")

    while True:
        current_cfg = read_calibration_values(base)
        temp_cfg = json.loads(json.dumps(cfg))
        temp_cfg["classes"][args.class_name] = current_cfg
        proc = preprocess_frame(frame, temp_cfg)
        hsv = cv2.cvtColor(proc, cv2.COLOR_BGR2HSV)
        mask = apply_hsv_mask(hsv, current_cfg)
        color = tuple(int(c) for c in temp_cfg["classes"][args.class_name].get("color", [0, 255, 0]))
        overlay = proc.copy()
        overlay[mask > 0] = cv2.addWeighted(overlay[mask > 0], 0.3, np.full_like(overlay[mask > 0], color), 0.7, 0)
        text = f"{args.class_name} L={current_cfg['lower']} U={current_cfg['upper']} area={current_cfg['min_area']} thr={current_cfg['threshold']:.2f}"
        cv2.putText(overlay, text, (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 2, cv2.LINE_AA)
        cv2.imshow("Calibrate", overlay)
        key = cv2.waitKey(30) & 0xFF

        if key == ord('s'):
            cfg["classes"][args.class_name] = current_cfg
            save_config(args.config, cfg)
            print(f"Tersimpan ke {args.config}")
        elif key == ord('n') and stream is not None:
            ok, next_frame = stream.read()
            if ok:
                frame = next_frame
        elif key == ord('q'):
            break

    if stream is not None:
        stream.release()
    cv2.destroyAllWindows()


def compute_bounds_from_pixels(hsv_pixels, h_margin=4, s_margin=18, v_margin=18, low_pct=5, high_pct=95):
    low = np.percentile(hsv_pixels, low_pct, axis=0)
    high = np.percentile(hsv_pixels, high_pct, axis=0)
    low[0] = max(0, low[0] - h_margin)
    low[1] = max(0, low[1] - s_margin)
    low[2] = max(0, low[2] - v_margin)
    high[0] = min(179, high[0] + h_margin)
    high[1] = min(255, high[1] + s_margin)
    high[2] = min(255, high[2] + v_margin)
    return low.astype(int).tolist(), high.astype(int).tolist()


def tune_from_rois(args):
    cfg = ensure_config(args.config)
    if args.class_name not in cfg["classes"]:
        raise KeyError(f"Class tidak ditemukan: {args.class_name}")

    frame, stream, src_type = get_first_frame(args.source)
    collected = []
    print("[TUNE] r=pilih ROI | n=frame berikutnya | s=simpan hasil | q=keluar")

    while True:
        temp_cfg = cfg["classes"][args.class_name].copy()
        preview = preprocess_frame(frame, cfg)
        hsv = cv2.cvtColor(preview, cv2.COLOR_BGR2HSV)

        if collected:
            pixels = np.vstack(collected)
            low, high = compute_bounds_from_pixels(
                pixels,
                h_margin=args.h_margin,
                s_margin=args.s_margin,
                v_margin=args.v_margin,
                low_pct=args.low_pct,
                high_pct=args.high_pct,
            )
            temp_cfg["lower"] = low
            temp_cfg["upper"] = high
            mask = apply_hsv_mask(hsv, temp_cfg)
            color = tuple(int(c) for c in cfg["classes"][args.class_name].get("color", [0, 255, 0]))
            layer = preview.copy()
            layer[mask > 0] = color
            preview = cv2.addWeighted(preview, 0.7, layer, 0.3, 0)
            info = f"ROI={len(collected)} L={low} U={high}"
        else:
            info = "Belum ada ROI. Tekan r untuk pilih area target."

        cv2.putText(preview, info, (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 2, cv2.LINE_AA)
        cv2.imshow("Tune ROI", preview)
        key = cv2.waitKey(30) & 0xFF

        if key == ord('r'):
            roi = cv2.selectROI("Tune ROI", preview, False, False)
            x, y, w, h = map(int, roi)
            if w > 0 and h > 0:
                roi_hsv = hsv[y:y + h, x:x + w].reshape(-1, 3)
                roi_hsv = roi_hsv[(roi_hsv[:, 1] > 15) & (roi_hsv[:, 2] > 15)]
                if len(roi_hsv) > 0:
                    collected.append(roi_hsv)
                    print(f"ROI ditambahkan. Total ROI: {len(collected)}")
        elif key == ord('n') and stream is not None:
            ok, next_frame = stream.read()
            if ok:
                frame = next_frame
        elif key == ord('s'):
            if not collected:
                print("Belum ada ROI untuk disimpan")
                continue
            pixels = np.vstack(collected)
            low, high = compute_bounds_from_pixels(
                pixels,
                h_margin=args.h_margin,
                s_margin=args.s_margin,
                v_margin=args.v_margin,
                low_pct=args.low_pct,
                high_pct=args.high_pct,
            )
            cfg["classes"][args.class_name]["lower"] = low
            cfg["classes"][args.class_name]["upper"] = high
            save_config(args.config, cfg)
            print(f"Tuning tersimpan: {args.class_name} -> lower={low} upper={high}")
        elif key == ord('q'):
            break

    if stream is not None:
        stream.release()
    cv2.destroyAllWindows()


def build_parser():
    p = argparse.ArgumentParser(description="MoonHarvest HSV suite: run, calibrate, dan tune ROI")
    sub = p.add_subparsers(dest="command", required=True)

    p_run = sub.add_parser("run", help="Jalankan analisis HSV pada image/video/camera")
    p_run.add_argument("--source", required=True, help="Path image/video atau index kamera, contoh: 0")
    p_run.add_argument("--config", default="moonharvest_hsv_config.json")
    p_run.add_argument("--output", default="", help="Simpan overlay output, png untuk image atau mp4 untuk video")
    p_run.add_argument("--csv", default="", help="Simpan log hasil per frame ke CSV")
    p_run.set_defaults(func=run_pipeline)

    p_cal = sub.add_parser("calibrate", help="Kalibrasi manual HSV dengan trackbar")
    p_cal.add_argument("--source", required=True, help="Path image/video atau index kamera")
    p_cal.add_argument("--config", default="moonharvest_hsv_config.json")
    p_cal.add_argument("--class-name", required=True, help="Nama class pada config")
    p_cal.set_defaults(func=calibrate_class)

    p_tune = sub.add_parser("tune", help="Tuning HSV dari ROI yang dipilih manual")
    p_tune.add_argument("--source", required=True, help="Path image/video atau index kamera")
    p_tune.add_argument("--config", default="moonharvest_hsv_config.json")
    p_tune.add_argument("--class-name", required=True, help="Nama class pada config")
    p_tune.add_argument("--low-pct", type=float, default=5.0)
    p_tune.add_argument("--high-pct", type=float, default=95.0)
    p_tune.add_argument("--h-margin", type=int, default=4)
    p_tune.add_argument("--s-margin", type=int, default=18)
    p_tune.add_argument("--v-margin", type=int, default=18)
    p_tune.set_defaults(func=tune_from_rois)
    return p


def main():
    parser = build_parser()
    args = parser.parse_args()
    start = time.time()
    args.func(args)
    print(f"Selesai dalam {time.time() - start:.2f} detik")


if __name__ == "__main__":
    main()