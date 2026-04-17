namespace AudioVisualFilter.Analyses
{
    static class LpcAnalysis
    {
        // Full LPC pipeline: downsample → pre-emphasis → Hamming window → autocorrelation → Levinson-Durbin → formants
        // Returns the LPC coefficients, extracted formants, and the effective sample rate after downsampling.
        public static (double[] Coefficients, double[] Formants, int SampleRate) Analyze(
            double[] samples,
            int sampleRate,
            int order = 14,
            int downsampleFactor = 4)
        {
            var ds = SignalProcessing.Downsample(samples, downsampleFactor);
            int dsRate = sampleRate / downsampleFactor;

            var data = SignalProcessing.PreEmphasis(ds);
            SignalProcessing.ApplyHammingWindow(data);

            var r = SignalProcessing.Autocorrelation(data, order);
            var a = LevinsonDurbin(r, order);
            var formants = ExtractFormants(a, dsRate);

            return (a, formants, dsRate);
        }

        // Levinson-Durbin recursion: given autocorrelation r[0..order], returns LPC coefficients a[0..order-1]
        public static double[] LevinsonDurbin(double[] r, int order)
        {
            var a = new double[order];
            var aPrev = new double[order];
            double error = r[0];

            for (int i = 0; i < order; i++)
            {
                double lambda = r[i + 1];
                for (int j = 0; j < i; j++)
                    lambda += a[j] * r[i - j];
                lambda = -lambda / error;

                Array.Copy(a, aPrev, i);
                a[i] = lambda;
                for (int j = 0; j < i; j++)
                    a[j] = aPrev[j] + lambda * aPrev[i - 1 - j];

                error *= 1.0 - lambda * lambda;
                if (error <= 0) break;
            }
            return a;
        }

        // Find formant frequencies from LPC coefficients via polynomial root finding.
        // Roots with positive imaginary part whose angle maps to [minHz, maxHz] are formants.
        public static double[] ExtractFormants(double[] lpcCoeffs, int sampleRate, double minHz = 90, double maxHz = 5500)
        {
            int order = lpcCoeffs.Length;

            // Build polynomial z^N + a[0]*z^(N-1) + ... + a[N-1]
            // MathNet Polynomial expects ascending degree: coeffs[0] = constant term
            var coeffs = new double[order + 1];
            coeffs[order] = 1.0;
            for (int i = 0; i < order; i++)
                coeffs[i] = lpcCoeffs[order - 1 - i];

            var roots = new MathNet.Numerics.Polynomial(coeffs).Roots();

            var formants = new List<double>();
            foreach (var root in roots)
            {
                if (root.Imaginary <= 0) continue;
                double freq = Math.Atan2(root.Imaginary, root.Real) * sampleRate / (2 * Math.PI);
                if (freq >= minHz && freq <= maxHz)
                    formants.Add(freq);
            }
            formants.Sort();
            return formants.ToArray();
        }
    }
}
