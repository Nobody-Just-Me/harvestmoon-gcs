#!/usr/bin/env python3
"""
Desktop speech-to-text helper for HarvestmoonGCS.

Records a short audio clip from the default microphone, then transcribes using
SpeechRecognition + Google Web Speech API.
Output format (stdout):
  {"text":"...", "confidence":0.82}
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile


def safe_output(text: str = "", confidence: float = 0.0, error: str = "") -> int:
    payload = {
        "text": text.strip(),
        "confidence": max(0.0, min(1.0, confidence)),
    }
    if error:
        payload["error"] = error
    print(json.dumps(payload), flush=True)
    return 0


def record_audio_to_wav(path: str, duration_seconds: int) -> tuple[bool, str]:
    arecord = shutil.which("arecord")
    if arecord:
        command = [
            arecord,
            "-q",
            "-f",
            "S16_LE",
            "-c",
            "1",
            "-r",
            "16000",
            "-d",
            str(duration_seconds),
            path,
        ]
        try:
            subprocess.run(command, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=duration_seconds + 6)
            return True, ""
        except subprocess.TimeoutExpired:
            return False, "Perekaman audio timeout."
        except subprocess.CalledProcessError as exc:
            message = (exc.stderr or b"").decode("utf-8", errors="ignore").strip()
            return False, message or "Gagal merekam audio dengan arecord."

    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg:
        command = [
            ffmpeg,
            "-loglevel",
            "error",
            "-y",
            "-f",
            "pulse",
            "-i",
            "default",
            "-t",
            str(duration_seconds),
            "-ac",
            "1",
            "-ar",
            "16000",
            path,
        ]
        try:
            subprocess.run(command, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=duration_seconds + 8)
            return True, ""
        except subprocess.TimeoutExpired:
            return False, "Perekaman audio timeout."
        except subprocess.CalledProcessError as exc:
            message = (exc.stderr or b"").decode("utf-8", errors="ignore").strip()
            return False, message or "Gagal merekam audio dengan ffmpeg."

    return False, "Recorder audio tidak ditemukan (butuh arecord atau ffmpeg)."


def transcribe_wav(path: str, language: str) -> tuple[str, float, str]:
    try:
        import speech_recognition as sr  # type: ignore
    except Exception:
        return "", 0.0, "Modul python 'speech_recognition' belum tersedia."

    recognizer = sr.Recognizer()
    recognizer.dynamic_energy_threshold = True
    recognizer.pause_threshold = 0.6

    try:
        with sr.AudioFile(path) as source:
            audio = recognizer.record(source)
    except Exception as exc:
        return "", 0.0, f"Gagal membaca audio: {exc}"

    try:
        text = recognizer.recognize_google(audio, language=language)
        cleaned = text.strip()
        if not cleaned:
            return "", 0.0, ""

        words = len(cleaned.split())
        confidence = 0.65 if words <= 2 else 0.82
        return cleaned, confidence, ""
    except sr.UnknownValueError:
        return "", 0.0, ""
    except sr.RequestError as exc:
        return "", 0.0, f"Layanan STT tidak dapat diakses: {exc}"
    except Exception as exc:
        return "", 0.0, f"Gagal transkripsi: {exc}"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="PIA desktop STT listener")
    parser.add_argument("--duration", type=int, default=int(os.getenv("PIA_STT_DURATION_SECONDS", "4")))
    parser.add_argument("--language", default=os.getenv("PIA_STT_LANGUAGE", "id-ID"))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    duration = args.duration if args.duration > 0 else 4
    language = (args.language or "id-ID").strip() or "id-ID"

    with tempfile.TemporaryDirectory(prefix="pia_stt_") as temp_dir:
        wav_path = os.path.join(temp_dir, "capture.wav")

        ok, record_error = record_audio_to_wav(wav_path, duration)
        if not ok:
            return safe_output(error=record_error)

        text, confidence, transcribe_error = transcribe_wav(wav_path, language)
        if transcribe_error:
            return safe_output(error=transcribe_error)

        return safe_output(text=text, confidence=confidence)


if __name__ == "__main__":
    raise SystemExit(main())
