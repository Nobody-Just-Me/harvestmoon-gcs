#!/usr/bin/env python3
"""
Simple camera bridge for Pigeon_Uno desktop.

Commands:
  list
  stream <source>
  picture <source> <output_file>
"""

from __future__ import annotations

import base64
import glob
import json
import os
import platform
import re
import sys
import time
from typing import Any

try:
    import cv2  # type: ignore
except Exception:
    cv2 = None


LOCAL_CAMERA_TYPE = 0
NETWORK_STREAM_TYPE = 1


def emit(payload: dict[str, Any]) -> None:
    print(json.dumps(payload), flush=True)


def emit_error(message: str) -> int:
    emit({"error": message})
    return 1


def parse_index_from_device(path: str) -> int | None:
    match = re.search(r"video(\d+)$", path)
    if match is None:
        return None
    return int(match.group(1))


def open_capture(source: str):
    if cv2 is None:
        return None

    if source.isdigit():
        index = int(source)
        if platform.system().lower().startswith("win") and hasattr(cv2, "CAP_DSHOW"):
            return cv2.VideoCapture(index, cv2.CAP_DSHOW)
        return cv2.VideoCapture(index)

    return cv2.VideoCapture(source)


def detect_sources() -> list[dict[str, Any]]:
    sources: list[dict[str, Any]] = []

    candidates: list[int] = []
    for device_path in sorted(glob.glob("/dev/video*")):
        index = parse_index_from_device(device_path)
        if index is not None:
            candidates.append(index)

    if not candidates:
        candidates = list(range(0, 6))

    tested: set[int] = set()
    for index in candidates:
        if index in tested:
            continue
        tested.add(index)

        if cv2 is None:
            # cv2 not available, still expose detected device ids for Linux
            if os.path.exists(f"/dev/video{index}"):
                sources.append(
                    {
                        "id": str(index),
                        "name": f"Camera {index}",
                        "description": f"Local Camera {index}",
                        "type": LOCAL_CAMERA_TYPE,
                        "isAvailable": True,
                    }
                )
            continue

        cap = open_capture(str(index))
        if cap is None:
            continue

        try:
            if not cap.isOpened():
                continue
            ok, frame = cap.read()
            if not ok or frame is None:
                continue

            width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
            height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
            fps = int(cap.get(cv2.CAP_PROP_FPS) or 0)
            fps_suffix = f" @ {fps}fps" if fps > 0 else ""

            sources.append(
                {
                    "id": str(index),
                    "name": f"Camera {index}",
                    "description": f"Local Camera {index} ({width}x{height}{fps_suffix})",
                    "type": LOCAL_CAMERA_TYPE,
                    "isAvailable": True,
                }
            )
        finally:
            cap.release()

    sources.append(
        {
            "id": "network",
            "name": "Network Stream (RTSP/HTTP)",
            "description": "Enter custom network stream URL",
            "type": NETWORK_STREAM_TYPE,
            "isAvailable": True,
        }
    )
    return sources


def cmd_list() -> int:
    print(json.dumps(detect_sources()))
    return 0


def cmd_stream(source: str) -> int:
    if cv2 is None:
        return emit_error("opencv-python (cv2) is not installed")

    cap = open_capture(source)
    if cap is None or not cap.isOpened():
        return emit_error(f"Failed to open camera source: {source}")

    try:
        while True:
            ok, frame = cap.read()
            if not ok or frame is None:
                time.sleep(0.05)
                continue

            ok, encoded = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), 78])
            if not ok:
                continue

            payload = base64.b64encode(encoded.tobytes()).decode("ascii")
            emit({"type": "frame", "data": payload})
            time.sleep(1.0 / 15.0)
    except (BrokenPipeError, KeyboardInterrupt):
        return 0
    finally:
        cap.release()


def cmd_picture(source: str, output_file: str) -> int:
    if cv2 is None:
        return emit_error("opencv-python (cv2) is not installed")

    cap = open_capture(source)
    if cap is None or not cap.isOpened():
        return emit_error(f"Failed to open camera source: {source}")

    try:
        ok, frame = cap.read()
        if not ok or frame is None:
            return emit_error("Failed to capture frame")

        output_dir = os.path.dirname(os.path.abspath(output_file))
        if output_dir:
            os.makedirs(output_dir, exist_ok=True)

        if not cv2.imwrite(output_file, frame):
            return emit_error(f"Failed to save image: {output_file}")

        print(json.dumps({"success": True, "filename": output_file}))
        return 0
    finally:
        cap.release()


def main() -> int:
    if len(sys.argv) < 2:
        return emit_error("Missing command. Use: list | stream <source> | picture <source> <output>")

    cmd = sys.argv[1].strip().lower()
    if cmd == "list":
        return cmd_list()
    if cmd == "stream":
        if len(sys.argv) < 3:
            return emit_error("Missing source for stream command")
        return cmd_stream(sys.argv[2])
    if cmd == "picture":
        if len(sys.argv) < 4:
            return emit_error("Missing source/output for picture command")
        return cmd_picture(sys.argv[2], sys.argv[3])

    return emit_error(f"Unknown command: {cmd}")


if __name__ == "__main__":
    raise SystemExit(main())
