using RJCP.IO.Ports;
using SerialSnoop.Wpf.Services;
using SerialSnoop.Wpf.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SerialSnoop.Wpf.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<string> AvailablePorts { get; } = new();

    private string? _selectedUpstreamPort;
    public string? SelectedUpstreamPort { get => _selectedUpstreamPort; set { _selectedUpstreamPort = value; OnPropertyChanged(); UpdateCanStart(); } }

    private string? _selectedDownstreamPort;
    public string? SelectedDownstreamPort { get => _selectedDownstreamPort; set { _selectedDownstreamPort = value; OnPropertyChanged(); UpdateCanStart(); } }

    private string? _selectedMirrorPort;
    public string? SelectedMirrorPort { get => _selectedMirrorPort; set { _selectedMirrorPort = value; OnPropertyChanged(); UpdateCanStart(); } }

    public ObservableCollection<int> BaudRates { get; } = new(new[] { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 });

    private int _baudRate = 115200;
    public int BaudRate { get => _baudRate; set { _baudRate = value; OnPropertyChanged(); } }

    private int _dataBits = 8;
    public int DataBits { get => _dataBits; set { _dataBits = value; OnPropertyChanged(); } }

    public Array Parities { get; } = Enum.GetValues(typeof(Parity));
    private Parity _parity = Parity.None;
    public Parity Parity { get => _parity; set { _parity = value; OnPropertyChanged(); } }

    public Array StopBitsValues { get; } = Enum.GetValues(typeof(StopBits));
    private StopBits _stopBits = StopBits.One;
    public StopBits StopBits { get => _stopBits; set { _stopBits = value; OnPropertyChanged(); } }

    public Array Handshakes { get; } = Enum.GetValues(typeof(Handshake));
    private Handshake _handshake = Handshake.None;
    public Handshake Handshake { get => _handshake; set { _handshake = value; OnPropertyChanged(); } }

    private bool _dtrEnable;
    public bool DtrEnable { get => _dtrEnable; set { _dtrEnable = value; OnPropertyChanged(); } }

    private bool _rtsEnable;
    public bool RtsEnable { get => _rtsEnable; set { _rtsEnable = value; OnPropertyChanged(); } }

    private bool _autoScroll = true;
    public bool AutoScroll { get => _autoScroll; set { _autoScroll = value; OnPropertyChanged(); } }

    private bool _logWrites = true;
    public bool LogWrites { get => _logWrites; set { _logWrites = value; OnPropertyChanged(); } }

    private string _statusText = "Idle";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set { _isRunning = value; OnPropertyChanged(); UpdateCanStart(); } }

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public RelayCommand RefreshPortsCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand SaveLogCommand { get; }

    private readonly ConcurrentQueue<LogEntry> _pending = new();
    private readonly DispatcherTimer _drainTimer;
    private readonly FileLogger _fileLogger = new();
    private readonly DiagnosticsLogger _diagLogger;
    private SerialBridge? _bridge;
    private const int LogCapacity = 10000;
    private const int DrainBatchSize = 200;

    public MainViewModel()
    {
        _diagLogger = new DiagnosticsLogger(System.IO.Path.Combine("logs", "diagnostics.log"));
        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        StartCommand = new RelayCommand(Start, () => CanStart());
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        ClearLogCommand = new RelayCommand(ClearLog);
        SaveLogCommand = new RelayCommand(SaveLog);

        _drainTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        _drainTimer.Tick += (s, e) => DrainLog();
        _drainTimer.Start();

        RefreshPorts();
    }

    private void UpdateCanStart()
    {
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }

    private bool CanStart()
    {
        if (IsRunning) return false;
        if (string.IsNullOrWhiteSpace(SelectedUpstreamPort)) return false;
        if (string.IsNullOrWhiteSpace(SelectedDownstreamPort)) return false;
        if (string.Equals(SelectedUpstreamPort, SelectedDownstreamPort, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(SelectedMirrorPort))
        {
            if (string.Equals(SelectedUpstreamPort, SelectedMirrorPort, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(SelectedDownstreamPort, SelectedMirrorPort, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private void RefreshPorts()
    {
        try
        {
            var ports = (new SerialPortStream()).GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
            AvailablePorts.Clear();
            foreach (var p in ports) AvailablePorts.Add(p);
            StatusText = ports.Length == 0 ? "No COM ports found" : $"Found {ports.Length} ports";
        }
        catch (Exception ex)
        {
            StatusText = $"Error listing ports: {ex.Message}";
        }
    }

    private SerialPortConfig BuildConfig(string portName)
    {
        return new SerialPortConfig
        {
            PortName = portName,
            BaudRate = BaudRate,
            DataBits = DataBits,
            Parity = Parity,
            StopBits = StopBits,
            Handshake = Handshake,
            DtrEnable = DtrEnable,
            RtsEnable = RtsEnable,
        };
    }

    private void Start()
    {
        if (!CanStart()) return;
        try
        {
            _bridge = new SerialBridge();
            _bridge.DataRelayed += OnDataRelayed;
            _bridge.Stopped += OnBridgeStopped;
            _bridge.DiagnosticsUpdated += OnDiagnostics;

            SerialPortConfig? mirrorConfig = null;
            if (!string.IsNullOrWhiteSpace(SelectedMirrorPort))
            {
                mirrorConfig = BuildConfig(SelectedMirrorPort);
            }

            _bridge.Start(BuildConfig(SelectedUpstreamPort!), BuildConfig(SelectedDownstreamPort!), mirrorConfig);
            IsRunning = true;
            
            string mirrorStatus = mirrorConfig != null ? $" + Mirror({SelectedMirrorPort})" : "";
            StatusText = $"Running: {SelectedUpstreamPort} ⇄ {SelectedDownstreamPort}{mirrorStatus} @ {BaudRate}bps";
        }
        catch (Exception ex)
        {
            StatusText = $"Start failed: {ex.Message}";
            MessageBox.Show(ex.Message, "Failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            IsRunning = false;
        }
    }

    private void Stop()
    {
        try
        {
            _bridge?.Stop();
        }
        catch { }
        finally
        {
            IsRunning = false;
            StatusText = "Stopped";
        }
    }

    private async void SaveLog()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save Log As",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = $"serial_snoop_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                using var writer = new System.IO.StreamWriter(dlg.FileName, append: true, System.Text.Encoding.UTF8);
                foreach (var e in LogEntries)
                {
                    string line = $"{e.Timestamp:HH:mm:ss.fff}\t{e.Direction}\t{e.Length}\t{e.Hex}\t{e.Ascii}";
                    await writer.WriteLineAsync(line);
                }
                StatusText = $"Saved log to {dlg.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ClearLog()
    {
        LogEntries.Clear();
        StatusText = "Log cleared";
    }

    private void OnBridgeStopped(Exception? ex)
    {
        // Log the exception details to diagnostics and then update UI
        if (ex != null)
        {
            try { _ = _diagLogger.LogAsync($"BridgeStopped: {ex}"); } catch { }
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            IsRunning = false;
            StatusText = ex is null ? "Bridge stopped" : $"Bridge error: {ex.Message}";
        });
    }

    private void OnDataRelayed(Direction dir, byte[] bytes)
    {
        if (!LogWrites) return;
        var now = DateTime.Now;
        var hex = HexDump.ToHex(bytes, 256);
        var ascii = HexDump.ToAscii(bytes, 256);
        var entry = new LogEntry(now, dir == Direction.Tx ? "TX" : "RX", bytes.Length, hex, ascii);
        _pending.Enqueue(entry);
    }

    private void DrainLog()
    {
        int added = 0;
        for (int i = 0; i < DrainBatchSize && _pending.TryDequeue(out var entry); i++)
        {
            LogEntries.Add(entry);
            added++;
        }

        if (added == 0)
        {
            return;
        }

        // Trim to capacity
        while (LogEntries.Count > LogCapacity)
        {
            LogEntries.RemoveAt(0);
        }

        // If there are still pending items, show a brief status indicating backlog
        if (!_pending.IsEmpty)
        {
            StatusText = $"Running: {SelectedUpstreamPort} ⇄ {SelectedDownstreamPort} @ {BaudRate}bps — Pending: {_pending.Count}";
        }
    }

    private void OnDiagnostics(BridgeStats stats)
    {
        // Log diagnostics asynchronously and update status briefly
        _ = _diagLogger.LogAsync($"UpPending={stats.UpPending} DownPending={stats.DownPending} UpDropped={stats.UpDropped} DownDropped={stats.DownDropped} BytesUp={stats.BytesUp} BytesDown={stats.BytesDown}");
        Application.Current.Dispatcher.Invoke(() =>
        {
            // don't clobber other status text unless running
            if (IsRunning)
            {
                StatusText = $"Running: {SelectedUpstreamPort} ⇄ {SelectedDownstreamPort} @ {BaudRate}bps — UpPend={stats.UpPending} DownPend={stats.DownPending}";
            }
        });
    }

    public void Dispose()
    {
        _bridge?.Dispose();
        _fileLogger.Dispose();
        _diagLogger.Dispose();
    }
}
