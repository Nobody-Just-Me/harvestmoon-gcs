# Accelerometer Calibration & Map Vehicle Icons

## TL;DR

> **Quick Summary**: Replace direction arrow images in accelerometer calibration with vehicle icons (plane/quadcopter), and update map vehicle marker to show appropriate icon based on vehicle type detected from MAVLink HEARTBEAT message.
> 
> **Deliverables**: 
> - 6 accelerometer calibration positions showing rotated vehicle icons
> - Map vehicle marker displaying plane/quadcopter icon based on MAV_TYPE
> - Vehicle type detection from HEARTBEAT message
>
> **Estimated Effort**: Medium
> **Parallel Execution**: YES - 3 waves
> **Critical Path**: TelemetryParser → SkiaMapControl → CalibrationControl

---

## Context

### Original Request
User ingin:
1. Menu kalibrasi accelerometer dengan icon pesawat untuk setiap posisi
2. Icon wahana di maps yang menunjukkan lokasi drone/pesawat
3. Icon berubah berdasarkan tipe wahana (plane/quadcopter) dari HEARTBEAT

### Interview Summary
**Key Discussions**:
- Vehicle type detection: Otomatis dari HEARTBEAT message
- Icon selection: Plane → `ikon-wahana-pesawat-1.png`, Quadcopter → `ikon-quadcopter.png`
- Rotation: Menggunakan RenderTransform di XAML untuk orientasi berbeda
- Fallback: Semua multirotor types (hexarotor, octorotor, dll) menggunakan quadcopter icon

**Research Findings**:
- Framework: **WinUI 3 / Windows App SDK** (bukan WPF)
- `FlightData.Type` property sudah ada (line 369 CoreModels.cs) - tinggal di-populate
- `MavType` enum sudah ada di GeneratedMessages.cs (FixedWing=1, Quadrotor=2)
- `AvionicsImageLoader` pattern sudah ada untuk load SKBitmap
- Asset URI: `ms-appx:///Assets/icons/...`

### Metis Review
**Identified Gaps** (addressed):
- Framework reference: Dikoreksi ke WinUI 3
- Fallback strategy: Didefinisikan (plane untuk unknown, quadcopter untuk semua multirotor)
- Multirotor types: Hexarotor, Octorotor, dll → fallback ke quadcopter icon
- Default behavior: Plane icon jika HEARTBEAT belum diterima

---

## Work Objectives

### Core Objective
Menambahkan visual yang lebih informatif untuk menunjukkan orientasi dan tipe wahana di UI calibration dan map.

### Concrete Deliverables
- `TelemetryParser.cs` - Extract vehicle type dari HEARTBEAT
- `SkiaMapControl.xaml.cs` - Draw vehicle marker dengan PNG icon
- `CalibrationControl.xaml` - 6 position images dengan vehicle icon + rotation
- `CalibrationControl.xaml.cs` - Handler untuk vehicle type change

### Definition of Done
- [ ] Accelerometer calibration menampilkan vehicle icon yang di-rotate sesuai orientasi
- [ ] Map menampilkan vehicle icon sesuai tipe dari HEARTBEAT
- [ ] FlightData.Type di-populate dari HEARTBEAT message
- [ ] Fallback icon bekerja untuk semua multirotor types
- [ ] Default icon ditampilkan sebelum HEARTBEAT diterima

### Must Have
- Vehicle icon di 6 posisi accelerometer calibration
- Vehicle icon di map marker
- Vehicle type detection dari HEARTBEAT
- Rotation correct untuk setiap posisi (FLIP_UP=0°, KIRI=90°CCW, KANAN=90°CW, DOWN=180°, UP=0°, FLIP_DOWN=180°)

### Must NOT Have (Guardrails)
- JANGAN buat icon baru (gunakan yang sudah ada)
- JANGAN tambah icon selection UI/settings
- JANGAN handle VTOL mode transitions
- JANGAN modifikasi calibration logic/algoritma
- JANGAN buat asset loading infrastructure baru (reuse AvionicsImageLoader pattern)

---

## Verification Strategy (MANDATORY)

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: NO
- **Automated tests**: NO (Agent-Executed QA only)
- **Framework**: None
- **QA Policy**: Every task includes agent-executed QA scenarios

### QA Policy
Every task MUST include agent-executed QA scenarios.
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **Frontend/UI**: Use Playwright (playwright skill) — Navigate, interact, assert DOM, screenshot
- **Backend/Parser**: Use Bash (curl/debug) — Send MAVLink message, verify parsed data
- **Integration**: Use Bash — Run app, verify UI updates on simulated HEARTBEAT

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — foundation):
├── Task 1: Add VehicleType property usage to FlightData [quick]
├── Task 2: Parse vehicle type from HEARTBEAT [quick]
└── Task 3: Add icon loading helper for SkiaSharp [quick]

Wave 2 (After Wave 1 — core features):
├── Task 4: Update SkiaMapControl vehicle marker [unspecified-high]
├── Task 5: Update CalibrationControl vehicle icons [visual-engineering]
└── Task 6: Add vehicle type change handler [quick]

Wave 3 (After Wave 2 — integration + verification):
├── Task 7: Integration testing [unspecified-high]
└── Task 8: Visual verification via Playwright [visual-engineering]

Critical Path: Task 2 → Task 4 → Task 5 → Task 7
Parallel Speedup: ~40% faster than sequential
```

### Dependency Matrix

| Task | Depends On | Blocks |
|------|------------|--------|
| 1 | — | 2, 4, 5, 6 |
| 2 | 1 | 4, 6 |
| 3 | — | 4 |
| 4 | 2, 3 | 7, 8 |
| 5 | 1 | 7, 8 |
| 6 | 2 | 5 |
| 7 | 4, 5 | 8 |
| 8 | 7 | — |

### Agent Dispatch Summary

- **Wave 1**: 3 tasks → `quick` (x3)
- **Wave 2**: 3 tasks → `unspecified-high`, `visual-engineering`, `quick`
- **Wave 3**: 2 tasks → `unspecified-high`, `visual-engineering`

---

## TODOs

- [x] 1. **Prepare VehicleType Property in FlightData** ✓ COMPLETED

  **What to do**:
  - Review existing `FlightData.Type` property in CoreModels.cs
  - Add clear semantics: rename to `VehicleType` or add documentation for `Type`
  - Add property changed notification if not present
  - Default value: `MavType.FixedWing` (plane as default)

  **Must NOT do**:
  - Do NOT add new property if Type already exists with correct semantics
  - Do NOT change existing telemetry logic

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Simple property verification/documentation
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2, 3)
  - **Blocks**: Tasks 2, 4, 5, 6
  - **Blocked By**: None

  **References**:
  - `Pigeon_Uno.Core/Models/CoreModels.cs:369` - FlightData.Type property (existing)
  - `Pigeon_Uno/Services/MavLink/GeneratedMessages.cs:81` - MavType enum definition

  **Acceptance Criteria**:
  - [ ] FlightData has VehicleType property (or Type with clear semantics)
  - [ ] Default value is MavType.FixedWing
  - [ ] Property is accessible from ViewModels

  **QA Scenarios**:
  ```
  Scenario: VehicleType property exists and has correct default
    Tool: Bash (grep)
    Preconditions: Project compiles successfully
    Steps:
      1. grep -n "VehicleType\|public.*Type" Pigeon_Uno.Core/Models/CoreModels.cs
      2. Verify property definition exists
      3. grep -n "MavType.FixedWing" Pigeon_Uno.Core/Models/CoreModels.cs
      4. Verify default value assignment
    Expected Result: Property found with default FixedWing
    Failure Indicators: Property not found, wrong default
    Evidence: .sisyphus/evidence/task-1-property-check.txt
  ```

  **Commit**: NO (groups with Task 2)

---

- [x] 2. **Parse Vehicle Type from HEARTBEAT Message** ✓ COMPLETED

  **What to do**:
  - Modify `TelemetryParser.ParseHeartbeat()` to extract `heartbeat.Type`
  - Map MavType enum value to FlightData.Type/VehicleType
  - Add debug logging for vehicle type changes
  - Handle edge case: type=0 (GENERIC) → use default

  **Must NOT do**:
  - Do NOT modify flight mode parsing
  - Do NOT add vehicle type events (out of scope)
  - Do NOT change other HEARTBEAT parsing logic

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Small modification to existing parser
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3)
  - **Blocks**: Tasks 4, 6
  - **Blocked By**: Task 1 (needs VehicleType property)

  **References**:
  - `Pigeon_Uno/Services/MavLink/TelemetryParser.cs:84-96` - ParseHeartbeat method
  - `Pigeon_Uno/Services/MavLink/GeneratedMessages.cs:81` - MavType enum (FixedWing=1, Quadrotor=2, Hexarotor=13, etc.)
  - Pattern: Extract enum value from heartbeat message property

  **Acceptance Criteria**:
  - [ ] TelemetryParser.ParseHeartbeat extracts heartbeat.Type
  - [ ] FlightData.VehicleType is set from HEARTBEAT
  - [ ] Debug log shows vehicle type on heartbeat
  - [ ] GENERIC (type=0) handled gracefully

  **QA Scenarios**:
  ```
  Scenario: Parse HEARTBEAT with FIXED_WING type
    Tool: Bash (debug log verification)
    Preconditions: App running with debug logging enabled
    Steps:
      1. Simulate HEARTBEAT message with type=1 (FIXED_WING)
      2. Check debug output for "VehicleType=1" or "FixedWing"
    Expected Result: FlightData.VehicleType = 1 (FixedWing)
    Failure Indicators: Type not logged, property not set
    Evidence: .sisyphus/evidence/task-2-fixedwing-parse.txt

  Scenario: Parse HEARTBEAT with QUADROTOR type
    Tool: Bash (debug log verification)
    Preconditions: App running with debug logging enabled
    Steps:
      1. Simulate HEARTBEAT message with type=2 (QUADROTOR)
      2. Check debug output for "VehicleType=2" or "Quadrotor"
    Expected Result: FlightData.VehicleType = 2 (Quadrotor)
    Failure Indicators: Type not logged, wrong value
    Evidence: .sisyphus/evidence/task-2-quadrotor-parse.txt

  Scenario: Parse HEARTBEAT with HEXAROTOR type (fallback test)
    Tool: Bash (debug log verification)
    Preconditions: App running
    Steps:
      1. Simulate HEARTBEAT message with type=13 (HEXAROTOR)
      2. Check FlightData.VehicleType = 13
    Expected Result: Property set to 13 (Hexarotor)
    Failure Indicators: Type ignored, error thrown
    Evidence: .sisyphus/evidence/task-2-hexarotor-parse.txt
  ```

  **Commit**: YES
  - Message: `feat(telemetry): extract vehicle type from HEARTBEAT message`
  - Files: `Pigeon_Uno/Services/MavLink/TelemetryParser.cs`, `Pigeon_Uno.Core/Models/CoreModels.cs`
  - Pre-commit: Build succeeds

---

- [x] 3. **Add Icon Loading Helper for SkiaSharp** ✓ COMPLETED

  **What to do**:
  - Create helper method to load PNG icon into SKBitmap
  - Follow `AvionicsImageLoader.LoadBitmapAsync()` pattern
  - Support loading from `ms-appx:///Assets/icons/` URI
  - Add caching for loaded icons (avoid repeated disk reads)

  **Must NOT do**:
  - Do NOT create new infrastructure if AvionicsImageLoader can be reused
  - Do NOT modify existing AvionicsImageLoader

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Simple helper method following existing pattern
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2)
  - **Blocks**: Task 4
  - **Blocked By**: None

  **References**:
  - `Pigeon_Uno/Controls/Avionics/AvionicsImageLoader.cs` - Pattern for loading SKBitmap
  - Pattern: Use `StorageFile.GetFileFromApplicationUriAsync()` then `SKBitmap.Decode(stream)`
  - Asset path: `ms-appx:///Assets/icons/`

  **Acceptance Criteria**:
  - [ ] Helper method loads PNG into SKBitmap
  - [ ] Method works with ms-appx:// URI scheme
  - [ ] Caching prevents repeated loads

  **QA Scenarios**:
  ```
  Scenario: Load plane icon successfully
    Tool: Bash (debug log)
    Preconditions: Icon file exists at Assets/icons/ikon-wahana-pesawat-1.png
    Steps:
      1. Call helper with "ms-appx:///Assets/icons/ikon-wahana-pesawat-1.png"
      2. Verify SKBitmap is not null
      3. Verify bitmap dimensions > 0
    Expected Result: Valid SKBitmap returned
    Failure Indicators: Null bitmap, exception thrown
    Evidence: .sisyphus/evidence/task-3-icon-load.txt

  Scenario: Load quadcopter icon successfully
    Tool: Bash (debug log)
    Preconditions: Icon file exists at Assets/icons/ikon-quadcopter.png
    Steps:
      1. Call helper with "ms-appx:///Assets/icons/ikon-quadcopter.png"
      2. Verify SKBitmap is not null
    Expected Result: Valid SKBitmap returned
    Failure Indicators: Null bitmap, file not found
    Evidence: .sisyphus/evidence/task-3-quad-icon-load.txt
  ```

  **Commit**: NO (groups with Task 4)

---

- [x] 4. **Update SkiaMapControl Vehicle Marker** ✓ COMPLETED

  **What to do**:
  - Modify `DrawVehicleMarker()` to use PNG icon instead of unicode "✈"
  - Add field for cached SKBitmap of vehicle icon
  - Add method `LoadVehicleIconAsync()` called on startup and vehicle type change
  - Icon selection logic:
    - MavType.FixedWing (1) → ikon-wahana-pesawat-1.png
    - MavType.Quadrotor (2), Hexarotor (13), Octorotor (14), Tricopter (15), Dodecarotor (29), GenericMultirotor (43) → ikon-quadcopter.png
    - Default/Unknown (0 or others) → ikon-wahana-pesawat-1.png
  - Rotate icon based on vehicle heading (yaw)
  - Draw icon at vehicle position with correct size

  **Must NOT do**:
  - Do NOT add icon size settings
  - Do NOT add icon customization UI
  - Do NOT modify other map rendering logic

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Requires SkiaSharp drawing code and async icon loading
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 2 (sequential with Task 5)
  - **Blocks**: Tasks 7, 8
  - **Blocked By**: Tasks 2, 3

  **References**:
  - `Pigeon_Uno/Controls/SkiaMapControl.xaml.cs:520-549` - DrawVehicleMarker method
  - `Pigeon_Uno/Controls/Avionics/AvionicsImageLoader.cs` - Pattern for loading SKBitmap
  - `Pigeon_Uno/Assets/icons/` - Available icon files

  **Acceptance Criteria**:
  - [ ] DrawVehicleMarker loads and draws PNG icon
  - [ ] Icon rotates based on heading
  - [ ] Plane icon shown for FixedWing type
  - [ ] Quadcopter icon shown for multirotor types
  - [ ] Default plane icon for unknown types

  **QA Scenarios**:
  ```
  Scenario: Vehicle marker shows plane icon for FIXED_WING
    Tool: Bash (visual verification via screenshot)
    Preconditions: App running, map visible, VehicleType=FixedWing
    Steps:
      1. Set FlightData.VehicleType = MavType.FixedWing (1)
      2. Set vehicle position to visible coordinates
      3. Trigger map render
      4. Take screenshot of map area
      5. Verify plane icon (not unicode character) is drawn
    Expected Result: Plane icon visible at vehicle position
    Failure Indicators: Unicode "✈" still visible, no marker
    Evidence: .sisyphus/evidence/task-4-plane-marker.png

  Scenario: Vehicle marker shows quadcopter icon for QUADROTOR
    Tool: Bash (visual verification)
    Preconditions: App running, VehicleType=Quadrotor
    Steps:
      1. Set FlightData.VehicleType = MavType.Quadrotor (2)
      2. Trigger map render
      3. Take screenshot
      4. Verify quadcopter icon is drawn
    Expected Result: Quadcopter icon visible
    Failure Indicators: Plane icon shown, unicode shown
    Evidence: .sisyphus/evidence/task-4-quad-marker.png

  Scenario: Icon rotates with heading
    Tool: Bash (visual verification)
    Preconditions: App running, vehicle visible on map
    Steps:
      1. Set heading to 0° (North)
      2. Take screenshot
      3. Set heading to 90° (East)
      4. Take screenshot
      5. Compare icon rotation between screenshots
    Expected Result: Icon rotated 90° clockwise in second screenshot
    Failure Indicators: Icon rotation unchanged
    Evidence: .sisyphus/evidence/task-4-rotation.png
  ```

  **Commit**: YES
  - Message: `feat(map): display vehicle icon based on MAV_TYPE`
  - Files: `Pigeon_Uno/Controls/SkiaMapControl.xaml.cs`
  - Pre-commit: Build succeeds, map renders correctly

---

- [x] 5. **Update CalibrationControl Vehicle Icons** ✓ COMPLETED

  **What to do**:
  - Replace Image sources in 6 Border controls with vehicle icon
  - Add RenderTransform to rotate icon based on position:
    - Border1 (FLIP_UP): 0° rotation
    - Border2 (KIRI/LEFT): -90° (270°) rotation
    - Border3 (KANAN/RIGHT): 90° rotation
    - Border4 (DOWN): 180° rotation
    - Border5 (UP): 0° rotation
    - Border6 (FLIP_DOWN): 180° rotation
  - Bind icon source to ViewModel property (VehicleIconPath)
  - Icon selection: Same logic as Task 4

  **Must NOT do**:
  - Do NOT modify calibration button logic
  - Do NOT change calibration step handling
  - Do NOT add new calibration positions

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: UI styling with rotation transforms
  - **Skills**: [`frontend-ui-ux`]
    - `frontend-ui-ux`: UI layout and visual styling

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 4, 6)
  - **Blocks**: Tasks 7, 8
  - **Blocked By**: Task 1

  **References**:
  - `Pigeon_Uno/Controls/CalibrationControl.xaml:176-230` - 6 Border controls with images
  - Pattern: `<Image Source="{Binding VehicleIconPath}" RenderTransformOrigin="0.5,0.5">` with `<RotateTransform Angle="..."/>`
  - WinUI rotation: Use `ui:ElementRotation` or `RotateTransform` in RenderTransform

  **Acceptance Criteria**:
  - [ ] All 6 positions show vehicle icon (not direction arrows)
  - [ ] Icons are rotated correctly for each position
  - [ ] Icon updates when vehicle type changes
  - [ ] Calibration functionality preserved

  **QA Scenarios**:
  ```
  Scenario: Calibration shows plane icons for FIXED_WING
    Tool: Playwright
    Preconditions: App running, calibration page visible, VehicleType=FixedWing
    Steps:
      1. Navigate to Calibration page
      2. Click Accelerometer tab
      3. Take screenshot
      4. Verify all 6 borders contain plane icon (ikon-wahana-pesawat-1.png)
    Expected Result: Plane icon visible in all 6 positions
    Failure Indicators: Direction arrows visible, wrong icon
    Evidence: .sisyphus/evidence/task-5-plane-calib.png

  Scenario: Calibration shows quadcopter icons for QUADROTOR
    Tool: Playwright
    Preconditions: App running, VehicleType=Quadrotor
    Steps:
      1. Navigate to Calibration page
      2. Click Accelerometer tab
      3. Take screenshot
      4. Verify all 6 borders contain quadcopter icon
    Expected Result: Quadcopter icon visible in all 6 positions
    Failure Indicators: Plane icon shown, arrows shown
    Evidence: .sisyphus/evidence/task-5-quad-calib.png

  Scenario: Icons rotate correctly for each position
    Tool: Playwright
    Preconditions: Calibration page visible
    Steps:
      1. Take screenshot of each position
      2. Compare rotation angles:
         - FLIP_UP: Icon pointing up (0°)
         - KIRI: Icon rotated left (-90°)
         - KANAN: Icon rotated right (90°)
         - DOWN: Icon upside down (180°)
         - UP: Icon pointing up (0°)
         - FLIP_DOWN: Icon upside down (180°)
    Expected Result: Each position has correct rotation
    Failure Indicators: Wrong rotation angles
    Evidence: .sisyphus/evidence/task-5-rotations.png
  ```

  **Commit**: YES
  - Message: `feat(calibration): show vehicle icons in accelerometer calibration`
  - Files: `Pigeon_Uno/Controls/CalibrationControl.xaml`
  - Pre-commit: Build succeeds, calibration UI renders

---

- [x] 6. **Add Vehicle Type Change Handler** ✓ COMPLETED

  **What to do**:
  - Add PropertyChanged handler for VehicleType in CalibrationControl
  - Update VehicleIconPath when VehicleType changes
  - Ensure UI updates in real-time when new HEARTBEAT arrives
  - Use DispatcherQueue for thread-safe UI updates

  **Must NOT do**:
  - Do NOT block UI thread during update
  - Do NOT add vehicle type change events

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Simple event handler addition
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 4, 5)
  - **Blocks**: Task 7
  - **Blocked By**: Task 2

  **References**:
  - `Pigeon_Uno/Controls/CalibrationControl.xaml.cs` - Existing PropertyChanged pattern
  - Pattern: Subscribe to ViewModel.PropertyChanged, check e.PropertyName == "VehicleType"
  - DispatcherQueue: `DispatcherQueue.TryEnqueue(() => { ... })`

  **Acceptance Criteria**:
  - [ ] CalibrationControl subscribes to VehicleType changes
  - [ ] VehicleIconPath updates when VehicleType changes
  - [ ] UI refreshes without blocking

  **QA Scenarios**:
  ```
  Scenario: Calibration UI updates on vehicle type change
    Tool: Playwright
    Preconditions: App running, calibration page visible, VehicleType=FixedWing
    Steps:
      1. Take screenshot (should show plane icon)
      2. Simulate HEARTBEAT with type=2 (Quadrotor)
      3. Wait 500ms for UI update
      4. Take screenshot
      5. Verify quadcopter icon now shown
    Expected Result: Icon changes from plane to quadcopter
    Failure Indicators: Icon unchanged, UI frozen
    Evidence: .sisyphus/evidence/task-6-type-change.png
  ```

  **Commit**: NO (groups with Task 5)

---

- [ ] 7. **Integration Testing**

  **What to do**:
  - Test complete flow: HEARTBEAT → VehicleType → Map marker + Calibration icons
  - Test with different vehicle types: FixedWing, Quadrotor, Hexarotor, Generic
  - Test edge cases: No connection, Missing HEARTBEAT, Unknown type
  - Verify no crashes or exceptions

  **Must NOT do**:
  - Do NOT modify code, only test

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Integration testing requires multiple component coordination
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3 (sequential)
  - **Blocks**: Task 8
  - **Blocked By**: Tasks 4, 5, 6

  **References**:
  - All modified files from Tasks 1-6
  - Test data: MAV_TYPE values (1, 2, 13, 14, 15, 29, 43, 0)

  **Acceptance Criteria**:
  - [ ] HEARTBEAT with FixedWing → plane icon everywhere
  - [ ] HEARTBEAT with Quadrotor → quadcopter icon everywhere
  - [ ] HEARTBEAT with Hexarotor → quadcopter icon (fallback)
  - [ ] No HEARTBEAT → default plane icon
  - [ ] No exceptions during type transitions

  **QA Scenarios**:
  ```
  Scenario: Complete flow with FixedWing vehicle
    Tool: Bash + Playwright
    Preconditions: Clean app start
    Steps:
      1. Start app (no connection)
      2. Verify default plane icons shown
      3. Simulate HEARTBEAT with type=1 (FixedWing)
      4. Verify map shows plane icon
      5. Navigate to calibration
      6. Verify calibration shows plane icons
    Expected Result: Plane icons shown consistently
    Failure Indicators: Inconsistent icons, errors
    Evidence: .sisyphus/evidence/task-7-fixedwing-flow.png

  Scenario: Complete flow with Quadrotor vehicle
    Tool: Bash + Playwright
    Preconditions: Clean app start
    Steps:
      1. Start app
      2. Simulate HEARTBEAT with type=2 (Quadrotor)
      3. Verify map shows quadcopter icon
      4. Navigate to calibration
      5. Verify calibration shows quadcopter icons
    Expected Result: Quadcopter icons shown consistently
    Failure Indicators: Plane icons shown, errors
    Evidence: .sisyphus/evidence/task-7-quadrotor-flow.png

  Scenario: Vehicle type transition mid-session
    Tool: Bash + Playwright
    Preconditions: App running with FixedWing type
    Steps:
      1. Verify plane icons shown
      2. Simulate new HEARTBEAT with type=2 (Quadrotor)
      3. Verify icons change to quadcopter
      4. Verify no exceptions in log
    Expected Result: Smooth transition without errors
    Failure Indicators: Crashes, frozen UI, wrong icons
    Evidence: .sisyphus/evidence/task-7-transition.txt
  ```

  **Commit**: NO (verification only)

---

- [ ] 8. **Visual Verification via Playwright**

  **What to do**:
  - Use Playwright to automate UI verification
  - Take screenshots of all key states:
    - Calibration with plane icon
    - Calibration with quadcopter icon
    - Map marker with plane icon
    - Map marker with quadcopter icon
    - Rotation states for calibration positions
  - Compare against expected visual output

  **Must NOT do**:
  - Do NOT modify code, only verify

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: Visual verification requires UI testing tools
  - **Skills**: [`playwright`, `frontend-ui-ux`]
    - `playwright`: Browser automation for screenshots
    - `frontend-ui-ux`: Understanding visual requirements

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3 (final)
  - **Blocks**: None
  - **Blocked By**: Task 7

  **References**:
  - Playwright skill for browser automation
  - Expected visual: Icons should be clearly visible, correctly rotated

  **Acceptance Criteria**:
  - [ ] Screenshots captured for all states
  - [ ] Visual comparison confirms correct icons
  - [ ] Evidence saved to .sisyphus/evidence/

  **QA Scenarios**:
  ```
  Scenario: Visual verification of all states
    Tool: Playwright
    Preconditions: App running with all vehicle types testable
    Steps:
      1. Capture calibration with FixedWing (6 position screenshots)
      2. Capture calibration with Quadrotor (6 position screenshots)
      3. Capture map marker with FixedWing
      4. Capture map marker with Quadrotor
      5. Save all to .sisyphus/evidence/task-8-visual/
    Expected Result: 14+ screenshots showing correct icons
    Failure Indicators: Missing screenshots, wrong visuals
    Evidence: .sisyphus/evidence/task-8-visual/*.png
  ```

  **Commit**: NO (verification only)

---

## Final Verification Wave (MANDATORY)

- [ ] F1. **Plan Compliance Audit** — `oracle`
- [ ] F2. **Code Quality Review** — `unspecified-high`
- [ ] F3. **Real Manual QA** — `unspecified-high`
- [ ] F4. **Scope Fidelity Check** — `deep`

---

## Commit Strategy

- **1**: `feat(telemetry): extract vehicle type from HEARTBEAT message`
- **2**: `feat(map): display vehicle icon based on MAV_TYPE`
- **3**: `feat(calibration): show vehicle icons in accelerometer calibration`
- **4**: `feat(integration): dynamic icon update on vehicle type change`

---

## Success Criteria

### Verification Commands
```bash
# Verify vehicle type parsing
dotnet run --project Pigeon_Uno.Desktop.csproj
# Send test HEARTBEAT with MAV_TYPE_QUADROTOR
# Verify FlightData.Type = 2 in debug output

# Verify map icon
# Take screenshot of map with vehicle marker
# Compare with expected icon
```

### Final Checklist
- [ ] All "Must Have" present
- [ ] All "Must NOT Have" absent
- [ ] Vehicle icons display correctly in calibration
- [ ] Map marker shows correct icon for vehicle type
- [ ] Fallback icons work for all multirotor types