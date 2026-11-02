using System;
using System.Globalization;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Forms;
using FieldLink.PacketBench.Connections;
using FieldLink.PacketBench.Diagnostics;
using FieldLink.PacketBench.Ui;
using FieldLink.Plc;

namespace FieldLink.PacketBench;

internal sealed partial class MainForm
{
    private void ApplyClientSelection()
    {
        if (_clientCombo.SelectedItem is not ClientDescriptor descriptor)
        {
            return;
        }

        _addressHintLabel.Text = "주소 예: " + descriptor.AddressExample;
        _toolTip.SetToolTip(_addressHintLabel, "주소 예: " + descriptor.AddressExample);

        bool isSerial = descriptor.Transport == TransportKind.Serial;
        foreach ((Label label, Control control) in _serialRows)
        {
            label.Visible = isSerial;
            control.Visible = isSerial;
        }

        _hostBox.Enabled = !isSerial;
        _portBox.Enabled = !isSerial;

        if (!isSerial && descriptor.DefaultPort > 0)
        {
            _portBox.Value = descriptor.DefaultPort;
        }

        if (descriptor.DefaultBaudRate is int baudRate)
        {
            _baudCombo.Text = baudRate.ToString(CultureInfo.InvariantCulture);
        }

        if (descriptor.DefaultParity is Parity parity)
        {
            _parityCombo.SelectedItem = parity;
        }

        if (descriptor.DefaultDataBits is int dataBits)
        {
            _dataBitsCombo.SelectedItem = dataBits;
        }

        _stationBox.Enabled = descriptor.UsesStation;
    }

    private ConnectionSettings ReadConnectionSettings()
    {
        ByteOrder? byteOrder = _byteOrderCombo.SelectedItem is Choice<ByteOrder?> choice ? choice.Value : null;

        return new ConnectionSettings
        {
            Host = _hostBox.Text.Trim(),
            Port = (int)_portBox.Value,
            SerialPortName = _serialPortCombo.Text.Trim(),
            BaudRate = int.TryParse(_baudCombo.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int baudRate)
                ? baudRate
                : 9600,
            Parity = _parityCombo.SelectedItem is Parity parity ? parity : Parity.Even,
            DataBits = _dataBitsCombo.SelectedItem is int dataBits ? dataBits : 8,
            StopBits = _stopBitsCombo.SelectedItem is StopBits stopBits ? stopBits : StopBits.One,
            Station = (byte)_stationBox.Value,
            TimeoutMilliseconds = (int)_timeoutBox.Value,
            ByteOrder = byteOrder,
        };
    }

    private async void OnConnectClickedAsync(object? sender, EventArgs e)
    {
        await ConnectAsync();
    }

    private async void OnDisconnectClickedAsync(object? sender, EventArgs e)
    {
        await DisconnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (_client is not null || _isBusy)
        {
            return;
        }

        if (_clientCombo.SelectedItem is not ClientDescriptor descriptor)
        {
            return;
        }

        ConnectionSettings settings = ReadConnectionSettings();
        SetBusy(true);

        RecordingTransport? recorder = null;
        try
        {
            recorder = new RecordingTransport(settings.CreateTransport(descriptor.Transport), _packetLog);
            PlcClient client = descriptor.CreateClient(recorder, settings);

            PlcResult result = await client.OpenAsync().ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                client.Dispose();
                ShowConnectionError("연결 실패", result.Error);
                return;
            }

            _client = client;
            _lastConnectionError = string.Empty;
            SetWork($"{descriptor.DisplayName} 연결됨 · {DateTime.Now:HH:mm:ss}");
        }
        catch (Exception exception)
        {
            recorder?.Dispose();
            ShowConnectionError("연결 실패", new PlcError(PlcErrorKind.ConnectionFailed, exception.Message, exception: exception));
        }
        finally
        {
            SetBusy(false);
            UpdateConnectionUi();
        }
    }

    private async Task DisconnectAsync()
    {
        PlcClient? client = _client;
        if (client is null || _isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            PlcResult result = await client.CloseAsync().ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                SetWork($"연결 해제 중 오류: {result.Error.Message}", isError: true);
            }
            else
            {
                SetWork("연결을 닫았습니다.");
            }

            client.Dispose();
            _client = null;
        }
        finally
        {
            SetBusy(false);
            UpdateConnectionUi();
        }
    }
}
