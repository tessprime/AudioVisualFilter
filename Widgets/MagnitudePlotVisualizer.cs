using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AudioVisualFilter.Widgets
{
    class MagnitudePlotVisualizer : IFrameListener
    {
        private const double Threshold = 0.01;
        private const double MaxMagnitude = 50.0;

        private readonly Canvas _canvas;
        private readonly double _minFrequency;
        private readonly double _maxFrequency;
        private readonly WriteableBitmap _bitmap;
        private readonly int _width;
        private readonly int _height;

        public MagnitudePlotVisualizer(Canvas canvas, double minFrequency, double maxFrequency)
        {
            _canvas = canvas;
            _minFrequency = minFrequency;
            _maxFrequency = maxFrequency;
            _width  = (int)canvas.Width;
            _height = (int)canvas.Height;

            _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgra32, null);
            var image = new Image { Width = _width, Height = _height, Source = _bitmap };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            _canvas.Children.Add(image);
        }

        public void OnFrame(AudioFrame frame)
        {
            var bins = frame.Spectrum;
            double hzPerBin = frame.SampleRate / (double)(bins.Length * 2);
            double logMin = Math.Log(_minFrequency);
            double logMax = Math.Log(_maxFrequency);

            var pixels = new byte[_width * _height * 4];

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 3] = 100;
            }

            for (int y = 0; y < _height; y++)
            {
                double t = 1.0 - (y + 1.0) / _height;
                double freq = Math.Exp(logMin + t * (logMax - logMin));
                int binIndex = Math.Clamp((int)(freq / hzPerBin), 0, bins.Length - 1);
                double freqRatio = freq / _minFrequency;
                double magnitude = bins[binIndex].Magnitude * (freqRatio * freqRatio);

                if (magnitude < Threshold)
                    continue;

                double colorT = Math.Clamp((magnitude - Threshold) / (MaxMagnitude - Threshold), 0, 1);
                int barWidth = (int)(colorT * _width);
                byte r = (byte)(colorT * 255);
                byte b = (byte)((1 - colorT) * 255);

                for (int x = 0; x < barWidth; x++)
                {
                    int idx = (y * _width + x) * 4;
                    pixels[idx + 0] = b;
                    pixels[idx + 1] = 0;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = 180;
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, _width, _height), pixels, _width * 4, 0);
        }
    }
}
