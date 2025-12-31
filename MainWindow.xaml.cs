using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace AudioVisualFilter
{
    public partial class MainWindow : Window
    {
        // Extended window styles
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020; // click-through
        private const int WS_EX_LAYERED = 0x00080000;     // required for transparency
        private const int WS_EX_TOOLWINDOW = 0x00000080;  // hides from Alt-Tab

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MainWindow()
        {
            InitializeComponent();
        }

        private class OpacityTimer : DispatcherTimer {
            private MainWindow _window;
            private double Opacity;
            private OpacityState _state = OpacityState.FadingOut;

            enum OpacityState
            {
                FadingIn,
                FadingOut,
                Stable
            }
            
            public OpacityTimer(MainWindow window, TimeSpan speed)
            {
                this._window = window;
                this.Interval = speed;
                this.Opacity = 0.0;
                this.Tick += OpacityTimer_Tick;
            }
            private void OpacityTimer_Tick(object? sender, EventArgs e)
            {
                var opacityByte = (byte)(this.Opacity * 255);
                // Logic to adjust opacity goes here
                if (this._state == OpacityState.FadingIn)
                {
                    this.Opacity += 0.05;
                    if (this.Opacity >= .8) {
                        this._state = OpacityState.FadingOut;
                        this.Opacity = .8;
                    }
                    _window.Background = new SolidColorBrush(Color.FromArgb(opacityByte, 255, 0, 0));

                }
                else if (this._state == OpacityState.FadingOut)
                {
                    this.Opacity -= 0.05;
                    if (this.Opacity <= 0.0) {
                        this._state = OpacityState.FadingIn;
                        this.Opacity = 0.0;
                    }
                    _window.Background = new SolidColorBrush(Color.FromArgb(opacityByte, 255, 0, 0));
                }
            }
        }

        public void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Cover the *virtual screen* (all monitors)
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // Make click-through + hide from Alt-Tab
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            // Setup Dispatch timer
            var timer = new OpacityTimer(this, TimeSpan.FromMilliseconds(100));
            timer.Start();

            var recorder = new Recorder(Dispatcher);

            // Subscribe to pitch updates
            recorder.OnPitch = (pitch) =>
            {
                // Handle the pitch on the UI thread
                // For example, update a UI element or trigger visual effects
                ProcessPitch(pitch);
            };
            
            recorder.StartMic();
        }

        // Microphone stuff, probably should move into it's own class.

        private void ProcessPitch(double pitch)
        {
            // TODO: Implement visual effects based on audio frame sum
            // This method is called on the UI thread for each audio frame
            this.OutputTextBox.Text = pitch.ToString("F2");
        }
    }
}