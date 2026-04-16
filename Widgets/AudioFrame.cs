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

        public AudioFrame(double[] samples, int sampleRate, FrequencyBin[] spectrum, double pitch, double pitchConfidence, double[] formants)
        {
            Samples = samples;
            SampleRate = sampleRate;
            Spectrum = spectrum;
            Pitch = pitch;
            PitchConfidence = pitchConfidence;
            Formants = formants;
        }
    }
}
