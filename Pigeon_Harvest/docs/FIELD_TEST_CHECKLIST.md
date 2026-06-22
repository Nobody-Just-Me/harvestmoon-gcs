# MoonHarvest Field Test Checklist

## Pre-flight

- [ ] Connect telemetry/MAVLink link.
- [ ] Confirm Dashboard opens without error.
- [ ] Confirm local camera device appears and streams in Dashboard.
- [ ] Press **Yolo Option** and verify status changes to **Yolo Active**.
- [ ] Confirm Mission Planner map opens and can add/move waypoints.
- [ ] Confirm geofence panel can create/clear/send geofence.

## Core Demo

- [ ] Camera frame appears in Dashboard.
- [ ] YOLO overlay appears when detections are present.
- [ ] Detection count updates in Dashboard.
- [ ] Irrigation priority progress updates from analysis result.
- [ ] Map shows waypoint/geofence/priority markers.

## Mission

- [ ] Generate grid survey mission.
- [ ] Upload mission to vehicle/SITL.
- [ ] Download mission and compare waypoint count.
- [ ] Trigger geofence alert with out-of-bounds coordinate.

## Reports

- [ ] Run crop analysis on an image.
- [ ] Confirm report appears in Reports page.
- [ ] Export JSON.
- [ ] Export CSV.
- [ ] Export PDF payload.
- [ ] Export validation CSV when ground-truth samples are available.

## Post-flight

- [ ] Save TLOG path into latest report.
- [ ] Attach operator notes.
- [ ] Verify exported files are present in Downloads/export path.
