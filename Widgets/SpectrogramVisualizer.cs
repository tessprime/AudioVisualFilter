using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AudioVisualFilter.DataStructures;

namespace AudioVisualFilter.Widgets
{
    class SpectrogramVisualizer : IFrameListener
    {
        private const double Threshold = 0.01;
        private const double MaxMagnitude = 1.0;

        private const double MinFrequency = 50.0;
        private const double MaxFrequency = 300.0;

        private static readonly int[] NotchFrequencies = { 50, 100, 150, 200, 250, 300 };

        private readonly Canvas _canvas;
        private readonly RingBuffer<FrequencyBin[]> _buffer;
        private readonly RingBuffer<double> _pitchBuffer;
        private readonly WriteableBitmap _bitmap;
        private readonly int _width;
        private readonly int _height;
        private int _sampleRate;
        private bool _labelsAdded;

        public SpectrogramVisualizer(Canvas canvas, int historySize = 400)
        {
            _canvas = canvas;
            _width = (int)canvas.Width;
            _height = (int)canvas.Height;
            _buffer = new RingBuffer<FrequencyBin[]>(historySize);
            _pitchBuffer = new RingBuffer<double>(historySize);

            _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgra32, null);
            var image = new Image { Width = _width, Height = _height, Source = _bitmap };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            _canvas.Children.Add(image);

            var border = new Rectangle
            {
                Width = _width,
                Height = _height,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(border, 0);
            Canvas.SetTop(border, 0);
            _canvas.Children.Add(border);
        }

        public void OnFrame(AudioFrame frame)
        {
            _sampleRate = frame.SampleRate;
            _buffer.Write(frame.Spectrum);
            _pitchBuffer.Write(frame.Pitch);
            Redraw();
        }

        private void Redraw()
        {
            if (_sampleRate == 0) return;

            var pixels = new byte[_width * _height * 4];

            double logMin = Math.Log(MinFrequency);
            double logMax = Math.Log(MaxFrequency);

            for (int x = 0; x < _buffer.Count; x++)
            {
                var bins = _buffer[x];
                double hzPerBin = (_sampleRate / 2.0) / bins.Length;

                for (int y = 0; y < _height; y++)
                {
                    double t = 1.0 - (y + 1.0) / _height;
                    double freq = Math.Exp(logMin + t * (logMax - logMin));
                    int binIndex = Math.Clamp((int)(freq / hzPerBin), 0, bins.Length - 1);
                    double magnitude = bins[binIndex].Magnitude;

                    byte r, g, b, a;
                    if (magnitude < Threshold)
                    {
                        r = g = b = a = 0;
                    }
                    else
                    {
                        double colorT = Math.Clamp((magnitude - Threshold) / (MaxMagnitude - Threshold), 0, 1);
                        r = (byte)(colorT * 255);
                        g = 0;
                        b = (byte)((1 - colorT) * 255);
                        a = 128;
                    }

                    int idx = (y * _width + x) * 4;
                    pixels[idx + 0] = b;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = a;
                }
            }

            // Draw dotted line at 200 Hz
            {
                double t200 = (Math.Log(200) - logMin) / (logMax - logMin);
                int ny200 = Math.Clamp((int)((1.0 - t200) * _height), 0, _height - 1);
                for (int x = 0; x < _width; x++)
                {
                    if ((x / 4) % 2 == 0)
                    {
                        int idx = (ny200 * _width + x) * 4;
                        pixels[idx + 0] = 255;
                        pixels[idx + 1] = 255;
                        pixels[idx + 2] = 255;
                        pixels[idx + 3] = 255;
                    }
                }
            }

            // Draw notch tick marks on left edge
            foreach (int freq in NotchFrequencies)
            {
                if (freq < MinFrequency || freq > MaxFrequency) continue;
                double t = (Math.Log(freq) - logMin) / (logMax - logMin);
                int ny = Math.Clamp((int)((1.0 - t) * _height), 0, _height - 1);
                for (int tx = 0; tx < 5; tx++)
                {
                    int idx = (ny * _width + tx) * 4;
                    pixels[idx + 0] = 255;
                    pixels[idx + 1] = 255;
                    pixels[idx + 2] = 255;
                    pixels[idx + 3] = 255;
                }
            }

            // Draw pitch line
            for (int x = 0; x < _pitchBuffer.Count; x++)
            {
                double pitch = _pitchBuffer[x];
                if (pitch <= 0 || pitch < MinFrequency || pitch > MaxFrequency) continue;
                double t = (Math.Log(pitch) - logMin) / (logMax - logMin);
                int py = Math.Clamp((int)((1.0 - t) * _height), 0, _height - 1);
                int idx = (py * _width + x) * 4;
                pixels[idx + 0] = 255;
                pixels[idx + 1] = 255;
                pixels[idx + 2] = 255;
                pixels[idx + 3] = 255;
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, _width, _height), pixels, _width * 4, 0);

            EnsureLabels(logMin, logMax);
        }

        private void EnsureLabels(double logMin, double logMax)
        {
            if (_labelsAdded) return;
            _labelsAdded = true;

            foreach (int freq in NotchFrequencies)
            {
                if (freq < MinFrequency || freq > MaxFrequency) continue;
                double t = (Math.Log(freq) - logMin) / (logMax - logMin);
                int ny = Math.Clamp((int)((1.0 - t) * _height), 0, _height - 1);

                var label = new System.Windows.Controls.TextBlock
                {
                    Text = freq.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 9,
                };
                Canvas.SetLeft(label, 6);
                Canvas.SetTop(label, ny - 7);
                _canvas.Children.Add(label);
            }
        }
    }
}
