using System.IO;
using NAudio.Wave;

namespace AudioVisualFilter.Analyses
{
    public record FormantFrame(double TimeSeconds, double[] Formants);

    public static class WavAnalyzer
    {
        private const double RmsThreshold = 0.01;
        private const int MaxFormants = 5;

        // Analyze a WAV file and return one FormantFrame per hop.
        // frameSize: samples per LPC window (higher = better frequency resolution)
        // hopSize:   samples between frames (lower = finer time resolution, ~441 = 10ms at 44100Hz)
        public static IReadOnlyList<FormantFrame> Analyze(string wavPath, AnalysisConfig? config = null)
        {
            config ??= new AnalysisConfig();
            var (samples, sampleRate) = ReadWav(wavPath);
            var frames = new List<FormantFrame>();

            for (int start = 0; start + config.LpcFrameSize <= samples.Length; start += config.HopSize)
            {
                double timeSeconds = (start + config.LpcFrameSize / 2.0) / sampleRate;
                var frame = samples[start..(start + config.LpcFrameSize)];

                if (SignalProcessing.Rms(frame) < RmsThreshold)
                {
                    frames.Add(new FormantFrame(timeSeconds, Array.Empty<double>()));
                    continue;
                }

                var (_, formants, _) = LpcAnalysis.Analyze(frame, sampleRate, config.LpcOrder, config.DownsampleFactor, config.Method);
                frames.Add(new FormantFrame(timeSeconds, formants));
            }

            return frames;
        }

        // Write a CSV in the same column layout as the Praat script output.
        public static void WriteCsv(IReadOnlyList<FormantFrame> frames, string outputPath)
        {
            var header = "time_s," + string.Join(",", Enumerable.Range(1, MaxFormants).Select(i => $"F{i}"));
            var lines = frames.Select(f =>
            {
                var cols = new string[MaxFormants];
                for (int i = 0; i < MaxFormants; i++)
                    cols[i] = i < f.Formants.Length ? f.Formants[i].ToString("F1") : "--undefined--";
                return $"{f.TimeSeconds:F4},{string.Join(",", cols)}";
            });

            File.WriteAllLines(outputPath, lines.Prepend(header));
        }

        private static (double[] samples, int sampleRate) ReadWav(string path)
        {
            using var reader = new AudioFileReader(path);
            int sampleRate = reader.WaveFormat.SampleRate;
            int channels   = reader.WaveFormat.Channels;

            var floats = new List<float>();
            var buffer = new float[4096];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                floats.AddRange(buffer.Take(read));

            // Mix down to mono by averaging channels
            int frameCount = floats.Count / channels;
            var mono = new double[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                double sum = 0;
                for (int c = 0; c < channels; c++)
                    sum += floats[i * channels + c];
                mono[i] = sum / channels;
            }

            return (mono, sampleRate);
        }
    }
}
