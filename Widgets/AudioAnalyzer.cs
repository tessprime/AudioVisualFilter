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
        private const int FrameSize = 8192;
        private const int SpectrumBins = FrameSize / 2;
        private const int CalibrationFrames = 11; // ~2s at 8192/44100
        private const int LpcWindowFrames = 1;
        private const int LpcWindowSize = FrameSize * LpcWindowFrames;

        private readonly List<double> _sampleBuffer = new();
        private readonly List<double> _lpcWindow = new();

        private double[]? _noiseSpectrum;
        private double[]? _noiseAutocorr;
        private double[]? _calibSpectrumAccum;
        private double[]? _calibAutocorrAccum;
        private int _calibFrameCount = -1; // -1 = not started

        public bool IsCalibrating => _calibFrameCount >= 0 && _calibFrameCount < CalibrationFrames;

        public void StartCalibration(int sampleRate)
        {
            _calibSpectrumAccum = new double[SpectrumBins];
            _calibAutocorrAccum = new double[LpcOrder + 1];
            _calibFrameCount = 0;
            _noiseSpectrum = null;
            _noiseAutocorr = null;
            Console.WriteLine($"Noise calibration started (~{CalibrationFrames * FrameSize / (double)sampleRate:F1}s)...");
        }

        public AudioFrame? Analyze(double[] samples, int sampleRate)
        {
            if (_calibFrameCount == -1)
                StartCalibration(sampleRate);

            _sampleBuffer.AddRange(samples);

            AudioFrame? result = null;
            while (_sampleBuffer.Count >= FrameSize)
            {
                var frame = _sampleBuffer.GetRange(0, FrameSize).ToArray();
                _sampleBuffer.RemoveRange(0, FrameSize);

                _lpcWindow.AddRange(frame);
                if (_lpcWindow.Count > LpcWindowSize)
                    _lpcWindow.RemoveRange(0, _lpcWindow.Count - LpcWindowSize);

                var spectrum = ComputeSpectrum(frame, sampleRate);
                var (pitch, confidence) = ComputePitch(frame, sampleRate);
                var lpcSamples = _lpcWindow.Count == LpcWindowSize ? _lpcWindow.ToArray() : frame;
                var formants = ComputeFormants(lpcSamples, sampleRate, confidence);

                if (IsCalibrating)
                {
                    for (int i = 0; i < SpectrumBins; i++)
                        _calibSpectrumAccum![i] += spectrum[i].Magnitude;

                    _calibFrameCount++;
                    if (_calibFrameCount >= CalibrationFrames)
                        FinalizeCalibration();
                }

                double secsRemaining = IsCalibrating
                    ? (CalibrationFrames - _calibFrameCount) * FrameSize / (double)sampleRate
                    : 0.0;
                result = new AudioFrame(frame, sampleRate, spectrum, pitch, confidence, formants, IsCalibrating, secsRemaining);
            }
            return result;
        }

        private void FinalizeCalibration()
        {
            _noiseSpectrum = new double[SpectrumBins];
            for (int i = 0; i < SpectrumBins; i++)
                _noiseSpectrum[i] = _calibSpectrumAccum![i] / CalibrationFrames;

            _noiseAutocorr = new double[LpcOrder + 1];
            for (int lag = 0; lag <= LpcOrder; lag++)
                _noiseAutocorr[lag] = _calibAutocorrAccum![lag] / CalibrationFrames;

            Console.WriteLine("Noise calibration complete.");
        }

        private FrequencyBin[] ComputeSpectrum(double[] samples, int sampleRate)
        {
            var complex = Array.ConvertAll(samples, s => new Complex(s, 0));
            Fourier.Forward(complex, FourierOptions.AsymmetricScaling);
            var bins = new FrequencyBin[SpectrumBins];
            for (int i = 0; i < SpectrumBins; i++)
            {
                double magnitude = complex[i].Magnitude;
                if (_noiseSpectrum != null)
                    magnitude = Math.Max(magnitude - _noiseSpectrum[i], 0.0);
                bins[i] = new FrequencyBin
                {
                    Frequency = i * sampleRate / (double)FrameSize,
                    Magnitude = magnitude
                };
            }
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

        private double[] ComputeFormants(double[] samples, int sampleRate, double pitchConfidence)
        {
            const double RmsThreshold = 0.01;
            double rms = Math.Sqrt(samples.Average(s => s * s));
            if (rms < RmsThreshold || pitchConfidence < ConfidenceThreshold)
                return Array.Empty<double>();

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

            // Accumulate autocorrelation during calibration
            if (IsCalibrating)
            {
                for (int lag = 0; lag <= LpcOrder; lag++)
                    _calibAutocorrAccum![lag] += r[lag];
            }

            // Subtract noise autocorrelation
            if (_noiseAutocorr != null)
            {
                r[0] = Math.Max(r[0] - _noiseAutocorr[0], 1e-10);
                for (int lag = 1; lag <= LpcOrder; lag++)
                    r[lag] -= _noiseAutocorr[lag];
            }

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
