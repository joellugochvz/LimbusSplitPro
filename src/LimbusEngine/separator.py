"""
Limbus Split Pro — Separation Engine v3
Uses Demucs (Meta Research) via subprocess for real AI stem separation.
Falls back to spectral heuristic when Demucs/PyTorch are unavailable.
"""

import os
import sys
import json
import math
import shutil
import struct
import subprocess
import tempfile
import wave


# ──────────────────────────────────────────────────────────────────
# Stem metadata
# ──────────────────────────────────────────────────────────────────

STEM_DISPLAY_NAMES = {
    "vocals":          "Voces",
    "lead_vocal":      "Voz_Principal",
    "backing_vocals":  "Coros_y_Segundas",
    "vocal_fx":        "Efectos_Vocales",
    "drums":           "Bateria_Completa",
    "kick":            "Bombo_y_Toms",
    "snare":           "Caja",
    "cymbals":         "Platos",
    "bass":            "Bajo",
    "guitar":          "Guitarra",
    "piano":           "Piano_y_Teclados",
    "other":           "Other",
}

# Mapping: our stem ID → Demucs output stem name
# htdemucs_6s produces: vocals, drums, bass, guitar, piano, other
# htdemucs (4s)  produces: vocals, drums, bass, other
STEM_TO_DEMUCS = {
    "vocals":          "vocals",
    "lead_vocal":      "vocals",    # ↳ 2nd-pass Mid/Side extraction
    "backing_vocals":  "vocals",    # ↳ 2nd-pass Mid/Side extraction
    "vocal_fx":        "vocals",    # ↳ 2nd-pass high-freq air extraction
    "drums":           "drums",
    "kick":            "drums",     # ↳ 2nd-pass IIR bandpass
    "snare":           "drums",     # ↳ 2nd-pass IIR bandpass
    "cymbals":         "drums",     # ↳ 2nd-pass IIR highpass
    "bass":            "bass",
    "guitar":          "guitar",
    "piano":           "piano",
    "other":           "other",
}


# ──────────────────────────────────────────────────────────────────
# Progress helper
# ──────────────────────────────────────────────────────────────────

def _report(callback, pct: float, stage: str, model: str = "Demucs", device: str = "CPU"):
    if callback:
        callback({"type": "progress", "percentage": round(pct, 1),
                  "stage": stage, "model": model, "device": device})


# ──────────────────────────────────────────────────────────────────
# WAV helpers (pure stdlib — no numpy)
# ──────────────────────────────────────────────────────────────────

def _read_wav_pure(file_path: str):
    with wave.open(file_path, 'rb') as wf:
        n_ch = wf.getnchannels()
        sw   = wf.getsampwidth()
        sr   = wf.getframerate()
        nf   = wf.getnframes()
        raw  = wf.readframes(nf)
        if sw != 2:
            raise ValueError("Solo WAV PCM 16-bit en modo fallback.")
        unpacked = struct.unpack(f"<{nf * n_ch}h", raw)
        samples  = [x / 32768.0 for x in unpacked]
        channels = [samples[ch::n_ch] for ch in range(n_ch)]
    return channels, sr


def _write_wav_pure(file_path: str, channels, sr: int):
    os.makedirs(os.path.dirname(os.path.abspath(file_path)), exist_ok=True)
    n_ch   = len(channels)
    n_samp = len(channels[0])
    il     = []
    for i in range(n_samp):
        for ch in range(n_ch):
            il.append(max(-32768, min(32767, int(round(max(-1.0, min(1.0, channels[ch][i])) * 32768.0)))))
    with wave.open(file_path, 'wb') as wf:
        wf.setnchannels(n_ch); wf.setsampwidth(2); wf.setframerate(sr)
        wf.writeframes(struct.pack(f"<{len(il)}h", *il))


# ──────────────────────────────────────────────────────────────────
# Demucs availability & installation
# ──────────────────────────────────────────────────────────────────

def _run_pip(packages: list, callback=None) -> bool:
    """Install one or more pip packages. Returns True on success."""
    cmd = [sys.executable, "-m", "pip", "install", *packages,
           "--quiet", "--no-warn-script-location"]
    try:
        r = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
        return r.returncode == 0
    except Exception:
        return False


def _demucs_runnable() -> bool:
    """
    Check if 'python -m demucs --help' exits cleanly.
    This is the only reliable test — it exercises the full runtime.
    """
    try:
        r = subprocess.run(
            [sys.executable, "-m", "demucs", "--help"],
            capture_output=True, text=True, timeout=15
        )
        return r.returncode == 0
    except Exception:
        return False


def _ensure_demucs(callback=None) -> bool:
    """
    Make sure demucs is runnable. Install numpy + demucs if needed.
    Returns True if demucs is ready to use.
    """
    if _demucs_runnable():
        return True

    _report(callback, 1.0, "Instalando numpy…", "setup")
    if not _run_pip(["numpy>=1.24.0"], callback):
        return False

    _report(callback, 3.0, "Instalando demucs (puede tardar 1-2 min)…", "setup")
    if not _run_pip(["demucs"], callback):
        return False

    # Final check
    return _demucs_runnable()


# ──────────────────────────────────────────────────────────────────
# 2-pass drum component splitter
# ──────────────────────────────────────────────────────────────────

def _lowpass_iir(sig: list, cutoff_hz: float, sr: int) -> list:
    """Single-pole IIR lowpass filter."""
    rc    = 1.0 / (2.0 * math.pi * cutoff_hz)
    dt    = 1.0 / sr
    alpha = dt / (rc + dt)
    out   = [0.0] * len(sig)
    prev  = sig[0] if sig else 0.0
    for i, x in enumerate(sig):
        prev = prev + alpha * (x - prev)
        out[i] = prev
    return out


def _highpass_iir(sig: list, cutoff_hz: float, sr: int) -> list:
    lp = _lowpass_iir(sig, cutoff_hz, sr)
    return [sig[i] - lp[i] for i in range(len(sig))]


def _bandpass_iir(sig: list, lo: float, hi: float, sr: int) -> list:
    return _highpass_iir(_lowpass_iir(sig, hi, sr), lo, sr)


def _extract_vocal_component(stem_id: str, vocals_wav: str,
                              output_dir: str, display_name: str) -> str:
    """
    2nd-pass vocal separation using Mid/Side matrix decomposition.

    The AI-isolated vocals stem still carries stereo information:
      Mid  = (L + R) / 2  → centre-panned signal  = lead vocal
                             (producers always centre the main voice)
      Side = (L - R) / 2  → panned/doubled signal  = backing vocals / harmonies
                             (duplications, harmonies and reverb tails are
                              typically panned off-centre)

    vocal_fx: M/S is not meaningful for reverb, so we keep the full stereo
    vocal stem but apply a mild high-frequency emphasis (2–8 kHz) to bring
    out the air, sibilance and reverb tail that sits in the upper mids.
    """
    channels, sr = _read_wav_pure(vocals_wav)

    # Ensure we have stereo; if mono, duplicate channel
    if len(channels) == 1:
        channels = [channels[0], channels[0]]
    L, R = channels[0], channels[1]

    if stem_id == "lead_vocal":
        # Mid channel: centred content = lead vocal
        mid = [(L[i] + R[i]) * 0.5 for i in range(len(L))]
        processed = [mid, mid]   # output as mono-ish stereo

    elif stem_id == "backing_vocals":
        # Side channel: panned content = harmonies, doubles, chorus voices
        side = [(L[i] - R[i]) * 0.5 for i in range(len(L))]
        processed = [side, side]

    elif stem_id == "vocal_fx":
        # Full stereo vocal with high-freq emphasis: keeps reverb/air (2–8 kHz)
        # while de-emphasising the dry centre vocal (already in lead_vocal)
        processed = []
        for ch in channels:
            hi  = _bandpass_iir(ch, 2000, 8000, sr)   # air / reverb tail
            dry = _lowpass_iir(ch, 2000, sr)           # dry fundamental
            # blend: more air, less dry  → feels like the ambience/reverb
            blend = [hi[i] * 0.7 + dry[i] * 0.3 for i in range(len(ch))]
            processed.append(blend)

    else:
        return ""

    dst = os.path.join(output_dir, f"{display_name}.wav")
    _write_wav_pure(dst, processed, sr)
    return dst



                             output_dir: str, display_name: str) -> str:
    """
    2nd-pass drum separation.
    Reads the Demucs 'drums' stem (already isolated from vocals/bass/guitar)
    and applies tuned IIR filters to split drum components.

    CALIBRATED from user testing:
      kick   : 60-400 Hz bandpass  — user confirmed this range captures kick
               (what was labelled 'toms' filter range actually sounded like kick)
      snare  : 200-900 Hz (body, raised from 150 to avoid kick bleed)
               + 2.5-7 kHz (snap/attack transient), blended 60/40
      toms   : 80-350 Hz with slight low-shelf boost — overlaps kick but lower
               energy in very low end
      cymbals: 5 kHz+ highpass (hi-hats, rides, crashes)
    """
    channels, sr = _read_wav_pure(drums_wav)

    if stem_id == "kick":
        # 60-400 Hz bandpass: captures kick drum fundamental + toms resonance.
        # Both instruments share this frequency range — unified stem.
        # Double lowpass for steeper rolloff above 400 Hz (-12 dB/oct).
        processed = []
        for ch in channels:
            lp1 = _lowpass_iir(ch,   400, sr)
            lp2 = _lowpass_iir(lp1,  400, sr)   # 2nd pass → steeper hi rolloff
            result = _highpass_iir(lp2, 60, sr)  # remove sub-rumble below 60 Hz
            processed.append(result)

    elif stem_id == "snare":
        # Body: 200-900 Hz (raised low-cut to avoid kick leakage at 150-200 Hz).
        # Snap: 2.5-7 kHz for the crack/attack of the snare hit.
        processed = []
        for ch in channels:
            body = _bandpass_iir(ch, 200, 900, sr)
            snap = _bandpass_iir(ch, 2500, 7000, sr)
            combined = [body[i] * 0.6 + snap[i] * 0.4 for i in range(len(ch))]
            processed.append(combined)

    elif stem_id == "cymbals":
        # Cymbals: highpass above 5 kHz → hi-hats, rides, crashes.
        processed = [_highpass_iir(ch, 5000, sr) for ch in channels]

    else:
        return ""

    dst = os.path.join(output_dir, f"{display_name}.wav")
    _write_wav_pure(dst, processed, sr)
    return dst



# ──────────────────────────────────────────────────────────────────
# Real AI separation — Demucs via subprocess
# ──────────────────────────────────────────────────────────────────

def separate_with_demucs(input_file: str, output_dir: str,
                         requested_stems: list, device: str, callback) -> dict:
    """
    Runs Demucs as a child process: python -m demucs ...
    Avoids all Python import-chain issues. Streams stderr for progress.
    """
    six_stem = {"guitar_acoustic", "guitar_electric", "piano", "guitar"}
    needs_6s = any(s in six_stem for s in requested_stems)
    model    = "htdemucs_6s" if needs_6s else "htdemucs"

    demucs_device = "cpu"
    if device.lower() in ("gpu", "cuda"):
        demucs_device = "cuda"

    _report(callback, 8.0, f"Cargando modelo {model} (primera vez descarga ~150 MB)…",
            model, device)

    tmp_out = tempfile.mkdtemp(prefix="limbus_demucs_")
    try:
        cmd = [
            sys.executable, "-m", "demucs",
            "-n", model,
            "-d", demucs_device,
            "--out", tmp_out,
            input_file,
        ]

        proc = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
        )

        # Stream output and fake progress 10→75%
        fake_pct = 10.0
        for line in proc.stdout:
            line = line.rstrip()
            if line:
                # Extract percentage if demucs prints one
                pct_token = None
                for token in line.split():
                    token = token.strip("%|")
                    try:
                        v = float(token)
                        if 0 < v <= 100:
                            pct_token = v
                            break
                    except ValueError:
                        pass

                if pct_token is not None:
                    fake_pct = 10.0 + pct_token * 0.65
                else:
                    fake_pct = min(fake_pct + 0.5, 74.0)

                stage = line if len(line) < 80 else line[:77] + "…"
                _report(callback, fake_pct, stage, model, device)

        proc.wait()
        if proc.returncode != 0:
            raise RuntimeError(
                f"Demucs terminó con código {proc.returncode}. "
                "Asegúrate de tener al menos 4 GB de RAM libres."
            )

        _report(callback, 78.0, "Separación completada, exportando pistas…", model, device)

        # Locate stems: tmp_out/<model>/<songname>/<stem>.wav
        song_name = os.path.splitext(os.path.basename(input_file))[0]
        stems_dir = os.path.join(tmp_out, model, song_name)
        if not os.path.isdir(stems_dir):
            # Some versions use a flattened layout
            stems_dir = tmp_out

        available = {}
        for fname in os.listdir(stems_dir):
            if fname.lower().endswith(".wav") or fname.lower().endswith(".mp3"):
                stem_name = os.path.splitext(fname)[0]
                available[stem_name] = os.path.join(stems_dir, fname)

        if not available:
            raise RuntimeError(f"Demucs no generó archivos en {stems_dir}.")

        os.makedirs(output_dir, exist_ok=True)
        generated  = {}
        copied_map = {}  # demucs_stem → dest path (avoid duplicates)

        # Which components need 2nd-pass processing on their parent stem?
        DRUM_COMPONENTS  = {"kick", "snare", "cymbals"}
        VOCAL_COMPONENTS = {"lead_vocal", "backing_vocals", "vocal_fx"}

        total = len(requested_stems)
        for idx, stem_id in enumerate(requested_stems):
            pct     = 78.0 + (idx / total) * 17.0
            display = STEM_DISPLAY_NAMES.get(stem_id, stem_id)
            _report(callback, pct, f"Exportando {display}…", model, device)

            # Drum sub-components: 2nd-pass IIR filtering on isolated drums stem
            if stem_id in DRUM_COMPONENTS and "drums" in available:
                dst = _extract_drum_component(
                    stem_id, available["drums"], output_dir, display
                )
                if dst:
                    generated[stem_id] = dst
                continue

            # Vocal sub-components: 2nd-pass Mid/Side extraction on isolated vocals stem
            if stem_id in VOCAL_COMPONENTS and "vocals" in available:
                dst = _extract_vocal_component(
                    stem_id, available["vocals"], output_dir, display
                )
                if dst:
                    generated[stem_id] = dst
                continue

            demucs_stem = STEM_TO_DEMUCS.get(stem_id, "other")
            if demucs_stem not in available:
                demucs_stem = "other"
            if demucs_stem not in available:
                continue

            if demucs_stem in copied_map:
                generated[stem_id] = copied_map[demucs_stem]

                continue

            ext = os.path.splitext(available[demucs_stem])[1]
            dst = os.path.join(output_dir, f"{display}{ext}")
            shutil.copy2(available[demucs_stem], dst)
            generated[stem_id] = dst
            copied_map[demucs_stem] = dst

        return generated

    finally:
        shutil.rmtree(tmp_out, ignore_errors=True)


# ──────────────────────────────────────────────────────────────────
# Fallback: spectral heuristic (no ML — for demo / offline)
# ──────────────────────────────────────────────────────────────────

def separate_fallback(input_file: str, output_dir: str,
                      requested_stems: list, device: str, callback) -> dict:
    """
    Frequency-domain heuristic separation using simple IIR filters.
    No ML quality — for demo only. Requires WAV PCM 16-bit input.
    """
    _report(callback, 5.0, "Modo demo (sin Demucs) — separación por frecuencias…",
            "Heurístico", device)

    if not input_file.lower().endswith(".wav"):
        raise RuntimeError(
            "El modo demo solo admite archivos WAV. "
            "Para separación real instala Demucs: pip install numpy demucs"
        )

    channels, sr = _read_wav_pure(input_file)

    def lowpass(sig, hz):
        a = (2 * math.pi * hz / sr) / (2 * math.pi * hz / sr + 1)
        out, prev = [0.0] * len(sig), 0.0
        for i, x in enumerate(sig):
            prev = prev + a * (x - prev); out[i] = prev
        return out

    def highpass(sig, hz):
        lp = lowpass(sig, hz)
        return [sig[i] - lp[i] for i in range(len(sig))]

    def bandpass(sig, lo, hi):
        return highpass(lowpass(sig, hi), lo)

    def scale(sig, f):
        return [x * f for x in sig]

    OPS = {
        "vocals":          lambda ch: bandpass(ch, 200,  4000),
        "lead_vocal":      lambda ch: bandpass(ch, 300,  3500),
        "backing_vocals":  lambda ch: bandpass(ch, 250,  3800),
        "vocal_fx":        lambda ch: scale(bandpass(ch, 1000, 8000), 0.6),
        "noise":           lambda ch: scale(highpass(ch, 8000), 0.5),
        "drums":           lambda ch: highpass(ch, 100),
        "kick":            lambda ch: lowpass(ch, 120),
        "snare":           lambda ch: bandpass(ch, 150,  500),
        "toms":            lambda ch: bandpass(ch, 80,   400),
        "cymbals":         lambda ch: highpass(ch, 5000),
        "bass":            lambda ch: lowpass(ch, 250),
        "guitar_acoustic": lambda ch: bandpass(ch, 80,   5000),
        "guitar_electric": lambda ch: bandpass(ch, 100,  6000),
        "piano":           lambda ch: bandpass(ch, 30,   4200),
        "other":           lambda ch: bandpass(ch, 500,  3000),
    }

    os.makedirs(output_dir, exist_ok=True)
    generated = {}
    total     = len(requested_stems)

    for idx, stem_id in enumerate(requested_stems):
        pct     = 10.0 + (idx / total) * 80.0
        display = STEM_DISPLAY_NAMES.get(stem_id, stem_id)
        _report(callback, pct, f"Procesando {display} (demo)…", "Heurístico", device)

        op          = OPS.get(stem_id, lambda ch: ch)
        out_channels = [op(ch) for ch in channels]
        dst          = os.path.join(output_dir, f"{display}.wav")
        _write_wav_pure(dst, out_channels, sr)
        generated[stem_id] = dst

    return generated


# ──────────────────────────────────────────────────────────────────
# Public engine class (called by cli_runner.py)
# ──────────────────────────────────────────────────────────────────

class LimbusSeparatorEngine:
    def __init__(self, models_dir: str = "", progress_callback=None):
        self.models_dir        = models_dir
        self.progress_callback = progress_callback

    def report_progress(self, pct, stage, model="Demucs", device="CPU"):
        if self.progress_callback:
            self.progress_callback({"type": "progress", "percentage": round(pct, 1),
                                    "stage": stage, "model": model, "device": device})

    def process(self, input_file: str, output_dir: str,
                requested_stems: list, device: str = "Auto") -> dict:
        cb = self.progress_callback

        # Step 1: try to get Demucs working (install if needed)
        _report(cb, 0.5, "Verificando motor de IA…")
        demucs_ready = _ensure_demucs(cb)

        if not demucs_ready:
            _report(cb, 5.0,
                "⚠️ Demucs no disponible — usando separación por frecuencias (demo).\n"
                "Para separación real: pip install numpy demucs",
                "Heurístico", device)
            return separate_fallback(input_file, output_dir, requested_stems, device, cb)

        # Step 2: run Demucs
        _report(cb, 6.0, "Iniciando separación con IA (Demucs)…", "Demucs", device)
        try:
            return separate_with_demucs(input_file, output_dir, requested_stems, device, cb)
        except Exception as e:
            _report(cb, 5.0,
                f"⚠️ Demucs falló ({e}) — usando separación por frecuencias.",
                "Heurístico", device)
            return separate_fallback(input_file, output_dir, requested_stems, device, cb)
