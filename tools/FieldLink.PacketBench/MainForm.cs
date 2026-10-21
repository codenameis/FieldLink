using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FieldLink.PacketBench.Connections;
using FieldLink.PacketBench.Diagnostics;
using FieldLink.PacketBench.VirtualPlc;
using FieldLink.Plc;

namespace FieldLink.PacketBench;

internal sealed partial class MainForm : Form
{
    private const int MaxPacketRows = 1200;

    private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);
    private static readonly Color ErrorColor = Color.FromArgb(196, 43, 28);
    private static readonly Color SuccessColor = Color.FromArgb(16, 124, 16);
    private static readonly Color MutedColor = Color.FromArgb(96, 100, 106);

    private readonly PacketLog _packetLog = new();
    private readonly VirtualPlcServer _virtualPlc;
    private readonly Timer _packetTimer = new();
    private readonly Timer _memoryTimer = new();
    private readonly ToolTip _toolTip = new();
    private readonly List<(Label Label, Control Control)> _serialRows = new();
    private readonly VirtualPlcMemory _modbusMemory = new(coilCount: 4096, discreteInputCount: 4096, holdingRegisterCount: 4096, inputRegisterCount: 4096);
    private readonly Mc3EMemory _mcMemory = new();

    private PlcClient? _client;
    private string _lastConnectionError = string.Empty;
    private bool _isBusy;
    private bool _packetPaused;
    private readonly bool _packetAutoScroll = true;
    private bool _suppressMemoryEdit;
    private bool _updatingReadUnit;
    private bool _updatingMemoryAreas;

    private SplitContainer _mainSplit = null!;
    private SplitContainer _sideSplit = null!;

    private ComboBox _clientCombo = null!;
    private Label _addressHintLabel = null!;
    private TextBox _hostBox = null!;
    private NumericUpDown _portBox = null!;
    private ComboBox _serialPortCombo = null!;
    private ComboBox _baudCombo = null!;
    private ComboBox _parityCombo = null!;
    private ComboBox _dataBitsCombo = null!;
    private ComboBox _stopBitsCombo = null!;
    private NumericUpDown _stationBox = null!;
    private NumericUpDown _timeoutBox = null!;
    private ComboBox _byteOrderCombo = null!;
    private Button _connectButton = null!;
    private Button _disconnectButton = null!;
    private Label _connectionStatusLabel = null!;

    private ComboBox _virtualProtocolCombo = null!;
    private NumericUpDown _virtualPortBox = null!;
    private NumericUpDown _virtualUnitBox = null!;
    private NumericUpDown _virtualDelayBox = null!;
    private ComboBox _virtualExceptionCombo = null!;
    private CheckBox _virtualSuppressCheck = null!;
    private Button _virtualStartButton = null!;
    private Button _virtualStopButton = null!;
    private Button _virtualConnectButton = null!;
    private Label _virtualStatusLabel = null!;

    private TextBox _readAddressBox = null!;
    private ComboBox _readTypeCombo = null!;
    private ComboBox _readUnitCombo = null!;
    private NumericUpDown _readCountBox = null!;
    private Button _readButton = null!;
    private Label _readInfoLabel = null!;
    private DataGridView _readGrid = null!;
    private TextBox _readRawBox = null!;

    private TextBox _writeAddressBox = null!;
    private ComboBox _writeTypeCombo = null!;
    private ComboBox _writeModeCombo = null!;
    private CheckBox _writeVerifyCheck = null!;
    private Button _writeButton = null!;
    private TextBox _writeValuesBox = null!;
    private NumericUpDown _genStartBox = null!;
    private NumericUpDown _genStepBox = null!;
    private NumericUpDown _genCountBox = null!;
    private Button _generateButton = null!;
    private Button _clearValuesButton = null!;
    private Label _writeResultLabel = null!;
    private DataGridView _writeGrid = null!;

    private ComboBox _memoryAreaCombo = null!;
    private NumericUpDown _memoryStartBox = null!;
    private NumericUpDown _memoryCountBox = null!;
    private Button _memoryRefreshButton = null!;
    private Button _memoryPatternButton = null!;
    private Button _memoryClearButton = null!;
    private CheckBox _memoryAutoCheck = null!;
    private DataGridView _memoryGrid = null!;
    private DataGridViewTextBoxColumn _memoryValueColumn = null!;
    private DataGridViewCheckBoxColumn _memoryBitColumn = null!;

    private Button _packetPauseButton = null!;
    private Button _packetClearButton = null!;
    private Button _packetCopyButton = null!;
    private Button _packetSaveButton = null!;
    private CheckBox _packetServerCheck = null!;
    private Label _packetCountLabel = null!;
    private DataGridView _packetGrid = null!;
    private TextBox _packetDetailBox = null!;

    private ToolStripStatusLabel _statusConnectionLabel = null!;
    private ToolStripStatusLabel _statusVirtualLabel = null!;
    private ToolStripStatusLabel _statusCounterLabel = null!;
    private ToolStripStatusLabel _statusWorkLabel = null!;

    public MainForm()
    {
        _virtualPlc = new VirtualPlcServer(_packetLog);

        BuildUi();

        _packetTimer.Interval = 120;
        _packetTimer.Tick += (_, _) => OnPacketTimerTick();
        _packetTimer.Start();

        _memoryTimer.Interval = 1000;
        _memoryTimer.Tick += (_, _) => AutoRefreshMemory();
        _memoryTimer.Start();

        FormClosing += OnFormClosing;

        UpdateConnectionUi();
        UpdateVirtualPlcUi();
        UpdateReadInfo();
        RefreshMemoryAreaOptions();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _packetTimer.Stop();
        _memoryTimer.Stop();

        _client?.Dispose();
        _client = null;

        _virtualPlc.Dispose();
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UseWaitCursor = isBusy;
        _readButton.Enabled = !isBusy;
        _writeButton.Enabled = !isBusy;
        _generateButton.Enabled = !isBusy;
        _virtualStartButton.Enabled = !isBusy && !_virtualPlc.IsRunning;
        _virtualStopButton.Enabled = !isBusy && _virtualPlc.IsRunning;
        _virtualConnectButton.Enabled = !isBusy;
        UpdateConnectionUi();
    }

    private void UpdateConnectionUi()
    {
        bool isConnected = _client is not null;

        _connectButton.Enabled = !_isBusy && !isConnected;
        _disconnectButton.Enabled = !_isBusy && isConnected;

        if (isConnected && _clientCombo.SelectedItem is ClientDescriptor descriptor)
        {
            _connectionStatusLabel.Text = $"연결됨 · {descriptor.DisplayName}";
            _connectionStatusLabel.ForeColor = SuccessColor;
            _statusConnectionLabel.Text = "연결됨";
            return;
        }

        bool hasError = _lastConnectionError.Length > 0;
        _connectionStatusLabel.Text = hasError ? _lastConnectionError : "연결 안 됨";
        _connectionStatusLabel.ForeColor = hasError ? ErrorColor : MutedColor;
        _statusConnectionLabel.Text = "연결 안 됨";
    }

    private void SetWork(string message, bool isError = false)
    {
        _statusWorkLabel.Text = message;
        _statusWorkLabel.ForeColor = isError ? ErrorColor : MutedColor;
    }

    private void ShowConnectionError(string title, PlcError error)
    {
        _lastConnectionError = DescribeError(title, error);
        SetWork(_lastConnectionError, isError: true);
        UpdateConnectionUi();
    }

    private static string DescribeError(string title, PlcError error)
    {
        return error.DeviceErrorCode is null
            ? $"{title}: {error.Message}"
            : $"{title}: {error.Message} (장치 코드 0x{error.DeviceErrorCode:X2})";
    }

    private void UpdateStatusCounters()
    {
        string text = $"TX {_packetLog.SentCount} / RX {_packetLog.ReceivedCount}";
        _statusCounterLabel.Text = text;
        _packetCountLabel.Text = text;
    }
}
