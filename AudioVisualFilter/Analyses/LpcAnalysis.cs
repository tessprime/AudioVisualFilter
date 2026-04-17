namespace AudioVisualFilter.Analyses
{
    public enum LpcMethod { Burg, LevinsonDurbin }

    static class LpcAnalysis
    {
        // Full LPC pipeline → formants.
        // Both methods downsample to ~11025 Hz first, matching Praat's internal behaviour:
        // Praat resamples to 2×maxFormant (=11000 Hz) before running Burg (Sound_to_Formant.cpp:276).
        //
        // Remaining differences vs Praat that can cause slot mismatches on synthetic vowels:
        //   - Sample rate:   we use 44100/4=11025 Hz; Praat uses exactly 11000 Hz
        //   - Pre-emphasis:  we use fixed 1-0.97z⁻¹; Praat uses a frequency-dependent formula from 50 Hz
        //   - Windowing:     Praat applies a Gaussian window before Burg; we do not
        //   - Frame size:    we use LpcFrameSize (~23ms); Praat uses 25ms
        // Use nearest-neighbour formant matching (not slot-by-slot) when comparing against Praat output.
        public static (double[] Coefficients, double[] Formants, int SampleRate) Analyze(
            double[] samples,
            int sampleRate,
            int order = 14,
            int downsampleFactor = 4,
            LpcMethod method = LpcMethod.Burg)
        {
            double[] a;
            int effectiveRate;

            // Downsample first — matches Praat's internal resample to 2×maxFormant.
            var ds = SignalProcessing.Downsample(samples, downsampleFactor);
            effectiveRate = sampleRate / downsampleFactor;
            var preemph = SignalProcessing.PreEmphasis(ds);

            if (method == LpcMethod.Burg)
            {
                a = Burg(preemph, order);
            }
            else
            {
                SignalProcessing.ApplyHammingWindow(preemph);
                var r = SignalProcessing.Autocorrelation(preemph, order);
                a = LevinsonDurbin(r, order);
            }

            var formants = ExtractFormants(a, effectiveRate);
            return (a, formants, effectiveRate);
        }

        // Burg algorithm: fits AR coefficients by minimizing forward+backward prediction error.
        // More stable than autocorrelation/Levinson on short frames; no windowing needed.
        public static double[] Burg(double[] x, int order)
        {
            int n = x.Length;
            var a   = new double[order];
            var f   = (double[])x.Clone();   // forward  prediction errors
            var b   = (double[])x.Clone();   // backward prediction errors

            for (int m = 0; m < order; m++)
            {
                // Burg reflection coefficient
                double num = 0, den = 0;
                for (int i = m + 1; i < n; i++)
                {
                    num += f[i] * b[i - 1];
                    den += f[i] * f[i] + b[i - 1] * b[i - 1];
                }
                double km = den > 0 ? -2.0 * num / den : 0.0;

                // Update AR coefficients (Levinson update step)
                var aPrev = (double[])a.Clone();
                a[m] = km;
                for (int i = 0; i < m; i++)
                    a[i] = aPrev[i] + km * aPrev[m - 1 - i];

                // Update forward and backward errors
                var fNew = new double[n];
                var bNew = new double[n];
                for (int i = m + 1; i < n; i++)
                {
                    fNew[i] = f[i] + km * b[i - 1];
                    bNew[i] = b[i - 1] + km * f[i];
                }
                f = fNew;
                b = bNew;
            }
            return a;
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
