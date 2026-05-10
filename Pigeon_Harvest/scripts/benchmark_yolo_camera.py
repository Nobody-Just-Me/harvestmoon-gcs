#!/usr/bin/env python3
import base64
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PYTHON = ROOT / ".venv-camera" / "bin" / "python"
SCRIPT = ROOT / "Pigeon_Uno" / "camera_service.py"


def main() -> int:
    python = str(PYTHON if PYTHON.exists() else sys.executable)
    proc = subprocess.Popen(
        [python, str(SCRIPT), "stream", "0"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
    )

    frame_count = 0
    byte_count = 0
    start = time.time()
    try:
        while time.time() - start < 10:
            line = proc.stdout.readline() if proc.stdout else ""
            if not line:
                continue
            try:
                payload = json.loads(line)
            except json.JSONDecodeError:
                continue
            if payload.get("type") == "frame":
                frame_count += 1
                byte_count += len(base64.b64decode(payload.get("data", "")))
    finally:
        proc.kill()

    elapsed = max(time.time() - start, 0.001)
    print(
        json.dumps(
            {
                "frames": frame_count,
                "fps": round(frame_count / elapsed, 2),
                "avg_jpeg_bytes": round(byte_count / max(frame_count, 1), 2),
            },
            indent=2,
        )
    )
    return 0 if frame_count > 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
