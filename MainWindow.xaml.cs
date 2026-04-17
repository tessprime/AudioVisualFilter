using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AudioVisualFilter.Effects;
using AudioVisualFilter.Widgets;

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
            //timer.Start();

            var spectrogramVisualizer = new SpectrogramVisualizer(MainCanvas);
            var spectrogramSidebar    = new MagnitudePlotVisualizer(SpectrogramSidebar, 50.0, 300.0);
            var formantVisualizer     = new FormantVisualizer(FormantCanvas);
            var formantSidebar        = new MagnitudePlotVisualizer(FormantSidebar, 200.0, 4000.0);
            var pitchVisualizer       = new PitchVisualizer(OutputTextBox);
            var recorder = new Recorder(Dispatcher, pitchVisualizer, spectrogramVisualizer, spectrogramSidebar, formantVisualizer, formantSidebar);
            recorder.StartMic();
        }

    }
}