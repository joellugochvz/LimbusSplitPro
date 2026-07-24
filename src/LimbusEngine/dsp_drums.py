import numpy as np
from scipy.signal import butter, filtfilt, hilbert

def butter_bandpass(lowcut, highcut, fs, order=4):
    nyq = 0.5 * fs
    low = lowcut / nyq
    high = highcut / nyq
    b, a = butter(order, [low, high], btype='band')
    return b, a

def butter_lowpass(cutoff, fs, order=4):
    nyq = 0.5 * fs
    normal_cutoff = cutoff / nyq
    b, a = butter(order, normal_cutoff, btype='low', analog=False)
    return b, a

def butter_highpass(cutoff, fs, order=4):
    nyq = 0.5 * fs
    normal_cutoff = cutoff / nyq
    b, a = butter(order, normal_cutoff, btype='high', analog=False)
    return b, a

def separate_sub_drums(drums_audio: np.ndarray, sample_rate: int) -> dict:
    """
    Decomposes a full drum stem into Kick, Snare, Toms, Hi-Hats/Cymbals using
    frequency-band transient filtering and envelope weighting.
    Garantiza alineación de muestras exacta y conservación del material.
    
    drums_audio: float32 numpy array of shape (channels, n_samples)
    returns: dict of stem_name -> numpy array of same shape
    """
    if drums_audio.ndim == 1:
        drums_audio = drums_audio[np.newaxis, :]

    channels, n_samples = drums_audio.shape
    kick = np.zeros_like(drums_audio)
    snare = np.zeros_like(drums_audio)
    toms = np.zeros_like(drums_audio)
    cymbals = np.zeros_like(drums_audio)

    # 1. Kick: Sub-bass & low frequencies (20Hz - 120Hz)
    b_kick, a_kick = butter_bandpass(20.0, 130.0, sample_rate, order=3)
    # 2. Toms: Low-mid punch (130Hz - 350Hz)
    b_toms, a_toms = butter_bandpass(130.0, 350.0, sample_rate, order=3)
    # 3. Snare: Mid transient & body (200Hz - 2500Hz)
    b_snare, a_snare = butter_bandpass(200.0, 2500.0, sample_rate, order=3)
    # 4. Cymbals / Hi-Hats: High frequency (2500Hz - Nyquist)
    b_cym, a_cym = butter_highpass(2500.0, sample_rate, order=3)

    for ch in range(channels):
        sig = drums_audio[ch]
        
        k_band = filtfilt(b_kick, a_kick, sig)
        t_band = filtfilt(b_toms, a_toms, sig)
        s_band = filtfilt(b_snare, a_snare, sig)
        c_band = filtfilt(b_cym, a_cym, sig)

        # Transient envelope separation
        env_k = np.abs(hilbert(k_band))
        env_s = np.abs(hilbert(s_band))
        env_c = np.abs(hilbert(c_band))

        sum_env = env_k + env_s + env_c + 1e-8
        mask_k = env_k / sum_env
        mask_s = env_s / sum_env
        mask_c = env_c / sum_env

        kick[ch] = k_band + (sig * 0.4 * mask_k)
        toms[ch] = t_band
        snare[ch] = s_band * mask_s
        cymbals[ch] = c_band + (s_band * (1.0 - mask_s))

    # Normalize energy to match original drum stem sum
    recon = kick + snare + toms + cymbals
    diff = drums_audio - recon
    # Assign residual energy to cymbals/snare evenly to avoid silence or energy loss
    cymbals += diff * 0.5
    snare += diff * 0.5

    return {
        "kick": kick.astype(np.float32),
        "snare": snare.astype(np.float32),
        "toms": toms.astype(np.float32),
        "cymbals": cymbals.astype(np.float32)
    }
