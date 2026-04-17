using System.Windows.Controls;
using System.Windows.Media;

namespace AudioVisualFilter.Widgets
{
    class PitchVisualizer : IFrameListener
    {
        private readonly TextBox _textBox;

        public PitchVisualizer(TextBox textBox)
        {
            _textBox = textBox;
        }

        public void OnFrame(AudioFrame frame)
        {
            _textBox.Text = frame.Pitch.ToString("F2");

            if (frame.PitchConfidence > 0.7 && frame.Pitch > 180)
                _textBox.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            else if (frame.PitchConfidence > 0.7 && frame.Pitch > 160)
                _textBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));
            else if (frame.PitchConfidence > 0.7 && frame.Pitch < 160)
                _textBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            else
                _textBox.Foreground = Brushes.White;
        }
    }
}
