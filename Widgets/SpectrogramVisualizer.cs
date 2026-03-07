using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AudioVisualFilter.DataStructures;
using MathNet.Numerics.IntegralTransforms;

namespace AudioVisualFilter.Widgets
{
    class SpectrogramVisualizer : IAudioListener
    {
        private const double Threshold = 0.01;
        private const double MaxMagnitude = 1.0;

        private readonly Canvas _canvas;
        private readonly RingBuffer<FrequencyBin[]> _buffer;
        private readonly WriteableBitmap _bitmap;
        private readonly int _width;
        private readonly int _height;

        public SpectrogramVisualizer(Canvas canvas, int historySize = 400)
        {
            _canvas = canvas;
            _width = (int)canvas.Width;
            _height = (int)canvas.Height;
            _buffer = new RingBuffer<FrequencyBin[]>(historySize);

            _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr32, null);
            var image = new Image { Width = _width, Height = _height, Source = _bitmap };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            _canvas.Children.Add(image);

            // Border drawn on top of the bitmap
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

        public void OnSamples(double[] samples, int sampleRate)
        {
            var complex = Array.ConvertAll(samples, s => new Complex(s, 0));
            Fourier.Forward(complex, FourierOptions.AsymmetricScaling);

            var bins = new FrequencyBin[complex.Length / 2];
            for (int i = 0; i < bins.Length; i++)
            {
                bins[i] = new FrequencyBin
                {
                    Frequency = i * sampleRate / (double)complex.Length,
                    Magnitude = complex[i].Magnitude
                };
            }

            _buffer.Write(bins);
            Redraw();
        }

        private void Redraw()
        {
            var pixels = new byte[_width * _height * 4];

            for (int x = 0; x < _buffer.Count; x++)
            {
                var bins = _buffer[x];
                for (int y = 0; y < _height; y++)
                {
                    // Bottom of canvas = low frequency, top = high frequency
                    int binIndex = (int)((1.0 - (y + 1.0) / _height) * bins.Length);
                    binIndex = Math.Clamp(binIndex, 0, bins.Length - 1);
                    double magnitude = bins[binIndex].Magnitude;

                    byte r, g, b;
                    if (magnitude < Threshold)
                    {
                        r = g = b = 0;
                    }
                    else
                    {
                        double t = Math.Clamp((magnitude - Threshold) / (MaxMagnitude - Threshold), 0, 1);
                        r = (byte)(t * 255);
                        g = 0;
                        b = (byte)((1 - t) * 255);
                    }

                    int idx = (y * _width + x) * 4;
                    pixels[idx + 0] = b;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, _width, _height), pixels, _width * 4, 0);
        }
    }
}
