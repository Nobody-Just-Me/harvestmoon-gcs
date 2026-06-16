"""
Data-driven HSV calibration from the user's UAV footage.
Samples frames, separates vegetation vs soil using ExG, then analyzes the
Hue/Sat/Val distribution to propose HSV ranges for the 5 crop classes.
"""
import cv2, glob, json, numpy as np

FRAMES = sorted(glob.glob("/data/frames/f_*.jpg"))
STEP = 2  # use every 2nd frame for speed
frames = FRAMES[::STEP]
print(f"Analyzing {len(frames)} frames")

hsv_all = []          # all pixels (downsampled)
exg_all = []
for fp in frames:
    img = cv2.imread(fp)
    if img is None:
        continue
    img = cv2.resize(img, (320, 180))
    b, g, r = cv2.split(img.astype(np.float32))
    # Excess Green index (vegetation indicator), normalized
    s = (r + g + b) + 1e-6
    rn, gn, bn = r / s, g / s, b / s
    exg = 2 * gn - rn - bn
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    hsv_all.append(hsv.reshape(-1, 3))
    exg_all.append(exg.reshape(-1))

hsv_all = np.concatenate(hsv_all, axis=0)
exg_all = np.concatenate(exg_all, axis=0)
H, S, V = hsv_all[:, 0], hsv_all[:, 1], hsv_all[:, 2]

def pct(a, ps):
    return {f"p{p}": float(np.percentile(a, p)) for p in ps}

report = {}
report["n_pixels"] = int(hsv_all.shape[0])
report["global"] = {
    "H": pct(H, [5, 25, 50, 75, 95]),
    "S": pct(S, [5, 25, 50, 75, 95]),
    "V": pct(V, [5, 25, 50, 75, 95]),
    "ExG": pct(exg_all, [5, 25, 50, 75, 95]),
}

# Vegetation mask: high ExG. Soil/bare: low ExG.
exg_thr = float(np.percentile(exg_all, 55))
veg = exg_all > exg_thr
soil = ~veg
report["exg_threshold"] = exg_thr
report["veg_fraction"] = float(veg.mean())

# Hue histogram within vegetation to find dominant color bands
veg_H = H[veg]
hist, edges = np.histogram(veg_H, bins=36, range=(0, 180))
report["veg_hue_hist"] = {
    "counts": hist.tolist(),
    "bin_width_deg(opencv)": 5,
}

# KMeans clustering on Hue+Sat+Val of vegetation pixels (OpenCV kmeans)
sample = hsv_all[np.random.choice(hsv_all.shape[0], min(60000, hsv_all.shape[0]), replace=False)].astype(np.float32)
K = 5
criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 20, 1.0)
_, labels, centers = cv2.kmeans(sample, K, None, criteria, 5, cv2.KMEANS_PP_CENTERS)
counts = np.bincount(labels.flatten(), minlength=K)
clusters = []
for i in range(K):
    clusters.append({
        "center_HSV": [round(float(c), 1) for c in centers[i]],
        "fraction": float(counts[i] / counts.sum()),
    })
clusters.sort(key=lambda c: -c["fraction"])
report["kmeans_clusters"] = clusters

# Soil color stats (for bare_soil class)
report["soil"] = {
    "H": pct(H[soil], [25, 50, 75]),
    "S": pct(S[soil], [25, 50, 75]),
    "V": pct(V[soil], [25, 50, 75]),
}

with open("/data/hsv_calibration.json", "w") as f:
    json.dump(report, f, indent=2)
print(json.dumps(report, indent=2))
