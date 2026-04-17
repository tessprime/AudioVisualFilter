namespace AudioVisualFilter.Analyses
{
    public record AnalysisConfig(
        int FrameSize             = 8192,
        int HopSize               = 441,
        int LpcOrder              = 14,         // at downsampled rate (~11025 Hz with 4x downsample); ~sampleRate/1000+2
        int DownsampleFactor      = 4,          // used only by LevinsonDurbin
        int LpcFrameSize          = 1024,       // ~23ms at 44100 Hz, close to Praat's 25ms window
        LpcMethod Method          = LpcMethod.Burg,
        double CalibrationSeconds = 2.0)
    {
        public int SpectrumBins => FrameSize / 2;
        public int CalibrationFrames(int sampleRate) =>
            Math.Max(1, (int)(CalibrationSeconds * sampleRate / FrameSize));
    }
}
