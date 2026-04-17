namespace AudioVisualFilter.Analyses
{
    record AnalysisConfig(
        int FrameSize             = 8192,
        int HopSize               = 441,
        int LpcOrder              = 14,
        int DownsampleFactor      = 4,
        int LpcWindowFrames       = 1,
        double CalibrationSeconds = 2.0)
    {
        public int SpectrumBins  => FrameSize / 2;
        public int LpcWindowSize => FrameSize * LpcWindowFrames;
        public int CalibrationFrames(int sampleRate) =>
            Math.Max(1, (int)(CalibrationSeconds * sampleRate / FrameSize));
    }
}
