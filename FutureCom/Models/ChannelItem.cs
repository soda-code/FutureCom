using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace FutureCom.Models
{
    public partial class ChannelItem : ObservableObject
    {
        public int Index { get; set; }

        [ObservableProperty] private string _name = "通道 1";
        [ObservableProperty] private string _unit = "V";
        [ObservableProperty] private double _value = 0.0;
        [ObservableProperty] private double _min = 0.0;
        [ObservableProperty] private double _max = 100.0;
        [ObservableProperty] private double _gain = 1.0;
        [ObservableProperty] private double _offset = 0.0;
        [ObservableProperty] private string _colorHex = "#38BDF8";
        [ObservableProperty] private bool _isVisible = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlotValue))]
        private double _percent = 0.0;

        public double PlotValue => (Value * Gain) + Offset;

        partial void OnValueChanged(double value)
        {
            Percent = Max > Min ? Math.Clamp((value - Min) / (Max - Min) * 100.0, 0.0, 100.0) : 0.0;
        }
    }
}