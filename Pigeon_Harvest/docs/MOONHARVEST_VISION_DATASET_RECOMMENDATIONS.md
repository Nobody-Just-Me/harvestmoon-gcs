# MoonHarvest Vision Dataset Recommendations

Dokumen ini merangkum dataset vision yang cocok untuk MoonHarvest, khususnya untuk target kelas:

- `healthy_crop`
- `stressed_crop`
- `drought_stress`
- `bare_soil`
- `disease_stress_vegetation`

MoonHarvest sebaiknya memakai kombinasi dua jenis model:

- **Classification** untuk menilai kondisi area/lahan.
- **Detection/YOLO** untuk hasil box kecil-kecil yang lebih presisi.

## Dataset Paling Disarankan

| Dataset | Cocok Untuk | Catatan |
|---|---|---|
| [Agricultural Water Stress Image Dataset](https://www.kaggle.com/datasets/zoya77/agricultural-water-stress-image-dataset) | `drought_stress`, `stressed_crop` | Penting untuk stress air/kekeringan pada maize, rice, sorghum, wheat. |
| [Healthy vs. Stressed Lettuce Image Dataset](https://www.kaggle.com/datasets/ashimstha/lettuce-health-compiled-dataset) | `healthy_crop`, `stressed_crop` | Bagus untuk training awal health classification. |
| [New Plant Diseases Dataset](https://www.kaggle.com/datasets/vipoooool/new-plant-diseases-dataset) | `healthy_crop`, `disease_stress_vegetation` | Dataset besar, sekitar 87K gambar RGB, 38 kelas healthy/diseased. |
| [PlantVillage Dataset](https://www.kaggle.com/datasets/emmarex/plantdisease) | `healthy_crop`, `disease_stress_vegetation` | Bagus sebagai tambahan data penyakit daun. |
| [UAV-Based Agricultural Image Dataset](https://www.kaggle.com/datasets/ziya07/uav-based-agricultural-image-dataset) | `bare_soil`, crop area dari drone | Sangat berguna untuk mengganti proxy `bare_soil`. |
| [Soil Image Dataset](https://www.kaggle.com/datasets/jayaprakashpondy/soil-image-dataset) | `bare_soil` | Untuk klasifikasi tanah/tanah kosong. |
| [Comprehensive Soil Classification Datasets](https://www.kaggle.com/datasets/ai4a-lab/comprehensive-soil-classification-datasets) | `bare_soil` | Ada alluvial, black, laterite, red, yellow, arid, mountain soil. |
| [PlantVillage for Object Detection YOLO](https://www.kaggle.com/datasets/sebastianpalaciob/plantvillage-for-object-detection-yolo) | disease detection | Penting untuk deteksi kecil-kecil dengan bounding box. |

## Dataset Tambahan yang Bagus

| Dataset | Cocok Untuk | Catatan |
|---|---|---|
| [PlantDoc](https://www.kaggle.com/datasets/andresmgs/plantdec) | `healthy_crop`, `disease_stress_vegetation` | Bisa classification dan detection. |
| [Lettuce Diseases](https://www.kaggle.com/datasets/ashishjstar/lettuce-diseases) | lettuce disease/stress vegetation | Tambahan untuk penyakit lettuce. |
| [Lettuce plant Disease Dataset](https://www.kaggle.com/datasets/santoshshaha/lettuce-plant-disease-dataset) | `disease_stress_vegetation` | Tambahan khusus lettuce. |
| [20k+ Multi-Class Crop Disease Images](https://www.kaggle.com/datasets/jawadali1045/20k-multi-class-crop-disease-images) | `disease_stress_vegetation` | Dataset penyakit crop multi-class. |
| [Leaves Healthy or Diseased](https://www.kaggle.com/datasets/prasanshasatpathy/leaves-healthy-or-diseased) | `healthy_crop`, `disease_stress_vegetation` | Ringan untuk baseline. |
| [EarlyNSD: Early Nutrient Stress Detection of Plants](https://www.kaggle.com/datasets/raiaone/early-nutrient-stress-detection-of-plants) | `stressed_crop` | Cocok untuk nutrient stress. |
| [Minimal Dataset for Multimodal Deep Learning](https://www.kaggle.com/datasets/jianbinyao/minimum-dataset) | drought/wheat stress | Tambahan untuk drought stress. |
| [Wheat Water Stress Detection Dataset](https://data.mendeley.com/datasets/ybjs4ppyzf) | `drought_stress` | Wheat water stress. |
| [Soil Types](https://www.kaggle.com/datasets/prasanshasatpathy/soil-types) | `bare_soil` | Ringan untuk tanah. |
| [Soil Classification Dataset](https://www.kaggle.com/datasets/mansijain14/soil-classification-dataset) | `bare_soil` | Tambahan soil classification. |

## Dataset untuk Bounding Box / Deteksi Kecil-Kecil

Kalau targetnya ingin hasil seperti YOLO box kecil-kecil, prioritaskan dataset yang punya anotasi bounding box.

| Dataset | Cocok Untuk | Catatan |
|---|---|---|
| [CropWeeds-YOLO Dataset](https://www.kaggle.com/datasets/swish9/weeds-detection) | YOLO crop/weed | Dataset YOLO untuk crop/weed. |
| [Crop and Weed Detection Data with Bounding Boxes](https://www.kaggle.com/datasets/ravirajsinh45/crop-and-weed-detection-data-with-bounding-boxes) | YOLO crop/weed | Sudah punya bounding box. |
| [WeedCrop Image Dataset](https://www.kaggle.com/datasets/vinayakshanawad/weedcrop-image-dataset) | YOLO weed/crop | Format YOLOv5. |
| [Tomato Leaf Diseases Dataset for Object Detection](https://www.kaggle.com/datasets/sebastianpalaciob/tomato-leaf-diseases-dataset-for-object-detection) | disease detection | Dataset object detection penyakit daun. |
| [Tomato Leaf Disease Detection YOLOv8 Dataset](https://www.kaggle.com/datasets/vasantharank/tomato-leaf-disease-detection-yolov8-dataset) | disease detection | Format YOLOv8. |
| [Lincoln Beet](https://www.kaggle.com/datasets/amiranmkrtchyan/amiran) | weed + sugar beet detection | Object detection untuk crop/weed. |
| [Cotton-Weed-12-Class](https://www.kaggle.com/datasets/jawadulkarim117/cotton-weed-12-class) | weed detection multi-class | Berguna untuk deteksi gulma. |

## Filter Dataset untuk Video UAV di Atas 60 m

Video dari ketinggian 60-80 m memiliki ciri:

- tanaman terlihat kecil,
- objek individual sulit terlihat,
- rumah, jalan, air, dan bayangan sering masuk frame,
- dataset close-up daun sering tidak cocok,
- dataset drone/aerial/top-down jauh lebih penting.

### Paling Cocok untuk UAV 80 m

| Prioritas | Dataset | Cocok Untuk | Alasan |
|---:|---|---|---|
| 1 | [UAV-Based Agricultural Image Dataset](https://www.kaggle.com/datasets/ziya07/uav-based-agricultural-image-dataset) | `healthy_crop`, `bare_soil`, crop area | Salah satu yang paling dekat dengan video UAV. |
| 2 | [Drone Camera Image Dataset of Agriculture Fields](https://www.kaggle.com/datasets/suhelahamed/drone-camera-image-dataset-of-agriculture-fields) | aerial agriculture, field monitoring | Bagus agar model tidak salah membaca rumah, jalan, air. |
| 3 | [Annotated UAV Image Dataset for Object Detection](https://data.mendeley.com/datasets/fwg6pt6ckd/1) | YOLO crop/weed | Format YOLO `.txt`, lebih cocok untuk bounding box. |
| 4 | [UAV Weed Dataset 25](https://www.kaggle.com/datasets/datasetengineer/uav-weed-dataset-25) | crop/weed dari UAV | Mendukung deteksi kecil-kecil dari udara. |
| 5 | [UAV Image Dataset for Cotton Crops and Weeds](https://pmc.ncbi.nlm.nih.gov/articles/PMC12887649/) | crop/weed detection | Bagus kalau targetnya box kecil-kecil. |
| 6 | [Vegetation-Density Drone Dataset for Peatland](https://data.mendeley.com/datasets/tb26zy2jst) | `bare_soil`, vegetation density | Ada bare dan vegetasi, cocok untuk lahan aerial. |
| 7 | [Crop Health and Environmental Stress Dataset](https://www.kaggle.com/datasets/datasetengineer/crop-health-and-environmental-stress-dataset) | `stressed_crop`, environmental stress | Perlu cek apakah image cukup visual/aerial. |
| 8 | [Agricultural Water Stress Image Dataset](https://www.kaggle.com/datasets/zoya77/agricultural-water-stress-image-dataset) | `drought_stress` | Tetap berguna, tapi bukan paling ideal jika gambarnya close-up. |
| 9 | [UAV-Based Multispectral Paddy Dataset](https://pmc.ncbi.nlm.nih.gov/articles/PMC11101735/) | paddy crop health, nitrogen/stress | Bagus kalau nanti memakai NDVI/multispectral. |
| 10 | [A UAV-Based Multispectral and RGB Dataset for Multi-Stage Paddy Crop Monitoring](https://arxiv.org/html/2601.01084v1) | RGB + multispectral paddy | Sangat mendukung konsep edge AI/drone agriculture. |

## Mapping untuk Kelas MoonHarvest

### `healthy_crop`

- UAV-Based Agricultural Image Dataset
- Drone Camera Image Dataset of Agriculture Fields
- UAV-Based Multispectral/RGB Paddy Dataset
- New Plant Diseases Dataset sebagai tambahan non-aerial
- PlantVillage sebagai tambahan non-aerial

### `stressed_crop`

- Crop Health and Environmental Stress Dataset
- Agricultural Water Stress Image Dataset
- UAV multispectral paddy/nitrogen dataset
- Healthy vs. Stressed Lettuce sebagai tambahan non-aerial
- EarlyNSD sebagai tambahan non-aerial

### `drought_stress`

- Agricultural Water Stress Image Dataset
- UAV crop water stress / multispectral datasets
- Wheat Water Stress Detection Dataset
- Minimal Dataset for Multimodal Deep Learning

### `bare_soil`

- UAV-Based Agricultural Image Dataset
- Vegetation-Density Drone Dataset
- Drone Camera Image Dataset of Agriculture Fields
- Soil Image Dataset sebagai tambahan
- Comprehensive Soil Classification sebagai tambahan
- Soil Types sebagai tambahan ringan

### `disease_stress_vegetation`

- UAV multispectral paddy dataset
- Crop Health and Environmental Stress Dataset
- PlantVillage for Object Detection YOLO
- PlantDoc
- New Plant Diseases Dataset
- PlantVillage
- Lettuce Diseases
- Tomato Leaf Disease Detection YOLOv8 Dataset

## Prioritas Download untuk MoonHarvest

### Jika Fokus Classification 5 Kelas

1. Agricultural Water Stress Image Dataset
2. Healthy vs. Stressed Lettuce Image Dataset
3. UAV-Based Agricultural Image Dataset
4. New Plant Diseases Dataset
5. Soil Image Dataset atau Comprehensive Soil Classification
6. PlantDoc
7. Crop Health and Environmental Stress Dataset

### Jika Fokus Video UAV di Atas 60 m

1. UAV-Based Agricultural Image Dataset
2. Drone Camera Image Dataset of Agriculture Fields
3. Annotated UAV Image Dataset for Object Detection
4. UAV Weed Dataset 25
5. Vegetation-Density Drone Dataset
6. UAV cotton crop/weed detection dataset
7. Agricultural Water Stress Image Dataset
8. UAV multispectral/RGB paddy dataset

### Jika Fokus Bounding Box Kecil-Kecil

1. Annotated UAV Image Dataset for Object Detection
2. CropWeeds-YOLO Dataset
3. Crop and Weed Detection Data with Bounding Boxes
4. WeedCrop Image Dataset
5. PlantVillage for Object Detection YOLO
6. Tomato Leaf Disease Detection YOLOv8 Dataset
7. Cotton-Weed-12-Class

## Rekomendasi Praktis

Untuk video UAV 60-80 m, jangan terlalu bergantung pada dataset close-up daun. Dataset seperti PlantVillage, Lettuce, dan tomato leaf tetap berguna untuk mengenali penyakit/stress secara visual, tetapi bukan sumber utama untuk video drone tinggi.

Prioritas utama harus dataset:

- drone/aerial/top-down,
- punya variasi non-pertanian seperti rumah, jalan, air, tanah,
- punya kelas bare/vegetation,
- punya anotasi YOLO jika ingin bounding box kecil-kecil.

Kesimpulan:

- **Classification 5 kelas**: pakai dataset health/stress/soil untuk menilai area.
- **Detection kecil-kecil**: pakai dataset YOLO bounding box dari UAV.
- **Video 60-80 m**: prioritaskan dataset UAV/aerial, bukan close-up daun.
