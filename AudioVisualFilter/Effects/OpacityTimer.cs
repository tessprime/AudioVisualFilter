using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace AudioVisualFilter.Effects
{
    class OpacityTimer : DispatcherTimer
    {
        private Window _window;
        private double Opacity;
        private OpacityState _state = OpacityState.FadingOut;

        enum OpacityState
        {
            FadingIn,
            FadingOut,
            Stable
        }

        public OpacityTimer(Window window, TimeSpan speed)
        {
            this._window = window;
            this.Interval = speed;
            this.Opacity = 0.0;
            this.Tick += OpacityTimer_Tick;
        }

        private void OpacityTimer_Tick(object? sender, EventArgs e)
        {
            var opacityByte = (byte)(this.Opacity * 255);
            if (this._state == OpacityState.FadingIn)
            {
                this.Opacity += 0.05;
                if (this.Opacity >= .8)
                {
                    this._state = OpacityState.FadingOut;
                    this.Opacity = .8;
                }
                _window.Background = new SolidColorBrush(Color.FromArgb(opacityByte, 255, 0, 0));
            }
            else if (this._state == OpacityState.FadingOut)
            {
                this.Opacity -= 0.05;
                if (this.Opacity <= 0.0)
                {
                    this._state = OpacityState.FadingIn;
                    this.Opacity = 0.0;
                }
                _window.Background = new SolidColorBrush(Color.FromArgb(opacityByte, 255, 0, 0));
            }
        }
    }
}
