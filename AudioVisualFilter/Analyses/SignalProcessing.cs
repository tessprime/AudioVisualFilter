namespace AudioVisualFilter.Analyses
{
    static class SignalProcessing
    {
        // Average-decimation downsampling — each output sample is the mean of `factor` input samples
        public static double[] Downsample(double[] samples, int factor)
        {
            int length = samples.Length / factor;
            var result = new double[length];
            for (int i = 0; i < length; i++)
            {
                double sum = 0;
                for (int j = 0; j < factor; j++)
                    sum += samples[i * factor + j];
                result[i] = sum / factor;
            }
            return result;
        }

        // First-order high-pass pre-emphasis: y[n] = x[n] - coeff * x[n-1]
        public static double[] PreEmphasis(double[] samples, double coeff = 0.97)
        {
            var result = new double[samples.Length];
            result[0] = samples[0];
            for (int i = 1; i < samples.Length; i++)
                result[i] = samples[i] - coeff * samples[i - 1];
            return result;
        }

        // Apply Hamming window in-place
        public static void ApplyHammingWindow(double[] samples)
        {
            var window = MathNet.Numerics.Window.Hamming(samples.Length);
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= window[i];
        }

        // Biased autocorrelation for lags 0..maxLag
        public static double[] Autocorrelation(double[] samples, int maxLag)
        {
            var r = new double[maxLag + 1];
            for (int lag = 0; lag <= maxLag; lag++)
                for (int i = lag; i < samples.Length; i++)
                    r[lag] += samples[i] * samples[i - lag];
            return r;
        }

        public static double Rms(double[] samples)
        {
            double sum = 0;
            foreach (var s in samples) sum += s * s;
            return Math.Sqrt(sum / samples.Length);
        }
    }
}
