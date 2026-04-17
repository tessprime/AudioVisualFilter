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

        private readonly AnalysisConfig _config;
        private readonly List<double> _sampleBuffer = new();
        private readonly List<double> _lpcWindow    = new();

        private double[]? _noiseSpectrum;
        private double[]? _noiseAutocorr;
        private double[]? _calibSpectrumAccum;
        private double[]? _calibAutocorrAccum;
        private int _calibFrameCount = -1;
        private int _calibFramesNeeded;

        public bool IsCalibrating => _calibFrameCount >= 0 && _calibFrameCount < _calibFramesNeeded;

        public AudioAnalyzer(AnalysisConfig? config = null)
        {
            _config = config ?? new AnalysisConfig();
        }

        public void StartCalibration(int sampleRate)
        {
            _calibFramesNeeded    = _config.CalibrationFrames(sampleRate);
            _calibSpectrumAccum   = new double[_config.SpectrumBins];
            _calibAutocorrAccum   = new double[_config.LpcOrder + 1];
            _calibFrameCount      = 0;
            _noiseSpectrum        = null;
            _noiseAutocorr        = null;
            Console.WriteLine($"Noise calibration started (~{_config.CalibrationSeconds:F1}s)...");
        }

        public AudioFrame? Analyze(double[] samples, int sampleRate)
        {
            if (_calibFrameCount == -1)
                StartCalibration(sampleRate);

            _sampleBuffer.AddRange(samples);

            AudioFrame? result = null;
            while (_sampleBuffer.Count >= _config.FrameSize)
            {
                var frame = _sampleBuffer.GetRange(0, _config.FrameSize).ToArray();
                _sampleBuffer.RemoveRange(0, _config.FrameSize);

                _lpcWindow.AddRange(frame);
                if (_lpcWindow.Count > _config.LpcWindowSize)
                    _lpcWindow.RemoveRange(0, _lpcWindow.Count - _config.LpcWindowSize);

                var spectrum = ComputeSpectrum(frame, sampleRate);
                var (pitch, confidence) = ComputePitch(frame, sampleRate);
                var lpcSamples = _lpcWindow.Count == _config.LpcWindowSize ? _lpcWindow.ToArray() : frame;
                var (formants, lpcCoeffs, lpcRate) = ComputeFormants(lpcSamples, sampleRate, confidence);

                if (IsCalibrating)
                {
                    for (int i = 0; i < _config.SpectrumBins; i++)
                        _calibSpectrumAccum![i] += spectrum[i].Magnitude;
                    _calibFrameCount++;
                    if (_calibFrameCount >= _calibFramesNeeded)
                        FinalizeCalibration();
                }

                double secsRemaining = IsCalibrating
                    ? (_calibFramesNeeded - _calibFrameCount) * _config.FrameSize / (double)sampleRate
                    : 0.0;
                result = new AudioFrame(frame, sampleRate, spectrum, pitch, confidence, formants, IsCalibrating, secsRemaining, lpcCoeffs, lpcRate);
            }
            return result;
        }

        private void FinalizeCalibration()
        {
            _noiseSpectrum = new double[_config.SpectrumBins];
            for (int i = 0; i < _config.SpectrumBins; i++)
                _noiseSpectrum[i] = _calibSpectrumAccum![i] / _calibFramesNeeded;

            _noiseAutocorr = new double[_config.LpcOrder + 1];
            for (int lag = 0; lag <= _config.LpcOrder; lag++)
                _noiseAutocorr[lag] = _calibAutocorrAccum![lag] / _calibFramesNeeded;

            Console.WriteLine("Noise calibration complete.");
        }

        private FrequencyBin[] ComputeSpectrum(double[] samples, int sampleRate)
        {
            var complex = Array.ConvertAll(samples, s => new Complex(s, 0));
            Fourier.Forward(complex, FourierOptions.AsymmetricScaling);
            var bins = new FrequencyBin[_config.SpectrumBins];
            for (int i = 0; i < _config.SpectrumBins; i++)
            {
                double magnitude = complex[i].Magnitude;
                if (_noiseSpectrum != null)
                    magnitude = Math.Max(magnitude - _noiseSpectrum[i], 0.0);
                bins[i] = new FrequencyBin
                {
                    Frequency = i * sampleRate / (double)_config.FrameSize,
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

            if (IsCalibrating)
            {
                var ds = SignalProcessing.Downsample(samples, _config.DownsampleFactor);
                var preemph = SignalProcessing.PreEmphasis(ds);
                SignalProcessing.ApplyHammingWindow(preemph);
                var rNoise = SignalProcessing.Autocorrelation(preemph, _config.LpcOrder);
                for (int lag = 0; lag <= _config.LpcOrder; lag++)
                    _calibAutocorrAccum![lag] += rNoise[lag];
            }

            var (a, formants, dsRate) = LpcAnalysis.Analyze(samples, sampleRate, _config.LpcOrder, _config.DownsampleFactor);

            if (_noiseAutocorr != null)
            {
                var ds = SignalProcessing.Downsample(samples, _config.DownsampleFactor);
                var preemph = SignalProcessing.PreEmphasis(ds);
                SignalProcessing.ApplyHammingWindow(preemph);
                var r = SignalProcessing.Autocorrelation(preemph, _config.LpcOrder);
                r[0] = Math.Max(r[0] - _noiseAutocorr[0], 1e-10);
                for (int lag = 1; lag <= _config.LpcOrder; lag++)
                    r[lag] -= _noiseAutocorr[lag];
                a = LpcAnalysis.LevinsonDurbin(r, _config.LpcOrder);
                formants = LpcAnalysis.ExtractFormants(a, dsRate);
            }

            return (formants, a, dsRate);
        }
    }
}
