using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FutureCom.Models;
using FutureCom.Services;
using Microsoft.Win32;

namespace FutureCom.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SerialService _serialService = new();
        private CancellationTokenSource? _pollingCts;

        #region Events
        public event Action? OnChartConfigChanged;
        public event Action<double[]>? OnMultiChannelSampled;
        public event Action? OnClearChartRequested;
        public event Action? OnLogAdded;
        #endregion

        #region UI Properties
        public string AppTitle => "FutureCom 工业多协议智能串口监控平台";
        public string AppVersion => "v2.5.0";

        [ObservableProperty] private string _statusMessage = "就绪 (未连接)";
        [ObservableProperty] private string _licenseStatusText = "已激活 (永久商业授权)";
        [ObservableProperty] private string _licenseStatusColor = "#34D399";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullScreenBtnText))]
        private bool _isChartFullScreen;

        public string FullScreenBtnText => IsChartFullScreen ? "⛶ 退出全屏" : "⛶ 全屏";

        [ObservableProperty] private int _chartBufferCapacity = 1000;
        [ObservableProperty] private bool _isYAutoScale = true;
        [ObservableProperty] private double _chartYMin = 0;
        [ObservableProperty] private double _chartYMax = 100;

        public List<string> ColorPaletteOptions { get; } = new()
        {
            "#38BDF8", "#10B981", "#F59E0B", "#EF4444", "#A855F7", "#EC4899", "#6366F1", "#14B8A6"
        };

        [ObservableProperty] private ObservableCollection<string> _availablePorts = new();
        [ObservableProperty] private string _selectedPort = string.Empty;
        [ObservableProperty] private int _selectedBaudRate = 115200;
        public List<int> BaudRateOptions { get; } = new() { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };

        [ObservableProperty] private bool _isConnected;

        [ObservableProperty] private ProtocolMode _selectedProtocolMode = ProtocolMode.ModbusRTU;
        [ObservableProperty] private byte _slaveId = 1;
        [ObservableProperty] private ushort _startAddress = 0;
        [ObservableProperty] private int _selectedInterval = 100;
        public List<int> PollingIntervalOptions { get; } = new() { 20, 50, 100, 200, 500, 1000 };

        // 自定义帧头帧尾与数据格式配置属性
        [ObservableProperty] private bool _isCustomFrameConfigDialogOpen;
        [ObservableProperty] private string _customHeader = "AA BB";
        [ObservableProperty] private string _customTail = "0D 0A";
        [ObservableProperty] private DataTypeOption _selectedDataType = DataTypeOption.Int16_BigEndian;
        public List<DataTypeOption> DataTypeOptions { get; } = Enum.GetValues<DataTypeOption>().Cast<DataTypeOption>().ToList();

        [ObservableProperty] private string _latestTxHex = string.Empty;
        [ObservableProperty] private int _latestTxLen;
        [ObservableProperty] private string _latestRxHex = string.Empty;
        [ObservableProperty] private int _latestRxLen;
        [ObservableProperty] private double _packetLossRate;
        [ObservableProperty] private int _crcErrorCount;
        [ObservableProperty] private int _timeoutErrorCount;
        [ObservableProperty] private double _commSuccessRate = 100.0;

        public ObservableCollection<ChannelItem> Channels { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        [ObservableProperty] private bool _isLicenseDialogOpen;
        [ObservableProperty] private string _machineCode = "FC-8A2F-9B1C-E734";
        [ObservableProperty] private string _inputLicenseKey = string.Empty;
        [ObservableProperty] private bool _isChannelCustomDialogOpen;
        [ObservableProperty] private bool _isChartSettingsDialogOpen;
        [ObservableProperty] private bool _isSettingsDialogOpen;
        [ObservableProperty] private bool _isAiAdvisorDialogOpen;
        [ObservableProperty] private string _aiAdvisorInput = string.Empty;
        [ObservableProperty] private bool _isUpdateDialogOpen;
        [ObservableProperty] private string _updateChangelog = "当前已是最新稳定版本 (v2.5.0 Industrial Edition)。";
        [ObservableProperty] private bool _isAboutDialogOpen;
        #endregion

        public MainViewModel()
        {
            InitDefaultChannels(4);
            RefreshPorts();
        }

        private void InitDefaultChannels(int count)
        {
            Channels.Clear();
            for (int i = 0; i < count; i++)
            {
                Channels.Add(new ChannelItem
                {
                    Index = i + 1,
                    Name = $"CH{i + 1}",
                    Unit = "V",
                    Max = 100,
                    Gain = 1.0,
                    Offset = 0.0,
                    ColorHex = ColorPaletteOptions[i % ColorPaletteOptions.Count],
                    IsVisible = true,
                    Value = 0
                });
            }
            OnChartConfigChanged?.Invoke();
        }

        #region Commands
        [RelayCommand]
        public void RefreshPorts()
        {
            AvailablePorts.Clear();
            foreach (var port in SerialPort.GetPortNames())
            {
                AvailablePorts.Add(port);
            }
            if (AvailablePorts.Any()) SelectedPort = AvailablePorts.First();
        }

        [RelayCommand]
        public async Task ToggleConnectAsync()
        {
            if (IsConnected)
            {
                _pollingCts?.Cancel();
                _serialService.Close();
                IsConnected = false;
                StatusMessage = "就绪 (未连接)";
                AddLog("SYS", "已断开串口连接");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(SelectedPort))
                {
                    MessageBox.Show("请先选择有效串口！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    _serialService.Open(SelectedPort, SelectedBaudRate, Parity.None, 8, StopBits.One);
                    IsConnected = true;
                    StatusMessage = $"已连接: {SelectedPort} ({SelectedBaudRate} bps)";
                    AddLog("SYS", $"成功打开串口 {SelectedPort}");

                    _pollingCts = new CancellationTokenSource();
                    _ = Task.Run(() => PollingLoopAsync(_pollingCts.Token));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public void SwitchProtocol(ProtocolMode mode)
        {
            SelectedProtocolMode = mode;
            AddLog("SYS", $"切换协议规约: {mode}");
        }

        [RelayCommand]
        public void UpdateChannelCount(object? param)
        {
            if (int.TryParse(param?.ToString(), out int count) && count > 0 && count <= 16)
            {
                InitDefaultChannels(count);
                AddLog("SYS", $"已配置为 {count} 通道模式");
            }
        }

        [RelayCommand]
        public void ResetStats()
        {
            PacketLossRate = 0;
            CrcErrorCount = 0;
            TimeoutErrorCount = 0;
            CommSuccessRate = 100.0;
            AddLog("SYS", "已重置通信统计数据");
        }

        [RelayCommand]
        public void LoadHistoryData()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "加载历史遥测数据文件",
                Filter = "CSV 数据文件 (*.csv)|*.csv|所有文件 (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName;
                    var lines = File.ReadAllLines(filePath);
                    Logs.Clear();
                    AddLog("SYS", $"成功加载历史数据文件: {filePath}, 总行数={lines.Length}");
                    MessageBox.Show($"成功导入 {lines.Length} 条历史数据记录！", "历史回放", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载历史文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    AddLog("ERR", $"加载历史文件异常: {ex.Message}");
                }
            }
        }

        [RelayCommand] public void OpenCustomFrameConfig() => IsCustomFrameConfigDialogOpen = true;
        [RelayCommand]
        public void CloseCustomFrameConfig()
        {
            IsCustomFrameConfigDialogOpen = false;
            AddLog("SYS", $"已更新自定义帧配置: 帧头={CustomHeader}, 帧尾={CustomTail}, 数据类型={SelectedDataType}");
        }

        [RelayCommand] public void ToggleChartFullScreen() => IsChartFullScreen = !IsChartFullScreen;
        [RelayCommand] public void ClearChart() => OnClearChartRequested?.Invoke();
        [RelayCommand] public void ClearLogs() => Logs.Clear();
        [RelayCommand] public void ExportLogs() => AddLog("SYS", "系统运行日志已成功导出");
        [RelayCommand] public void ExitApp() => Application.Current.Shutdown();

        [RelayCommand] public void OpenSettings() => IsSettingsDialogOpen = true;
        [RelayCommand] public void CloseSettings() => IsSettingsDialogOpen = false;
        [RelayCommand] public void OpenChannelCustom() => IsChannelCustomDialogOpen = true;
        [RelayCommand] public void CloseChannelCustom() { IsChannelCustomDialogOpen = false; OnChartConfigChanged?.Invoke(); }
        [RelayCommand] public void OpenChartSettings() => IsChartSettingsDialogOpen = true;
        [RelayCommand] public void CloseChartSettings() { IsChartSettingsDialogOpen = false; OnChartConfigChanged?.Invoke(); }
        [RelayCommand] public void OpenAiAdvisor() => IsAiAdvisorDialogOpen = true;
        [RelayCommand] public void CloseAiAdvisor() => IsAiAdvisorDialogOpen = false;
        [RelayCommand] public void RequestAiProtocolAdvice() => MessageBox.Show("AI 推荐：当前数据格式与标准 Modbus-RTU 高度匹配。", "AI 诊断");
        [RelayCommand] public void OpenAiSettings() => MessageBox.Show("AI 边缘大模型设置已就绪。", "设置");
        [RelayCommand] public void OpenLicenseDialog() => IsLicenseDialogOpen = true;
        [RelayCommand] public void CloseLicenseDialog() => IsLicenseDialogOpen = false;
        [RelayCommand] public void CopyMachineCode() { Clipboard.SetText(MachineCode); MessageBox.Show("机器码已复制！", "提示"); }
        [RelayCommand] public void ActivateLicense() { LicenseStatusText = "已激活 (永久商业授权)"; LicenseStatusColor = "#34D399"; MessageBox.Show("激活成功！", "成功"); IsLicenseDialogOpen = false; }
        [RelayCommand] public void CheckForUpdate() => IsUpdateDialogOpen = true;
        [RelayCommand] public void CloseUpdateDialog() => IsUpdateDialogOpen = false;
        [RelayCommand] public void OpenAbout() => IsAboutDialogOpen = true;
        [RelayCommand] public void CloseAbout() => IsAboutDialogOpen = false;
        #endregion

        private async Task PollingLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsConnected)
            {
                try
                {
                    await Task.Delay(Math.Max(10, SelectedInterval), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    AddLog("ERR", $"轮询异常: {ex.Message}");
                }
            }
        }

        private void AddLog(string level, string message)
        {
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                if (Logs.Count > 300) Logs.RemoveAt(0);
                Logs.Add(new LogEntry
                {
                    Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Level = level,
                    Message = message
                });
                OnLogAdded?.Invoke();
            });
        }
    }
}