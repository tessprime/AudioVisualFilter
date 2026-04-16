using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Statistics;

namespace AudioVisualFilter.Widgets
{
    class AudioAnalyzer
    {
        private const double ConfidenceThreshold = 0.7;
        private const int AutocorrelationSkip = 50;
        private const int LpcOrder = 14;

        public AudioFrame Analyze(double[] samples, int sampleRate)
        {
            var spectrum = ComputeSpectrum(samples, sampleRate);
            var (pitch, confidence) = ComputePitch(samples, sampleRate);
            var formants = ComputeFormants(samples, sampleRate);
            return new AudioFrame(samples, sampleRate, spectrum, pitch, confidence, formants);
        }

        private FrequencyBin[] ComputeSpectrum(double[] samples, int sampleRate)
        {
            var complex = Array.ConvertAll(samples, s => new Complex(s, 0));
            Fourier.Forward(complex, FourierOptions.AsymmetricScaling);
            var bins = new FrequencyBin[complex.Length / 2];
            for (int i = 0; i < bins.Length; i++)
                bins[i] = new FrequencyBin
                {
                    Frequency = i * sampleRate / (double)complex.Length,
                    Magnitude = complex[i].Magnitude
                };
            return bins;
        }

        private (double pitch, double confidence) ComputePitch(double[] samples, int sampleRate)
        {
            var data = (double[])samples.Clone();
            var hann = MathNet.Numerics.Window.Hann(data.Length);
            for (int i = 1; i < data.Length; i++)
                data[i] = data[i] - 0.97 * data[i - 1];
            for (int i = 0; i < data.Length; i++)
                data[i] *= hann[i];

            var correlation = Correlation.Auto(data);
            int index = MaxCorrelationIndex(correlation, AutocorrelationSkip);
            double frequency = sampleRate / (double)index;
            double confidence = correlation[index];

            Console.ForegroundColor = confidence > ConfidenceThreshold && frequency > 180 ? ConsoleColor.Green
                : confidence > ConfidenceThreshold ? ConsoleColor.Red : ConsoleColor.White;
            Console.WriteLine($"Index: {index}, Frequency: {frequency} Strength: {confidence}");
            Console.ResetColor();

            if (index <= AutocorrelationSkip || confidence < ConfidenceThreshold)
                return (0.0, 0.0);
            return (frequency, confidence);
        }

        private int MaxCorrelationIndex(double[] array, int skip)
        {
            int maxIdx = skip;
            double maxVal = array[skip];
            for (int i = skip + 1; i < array.Length; i++)
            {
                if (array[i] > maxVal) { maxVal = array[i]; maxIdx = i; }
            }
            return maxIdx;
        }

        private double[] ComputeFormants(double[] samples, int sampleRate)
        {
            // Pre-emphasis + Hamming window
            var data = new double[samples.Length];
            data[0] = samples[0];
            for (int i = 1; i < samples.Length; i++)
                data[i] = samples[i] - 0.97 * samples[i - 1];
            var window = MathNet.Numerics.Window.Hamming(data.Length);
            for (int i = 0; i < data.Length; i++)
                data[i] *= window[i];

            // Autocorrelation lags 0..LpcOrder
            var r = new double[LpcOrder + 1];
            for (int lag = 0; lag <= LpcOrder; lag++)
                for (int i = lag; i < data.Length; i++)
                    r[lag] += data[i] * data[i - lag];

            var a = LevinsonDurbin(r, LpcOrder);

            // Build polynomial: z^N + a[0]*z^(N-1) + ... + a[N-1]
            // MathNet Polynomial expects ascending degree: coeffs[0] = constant term
            var coeffs = new double[LpcOrder + 1];
            coeffs[LpcOrder] = 1.0;
            for (int i = 0; i < LpcOrder; i++)
                coeffs[i] = a[LpcOrder - 1 - i];

            var roots = new MathNet.Numerics.Polynomial(coeffs).Roots();

            var formants = new List<double>();
            foreach (var root in roots)
            {
                if (root.Imaginary <= 0) continue;
                double freq = Math.Atan2(root.Imaginary, root.Real) * sampleRate / (2 * Math.PI);
                if (freq >= 90 && freq <= 5500)
                    formants.Add(freq);
            }
            formants.Sort();
            return formants.ToArray();
        }

        private static double[] LevinsonDurbin(double[] r, int order)
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
    }
}
