"""
generate_synthetic.py
Generates synthetic voiced speech WAV files with known formants.

The signal is a harmonic series (glottal buzz) passed through a cascade of
resonant IIR filters — one biquad per formant. Because the signal is exactly
all-pole by construction, LPC should recover the formant frequencies closely.

Usage:
    python generate_synthetic.py                  # writes built-in presets
    python generate_synthetic.py --list           # list available vowels
    python generate_synthetic.py --vowel a --out out.wav

Requires: numpy, scipy
    pip install numpy scipy
"""

import argparse
import numpy as np
from scipy.io import wavfile
from scipy.signal import lfilter

SAMPLE_RATE = 44100
DURATION    = 2.0       # seconds
F0          = 120.0     # fundamental (Hz) — typical male voice

# Approximate formant targets for English vowels (Hz)
# (F1, F2, F3) with bandwidths (B1, B2, B3)
VOWEL_PRESETS = {
    #       F1    F2    F3    B1   B2   B3
    "a":  (800,  1200, 2500, 80,  90,  120),
    "e":  (400,  2000, 2600, 70,  100, 120),
    "i":  (240,  2400, 3000, 60,  80,  100),
    "o":  (500,  800,  2800, 80,  80,  120),
    "u":  (300,  700,  2200, 60,  80,  110),
    "ae": (700,  1800, 2500, 80,  100, 120),  # /æ/ as in "cat"
}


def harmonic_source(f0, duration, sample_rate, n_harmonics=40):
    """Sum of harmonics with 1/k amplitude (approximates glottal source)."""
    t = np.linspace(0, duration, int(sample_rate * duration), endpoint=False)
    signal = np.zeros_like(t)
    for k in range(1, n_harmonics + 1):
        if k * f0 >= sample_rate / 2:
            break
        signal += (1.0 / k) * np.sin(2 * np.pi * k * f0 * t)
    return signal


def apply_formant(signal, freq, bandwidth, sample_rate):
    """Apply a single resonant biquad (all-pole) filter for one formant."""
    r     = np.exp(-np.pi * bandwidth / sample_rate)
    theta = 2 * np.pi * freq / sample_rate
    # H(z) = 1 / (1 - 2r*cos(θ)*z⁻¹ + r²*z⁻²)
    b = [1.0]
    a = [1.0, -2 * r * np.cos(theta), r ** 2]
    return lfilter(b, a, signal)


def generate(f1, f2, f3, b1, b2, b3, f0=F0, duration=DURATION, sample_rate=SAMPLE_RATE):
    """Generate a synthetic vowel with the given formants and bandwidths."""
    signal = harmonic_source(f0, duration, sample_rate)
    signal = apply_formant(signal, f1, b1, sample_rate)
    signal = apply_formant(signal, f2, b2, sample_rate)
    signal = apply_formant(signal, f3, b3, sample_rate)

    # Normalize to 16-bit range with some headroom
    peak = np.max(np.abs(signal))
    if peak > 0:
        signal = signal / peak * 0.9
    return (signal * 32767).astype(np.int16)


def write_wav(path, samples, sample_rate=SAMPLE_RATE):
    wavfile.write(path, sample_rate, samples)
    print(f"Wrote {path}  (formants embedded in filename metadata below)")


def main():
    parser = argparse.ArgumentParser(description="Generate synthetic vowel WAV files")
    parser.add_argument("--vowel",  help="Vowel preset name (see --list)")
    parser.add_argument("--out",    help="Output WAV path (default: <vowel>.wav)")
    parser.add_argument("--list",   action="store_true", help="List available vowels")
    parser.add_argument("--f0",     type=float, default=F0,       help="Fundamental frequency (Hz)")
    parser.add_argument("--dur",    type=float, default=DURATION,  help="Duration (seconds)")
    parser.add_argument("--all",    action="store_true", help="Generate all presets")
    args = parser.parse_args()

    if args.list:
        print("Available vowel presets:")
        for name, (f1, f2, f3, b1, b2, b3) in VOWEL_PRESETS.items():
            print(f"  {name:4s}  F1={f1} F2={f2} F3={f3} Hz  (B={b1}/{b2}/{b3} Hz)")
        return

    if args.all:
        for name, params in VOWEL_PRESETS.items():
            f1, f2, f3, b1, b2, b3 = params
            samples = generate(f1, f2, f3, b1, b2, b3, f0=args.f0, duration=args.dur)
            write_wav(f"vowel_{name}.wav", samples)
            print(f"  Expected: F1={f1} F2={f2} F3={f3} Hz")
        return

    if args.vowel:
        if args.vowel not in VOWEL_PRESETS:
            print(f"Unknown vowel '{args.vowel}'. Use --list to see options.")
            return
        f1, f2, f3, b1, b2, b3 = VOWEL_PRESETS[args.vowel]
        out = args.out or f"vowel_{args.vowel}.wav"
        samples = generate(f1, f2, f3, b1, b2, b3, f0=args.f0, duration=args.dur)
        write_wav(out, samples)
        print(f"  Expected: F1={f1} F2={f2} F3={f3} Hz")
        return

    parser.print_help()


if __name__ == "__main__":
    main()
