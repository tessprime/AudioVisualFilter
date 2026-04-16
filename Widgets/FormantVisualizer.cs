using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AudioVisualFilter.DataStructures;

namespace AudioVisualFilter.Widgets
{
    class FormantVisualizer : IFrameListener
    {
        private const double MinFrequency = 200.0;
        private const double MaxFrequency = 4000.0;

        // F1=red, F2=green, F3=blue, F4=yellow
        private static readonly (byte r, byte g, byte b)[] FormantColors =
        [
            (255, 80,  80),
            (80,  255, 80),
            (80,  160, 255),
            (255, 255, 80),
        ];

        private static readonly int[] LabelFrequencies = { 500, 1000, 2000, 3000 };

        private readonly Canvas _canvas;
        private readonly RingBuffer<double[]> _buffer;
        private readonly WriteableBitmap _bitmap;
        private readonly int _width;
        private readonly int _height;
        private bool _labelsAdded;

        public FormantVisualizer(Canvas canvas, int historySize = 400)
        {
            _canvas = canvas;
            _width  = (int)canvas.Width;
            _height = (int)canvas.Height;
            _buffer = new RingBuffer<double[]>(historySize);

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
            _buffer.Write(frame.Formants);
            Redraw();
        }

        private void Redraw()
        {
            var pixels = new byte[_width * _height * 4];

            double logMin = Math.Log(MinFrequency);
            double logMax = Math.Log(MaxFrequency);

            // Semi-transparent dark background
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 0] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 100;
            }

            for (int x = 0; x < _buffer.Count; x++)
            {
                var formants = _buffer[x];
                for (int fi = 0; fi < Math.Min(formants.Length, FormantColors.Length); fi++)
                {
                    double freq = formants[fi];
                    if (freq < MinFrequency || freq > MaxFrequency) continue;

                    double t = (Math.Log(freq) - logMin) / (logMax - logMin);
                    int py = Math.Clamp((int)((1.0 - t) * _height), 0, _height - 1);

                    var (cr, cg, cb) = FormantColors[fi];
                    PlotDot(pixels, x, py, cr, cg, cb);
                }
            }

            // Frequency guide lines
            foreach (int freq in LabelFrequencies)
            {
                double t = (Math.Log(freq) - logMin) / (logMax - logMin);
                int ny = Math.Clamp((int)((1.0 - t) * _height), 0, _height - 1);
                for (int x = 0; x < _width; x++)
                {
                    if ((x / 6) % 2 != 0) continue;
                    int idx = (ny * _width + x) * 4;
                    pixels[idx + 0] = 180;
                    pixels[idx + 1] = 180;
                    pixels[idx + 2] = 180;
                    pixels[idx + 3] = 80;
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, _width, _height), pixels, _width * 4, 0);
            EnsureLabels(logMin, logMax);
        }

        private void PlotDot(byte[] pixels, int cx, int cy, byte r, byte g, byte b)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int py = cy + dy;
                if (py < 0 || py >= _height) continue;
                int idx = (py * _width + cx) * 4;
                pixels[idx + 0] = b;
                pixels[idx + 1] = g;
                pixels[idx + 2] = r;
                pixels[idx + 3] = 200;
            }
        }

        private void EnsureLabels(double logMin, double logMax)
        {
            if (_labelsAdded) return;
            _labelsAdded = true;

            foreach (int freq in LabelFrequencies)
            {
                double t = (Math.Log(freq) - logMin) / (logMax - logMin);
                int ny = Math.Clamp((int)((1.0 - t) * _height), 0, _height - 1);

                var label = new System.Windows.Controls.TextBlock
                {
                    Text = freq >= 1000 ? $"{freq / 1000}k" : freq.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 9,
                };
                Canvas.SetLeft(label, 6);
                Canvas.SetTop(label, ny - 7);
                _canvas.Children.Add(label);
            }

            // Legend: F1–F4
            for (int fi = 0; fi < FormantColors.Length; fi++)
            {
                var (cr, cg, cb) = FormantColors[fi];
                var label = new System.Windows.Controls.TextBlock
                {
                    Text = $"F{fi + 1}",
                    Foreground = new SolidColorBrush(Color.FromRgb(cr, cg, cb)),
                    FontSize = 9,
                };
                Canvas.SetLeft(label, _width - 24);
                Canvas.SetTop(label, fi * 14 + 4);
                _canvas.Children.Add(label);
            }
        }
    }
}
