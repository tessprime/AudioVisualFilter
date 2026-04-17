namespace AudioVisualFilter.Widgets
{
    class AudioFrame
    {
        public double[] Samples { get; }
        public int SampleRate { get; }
        public FrequencyBin[] Spectrum { get; }
        public double Pitch { get; }
        public double PitchConfidence { get; }
        public double[] Formants { get; }
        public bool IsCalibrating { get; }
        public double CalibrationSecondsRemaining { get; }
        public double[]? LpcCoefficients { get; }
        public int LpcSampleRate { get; }

        public AudioFrame(double[] samples, int sampleRate, FrequencyBin[] spectrum, double pitch, double pitchConfidence, double[] formants, bool isCalibrating, double calibrationSecondsRemaining, double[]? lpcCoefficients = null, int lpcSampleRate = 0)
        {
            Samples = samples;
            SampleRate = sampleRate;
            Spectrum = spectrum;
            Pitch = pitch;
            PitchConfidence = pitchConfidence;
            Formants = formants;
            IsCalibrating = isCalibrating;
            CalibrationSecondsRemaining = calibrationSecondsRemaining;
            LpcCoefficients = lpcCoefficients;
            LpcSampleRate = lpcSampleRate;
        }
    }
}
