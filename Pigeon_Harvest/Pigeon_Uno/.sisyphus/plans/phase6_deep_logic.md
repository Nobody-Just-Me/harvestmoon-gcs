# Pigeon Uno Migration - Phase 6: Deep Logic Porting

## Objective
Reach true 100% parity by porting the specific C# logic from WPF code-behind to Uno ViewModels/Services.

## Analysis of `FlightControl.xaml.cs` (WPF)
- **Vegetation Analysis**: Uses `OpenCvSharp`.
    - Logic: `VegetationAnalysis(Bitmap image)` -> Converts to HSV -> Thresholds for "Green" -> Calculates Percentage.
    - Status: **Missing in Uno**. Need to implement in `CameraService` or a helper.
- **YOLO**: Uses `YoloWrapper`.
    - Logic: `Detect(Bitmap)` -> Returns bounding boxes -> Draws on overlay.
    - Status: **Missing in Uno**. Need `OnnxRuntime` or similar if `YoloWrapper` is not portable.
- **Stream Panel**:
    - `SendSelectedCommand`: Sends specific string commands or MavLink messages.
    - `READ`: Reads from a stream? Seems to just read `in_stream` text?
    - Logic seems tied to specific custom protocol or just standard MavLink.
    - **Uno Implementation**: Currently simplified. Need to verify specific commands.

## Analysis of `Waypoint.xaml.cs` (WPF)
- **Map Interaction**:
    - Uses `GMap.NET`.
    - `MouseDoubleClick`: Adds waypoint.
    - `Geofence`: Draws polygons.
    - **Uno Implementation**: Uses `Mapsui`. Need to ensure "Add Waypoint" works similarly (Tap/Click).
    - **Mission**: `UploadMission` / `DownloadMission` uses `MavLink.SendMessage`.

## Plan
1.  **Vegetation Analysis**: Port the OpenCV logic to `Pigeon_Uno.Core`.
    - Add `OpenCvSharp` to Core.
    - Create `IVisionService` for image processing.
2.  **YOLO**: Check if we can port the detection logic easily. If strict dependency issues, maybe defer or use a mock with "Not Supported on Uno" message if models are missing.
3.  **Stream Panel**: Refine `FlightViewModel` commands to match WPF exactly (if they are MavLink CMDs).
4.  **Map Logic**: Ensure `MapViewModel` handles the list management and upload/download sequences correctly.

## Immediate Tasks
1.  **Vision Service**: Implement `VegetationAnalysis` using OpenCvSharp in Core.
2.  **Flight VM**: Integrate Vision Service to process frames from Camera.
