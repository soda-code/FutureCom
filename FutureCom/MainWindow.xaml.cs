using FutureCom.ViewModels;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace FutureCom
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;

        private readonly MainViewModel _vm;
        private readonly List<ScottPlot.Plottables.DataStreamer> _streamers = new();

        public MainWindow()
        {
            InitializeComponent();

            SourceInitialized += (s, e) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    int darkMode = 1;
                    DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                    int captionColor = 0x00190F0B;
                    DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
                }
            };

            _vm = new MainViewModel();
            DataContext = _vm;

            // 初始化 ScottPlot 底色与图例
            ChartPlot.Plot.FigureBackground.Color = Color.FromHex("#131D2E");
            ChartPlot.Plot.DataBackground.Color = Color.FromHex("#080C14");
            ChartPlot.Plot.Axes.Color(Color.FromHex("#CBD5E1"));
            ChartPlot.Plot.ShowLegend(Alignment.UpperRight);

            InitAllStreamers();

            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsChartFullScreen))
                {
                    Dispatcher.Invoke(() => UpdateFullScreenLayout(_vm.IsChartFullScreen));
                }
            };

            _vm.OnChartConfigChanged += () =>
            {
                Dispatcher.Invoke(InitAllStreamers);
            };

            // 全通道同屏数据推流
            _vm.OnMultiChannelSampled += plotValues =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    for (int i = 0; i < plotValues.Length && i < _streamers.Count; i++)
                    {
                        if (_vm.Channels[i].IsVisible)
                        {
                            _streamers[i].Add(plotValues[i]);
                        }
                    }
                    ChartPlot.Refresh();
                });
            };

            _vm.OnClearChartRequested += () =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    foreach (var s in _streamers) s.Clear();
                    ChartPlot.Refresh();
                });
            };

            _vm.OnLogAdded += () =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                });
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.F11) _vm.ToggleChartFullScreenCommand.Execute(null);
                else if (e.Key == Key.Escape && _vm.IsChartFullScreen) _vm.ToggleChartFullScreenCommand.Execute(null);
            };
        }

        private void InitAllStreamers()
        {
            ChartPlot.Plot.Clear();
            _streamers.Clear();

            int capacity = _vm.ChartBufferCapacity > 0 ? _vm.ChartBufferCapacity : 1000;

            for (int i = 0; i < _vm.Channels.Count; i++)
            {
                var ch = _vm.Channels[i];
                var s = ChartPlot.Plot.Add.DataStreamer(capacity);
                s.Color = Color.FromHex(ch.ColorHex);
                s.LegendText = $"{ch.Name} (×{ch.Gain}+{ch.Offset})";
                s.IsVisible = ch.IsVisible;
                s.ViewScrollLeft();
                _streamers.Add(s);
            }

            if (_vm.IsYAutoScale)
            {
                ChartPlot.Plot.Axes.AutoScale();
            }
            else
            {
                double yMin = _vm.ChartYMin;
                double yMax = _vm.ChartYMax > yMin ? _vm.ChartYMax : yMin + 10.0;
                ChartPlot.Plot.Axes.SetLimitsY(yMin, yMax);
            }

            ChartPlot.Refresh();
        }

        private void UpdateFullScreenLayout(bool isFullScreen)
        {
            if (isFullScreen)
            {
                LeftCol.Width = new GridLength(0);
                LeftPanel.Visibility = Visibility.Collapsed;
                BottomLogRow.Height = new GridLength(0);
                BottomLogBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                LeftCol.Width = new GridLength(420);
                LeftPanel.Visibility = Visibility.Visible;
                BottomLogRow.Height = new GridLength(240);
                BottomLogBorder.Visibility = Visibility.Visible;
            }
            ChartPlot.Refresh();
        }

        private void ChartPlot_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _vm.ToggleChartFullScreenCommand.Execute(null);
        }
    }
}