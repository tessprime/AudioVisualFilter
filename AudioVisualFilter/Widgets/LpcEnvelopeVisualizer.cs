using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AudioVisualFilter.Widgets
{
    class LpcEnvelopeVisualizer : IFrameListener
    {
        private readonly WriteableBitmap _bitmap;
        private readonly int _width;
        private readonly int _height;
        private readonly double _minFrequency;
        private readonly double _maxFrequency;

        public LpcEnvelopeVisualizer(Canvas canvas, double minFrequency, double maxFrequency)
        {
            _width = (int)canvas.Width;
            _height = (int)canvas.Height;
            _minFrequency = minFrequency;
            _maxFrequency = maxFrequency;

            _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgra32, null);
            var image = new Image { Width = _width, Height = _height, Source = _bitmap };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            canvas.Children.Add(image);
        }

        public void OnFrame(AudioFrame frame)
        {
            var pixels = new byte[_width * _height * 4];

            if (frame.LpcCoefficients is { } a && frame.LpcSampleRate > 0)
            {
                double logMin = Math.Log(_minFrequency);
                double logMax = Math.Log(_maxFrequency);

                // Compute H(f) = 1/|A(e^{j2πf/fs})| for each row
                var hValues = new double[_height];
                double maxH = 0;
                for (int y = 0; y < _height; y++)
                {
                    double t = 1.0 - (y + 1.0) / _height;
                    double freq = Math.Exp(logMin + t * (logMax - logMin));
                    double omega = 2 * Math.PI * freq / frame.LpcSampleRate;

                    double re = 1.0, im = 0.0;
                    for (int k = 0; k < a.Length; k++)
                    {
                        re += a[k] * Math.Cos(-(k + 1) * omega);
                        im += a[k] * Math.Sin(-(k + 1) * omega);
                    }
                    double h = 1.0 / Math.Sqrt(re * re + im * im);
                    hValues[y] = h;
                    if (h > maxH) maxH = h;
                }

                if (maxH > 0)
                {
                    for (int y = 0; y < _height; y++)
                    {
                        int x = Math.Clamp((int)(hValues[y] / maxH * (_width - 2)), 0, _width - 2);
                        // Draw a 2-pixel wide yellow line
                        for (int dx = 0; dx <= 1; dx++)
                        {
                            int idx = (y * _width + x + dx) * 4;
                            pixels[idx + 0] = 0;
                            pixels[idx + 1] = 255;
                            pixels[idx + 2] = 255;
                            pixels[idx + 3] = 220;
                        }
                    }
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, _width, _height), pixels, _width * 4, 0);
        }
    }
}
