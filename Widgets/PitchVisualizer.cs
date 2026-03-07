using System.Windows.Controls;
using System.Windows.Media;

namespace AudioVisualFilter.Widgets
{
    class PitchVisualizer
    {
        private readonly TextBox _textBox;

        public PitchVisualizer(TextBox textBox)
        {
            _textBox = textBox;
        }

        public void OnPitch(FrequencyBin bin)
        {
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
    }
}
