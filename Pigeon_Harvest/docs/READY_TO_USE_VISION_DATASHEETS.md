# Ready-To-Use Vision Datasheets For MoonHarvest

Dokumen ini berisi pilihan dataset/model vision yang paling siap digunakan untuk kebutuhan proposal MoonHarvest: YOLO crop detection, pest/disease detection, weed/crop detection, dan vegetation stress overlay.

## Rekomendasi Paling Siap

| Prioritas | Resource | Status siap pakai | Cocok untuk class proposal | Catatan |
| ---: | --- | --- | --- | --- |
| 1 | PlantDoc / Plant Disease Detection Dataset | Siap export YOLO dari Roboflow Public | `disease`, `yellowing`, `pest_damage`, `crop_stress` | Paling cepat untuk demo bounding box penyakit tanaman. |
| 2 | UAV Crop And Weed Dataset With YOLO Annotations | Siap training YOLO, berbasis citra UAV | `healthy_crop`, `weed`, `bare_soil` | Paling cocok untuk narasi UAV/agriculture dari proposal. |
| 3 | Crop And Weed Detection Data With Bounding Boxes | Siap pakai YOLO format | `weed`, `healthy_crop`, `bare_soil` | Dataset ringkas, bagus untuk validasi pipeline. |
| 4 | Agriculture 12-Class Custom Datasheet Lokal | Siap dijadikan class schema | Semua 12 class proposal | Perlu data sendiri, tapi class file sudah siap di repo. |

## 1. PlantDoc / Plant Disease Detection Dataset

Link:

- Roboflow Public: https://public.roboflow.com/object-detection/plantdoc
- Kaggle mirror: https://www.kaggle.com/datasets/mabdullahsajid/plantdoc-dataset

Kegunaan untuk MoonHarvest:

- Membuktikan YOLO bounding box pada objek/gejala tanaman.
- Cocok untuk demo `disease`, `yellowing`, `crop_stress`, dan sebagian `pest_damage`.
- Dapat diekspor ke format YOLO, lalu dilatih ulang dengan YOLOv8n dan diekspor ke ONNX.

Mapping ke proposal:

| PlantDoc type | MoonHarvest class |
| --- | --- |
| disease leaf classes | `disease` |
| yellow/spot/blight classes | `yellowing` / `crop_stress` |
| damaged leaf symptoms | `pest_damage` atau `crop_stress` |
| healthy leaf classes | `healthy_crop` |

Kelebihan:

- Dataset object detection, bukan hanya image classification.
- Cocok untuk bounding box + confidence score pada live feed.
- Cepat untuk validasi pipeline YOLO.

Kekurangan:

- Banyak data close-up daun, bukan UAV nadir.
- Untuk demo UAV field, perlu ditambah dataset drone sendiri.

## 2. UAV Crop And Weed Dataset With YOLO Annotations

Link:

- Mendeley Data: https://data.mendeley.com/datasets/vj7m6f5vzn/

Kegunaan untuk MoonHarvest:

- Dataset berbasis UAV/drone dengan anotasi YOLO.
- Cocok untuk mendukung klaim proposal bahwa sistem bekerja pada video UAV.
- Class utama biasanya crop dan weed, sehingga paling cocok untuk `healthy_crop`, `weed`, dan `bare_soil`.

Mapping ke proposal:

| Dataset class | MoonHarvest class |
| --- | --- |
| crop | `healthy_crop` |
| weed | `weed` |
| soil/background | `bare_soil` jika dilabel ulang |

Kelebihan:

- Lebih sesuai dengan sudut pandang UAV.
- Anotasi YOLO sudah tersedia.

Kekurangan:

- Tidak langsung mencakup drought/water stress.
- Perlu training model sendiri untuk ONNX.

## 3. Crop And Weed Detection Data With Bounding Boxes

Link:

- Kaggle: https://www.kaggle.com/datasets/ravirajsinh45/crop-and-weed-detection-data-with-bounding-boxes

Kegunaan untuk MoonHarvest:

- Dataset ringkas untuk deteksi crop/weed.
- Biasanya lebih mudah dipakai untuk eksperimen awal YOLO.

Mapping ke proposal:

| Dataset class | MoonHarvest class |
| --- | --- |
| crop | `healthy_crop` |
| weed | `weed` |
| soil/background | `bare_soil` jika dilabel ulang |

Kelebihan:

- Format bounding box.
- Cocok untuk cepat membuktikan training dan export ONNX.

Kekurangan:

- Bukan lengkap untuk semua gejala proposal.
- Perlu cek ulang license/format sebelum dimasukkan ke repo publik.

## 4. Dataset Lokal Yang Sudah Siap Di Repo

File class siap pakai:

```text
HarvestmoonGCS/Assets/models/vegetation.names
HarvestmoonGCS/Assets/models/classes-yolov8n-agri-basic.txt
docs/vision_templates/vegetation_4class_data.yaml
docs/vision_templates/agriculture_12class_data.yaml
```

### MVP 4 Kelas

Paling cocok untuk `VegetationYoloAnalyzer.cs`:

| ID | Class |
| ---: | --- |
| 0 | `green_healthy` |
| 1 | `yellow_stress` |
| 2 | `brown_drought` |
| 3 | `soil_crack` |

Gunakan ini jika targetnya mencoba fitur vision dalam waktu singkat.

### Full 12 Kelas Proposal

| ID | Class |
| ---: | --- |
| 0 | `healthy_crop` |
| 1 | `crop_stress` |
| 2 | `dry_soil` |
| 3 | `water_stress` |
| 4 | `pest_damage` |
| 5 | `weed` |
| 6 | `disease` |
| 7 | `yellowing` |
| 8 | `wilting` |
| 9 | `bare_soil` |
| 10 | `irrigation_channel` |
| 11 | `standing_water` |

Gunakan ini untuk narasi final proposal, tetapi perlu dataset gabungan dari beberapa sumber dan data lapangan sendiri.

## Paket Cepat Untuk Demo 2 Hari

Jika harus mencoba dalam dua hari, gunakan skenario ini:

1. Pakai model bawaan:
   ```text
   HarvestmoonGCS/Assets/models/yolov8n-320.onnx
   HarvestmoonGCS/Assets/models/classes-yolov8n-coco.txt
   ```
2. Jalankan live camera untuk membuktikan pipeline YOLO real-time.
3. Pakai HSV/vegetation overlay untuk membuktikan health map.
4. Untuk datasheet proposal, pakai PlantDoc + UAV Crop/Weed sebagai rujukan dataset siap pakai.
5. Setelah demo, training model custom:
   - `yolov8n-vegetation-320.onnx` untuk MVP 4 kelas, atau
   - `yolov8n-agri-320.onnx` untuk 12 kelas proposal.

## Perintah Training/Export

Contoh training YOLOv8n:

```bash
yolo detect train model=yolov8n.pt data=docs/vision_templates/vegetation_4class_data.yaml imgsz=640 epochs=100
```

Export ONNX desktop:

```bash
yolo export model=runs/detect/train/weights/best.pt format=onnx imgsz=640
```

Export ONNX Android/tablet:

```bash
yolo export model=runs/detect/train/weights/best.pt format=onnx imgsz=320
```

Taruh hasil export di:

```text
HarvestmoonGCS/Assets/models/yolov8n-vegetation-320.onnx
```

atau:

```text
HarvestmoonGCS/Assets/models/yolov8n-agri-320.onnx
```

## Pilihan Akhir

Untuk kebutuhan kamu sekarang, pilihan paling realistis:

1. **Demo cepat:** pakai YOLO bawaan + vegetation HSV overlay.
2. **Dataset siap pakai untuk proposal:** pakai PlantDoc untuk disease/stress dan UAV Crop/Weed untuk drone crop/weed.
3. **Model final:** gabungkan dataset siap pakai dengan data lapangan sendiri agar class `dry_soil`, `water_stress`, `soil_crack`, dan `standing_water` benar-benar sesuai kondisi lahan target.
