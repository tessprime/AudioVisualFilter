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
        private const int FrameSize = 2048;

        private readonly List<double> _sampleBuffer = new();

        public AudioFrame? Analyze(double[] samples, int sampleRate)
        {
            _sampleBuffer.AddRange(samples);

            AudioFrame? result = null;
            while (_sampleBuffer.Count >= FrameSize)
            {
                var frame = _sampleBuffer.GetRange(0, FrameSize).ToArray();
                _sampleBuffer.RemoveRange(0, FrameSize);

                var spectrum = ComputeSpectrum(frame, sampleRate);
                var (pitch, confidence) = ComputePitch(frame, sampleRate);
                var formants = ComputeFormants(frame, sampleRate);
                result = new AudioFrame(frame, sampleRate, spectrum, pitch, confidence, formants);
            }
            return result;
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
            // Downsample by 4 (44100 → ~11025 Hz) with averaging for anti-aliasing
            const int downsampleFactor = 4;
            int dsLength = samples.Length / downsampleFactor;
            int dsRate = sampleRate / downsampleFactor;
            var ds = new double[dsLength];
            for (int i = 0; i < dsLength; i++)
            {
                double sum = 0;
                for (int j = 0; j < downsampleFactor; j++)
                    sum += samples[i * downsampleFactor + j];
                ds[i] = sum / downsampleFactor;
            }

            // Pre-emphasis + Hamming window on downsampled signal
            var data = new double[dsLength];
            data[0] = ds[0];
            for (int i = 1; i < dsLength; i++)
                data[i] = ds[i] - 0.97 * ds[i - 1];
            var window = MathNet.Numerics.Window.Hamming(dsLength);
            for (int i = 0; i < dsLength; i++)
                data[i] *= window[i];

            // Autocorrelation lags 0..LpcOrder
            var r = new double[LpcOrder + 1];
            for (int lag = 0; lag <= LpcOrder; lag++)
                for (int i = lag; i < dsLength; i++)
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
                double freq = Math.Atan2(root.Imaginary, root.Real) * dsRate / (2 * Math.PI);
                if (freq >= 90 && freq <= 5500)
                    formants.Add(freq);
            }
            formants.Sort();
            //Console.WriteLine($"Formants: [{string.Join(", ", formants.Select(f => f.ToString("F0")))}]");
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
