using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Windows.Threading;

namespace AudioVisualFilter.Widgets
{
    public class FrequencyBin
    {
        public double Frequency { get; set; }
        public double Magnitude { get; set; }
    }

    interface IFrameListener
    {
        void OnFrame(AudioFrame frame);
    }

    class Recorder
    {
        private WasapiCapture? _capture;
        private readonly Dispatcher _dispatcher;
        private readonly IFrameListener[] _listeners;
        private readonly AudioAnalyzer _analyzer = new();

        public Recorder(Dispatcher dispatcher, params IFrameListener[] listeners)
        {
            _dispatcher = dispatcher;
            _listeners = listeners;
        }

        public void StartMic()
        {
            _capture = new WasapiCapture();
            _capture.WaveFormat = new WaveFormat(44100, 16, 1);

            _capture.DataAvailable += (s, e) =>
            {
                short[] shorts = new short[e.BytesRecorded / 2];
                Buffer.BlockCopy(e.Buffer, 0, shorts, 0, e.BytesRecorded);

                double[] samples = new double[shorts.Length];
                const double scale = 1.0 / 32768.0;
                for (int i = 0; i < shorts.Length; i++)
                    samples[i] = shorts[i] * scale;

                int sampleRate = _capture.WaveFormat.SampleRate;
                _dispatcher.BeginInvoke(() =>
                {
                    var frame = _analyzer.Analyze(samples, sampleRate);
                    foreach (var listener in _listeners)
                        listener.OnFrame(frame);
                });
            };

            _capture.StartRecording();
        }

        private void StopMic()
        {
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
        }
    }
}
