using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Statistics;
using AudioVisualFilter.Analyses;

namespace AudioVisualFilter.Widgets
{
    class AudioAnalyzer
    {
        private const double ConfidenceThreshold = 0.3;
        private const int AutocorrelationSkip = 50;

        private const double FormantSmoothAlpha  = 0.3;  // EMA weight for new value; lower = smoother
        private const double FormantMatchWindowHz = 300.0; // max Hz to consider two formants the same

        private readonly AnalysisConfig _config;
        private readonly List<double> _sampleBuffer = new();
        private double[] _smoothedFormants = Array.Empty<double>();

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

var spectrum = ComputeSpectrum(frame, sampleRate);
                var (pitch, confidence) = ComputePitch(frame, sampleRate);
                var lpcFrame = frame[^Math.Min(_config.LpcFrameSize, frame.Length)..];
                var (formants, lpcCoeffs, lpcRate) = ComputeFormants(lpcFrame, sampleRate, confidence);
                formants = SmoothFormants(formants);

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

        private double[] SmoothFormants(double[] newFormants)
        {
            if (newFormants.Length == 0)
            {
                _smoothedFormants = Array.Empty<double>();
                return newFormants;
            }

            if (_smoothedFormants.Length == 0)
            {
                _smoothedFormants = (double[])newFormants.Clone();
                return _smoothedFormants;
            }

            // Match each new formant to the nearest previous smoothed formant within the window.
            // Unmatched new formants are taken as-is; unmatched previous ones are dropped.
            var matched = new double[newFormants.Length];
            var used    = new bool[_smoothedFormants.Length];

            for (int i = 0; i < newFormants.Length; i++)
            {
                int bestJ   = -1;
                double bestD = FormantMatchWindowHz;
                for (int j = 0; j < _smoothedFormants.Length; j++)
                {
                    double d = Math.Abs(newFormants[i] - _smoothedFormants[j]);
                    if (!used[j] && d < bestD) { bestD = d; bestJ = j; }
                }

                matched[i] = bestJ >= 0
                    ? FormantSmoothAlpha * newFormants[i] + (1 - FormantSmoothAlpha) * _smoothedFormants[bestJ]
                    : newFormants[i];

                if (bestJ >= 0) used[bestJ] = true;
            }

            _smoothedFormants = matched;
            return _smoothedFormants;
        }

        private (double[] formants, double[]? lpcCoeffs, int lpcRate) ComputeFormants(
            double[] samples, int sampleRate, double pitchConfidence)
        {
            const double RmsThreshold = 0.001;
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

            var (a, formants, effectiveRate) = LpcAnalysis.Analyze(
                samples, sampleRate, _config.LpcOrder, _config.DownsampleFactor, _config.Method);

            // Autocorrelation-domain noise subtraction only applies to LevinsonDurbin.
            if (_config.Method == LpcMethod.LevinsonDurbin && _noiseAutocorr != null)
            {
                var ds = SignalProcessing.Downsample(samples, _config.DownsampleFactor);
                var preemph = SignalProcessing.PreEmphasis(ds);
                SignalProcessing.ApplyHammingWindow(preemph);
                var r = SignalProcessing.Autocorrelation(preemph, _config.LpcOrder);
                r[0] = Math.Max(r[0] - _noiseAutocorr[0], 1e-10);
                for (int lag = 1; lag <= _config.LpcOrder; lag++)
                    r[lag] -= _noiseAutocorr[lag];
                a = LpcAnalysis.LevinsonDurbin(r, _config.LpcOrder);
                formants = LpcAnalysis.ExtractFormants(a, effectiveRate);
            }

            return (formants, a, effectiveRate);
        }
    }
}
