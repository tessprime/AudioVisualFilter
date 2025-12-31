using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Windows.Threading;
using MathNet.Numerics.Statistics;
using Microsoft.VisualBasic.Devices;
using NWaves.FeatureExtractors.Base;

namespace AudioVisualFilter
{
    public class FrequencyBin
    {
        public double Frequency { get; set; }
        public double Magnitude { get; set; }
    }

    class Recorder
    {
        private WasapiCapture? _capture;
        private readonly Dispatcher _dispatcher;
        
        /*private readonly System.Threading.Channels.Channel<byte[]> _audioQueue =
            System.Threading.Channels.Channel.CreateUnbounded<byte[]>();*/

        public Action<FrequencyBin>? OnPitch { get; set; }

        public Recorder(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void StartMic()
        {
            // Default input device
            _capture = new WasapiCapture(); // or new WasapiCapture(mmDevice)
            // Pick a format; VAD libraries often want 16kHz mono 16-bit PCM.
            // If your capture is not 16kHz mono PCM, you’ll resample/convert later.
            _capture.WaveFormat = new WaveFormat(44100, 16, 1);


            _capture.DataAvailable += (s, e) =>
            {
                // Copy buffer because NAudio reuses it
                var copy = new byte[e.BytesRecorded     ];
                Buffer.BlockCopy(e.Buffer, 0, copy, 0, e.BytesRecorded);

                // Fast decode bytes -> short[]
                short[] shorts = new short[e.BytesRecorded / 2];
                Buffer.BlockCopy(e.Buffer, 0, shorts, 0, e.BytesRecorded);

                // Convert to double in [-1, 1)
                double[] samples = new double[shorts.Length];
                const double scale = 1.0 / 32768.0; // Int16 range
                for (int i = 0; i < shorts.Length; i++)
                    samples[i] = shorts[i] * scale;

                // Calculate pitch
                FrequencyBin bin = CalculatePitch(samples, _capture.WaveFormat);

                // Post to UI thread
                _dispatcher.BeginInvoke(() => OnPitch?.Invoke(bin));

                // push to processing pipeline
                //_audioQueue.Writer.TryWrite(copy);
            };

            _capture.RecordingStopped += (s, e) =>
            {
                //_audioQueue.Writer.TryComplete(e.Exception);
            };

            _capture.StartRecording();
        }

        private void StopMic()
        {
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
        }

        private int maxIndex(double [] array, int skip = 0)
        {
            int maxIdx = skip;
            double maxVal = array[skip];
            for (int i = skip + 1; i < array.Length; i++)
            {
                if (array[i] > maxVal)
                {
                    maxVal = array[i];
                    maxIdx = i;
                }
            }
            return maxIdx;
        }
        
        private FrequencyBin CalculatePitch(double[] audioData, WaveFormat format)
        {
            var skip = 50;
            var Hann = MathNet.Numerics.Window.Hann(audioData.Length);
            for (int i = 1; i < audioData.Length; i++)
            {
                // Pre-Emp
                audioData[i] = audioData[i] - 0.97*audioData[i-1]; // Apply window
            }
            
            for (int i = 0; i < audioData.Length; i++)
            {
                audioData[i] *= Hann[i];// Zero out low lags
            }
            var correlation = Correlation.Auto(audioData);
            var index =  maxIndex(correlation, skip);
            // Convert index to time lag
            double timeLag = index / (double)format.SampleRate;

            // Convert time lag to frequency
            double frequency = 1.0 / timeLag;

            if (correlation[index] > .7 && frequency > 180 )
            {
                Console.ForegroundColor = ConsoleColor.Green;
            } else if (correlation[index] > .7 && frequency < 180)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            Console.WriteLine($"Index: {index}, Frequency: {frequency} Strength: {correlation[index]}");
            Console.ResetColor();
            if (index == skip)
            {
                return new FrequencyBin { Frequency = 0.0, Magnitude = 0.0 }; // No pitch detected
            }
            return new FrequencyBin { Frequency = frequency, Magnitude = correlation[index] };
        }
    }

}