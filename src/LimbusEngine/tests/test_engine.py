import unittest
import os
import sys
import wave
import struct

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from separator import LimbusSeparatorEngine, read_wav_file_pure, write_wav_file_pure

class TestLimbusEngine(unittest.TestCase):
    def setUp(self):
        self.test_wav = "test_unit_mix.wav"
        self.output_dir = "test_unit_out"
        
        fs = 44100
        n_samples = 44100 * 2 # 2 seconds
        
        with wave.open(self.test_wav, 'wb') as wf:
            wf.setnchannels(2)
            wf.setsampwidth(2)
            wf.setframerate(fs)
            frames = []
            for i in range(n_samples):
                sample = int(0.5 * 32767.0 * (i / n_samples))
                frames.append(struct.pack('<hh', sample, sample))
            wf.writeframes(b''.join(frames))

    def tearDown(self):
        if os.path.exists(self.test_wav):
            os.remove(self.test_wav)
        if os.path.exists(self.output_dir):
            import shutil
            shutil.rmtree(self.output_dir, ignore_errors=True)

    def test_residual_math_reconstruction(self):
        engine = LimbusSeparatorEngine()
        results = engine.process(self.test_wav, self.output_dir, ["vocals", "drums", "bass", "other"], device="CPU")
        
        self.assertIn("vocals", results)
        self.assertIn("drums", results)
        self.assertIn("bass", results)
        self.assertIn("other", results)

        mix_data, _ = read_wav_file_pure(self.test_wav)
        voc_data, _ = read_wav_file_pure(results["vocals"])
        drum_data, _ = read_wav_file_pure(results["drums"])
        bass_data, _ = read_wav_file_pure(results["bass"])
        oth_data, _ = read_wav_file_pure(results["other"])

        n_ch = len(mix_data)
        n_samples = len(mix_data[0])

        for ch in range(n_ch):
            for i in range(n_samples):
                recon = voc_data[ch][i] + drum_data[ch][i] + bass_data[ch][i] + oth_data[ch][i]
                diff = abs(mix_data[ch][i] - recon)
                self.assertLess(diff, 1e-4, f"Reconstruction diff exceeded tolerance at ch {ch}, sample {i}")

if __name__ == "__main__":
    unittest.main()
