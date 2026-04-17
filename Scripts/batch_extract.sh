#!/usr/bin/env bash
# batch_extract.sh
# Runs Praat formant extraction on all WAV files in a directory.
#
# Usage:
#   ./batch_extract.sh <wav_dir> <output_dir> [praat_path]
#
# Example:
#   ./batch_extract.sh ./wavs ./formants "/c/Program Files/Praat/Praat.exe"

WAV_DIR="${1:?Usage: $0 <wav_dir> <output_dir> [praat_path]}"
OUT_DIR="${2:?Usage: $0 <wav_dir> <output_dir> [praat_path]}"
PRAAT="${3:-praat}"
SCRIPT="$(dirname "$0")/extract_formants.praat"

mkdir -p "$OUT_DIR"

for wav in "$WAV_DIR"/*.wav; do
    [ -f "$wav" ] || { echo "No WAV files found in $WAV_DIR"; exit 1; }
    base="$(basename "$wav" .wav)"
    out="$OUT_DIR/$base.csv"
    echo "Analyzing: $wav -> $out"
    "$PRAAT" --run "$SCRIPT" "$wav" "$out"
done

echo "Done. CSVs written to $OUT_DIR"
