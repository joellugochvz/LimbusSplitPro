import numpy as np
from scipy.signal import butter, filtfilt

def separate_vocal_components(vocal_audio: np.ndarray, sample_rate: int) -> dict:
    """
    Decomposes a vocal stem into Lead Vocal, Backing Vocals, Vocal FX/Reverb, and Noise/Artifacts.
    Utiliza descomposición Mid/Side (Centro/Laterales), filtrado espectral y estimación de reverberación.
    
    vocal_audio: float32 numpy array of shape (channels, n_samples)
    returns: dict of stem_name -> numpy array of same shape
    """
    if vocal_audio.ndim == 1:
        vocal_audio = vocal_audio[np.newaxis, :]

    channels, n_samples = vocal_audio.shape
    
    lead_vocal = np.zeros_like(vocal_audio)
    backing_vocals = np.zeros_like(vocal_audio)
    vocal_fx = np.zeros_like(vocal_audio)
    noise = np.zeros_like(vocal_audio)

    if channels >= 2:
        # Mid/Side Processing
        left = vocal_audio[0]
        right = vocal_audio[1]
        
        mid = 0.5 * (left + right)   # Center (Lead vocal predominantly)
        side = 0.5 * (left - right)  # Stereo sides (Backing vocals & Reverb)

        # High pass filter for noise residual (>10kHz low energy sibilance/noise)
        b_hp, a_hp = butter(4, 10000.0 / (0.5 * sample_rate), btype='high')
        noise_est = filtfilt(b_hp, a_hp, mid) * 0.3

        # Lead Vocal is Mid minus high noise
        lead_mid = mid - noise_est
        
        lead_vocal[0] = lead_mid
        lead_vocal[1] = lead_mid

        # Backing Vocals from Side channel (pan stereo)
        backing_vocals[0] = side
        backing_vocals[1] = -side

        # Vocal FX (Reverb tail estimation from difference between envelope and transient)
        reverb_est = (vocal_audio - lead_vocal - backing_vocals) * 0.7
        vocal_fx[0] = reverb_est[0]
        vocal_fx[1] = reverb_est[1]

        noise[0] = noise_est
        noise[1] = noise_est
    else:
        # Mono vocal stem decomposition
        sig = vocal_audio[0]
        b_lp, a_lp = butter(4, 3000.0 / (0.5 * sample_rate), btype='low')
        lead_vocal[0] = filtfilt(b_lp, a_lp, sig)
        
        b_hp, a_hp = butter(4, 3000.0 / (0.5 * sample_rate), btype='high')
        high_part = filtfilt(b_hp, a_hp, sig)
        
        backing_vocals[0] = high_part * 0.6
        vocal_fx[0] = high_part * 0.3
        noise[0] = high_part * 0.1

    # Guarantee sum reconstruction
    total_reconstructed = lead_vocal + backing_vocals + vocal_fx + noise
    residual = vocal_audio - total_reconstructed
    lead_vocal += residual * 0.5
    vocal_fx += residual * 0.5

    return {
        "lead_vocal": lead_vocal.astype(np.float32),
        "backing_vocals": backing_vocals.astype(np.float32),
        "vocal_fx": vocal_fx.astype(np.float32),
        "noise": noise.astype(np.float32)
    }
