#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MoonHarvest HSV Crop Condition Detector (Advanced)
==================================================
Deteksi 5 kelas kondisi lahan dari citra/UAV video MENGGUNAKAN HSV + indeks
vegetasi (ExG) + analisis tekstur. Tanpa deep learning -- murni computer vision
klasik, ringan, dan cocok untuk perangkat kelas budget (mis. RealmePad Mini).

Kelas:
  0 healthy_crop                 (hijau sehat)
  1 stressed_crop                (stress non-spesifik / menguning)
  2 disease_stress_vegetation    (terserang penyakit / nekrosis berbintik)
  3 drought_stress               (stress kekeringan / coklat-tan kering)
  4 bare_soil                    (tanah kosong)
  (background: langit/objek non-lahan -> diabaikan)
  (shadow: bayangan gelap -> diabaikan)

Pipeline:
  1. Normalisasi pencahayaan : Gray-World white balance + CLAHE pada channel V
  2. Ruang warna             : BGR->HSV  dan  ExG (Excess Green) = 2g - r - b
  3. Tekstur                 : standar deviasi lokal (sliding window) pada grayscale
  4. Klasifikasi per-piksel  : aturan HSV berprioritas + ExG + tekstur
  5. Pembersihan morfologi   : median filter label + open/close per kelas
  6. Ekstraksi region        : connected components -> bounding box + confidence
  7. Analisis zona grid      : NxM sel -> kelas dominan + skor keparahan
  8. Smoothing temporal      : EMA distribusi kelas antar frame (mode video)
  9. Output                  : overlay, ExG heatmap, JSON/CSV report, video

Threshold default DIKALIBRASI dari video pengguna (footage desaturated, S~35).
Semua nilai dapat diatur runtime lewat file config JSON (--config) atau di-
kalibrasi ulang otomatis (subcommand: calibrate).

Pemakaian:
  python3 moonharvest_hsv.py image  -i frame.jpg -o out/        [--grid 8x8]
  python3 moonharvest_hsv.py video  -i clip.mp4  -o out/  --fps 2 [--no-video]
  python3 moonharvest_hsv.py calibrate -i clip.mp4 -o cfg.json   --k 6
"""
import argparse, csv, json, os, sys, time
import cv2
import numpy as np

# ---------------------------------------------------------------------------
# DEMO_MODE — semua tampilan disesuaikan dengan proposal TEKNOFEST
# ---------------------------------------------------------------------------
DEMO_MODE = os.environ.get("MOONHARVEST_DEMO", "1") == "1"

# Mapping kelas internal → label demo (None = tersembunyi/background)
DISPLAY_MAP = {
    "healthy_crop":              "Healthy",
    "stressed_crop":             "Stress",
    "disease_stress_vegetation": "Disease",
    "drought_stress":            "Stress",   # digabung ke Stress
    "bare_soil":                 None,        # disembunyikan
}
HIDDEN_CLASSES = {"bare_soil"}

# Warna (BGR) untuk label demo
DEMO_PALETTE = {
    "Healthy": (0, 255, 0),
    "Stress":  (0, 255, 255),
    "Disease": (0, 0, 255),
    "Pest":    (0, 140, 255),
}

# ----------------------------------------------------------------------------
# KONFIGURASI / KALIBRASI (OpenCV HSV: H 0-179, S 0-255, V 0-255)
# Nilai di bawah dikalibrasi dari footage UAV pengguna (saturasi rendah).
# ----------------------------------------------------------------------------
CLASSES = [
    "healthy_crop",
    "stressed_crop",
    "disease_stress_vegetation",
    "drought_stress",
    "bare_soil",
]
IGNORE = {"background": 250, "shadow": 251, "unknown": 255}

# Warna overlay (BGR) per kelas — dipakai di mode non-demo
PALETTE = {
    "healthy_crop":              (60, 200, 60),
    "stressed_crop":             (40, 220, 230),
    "disease_stress_vegetation": (40, 40, 230),
    "drought_stress":            (30, 140, 250),
    "bare_soil":                 (110, 120, 140),
    "background":                (200, 160, 90),
    "shadow":                    (50, 50, 50),
}
# Bobot keparahan untuk Field Health Index
SEVERITY = {
    "healthy_crop": 0.0, "stressed_crop": 0.45,
    "drought_stress": 0.75, "disease_stress_vegetation": 1.0, "bare_soil": 0.0,
}

# Swatch default (FRAKSI [x0,y0,x1,y1]) untuk melatih engine statistik.
# Diturunkan dari frame referensi lahan padi: kiri hijau -> kuning stress ->
# tengah tanah rusak -> air di kanan bawah. drought/disease memakai prototipe.
DEFAULT_SWATCHES = {
    "healthy_crop":  [[0.03, 0.22, 0.18, 0.85]],
    "stressed_crop": [[0.24, 0.18, 0.40, 0.85]],
    "bare_soil":     [[0.49, 0.20, 0.70, 0.72]],
    "water":         [[0.91, 0.60, 0.99, 0.92]],
}

DEFAULT_CFG = {
    # Normalisasi
    "white_balance": True,
    "clahe_clip": 2.0,
    "clahe_grid": 8,
    # Pemisahan dasar (DIKALIBRASI dari frame referensi lahan padi pengguna)
    "shadow_v_max": 45,          # V di bawah ini = bayangan
    "exg_veg_thr": 0.04,         # ExG > ini = ada vegetasi (stress kuning ~0.06)
    "exg_healthy_min": 0.11,     # ExG >= ini = HIJAU KUAT (healthy). kiri ~0.18
    # Background: hue kebiruan/cyan = langit, air/sungai, kabut -> DIABAIKAN.
    # HARUS cukup tersaturasi (air sungai S~48); tanah rusak desaturasi (S~14)
    # TIDAK ikut terbuang dan tetap jadi bare_soil.
    "bg_h": [84, 140], "bg_s_min": 34,
    # Rentang HSV per kelas (OpenCV H 0-179)
    # healthy: hijau kuat (kiri: H54 S66). Tidak memaksa hijau lemah jadi sehat.
    "healthy":  {"h": [40, 82], "s_lo": 48, "v": [80, 255]},
    # stressed: kuning s/d hijau lemah (band kuning: H30 S43). Mencakup tanaman
    # yang \"tidak terlalu hijau\" agar tidak salah jadi healthy.
    "stressed": {"h": [20, 82], "s_lo": 26, "v": [95, 255]},
    "drought":  {"h": [10, 20], "s_lo": 28, "v": [90, 255]},
    # disease: merah/nekrosis berbintik (butuh saturasi & tekstur tinggi)
    "disease":  {"h1": [0, 10], "h2": [168, 179], "s_lo": 45, "v": [25, 215]},
    # bare soil / tanah rusak: saturasi sangat rendah, non-vegetasi (tengah: S14)
    "soil":     {"s_max": 30, "v": [110, 240]},
    # Tekstur (std lokal) -> penyakit (berbintik) vs kering (rata)
    "texture_win": 9,
    "disease_texture_min": 24.0,  # dinaikkan: 16 terlalu sensitif, tangkap tepi/jalan
    # Morfologi & region
    "morph_kernel": 5,
    "label_median": 7,
    "min_region_area_frac": 0.0015,  # area minimum box relatif total piksel
    # Penyingkiran jalan/rumah/pematang: buka morfologi besar pada bare_soil
    # untuk menghapus garis tipis (jalan/pematang) & objek kecil (rumah).
    "suppress_structures": True,
    "road_open_kernel": 11,          # makin besar makin agresif buang garis tipis
    "struct_min_area_frac": 0.0008,  # komponen lebih kecil dari ini -> background
    # Engine statistik (akurat): ambang penolakan jarak Mahalanobis.
    # Piksel yang terlalu jauh dari semua kelas -> background.
    "stat_reject_maha": 90.0,
    # Bias healthy: piksel "stressed" yang BUKAN kuning sejati dikembalikan
    # ke healthy. Hanya kuning vivid (hue kuning + ExG rendah + S tinggi)
    # yang tetap stressed. Longgarkan/perketat lewat 3 nilai di bawah.
    "stressed_to_healthy": True,
    "yellow_h": [18, 36],      # rentang hue kuning sejati (OpenCV)
    "yellow_exg_max": 0.085,   # ExG di atas ini = masih cukup hijau -> healthy
    "yellow_s_min": 36,        # kuning harus cukup pekat; pucat -> healthy
    # Area gelap (bayangan/tanah gelap) -> bare_soil (bukan diabaikan).
    "dark_as_soil": True,
    "dark_v_max": 55,          # V di bawah ini dianggap gelap -> bare_soil
    # Temporal (video)
    "ema_alpha": 0.6,
}


def load_cfg(path=None):
    cfg = json.loads(json.dumps(DEFAULT_CFG))
    if path and os.path.exists(path):
        with open(path) as f:
            cfg.update(json.load(f))
    return cfg


# ----------------------------------------------------------------------------
# PRA-PROSES
# ----------------------------------------------------------------------------
def gray_world_wb(bgr):
    """Koreksi white balance sederhana agar threshold HSV stabil antar cahaya."""
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean() + 1e-6, g.mean() + 1e-6, r.mean() + 1e-6
    k = (mb + mg + mr) / 3.0
    b = np.clip(b * (k / mb), 0, 255)
    g = np.clip(g * (k / mg), 0, 255)
    r = np.clip(r * (k / mr), 0, 255)
    return cv2.merge([b, g, r]).astype(np.uint8)


def preprocess(bgr, cfg):
    out = gray_world_wb(bgr) if cfg["white_balance"] else bgr.copy()
    hsv = cv2.cvtColor(out, cv2.COLOR_BGR2HSV)
    h, s, v = cv2.split(hsv)
    clahe = cv2.createCLAHE(clipLimit=cfg["clahe_clip"],
                            tileGridSize=(cfg["clahe_grid"], cfg["clahe_grid"]))
    v = clahe.apply(v)
    hsv = cv2.merge([h, s, v])
    return out, hsv


def excess_green(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    tot = b + g + r + 1e-6
    return (2 * g - r - b) / tot   # ~[-1,1]


def local_std(gray, win):
    g = gray.astype(np.float32)
    mean = cv2.boxFilter(g, -1, (win, win))
    sq = cv2.boxFilter(g * g, -1, (win, win))
    var = np.clip(sq - mean * mean, 0, None)
    return np.sqrt(var)


# ----------------------------------------------------------------------------
# FITUR MULTI-INDEKS (HSV + indeks vegetasi RGB + tekstur)
# ----------------------------------------------------------------------------
FEAT_DIM = 8


def veg_indices(bgr):
    """GLI & VARI: indeks vegetasi berbasis RGB, pelengkap Hue agar pemisahan
    healthy/stressed/drought lebih tahan pencahayaan."""
    b, g, r = cv2.split(bgr.astype(np.float32))
    tot = r + g + b + 1e-6
    rn, gn, bn = r / tot, g / tot, b / tot
    gli = (2 * gn - rn - bn) / (2 * gn + rn + bn + 1e-6)
    vari = (gn - rn) / (gn + rn - bn + 1e-6)
    return gli, vari


def compute_features(bgr, hsv, exg, texture):
    """Vektor fitur per piksel: [cosH, sinH, S, V, ExG, GLI, VARI, tekstur].
    Hue diubah ke cos/sin karena melingkar (0 dan 179 berdekatan)."""
    Hr = hsv[:, :, 0].astype(np.float32) * (2 * np.pi / 180.0)
    S = hsv[:, :, 1].astype(np.float32) / 255.0
    V = hsv[:, :, 2].astype(np.float32) / 255.0
    gli, vari = veg_indices(bgr)
    feats = np.stack([
        np.cos(Hr), np.sin(Hr), S, V,
        np.clip(exg, -0.3, 0.5),
        np.clip(gli, -1.0, 1.0),
        np.clip(vari, -1.0, 1.0),
        np.clip(texture / 64.0, 0.0, 2.0),
    ], axis=-1).astype(np.float32)
    return feats


def proto_feature(h, s, v):
    """Vektor fitur dari satu warna prototipe HSV (untuk kelas tanpa swatch)."""
    px = np.uint8([[[h, s, v]]])
    bgr = cv2.cvtColor(px, cv2.COLOR_HSV2BGR).astype(np.float32)
    b, g, r = bgr[0, 0]
    tot = r + g + b + 1e-6
    rn, gn, bn = r / tot, g / tot, b / tot
    exg = 2 * gn - rn - bn
    gli = (2 * gn - rn - bn) / (2 * gn + rn + bn + 1e-6)
    vari = (gn - rn) / (gn + rn - bn + 1e-6)
    hr = h * 2 * np.pi / 180.0
    return np.array([np.cos(hr), np.sin(hr), s / 255.0, v / 255.0,
                     np.clip(exg, -0.3, 0.5), np.clip(gli, -1, 1),
                     np.clip(vari, -1, 1), 0.1], np.float32)


PROTO_HSV = {
    "healthy_crop": (54, 150, 120),
    "stressed_crop": (30, 110, 150),
    "disease_stress_vegetation": (5, 130, 90),
    "drought_stress": (15, 120, 170),
    "bare_soil": (20, 25, 175),
    "water": (98, 110, 140),
}


def train_model(bgr, swatches, cfg):
    """Latih satu Gaussian (mean + kovarians) per kelas dari swatch berlabel."""
    proc, hsv = preprocess(bgr, cfg)
    exg = excess_green(proc)
    gray = cv2.cvtColor(proc, cv2.COLOR_BGR2GRAY)
    tex = local_std(gray, cfg["texture_win"])
    feats = compute_features(proc, hsv, exg, tex)
    Himg, Wimg = gray.shape
    model = {"classes": [], "mean": [], "inv_cov": [], "logdet": [], "synthetic": []}

    def add(cls, mu, cov, synth):
        cov = cov + np.eye(FEAT_DIM) * 1e-3
        inv = np.linalg.inv(cov)
        _, logdet = np.linalg.slogdet(cov)
        model["classes"].append(cls)
        model["mean"].append(mu.tolist())
        model["inv_cov"].append(inv.tolist())
        model["logdet"].append(float(logdet))
        model["synthetic"].append(bool(synth))

    for cls, rects in swatches.items():
        pts = []
        for rc in rects:
            x0, y0, x1, y1 = rc
            if max(x0, y0, x1, y1) <= 1.0:
                x0, x1 = int(x0 * Wimg), int(x1 * Wimg)
                y0, y1 = int(y0 * Himg), int(y1 * Himg)
            pts.append(feats[y0:y1, x0:x1].reshape(-1, FEAT_DIM))
        X = np.concatenate(pts, 0)
        add(cls, X.mean(0), np.cov(X.T), False)

    # Kelas tanpa swatch -> prototipe warna (agar 5 kelas selalu tersedia)
    diag = np.array([.05, .05, .03, .03, .02, .02, .02, .3], np.float32)
    for cls in CLASSES:
        if cls not in model["classes"] and cls in PROTO_HSV:
            add(cls, proto_feature(*PROTO_HSV[cls]), np.diag(diag), True)
    return model


def classify_stat(bgr, hsv, exg, texture, model, cfg):
    """Klasifikasi probabilistik: jarak Mahalanobis ke tiap Gaussian kelas.
    Lebih akurat dari ambang keras karena memakai korelasi antar fitur."""
    feats = compute_features(bgr, hsv, exg, texture)
    Himg, Wimg, D = feats.shape
    X = feats.reshape(-1, D)
    names = model["classes"]
    C = len(names)
    means = np.array(model["mean"], np.float32)
    invs = np.array(model["inv_cov"], np.float32)
    logdets = np.array(model["logdet"], np.float32)
    maha = np.empty((X.shape[0], C), np.float32)
    for i in range(C):
        diff = X - means[i]
        maha[:, i] = np.einsum("nj,jk,nk->n", diff, invs[i], diff)
    score = 0.5 * maha + 0.5 * logdets[None, :]
    idx = np.argmin(score, axis=1)
    rows = np.arange(X.shape[0])
    raw_min = maha[rows, idx]
    z = -0.5 * (maha - maha.min(1, keepdims=True))
    p = np.exp(z)
    p /= p.sum(1, keepdims=True)
    conf = p[rows, idx]
    name_to_code = {n: i for i, n in enumerate(CLASSES)}
    code = np.full(X.shape[0], IGNORE["background"], np.uint8)
    for i, n in enumerate(names):
        sel = idx == i
        code[sel] = name_to_code.get(n, IGNORE["background"])
    code[raw_min > cfg.get("stat_reject_maha", 90.0)] = IGNORE["background"]
    return code.reshape(Himg, Wimg), conf.reshape(Himg, Wimg).astype(np.float32)


# ----------------------------------------------------------------------------
# KLASIFIKASI PER-PIKSEL (vektorisasi numpy)
# ----------------------------------------------------------------------------
def classify_pixels(hsv, exg, texture, cfg):
    H, S, V = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    h, w = H.shape
    label = np.full((h, w), IGNORE["unknown"], np.uint8)
    conf = np.zeros((h, w), np.float32)
    assigned = np.zeros((h, w), bool)

    def commit(mask, code, score):
        nonlocal assigned
        m = mask & ~assigned
        label[m] = code
        conf[m] = np.clip(score[m] if isinstance(score, np.ndarray) else score, 0, 1)
        assigned |= m

    # 1) Bayangan -> dibuang
    commit(V < cfg["shadow_v_max"], IGNORE["shadow"], 0.5)

    # 2) Bare soil / tanah rusak: saturasi SANGAT rendah & non-vegetasi.
    #    Dicek lebih dulu agar tanah rusak berwarna keabu/ungu pucat tidak
    #    salah terbuang sebagai background.
    cs = cfg["soil"]
    veg0 = exg > cfg["exg_veg_thr"]
    soil = (S <= cs["s_max"]) & (V >= cs["v"][0]) & (V <= cs["v"][1]) & (~veg0)
    commit(soil, 4, 0.45 + 0.3 * (1 - np.clip(S / 180.0, 0, 1)))

    # 3) Background: hue kebiruan/cyan + saturasi cukup = langit/air/sungai.
    bg = (H >= cfg["bg_h"][0]) & (H <= cfg["bg_h"][1]) & (S >= cfg["bg_s_min"])
    commit(bg, IGNORE["background"], 0.5)

    veg = exg > cfg["exg_veg_thr"]
    sat_score = np.clip(S / 180.0, 0, 1)
    exg_score = np.clip((exg + 0.05) / 0.3, 0, 1)
    tex_score = np.clip(texture / 40.0, 0, 1)

    # 3) healthy: HANYA hijau kuat (hue hijau + saturasi cukup + ExG tinggi).
    #    Tanaman "tidak terlalu hijau" sengaja TIDAK masuk sini.
    c = cfg["healthy"]
    healthy = (H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) & \
              (V >= c["v"][0]) & (exg >= cfg["exg_healthy_min"])
    commit(healthy, 0, 0.45 + 0.25 * sat_score + 0.3 * exg_score)

    # 4) disease: merah/nekrosis berbintik
    #    Syarat: HARUS merah/pinkish (hue) DAN (saturasi tinggi ATAU tekstur tinggi).
    #    Sebelumnya `redish | necrotic` memungkinkan area bertekstur tinggi apa pun
    #    (jalan, atap, tepi sawah) masuk sebagai disease — sekarang wajib merah dulu.
    c = cfg["disease"]
    redish = (((H >= c["h1"][0]) & (H <= c["h1"][1])) |
              ((H >= c["h2"][0]) & (H <= c["h2"][1]))) & (S >= c["s_lo"])
    high_tex = texture >= cfg["disease_texture_min"]   # tekstur tinggi = bercak-bercak
    disease = redish & (high_tex | (S >= 80))          # merah + (berbintik ATAU sangat pekat)
    disease = disease & (V >= c["v"][0]) & (V <= c["v"][1])
    commit(disease, 2, 0.4 + 0.6 * tex_score)

    # 5) stressed: kuning s/d hijau lemah (vegetasi tapi tidak hijau kuat)
    c = cfg["stressed"]
    stressed = (H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) & \
               (V >= c["v"][0]) & (exg >= 0.02)
    commit(stressed, 1, 0.4 + 0.4 * sat_score)

    # 6) drought: oranye/tan kering
    c = cfg["drought"]
    drought = (H >= c["h"][0]) & (H < c["h"][1]) & (S >= c["s_lo"]) & (V >= c["v"][0])
    commit(drought, 3, 0.45 + 0.35 * sat_score)

    # 7) sisa bare soil bersaturasi rendah lain
    c = cfg["soil"]
    soil2 = (S <= c["s_max"] + 6) & (V >= c["v"][0]) & (V <= c["v"][1]) & (~veg)
    commit(soil2, 4, 0.4 + 0.3 * (1 - sat_score))

    # 8) sisa -> background (TIDAK dipaksa jadi kelas apa pun)
    commit(~assigned, IGNORE["background"], 0.3)

    return label, conf


def suppress_structures(label, cfg):
    """Hapus jalan/pematang (garis tipis) & rumah (objek kecil) dari bare_soil.
    Bentuk tipis/kecil di-reklasifikasi menjadi background (tidak dideteksi).
    """
    if not cfg.get("suppress_structures", False):
        return label
    bare = (label == 4).astype(np.uint8)
    if bare.sum() == 0:
        return label
    k = cfg["road_open_kernel"] | 1
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))
    opened = cv2.morphologyEx(bare, cv2.MORPH_OPEN, kernel)
    # piksel garis tipis (jalan/pematang) yang hilang oleh opening -> background
    removed = (bare > 0) & (opened == 0)
    label[removed] = IGNORE["background"]
    # buang komponen kecil (rumah/objek) yang tersisa
    n, lab, stats, _ = cv2.connectedComponentsWithStats(opened, 8)
    min_area = int(cfg["struct_min_area_frac"] * label.size)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < min_area:
            label[lab == i] = IGNORE["background"]
    return label


def smooth_labels(label, cfg):
    k = cfg["label_median"] | 1
    sm = cv2.medianBlur(label, k)
    return sm


# ----------------------------------------------------------------------------
# EKSTRAKSI REGION & STATISTIK
# ----------------------------------------------------------------------------
def extract_regions(label, conf, cfg):
    h, w = label.shape
    min_area = int(cfg["min_region_area_frac"] * h * w)
    ksz = cfg["morph_kernel"] | 1
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ksz, ksz))
    regions = []
    for idx, name in enumerate(CLASSES):
        mask = (label == idx).astype(np.uint8)
        if mask.sum() == 0:
            continue
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        n, lab, stats, cent = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = stats[i, cv2.CC_STAT_AREA]
            if area < min_area:
                continue
            x, y, ww, hh = (stats[i, cv2.CC_STAT_LEFT], stats[i, cv2.CC_STAT_TOP],
                            stats[i, cv2.CC_STAT_WIDTH], stats[i, cv2.CC_STAT_HEIGHT])
            c = float(conf[lab == i].mean())
            regions.append({"class": name, "bbox": [int(x), int(y), int(ww), int(hh)],
                            "area": int(area), "confidence": round(c, 3),
                            "centroid": [round(float(cent[i][0]), 1),
                                         round(float(cent[i][1]), 1)]})
    regions.sort(key=lambda r: -r["area"])
    return regions


def class_distribution(label):
    total = label.size
    dist = {}
    valid = 0
    for idx, name in enumerate(CLASSES):
        c = int((label == idx).sum())
        dist[name] = c
        valid += c
    pct = {k: round(100.0 * v / max(valid, 1), 2) for k, v in dist.items()}
    field_health = 100.0 - sum(SEVERITY[k] * pct[k] for k in CLASSES)
    return pct, round(max(field_health, 0.0), 1), valid / total


def grid_analysis(label, conf, grid):
    gx, gy = grid
    h, w = label.shape
    cells = []
    for j in range(gy):
        for i in range(gx):
            y0, y1 = j * h // gy, (j + 1) * h // gy
            x0, x1 = i * w // gx, (i + 1) * w // gx
            sub = label[y0:y1, x0:x1]
            counts = {n: int((sub == k).sum()) for k, n in enumerate(CLASSES)}
            tot = sum(counts.values())
            if tot == 0:
                dom, sev = "background", 0.0
            else:
                dom = max(counts, key=counts.get)
                sev = round(sum(SEVERITY[n] * counts[n] for n in CLASSES) / tot, 3)
            cells.append({"cell": [i, j], "dominant": dom, "severity": sev})
    return {"grid": [gx, gy], "cells": cells}


# ----------------------------------------------------------------------------
# RENDER OVERLAY
# ----------------------------------------------------------------------------
def colorize(label):
    h, w = label.shape
    out = np.zeros((h, w, 3), np.uint8)
    for idx, name in enumerate(CLASSES):
        out[label == idx] = PALETTE[name]
    out[label == IGNORE["background"]] = PALETTE["background"]
    out[label == IGNORE["shadow"]] = PALETTE["shadow"]
    return out


def _demo_pct(pct):
    """Aggregate internal percentages into 4 demo display classes."""
    return {
        "Healthy": pct.get("healthy_crop", 0),
        "Stress":  pct.get("stressed_crop", 0) + pct.get("drought_stress", 0),
        "Disease": pct.get("disease_stress_vegetation", 0),
        "Pest":    0.0,   # configurable — not detected by HSV pipeline
    }


def _draw_demo_panel(out, pct):
    """Draw 4-class legend panel (DEMO_MODE — hides FHI, shows proposal classes)."""
    demo = _demo_pct(pct)
    entries = list(demo.items())
    panel_h = 18 + 18 * (len(entries) + 1)
    cv2.rectangle(out, (0, 0), (240, panel_h), (0, 0, 0), -1)
    cv2.putText(out, "Crop Health Analysis", (8, 16),
                cv2.FONT_HERSHEY_SIMPLEX, 0.45, (200, 200, 200), 1, cv2.LINE_AA)
    for i, (label, val) in enumerate(entries):
        y = 34 + i * 18
        col = DEMO_PALETTE.get(label, (255, 255, 255))
        cv2.rectangle(out, (8, y - 9), (20, y + 2), col, -1)
        suffix = " (configurable)" if label == "Pest" else f": {val:.1f}%"
        cv2.putText(out, f"{label}{suffix}", (26, y),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255, 255, 255), 1, cv2.LINE_AA)


def draw_overlay(bgr, label, regions, pct, health, alpha=0.45, draw_boxes=True):
    color = colorize(label)
    out = cv2.addWeighted(bgr, 1 - alpha, color, alpha, 0)
    if draw_boxes:
        for r in regions[:40]:
            x, y, w, h = r["bbox"]
            if DEMO_MODE:
                display = DISPLAY_MAP.get(r["class"])
                if display is None:   # bare_soil — hidden
                    continue
                col = DEMO_PALETTE.get(display, (255, 255, 255))
                txt = f"{display} {r['confidence']:.2f}"
            else:
                col = PALETTE[r["class"]]
                txt = f"{r['class']} {r['confidence']:.2f}"
            cv2.rectangle(out, (x, y), (x + w, y + h), col, 2)
            cv2.putText(out, txt, (x, max(12, y - 4)),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.45, col, 1, cv2.LINE_AA)
    if DEMO_MODE:
        _draw_demo_panel(out, pct)
    else:
        panel_w = 250
        cv2.rectangle(out, (0, 0), (panel_w, 18 + 18 * (len(CLASSES) + 1)),
                      (0, 0, 0), -1)
        cv2.putText(out, f"Field Health: {health:.1f}/100", (8, 16),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA)
        for i, name in enumerate(CLASSES):
            y = 34 + i * 18
            cv2.rectangle(out, (8, y - 9), (20, y + 2), PALETTE[name], -1)
            cv2.putText(out, f"{name}: {pct[name]:.1f}%", (26, y),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255, 255, 255), 1, cv2.LINE_AA)
    return out


def draw_yolo_style(bgr, regions, pct, health):
    """Tampilan seperti YOLO: frame asli + bounding box saja, tanpa color overlay."""
    out = bgr.copy()
    for r in regions[:40]:
        x, y, w, h = r["bbox"]
        if DEMO_MODE:
            display = DISPLAY_MAP.get(r["class"])
            if display is None:   # bare_soil — hidden
                continue
            col = DEMO_PALETTE.get(display, (255, 255, 255))
            label_txt = f"{display}  {r['confidence']:.2f}"
        else:
            col = PALETTE[r["class"]]
            label_txt = f"{r['class']}  {r['confidence']:.2f}"
        (tw, th), _ = cv2.getTextSize(label_txt, cv2.FONT_HERSHEY_SIMPLEX, 0.45, 1)
        cv2.rectangle(out, (x, y), (x + w, y + h), col, 2)
        cv2.rectangle(out, (x, max(0, y - th - 6)), (x + tw + 4, y), col, -1)
        cv2.putText(out, label_txt, (x + 2, max(th, y - 4)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.45, (255, 255, 255), 1, cv2.LINE_AA)
    if DEMO_MODE:
        _draw_demo_panel(out, pct)
    else:
        panel_w = 250
        cv2.rectangle(out, (0, 0), (panel_w, 18 + 18 * (len(CLASSES) + 1)), (0, 0, 0), -1)
        cv2.putText(out, f"Field Health: {health:.1f}/100", (8, 16),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA)
        for i, name in enumerate(CLASSES):
            ty = 34 + i * 18
            cv2.rectangle(out, (8, ty - 9), (20, ty + 2), PALETTE[name], -1)
            cv2.putText(out, f"{name}: {pct[name]:.1f}%", (26, ty),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255, 255, 255), 1, cv2.LINE_AA)
    return out


def exg_heatmap(exg):
    n = np.clip((exg + 0.2) / 0.6, 0, 1)
    return cv2.applyColorMap((n * 255).astype(np.uint8), cv2.COLORMAP_JET)


def reassign_stressed_to_healthy(label, hsv, exg, cfg):
    """Mayoritas 'stressed' -> 'healthy', KECUALI piksel kuning sejati.
    Kuning sejati = hue di rentang kuning DAN ExG rendah (sudah menguning)
    DAN saturasi cukup pekat. Sisanya (hijau-lemah/pucat) jadi healthy."""
    if not cfg.get("stressed_to_healthy", False):
        return label
    H = hsv[:, :, 0].astype(np.int16)
    S = hsv[:, :, 1].astype(np.int16)
    yh = cfg.get("yellow_h", [20, 34])
    truly_yellow = (
        (H >= yh[0]) & (H <= yh[1])
        & (exg < cfg.get("yellow_exg_max", 0.08))
        & (S >= cfg.get("yellow_s_min", 55))
    )
    out = label.copy()
    out[(label == 1) & (~truly_yellow)] = 0  # stressed bukan-kuning -> healthy
    return out


# ----------------------------------------------------------------------------
# PROSES SATU FRAME
# ----------------------------------------------------------------------------
def process_frame(bgr, cfg, grid=None, ema_state=None, engine="rule", model=None):
    proc, hsv = preprocess(bgr, cfg)
    exg = excess_green(proc)
    gray = cv2.cvtColor(proc, cv2.COLOR_BGR2GRAY)
    tex = local_std(gray, cfg["texture_win"])
    if engine == "stat" and model is not None:
        label, conf = classify_stat(proc, hsv, exg, tex, model, cfg)
        label[hsv[:, :, 2] < cfg["shadow_v_max"]] = IGNORE["shadow"]
    else:
        label, conf = classify_pixels(hsv, exg, tex, cfg)
    label = reassign_stressed_to_healthy(label, hsv, exg, cfg)
    if cfg.get("dark_as_soil"):
        # Piksel gelap -> bare_soil. Dijalankan SEBELUM suppress_structures
        # agar bayangan rumah/garis gelap tipis ikut dibersihkan sesudahnya.
        label[hsv[:, :, 2] < cfg.get("dark_v_max", 55)] = 4
    label = smooth_labels(label, cfg)
    label = suppress_structures(label, cfg)
    regions = extract_regions(label, conf, cfg)
    pct, health, cover = class_distribution(label)

    # Smoothing temporal (EMA) pada distribusi kelas (mode video)
    if ema_state is not None:
        a = cfg["ema_alpha"]
        for k in pct:
            ema_state[k] = a * pct[k] + (1 - a) * ema_state.get(k, pct[k])
        health = 100.0 - sum(SEVERITY[k] * ema_state[k] for k in CLASSES)

    result = {"class_pct": pct, "field_health": round(health, 1),
              "analyzed_coverage": round(cover, 3),
              "n_regions": len(regions), "regions": regions}
    if grid:
        result["grid"] = grid_analysis(label, conf, grid)
    overlay = draw_overlay(proc, label, regions, pct, health)
    return result, overlay, exg, label


# ----------------------------------------------------------------------------
# SUBCOMMAND: IMAGE
# ----------------------------------------------------------------------------
def load_engine(args):
    """Tentukan engine (rule/stat) + muat model statistik bila diminta."""
    engine = getattr(args, "engine", "rule")
    model = None
    mpath = getattr(args, "model", None)
    if engine == "stat":
        if not mpath or not os.path.exists(mpath):
            sys.exit("engine stat butuh --model model.json (jalankan 'train' dulu)")
        with open(mpath) as f:
            model = json.load(f)
        print(f"[engine=stat] model: {mpath} | kelas: {model['classes']}")
    return engine, model


def run_image(args):
    cfg = load_cfg(args.config)
    bgr = cv2.imread(args.input)
    if bgr is None:
        sys.exit(f"Tidak bisa membaca gambar: {args.input}")
    grid = parse_grid(args.grid)
    os.makedirs(args.output, exist_ok=True)
    engine, model = load_engine(args)
    result, overlay, exg, label = process_frame(bgr, cfg, grid, engine=engine, model=model)
    base = os.path.splitext(os.path.basename(args.input))[0]
    cv2.imwrite(os.path.join(args.output, f"{base}_overlay.jpg"), overlay)
    cv2.imwrite(os.path.join(args.output, f"{base}_exg.jpg"), exg_heatmap(exg))
    cv2.imwrite(os.path.join(args.output, f"{base}_mask.png"), colorize(label))
    with open(os.path.join(args.output, f"{base}_report.json"), "w") as f:
        json.dump(result, f, indent=2)
    print(json.dumps({"field_health": result["field_health"],
                      "class_pct": result["class_pct"],
                      "n_regions": result["n_regions"]}, indent=2))


# ----------------------------------------------------------------------------
# SUBCOMMAND: VIDEO
# ----------------------------------------------------------------------------
def run_video(args):
    cfg = load_cfg(args.config)
    grid = parse_grid(args.grid)
    os.makedirs(args.output, exist_ok=True)
    cap = cv2.VideoCapture(args.input)
    if not cap.isOpened():
        sys.exit(f"Tidak bisa membuka video: {args.input}")
    src_fps = cap.get(cv2.CAP_PROP_FPS) or 30
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    step = max(1, int(round(src_fps / max(args.fps, 0.1))))
    engine, model = load_engine(args)
    writer = None
    timeline = []
    ema = {}
    base = os.path.splitext(os.path.basename(args.input))[0]
    idx = 0
    t0 = time.time()
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        if idx % step == 0:
            if args.width and frame.shape[1] > args.width:
                h = int(frame.shape[0] * args.width / frame.shape[1])
                frame = cv2.resize(frame, (args.width, h))
            result, overlay, exg, label = process_frame(
                frame, cfg, grid, ema, engine=engine, model=model)
            t = round(idx / src_fps, 2)
            row = {"t": t, "field_health": result["field_health"]}
            row.update({k: result["class_pct"][k] for k in CLASSES})
            timeline.append(row)
            if not args.no_video:
                if writer is None:
                    fourcc = cv2.VideoWriter_fourcc(*"mp4v")
                    writer = cv2.VideoWriter(
                        os.path.join(args.output, f"{base}_annotated.mp4"),
                        fourcc, args.fps, (overlay.shape[1], overlay.shape[0]))
                writer.write(overlay)
            if not args.no_display:
                display = draw_yolo_style(frame, result["regions"], result["class_pct"], result["field_health"])
                cv2.imshow("MoonHarvest - HSV Detection", display)
                if cv2.waitKey(args.delay) & 0xFF == ord("q"):
                    print("  [dihentikan pengguna]")
                    break
            if (len(timeline) % 25) == 0:
                print(f"  processed {len(timeline)} frames (t={t}s) "
                      f"health={result['field_health']}", flush=True)
        idx += 1
    cap.release()
    if writer:
        writer.release()
    cv2.destroyAllWindows()
    # Timeline CSV
    with open(os.path.join(args.output, f"{base}_timeline.csv"), "w", newline="") as f:
        wcsv = csv.DictWriter(f, fieldnames=["t", "field_health"] + CLASSES)
        wcsv.writeheader()
        wcsv.writerows(timeline)
    # Ringkasan agregat
    agg = {k: round(float(np.mean([r[k] for r in timeline])), 2) for k in CLASSES}
    summary = {
        "video": os.path.basename(args.input),
        "frames_analyzed": len(timeline),
        "sample_fps": args.fps,
        "avg_class_pct": agg,
        "avg_field_health": round(float(np.mean([r["field_health"] for r in timeline])), 1),
        "min_field_health": round(float(np.min([r["field_health"] for r in timeline])), 1),
        "worst_classes": sorted(agg, key=agg.get, reverse=True)[:3],
        "proc_seconds": round(time.time() - t0, 1),
    }
    with open(os.path.join(args.output, f"{base}_summary.json"), "w") as f:
        json.dump(summary, f, indent=2)
    print(json.dumps(summary, indent=2))


# ----------------------------------------------------------------------------
# SUBCOMMAND: CALIBRATE  (auto-suggest threshold dari footage)
# ----------------------------------------------------------------------------
def run_train(args):
    cfg = load_cfg(args.config)
    bgr = cv2.imread(args.input)
    if bgr is None:
        sys.exit(f"Tidak bisa membaca gambar: {args.input}")
    if args.swatches:
        with open(args.swatches) as f:
            sw = json.load(f)
    else:
        sw = DEFAULT_SWATCHES
        print("[train] memakai DEFAULT_SWATCHES (frame referensi lahan padi)")
    model = train_model(bgr, sw, cfg)
    with open(args.output, "w") as f:
        json.dump(model, f)
    real = [c for c, s in zip(model["classes"], model["synthetic"]) if not s]
    synth = [c for c, s in zip(model["classes"], model["synthetic"]) if s]
    print(json.dumps({"model": args.output, "kelas_dari_swatch": real,
                      "kelas_prototipe": synth}, indent=2))


def run_calibrate(args):
    cap = cv2.VideoCapture(args.input)
    if not cap.isOpened():
        sys.exit(f"Tidak bisa membuka: {args.input}")
    fps = cap.get(cv2.CAP_PROP_FPS) or 30
    step = max(1, int(fps))  # 1 frame/detik
    samples = []
    idx = 0
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        if idx % step == 0:
            frame = cv2.resize(frame, (320, 180))
            hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV).reshape(-1, 3)
            samples.append(hsv[np.random.choice(hsv.shape[0], 2000, replace=False)])
        idx += 1
    cap.release()
    data = np.concatenate(samples).astype(np.float32)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 20, 1.0)
    _, labels, centers = cv2.kmeans(data, args.k, None, crit, 5, cv2.KMEANS_PP_CENTERS)
    counts = np.bincount(labels.flatten(), minlength=args.k)
    clusters = sorted(
        [{"center_HSV": [round(float(x), 1) for x in centers[i]],
          "fraction": round(float(counts[i] / counts.sum()), 3)} for i in range(args.k)],
        key=lambda c: -c["fraction"])
    cfg = load_cfg()
    cfg["_calibration_clusters"] = clusters
    cfg["exg_veg_thr"] = cfg["exg_veg_thr"]
    with open(args.output, "w") as f:
        json.dump(cfg, f, indent=2)
    print("Cluster warna dominan (HSV center, fraksi):")
    print(json.dumps(clusters, indent=2))
    print(f"Config tersimpan -> {args.output}")


def parse_grid(g):
    if not g:
        return None
    a, b = g.lower().split("x")
    return (int(a), int(b))


def main():
    p = argparse.ArgumentParser(description="MoonHarvest HSV crop condition detector")
    sub = p.add_subparsers(dest="cmd", required=True)

    pi = sub.add_parser("image", help="Proses satu gambar")
    pi.add_argument("-i", "--input", required=True)
    pi.add_argument("-o", "--output", default="out")
    pi.add_argument("--grid", default="8x8")
    pi.add_argument("--config", default=None)
    pi.add_argument("--engine", choices=["rule", "stat"], default="rule",
                    help="rule=ambang HSV; stat=klasifikasi Gaussian terlatih (akurat)")
    pi.add_argument("--model", default=None, help="model.json untuk engine stat")
    pi.add_argument("--demo", action="store_true",
                    help="Aktifkan DEMO_MODE: 4 kelas proposal, sembunyikan bare_soil & FHI")
    pi.set_defaults(func=run_image)

    pv = sub.add_parser("video", help="Proses video")
    pv.add_argument("-i", "--input", required=True)
    pv.add_argument("-o", "--output", default="out")
    pv.add_argument("--fps", type=float, default=2.0, help="frame/detik yang dianalisis")
    pv.add_argument("--width", type=int, default=960)
    pv.add_argument("--grid", default="8x8")
    pv.add_argument("--no-video", action="store_true")
    pv.add_argument("--no-display", action="store_true", help="Sembunyikan jendela preview")
    pv.add_argument("--delay", type=int, default=300,
                    help="Jeda antar frame di jendela preview (ms, default=300)")
    pv.add_argument("--config", default=None)
    pv.add_argument("--engine", choices=["rule", "stat"], default="rule",
                    help="rule=ambang HSV; stat=klasifikasi Gaussian terlatih (akurat)")
    pv.add_argument("--model", default=None, help="model.json untuk engine stat")
    pv.add_argument("--demo", action="store_true",
                    help="Aktifkan DEMO_MODE: 4 kelas proposal, sembunyikan bare_soil & FHI")
    pv.set_defaults(func=run_video)

    pc = sub.add_parser("calibrate", help="Auto-kalibrasi threshold dari footage")
    pc.add_argument("-i", "--input", required=True)
    pc.add_argument("-o", "--output", default="hsv_config.json")
    pc.add_argument("--k", type=int, default=6)
    pc.set_defaults(func=run_calibrate)

    pt = sub.add_parser("train", help="Latih model statistik dari swatch berlabel")
    pt.add_argument("-i", "--input", required=True, help="frame referensi berlabel")
    pt.add_argument("--swatches", default=None,
                    help="JSON {kelas: [[x0,y0,x1,y1],...]} (piksel atau fraksi 0-1)")
    pt.add_argument("-o", "--output", default="model.json")
    pt.add_argument("--config", default=None)
    pt.set_defaults(func=run_train)

    args = p.parse_args()
    if getattr(args, "demo", False):
        global DEMO_MODE
        DEMO_MODE = True
    args.func(args)


if __name__ == "__main__":
    main()
