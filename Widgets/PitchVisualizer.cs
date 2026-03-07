using System.Windows.Controls;
using System.Windows.Media;
using MathNet.Numerics.Statistics;

namespace AudioVisualFilter.Widgets
{
    class PitchVisualizer : IAudioListener
    {
        private readonly TextBox _textBox;

        public PitchVisualizer(TextBox textBox)
        {
            _textBox = textBox;
        }

        public void OnSamples(double[] samples, int sampleRate)
        {
            var bin = CalculatePitch(samples, sampleRate);
            _textBox.Text = bin.Frequency.ToString("F2");
            if (bin.Magnitude > .7 && bin.Frequency > 180)
            {
                _textBox.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            }
            else if (bin.Magnitude > .7 && bin.Frequency > 160)
            {
                _textBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));
            }
            else if (bin.Magnitude > .7 && bin.Frequency < 160)
            {
                _textBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            }
            else
            {
                _textBox.Foreground = Brushes.White;
            }
        }

        private FrequencyBin CalculatePitch(double[] audioData, int sampleRate)
        {
            var skip = 50;
            var hann = MathNet.Numerics.Window.Hann(audioData.Length);
            for (int i = 1; i < audioData.Length; i++)
                audioData[i] = audioData[i] - 0.97 * audioData[i - 1];
            for (int i = 0; i < audioData.Length; i++)
                audioData[i] *= hann[i];

            var correlation = Correlation.Auto(audioData);
            int index = MaxIndex(correlation, skip);
            double frequency = sampleRate / (double)index;

            Console.ForegroundColor = correlation[index] > .7 && frequency > 180 ? ConsoleColor.Green
                : correlation[index] > .7 ? ConsoleColor.Red : ConsoleColor.White;
            Console.WriteLine($"Index: {index}, Frequency: {frequency} Strength: {correlation[index]}");
            Console.ResetColor();

            if (index <= skip || correlation[index] < .7)
                return new FrequencyBin { Frequency = 0.0, Magnitude = 0.0 };
            return new FrequencyBin { Frequency = frequency, Magnitude = correlation[index] };
        }

        private int MaxIndex(double[] array, int skip = 0)
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
    }
}
