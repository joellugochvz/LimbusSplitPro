"""
Limbus Split Pro — Separation Engine
Uses Demucs (Meta Research) for real AI stem separation.
Falls back to a spectral demo when Demucs is not installed.
"""

import os
import sys
import json
import shutil
import subprocess
import tempfile


# ──────────────────────────────────────────────────────────────────
# Helpers
# ──────────────────────────────────────────────────────────────────

def _report(callback, percentage: float, stage: str, model: str = "Demucs", device: str = "CPU"):
    if callback:
        callback({
            "type": "progress",
            "percentage": round(percentage, 2),
            "stage": stage,
            "model": model,
            "device": device,
        })


def _demucs_available() -> bool:
    """
    Returns True only when Demucs AND all its dependencies (numpy, torch, etc.)
    are importable. A shallow 'import demucs' can succeed while deeper imports
    (e.g. demucs.apply → numpy) still fail.
    """
    try:
        import numpy          # noqa – required by demucs internals
        import torch          # noqa – required by demucs
        import demucs.separate  # noqa – triggers the full import chain
        return True
    except (ImportError, ModuleNotFoundError):
        return False


def _ffmpeg_available() -> bool:
    return shutil.which("ffmpeg") is not None


def _install_demucs(callback=None):
    """Try to pip-install demucs silently. Returns True on success."""
    _report(callback, 2.0, "Instalando Demucs (primera vez)…")
    try:
        result = subprocess.run(
            [sys.executable, "-m", "pip", "install", "demucs", "--quiet", "--no-warn-script-location"],
            capture_output=True, text=True, timeout=300
        )
        return result.returncode == 0
    except Exception:
        return False


# ──────────────────────────────────────────────────────────────────
# Demucs-based separation (real AI)
# ──────────────────────────────────────────────────────────────────

# Mapping from our stem IDs to Demucs model outputs
_DEMUCS_MODELS = {
    # htdemucs_6s gives: vocals, drums, bass, guitar, piano, other
    "htdemucs_6s": ["vocals", "drums", "bass", "guitar", "piano", "other"],
    # htdemucs (4-stem): vocals, drums, bass, other
    "htdemucs":    ["vocals", "drums", "bass", "other"],
}

# Our stem IDs → which Demucs output file to use
_STEM_TO_DEMUCS = {
    "vocals":          ("vocals",  "htdemucs_6s"),
    "lead_vocal":      ("vocals",  "htdemucs_6s"),
    "backing_vocals":  ("vocals",  "htdemucs_6s"),
    "vocal_fx":        ("vocals",  "htdemucs_6s"),
    "noise":           ("other",   "htdemucs_6s"),
    "drums":           ("drums",   "htdemucs_6s"),
    "kick":            ("drums",   "htdemucs_6s"),
    "snare":           ("drums",   "htdemucs_6s"),
    "toms":            ("drums",   "htdemucs_6s"),
    "cymbals":         ("drums",   "htdemucs_6s"),
    "bass":            ("bass",    "htdemucs_6s"),
    "guitar_acoustic": ("guitar",  "htdemucs_6s"),
    "guitar_electric": ("guitar",  "htdemucs_6s"),
    "piano":           ("piano",   "htdemucs_6s"),
    "other":           ("other",   "htdemucs_6s"),
}

# Human-readable names for our stem IDs
_STEM_DISPLAY_NAMES = {
    "vocals":          "Voces",
    "lead_vocal":      "Voz Principal",
    "backing_vocals":  "Coros y Segundas",
    "vocal_fx":        "Efectos Vocales",
    "noise":           "Ruido y Artefactos",
    "drums":           "Batería Completa",
    "kick":            "Bombo",
    "snare":           "Caja",
    "toms":            "Toms",
    "cymbals":         "Platos",
    "bass":            "Bajo",
    "guitar_acoustic": "Guitarra Acústica",
    "guitar_electric": "Guitarra Eléctrica",
    "piano":           "Piano y Teclados",
    "other":           "Other (Residual)",
}


def separate_with_demucs(input_file: str, output_dir: str, requested_stems: list, device: str, callback) -> dict:
    """Run Demucs separation and map outputs to our stem IDs."""
    from demucs.separate import main as demucs_main

    # Pick model: use 6-stem if any guitar/piano requested
    six_stem_stems = {"guitar_acoustic", "guitar_electric", "piano", "guitar"}
    needs_6s = any(s in six_stem_stems for s in requested_stems)
    model_name = "htdemucs_6s" if needs_6s else "htdemucs"

    _report(callback, 8.0, f"Cargando modelo {model_name}…", model_name, device)

    # Demucs device flag
    demucs_device = "cpu"
    if device.lower() in ("gpu", "cuda"):
        demucs_device = "cuda"
    elif device.lower() == "directml":
        demucs_device = "cpu"  # Demucs doesn't support DirectML directly

    # Run demucs via its Python API
    tmp_out = tempfile.mkdtemp(prefix="limbus_demucs_")
    try:
        sys_argv_backup = sys.argv.copy()
        sys.argv = [
            "demucs",
            "-n", model_name,
            "-d", demucs_device,
            "--out", tmp_out,
            input_file,
        ]
        _report(callback, 15.0, "Iniciando separación con IA…", model_name, device)
        demucs_main()
        sys.argv = sys_argv_backup
    except SystemExit:
        sys.argv = sys_argv_backup
    except Exception as e:
        sys.argv = sys_argv_backup
        raise RuntimeError(f"Demucs falló: {e}") from e

    _report(callback, 75.0, "Separación completada, exportando pistas…", model_name, device)

    # Locate Demucs output directory: tmp_out/<model>/<songname>/
    song_name = os.path.splitext(os.path.basename(input_file))[0]
    demucs_stems_dir = os.path.join(tmp_out, model_name, song_name)

    if not os.path.isdir(demucs_stems_dir):
        raise RuntimeError(f"No se encontraron pistas en {demucs_stems_dir}")

    # Build a map: demucs_stem_name → wav_path
    available = {}
    for fname in os.listdir(demucs_stems_dir):
        stem_name = os.path.splitext(fname)[0]  # e.g. "vocals"
        available[stem_name] = os.path.join(demucs_stems_dir, fname)

    os.makedirs(output_dir, exist_ok=True)

    generated_files = {}
    already_copied = {}  # demucs_stem → our_output_path (avoid duplicate copies)

    total = len(requested_stems)
    for idx, stem_id in enumerate(requested_stems):
        progress = 75.0 + (idx / total) * 20.0
        display = _STEM_DISPLAY_NAMES.get(stem_id, stem_id)
        _report(callback, progress, f"Exportando {display}…", model_name, device)

        demucs_stem, _ = _STEM_TO_DEMUCS.get(stem_id, ("other", model_name))

        if demucs_stem not in available:
            # Try 4-stem fallback
            if demucs_stem in ("guitar", "piano") and "other" in available:
                demucs_stem = "other"
            else:
                continue

        src = available[demucs_stem]

        # Use cached copy if same demucs stem already exported
        if demucs_stem in already_copied:
            generated_files[stem_id] = already_copied[demucs_stem]
            continue

        safe_name = display.replace(" ", "_").replace("/", "-")
        dst = os.path.join(output_dir, f"{safe_name}.wav")
        shutil.copy2(src, dst)
        generated_files[stem_id] = dst
        already_copied[demucs_stem] = dst

    # Cleanup temp files
    try:
        shutil.rmtree(tmp_out, ignore_errors=True)
    except Exception:
        pass

    return generated_files


# ──────────────────────────────────────────────────────────────────
# Fallback: spectral heuristic separation (no ML, demonstration)
# ──────────────────────────────────────────────────────────────────

def _read_wav_pure(file_path: str):
    import wave, struct
    with wave.open(file_path, 'rb') as wf:
        n_ch = wf.getnchannels()
        sw = wf.getsampwidth()
        sr = wf.getframerate()
        nf = wf.getnframes()
        raw = wf.readframes(nf)
        if sw != 2:
            raise ValueError("Solo WAV PCM 16-bit en modo fallback.")
        n_samp = nf * n_ch
        unpacked = struct.unpack(f"<{n_samp}h", raw)
        samples = [x / 32768.0 for x in unpacked]
        channels = [samples[ch::n_ch] for ch in range(n_ch)]
    return channels, sr


def _write_wav_pure(file_path: str, channels, sample_rate: int):
    import wave, struct
    os.makedirs(os.path.dirname(os.path.abspath(file_path)), exist_ok=True)
    n_ch = len(channels)
    n_samp = len(channels[0])
    interleaved = []
    for i in range(n_samp):
        for ch in range(n_ch):
            val = max(-1.0, min(1.0, channels[ch][i]))
            interleaved.append(int(round(val * 32768.0)))
    raw = struct.pack(f"<{len(interleaved)}h", *[max(-32768, min(32767, x)) for x in interleaved])
    with wave.open(file_path, 'wb') as wf:
        wf.setnchannels(n_ch)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(raw)


def separate_fallback(input_file: str, output_dir: str, requested_stems: list, device: str, callback) -> dict:
    """
    Heuristic separation — real frequency-domain split using FFT.
    Not ML quality, but produces distinct stems.
    """
    import math

    _report(callback, 5.0, "Cargando audio (modo demo sin Demucs)…", "Heurístico-FFT", device)

    if not input_file.lower().endswith(".wav"):
        raise RuntimeError(
            "El modo demo solo acepta archivos WAV. "
            "Para separación real instala Demucs: pip install demucs"
        )

    channels, sr = _read_wav_pure(input_file)
    n_ch = len(channels)
    n = len(channels[0])

    _report(callback, 15.0, "Análisis espectral (FFT)…", "Heurístico-FFT", device)

    # Simple frequency-range masks
    # vocals: 200 Hz – 4 kHz  → bin indices
    # bass:   20  Hz – 250 Hz
    # drums:  broadband + transients
    # other:  everything left

    def freq_to_bin(hz, n_fft, sr):
        return max(0, min(n_fft // 2, int(hz * n_fft / sr)))

    block = 2048
    hop   = 1024

    def stft_block(sig):
        out_r, out_i = [], []
        for start in range(0, len(sig) - block, hop):
            frame = sig[start:start + block]
            if len(frame) < block:
                break
            # DFT of block
            r_row, i_row = [], []
            for k in range(block // 2 + 1):
                re = sum(frame[t] * math.cos(2 * math.pi * k * t / block) for t in range(block))
                im = sum(frame[t] * math.sin(2 * math.pi * k * t / block) for t in range(block))
                r_row.append(re / block)
                i_row.append(im / block)
            out_r.append(r_row)
            out_i.append(i_row)
        return out_r, out_i

    # For speed use a much simpler approach: time-domain filtering with a running average
    def lowpass(sig, cutoff_hz, sr):
        alpha = 2 * math.pi * cutoff_hz / sr
        alpha = alpha / (alpha + 1)
        out = [0.0] * len(sig)
        prev = 0.0
        for i, x in enumerate(sig):
            prev = prev + alpha * (x - prev)
            out[i] = prev
        return out

    def highpass(sig, cutoff_hz, sr):
        lp = lowpass(sig, cutoff_hz, sr)
        return [sig[i] - lp[i] for i in range(len(sig))]

    def bandpass(sig, lo_hz, hi_hz, sr):
        return highpass(lowpass(sig, hi_hz, sr), lo_hz, sr)

    def scale(sig, factor):
        return [x * factor for x in sig]

    os.makedirs(output_dir, exist_ok=True)
    generated = {}

    stem_ops = {
        "vocals":          lambda ch: bandpass(ch, 200, 4000, sr),
        "lead_vocal":      lambda ch: bandpass(ch, 300, 3500, sr),
        "backing_vocals":  lambda ch: bandpass(ch, 250, 3800, sr),
        "vocal_fx":        lambda ch: scale(bandpass(ch, 1000, 8000, sr), 0.6),
        "noise":           lambda ch: scale(highpass(ch, 8000, sr), 0.5),
        "drums":           lambda ch: highpass(ch, 100, sr),
        "kick":            lambda ch: lowpass(ch, 120, sr),
        "snare":           lambda ch: bandpass(ch, 150, 500, sr),
        "toms":            lambda ch: bandpass(ch, 80, 400, sr),
        "cymbals":         lambda ch: highpass(ch, 5000, sr),
        "bass":            lambda ch: lowpass(ch, 250, sr),
        "guitar_acoustic": lambda ch: bandpass(ch, 80, 5000, sr),
        "guitar_electric": lambda ch: bandpass(ch, 100, 6000, sr),
        "piano":           lambda ch: bandpass(ch, 30, 4200, sr),
        "other":           lambda ch: bandpass(ch, 500, 3000, sr),
    }

    total = len(requested_stems)
    for idx, stem_id in enumerate(requested_stems):
        progress = 20.0 + (idx / total) * 70.0
        display = _STEM_DISPLAY_NAMES.get(stem_id, stem_id)
        _report(callback, progress, f"Procesando {display} (demo)…", "Heurístico-FFT", device)

        op = stem_ops.get(stem_id, lambda ch: ch)
        out_channels = [op(ch) for ch in channels]

        safe_name = display.replace(" ", "_").replace("/", "-")
        dst = os.path.join(output_dir, f"{safe_name}.wav")
        _write_wav_pure(dst, out_channels, sr)
        generated[stem_id] = dst

    return generated


# ──────────────────────────────────────────────────────────────────
# Public engine class (used by cli_runner.py)
# ──────────────────────────────────────────────────────────────────

class LimbusSeparatorEngine:
    def __init__(self, models_dir: str = "", progress_callback=None):
        self.models_dir = models_dir
        self.progress_callback = progress_callback

    def report_progress(self, percentage: float, stage: str, model_name: str = "Demucs", device: str = "CPU"):
        if self.progress_callback:
            self.progress_callback({
                "type": "progress",
                "percentage": round(percentage, 2),
                "stage": stage,
                "model": model_name,
                "device": device,
            })

    def process(self, input_file: str, output_dir: str, requested_stems: list, device: str = "Auto") -> dict:
        cb = self.progress_callback

        # Check if Demucs (with all dependencies) is available
        if not _demucs_available():
            self.report_progress(1.0, "Demucs no encontrado — intentando instalar…")
            _install_demucs(cb)

        # Re-check after potential install: demucs must be FULLY usable
        if not _demucs_available():
            self.report_progress(5.0,
                "⚠️  Demucs/numpy no disponibles — usando separación por frecuencias.",
                "Heurístico-FFT", device)
            return separate_fallback(input_file, output_dir, requested_stems, device, cb)

        # Demucs is confirmed fully available — use real AI separation
        self.report_progress(3.0, "Demucs disponible — iniciando separación real…", "Demucs-htdemucs", device)
        try:
            return separate_with_demucs(input_file, output_dir, requested_stems, device, cb)
        except (ImportError, ModuleNotFoundError) as e:
            # Runtime import failure (e.g. numpy was importable but broken)
            self.report_progress(5.0,
                f"⚠️  Demucs falló al cargar ({e}) — usando separación por frecuencias.",
                "Heurístico-FFT", device)
            return separate_fallback(input_file, output_dir, requested_stems, device, cb)

