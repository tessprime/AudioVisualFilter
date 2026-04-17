using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Statistics;
using AudioVisualFilter.Analyses;

namespace AudioVisualFilter.Widgets
{
    class AudioAnalyzer
    {
        private const double ConfidenceThreshold = 0.7;
        private const int AutocorrelationSkip = 50;
        private const int FrameSize = 8192;
        private const int SpectrumBins = FrameSize / 2;
        private const int CalibrationFrames = 11; // ~2s at 8192/44100

        private readonly List<double> _sampleBuffer = new();

        private double[]? _noiseSpectrum;
        private double[]? _noiseAutocorr;
        private double[]? _calibSpectrumAccum;
        private double[]? _calibAutocorrAccum;
        private int _calibFrameCount = -1;

        public bool IsCalibrating => _calibFrameCount >= 0 && _calibFrameCount < CalibrationFrames;

        public void StartCalibration(int sampleRate)
        {
            _calibSpectrumAccum = new double[SpectrumBins];
            _calibAutocorrAccum = new double[LpcAnalysis.DefaultOrder + 1];
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

                var spectrum = ComputeSpectrum(frame, sampleRate);
                var (pitch, confidence) = ComputePitch(frame, sampleRate);
                var (formants, lpcCoeffs, lpcRate) = ComputeFormants(frame, sampleRate, confidence);

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
                result = new AudioFrame(frame, sampleRate, spectrum, pitch, confidence, formants, IsCalibrating, secsRemaining, lpcCoeffs, lpcRate);
            }
            return result;
        }

        private void FinalizeCalibration()
        {
            _noiseSpectrum = new double[SpectrumBins];
            for (int i = 0; i < SpectrumBins; i++)
                _noiseSpectrum[i] = _calibSpectrumAccum![i] / CalibrationFrames;

            _noiseAutocorr = new double[LpcAnalysis.DefaultOrder + 1];
            for (int lag = 0; lag <= LpcAnalysis.DefaultOrder; lag++)
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

        private (double[] formants, double[]? lpcCoeffs, int lpcRate) ComputeFormants(
            double[] samples, int sampleRate, double pitchConfidence)
        {
            const double RmsThreshold = 0.01;
            if (SignalProcessing.Rms(samples) < RmsThreshold || pitchConfidence < ConfidenceThreshold)
                return (Array.Empty<double>(), null, 0);

            // Accumulate noise autocorrelation during calibration (on downsampled signal)
            if (IsCalibrating)
            {
                var ds = SignalProcessing.Downsample(samples, LpcAnalysis.DefaultDownsampleFactor);
                var preemph = SignalProcessing.PreEmphasis(ds);
                SignalProcessing.ApplyHammingWindow(preemph);
                var rNoise = SignalProcessing.Autocorrelation(preemph, LpcAnalysis.DefaultOrder);
                for (int lag = 0; lag <= LpcAnalysis.DefaultOrder; lag++)
                    _calibAutocorrAccum![lag] += rNoise[lag];
            }

            var (a, formants, dsRate) = LpcAnalysis.Analyze(samples, sampleRate);

            // Subtract noise autocorrelation if calibrated
            // (Applied before Levinson-Durbin via re-running with adjusted r)
            if (_noiseAutocorr != null)
            {
                var ds = SignalProcessing.Downsample(samples, LpcAnalysis.DefaultDownsampleFactor);
                var preemph = SignalProcessing.PreEmphasis(ds);
                SignalProcessing.ApplyHammingWindow(preemph);
                var r = SignalProcessing.Autocorrelation(preemph, LpcAnalysis.DefaultOrder);
                r[0] = Math.Max(r[0] - _noiseAutocorr[0], 1e-10);
                for (int lag = 1; lag <= LpcAnalysis.DefaultOrder; lag++)
                    r[lag] -= _noiseAutocorr[lag];
                a = LpcAnalysis.LevinsonDurbin(r, LpcAnalysis.DefaultOrder);
                formants = LpcAnalysis.ExtractFormants(a, dsRate);
            }

            return (formants, a, dsRate);
        }
    }
}
