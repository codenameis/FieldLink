using System;
using System.Globalization;
using System.Windows.Forms;
using FieldLink.PacketBench.Connections;
using FieldLink.PacketBench.Operations;
using FieldLink.PacketBench.Ui;
using FieldLink.PacketBench.VirtualPlc;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.PacketBench;

internal sealed partial class MainForm
{
    private VirtualPlcProtocol SelectedVirtualProtocol =>
        _virtualProtocolCombo.SelectedItem is Choice<VirtualPlcProtocol> choice
            ? choice.Value
            : VirtualPlcProtocol.ModbusTcp;

    private string SelectedMemoryArea =>
        _memoryAreaCombo.SelectedItem is Choice<string> choice ? choice.Value : string.Empty;

    private void OnVirtualProtocolChanged()
    {
        VirtualPlcProtocol protocol = SelectedVirtualProtocol;

        _virtualPortBox.Value = protocol == VirtualPlcProtocol.Mc3EBinary ? 5000 : 1502;
        _virtualExceptionCombo.Enabled = protocol == VirtualPlcProtocol.ModbusTcp;
        _virtualConnectButton.Text = protocol == VirtualPlcProtocol.Mc3EBinary ? "MC로 연결" : "여기에 연결";

        RefreshMemoryAreaOptions();
        SetWork(protocol == VirtualPlcProtocol.Mc3EBinary
            ? "가상 PLC 프로토콜: MC 3E Binary (Q/L · 일괄 읽기/쓰기)"
            : "가상 PLC 프로토콜: Modbus TCP");
    }

    private void RefreshMemoryAreaOptions()
    {
        _updatingMemoryAreas = true;
        _memoryAreaCombo.Items.Clear();

        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            foreach (Mc3EDevice device in Enum.GetValues<Mc3EDevice>())
            {
                _memoryAreaCombo.Items.Add(new Choice<string>(device.ToString(), device.GetDisplayName()));
            }
        }
        else
        {
            _memoryAreaCombo.Items.Add(new Choice<string>("4x", "보유 레지스터 (4x)"));
            _memoryAreaCombo.Items.Add(new Choice<string>("0x", "코일 (0x)"));
            _memoryAreaCombo.Items.Add(new Choice<string>("3x", "입력 레지스터 (3x)"));
            _memoryAreaCombo.Items.Add(new Choice<string>("1x", "이산 입력 (1x)"));
        }

        _memoryAreaCombo.SelectedIndex = 0;
        _updatingMemoryAreas = false;

        RefreshMemoryGrid();
    }

    private void OnVirtualStartClicked(object? sender, EventArgs e)
    {
        if (_virtualPlc.IsRunning)
        {
            return;
        }

        StartVirtualPlc();
    }

    private void StartVirtualPlc()
    {
        int port = (int)_virtualPortBox.Value;
        IVirtualPlcResponder responder = SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary
            ? new Mc3EBinaryResponder(_mcMemory)
            : new ModbusTcpResponder(_modbusMemory);

        try
        {
            _virtualPlc.Start(port, responder);
            ApplyVirtualPlcSettings();
            UpdateVirtualPlcUi();
            RefreshMemoryGrid();
            SetWork($"가상 PLC 시작 · 127.0.0.1:{port} ({responder.DisplayName})");
        }
        catch (Exception exception)
        {
            SetWork($"가상 PLC 시작 실패: {exception.Message}", isError: true);
        }
    }

    private void OnVirtualStopClicked(object? sender, EventArgs e)
    {
        _virtualPlc.Stop();
        UpdateVirtualPlcUi();
        SetWork("가상 PLC를 중지했습니다.");
    }

    private async void OnVirtualConnectClickedAsync(object? sender, EventArgs e)
    {
        if (_client is not null || _isBusy)
        {
            return;
        }

        if (!_virtualPlc.IsRunning)
        {
            StartVirtualPlc();
        }

        if (!_virtualPlc.IsRunning)
        {
            return;
        }

        SelectClient(SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary ? "mc-binary" : "modbus-tcp");
        _hostBox.Text = "127.0.0.1";
        _portBox.Value = _virtualPlc.Port;
        _stationBox.Value = _virtualPlc.Settings.UnitId == 0 ? 1 : _virtualPlc.Settings.UnitId;

        await ConnectAsync();
    }

    private void SelectClient(string id)
    {
        foreach (object item in _clientCombo.Items)
        {
            if (item is ClientDescriptor descriptor && descriptor.Id == id)
            {
                _clientCombo.SelectedItem = descriptor;
                return;
            }
        }
    }

    private void ApplyVirtualPlcSettings()
    {
        _virtualPlc.Settings.UnitId = (byte)_virtualUnitBox.Value;
        _virtualPlc.Settings.ResponseDelayMilliseconds = (int)_virtualDelayBox.Value;
        _virtualPlc.Settings.ForcedExceptionCode =
            _virtualExceptionCombo.SelectedItem is Choice<byte> choice ? choice.Value : (byte)0;
        _virtualPlc.Settings.SuppressResponse = _virtualSuppressCheck.Checked;
    }

    private void UpdateVirtualPlcUi()
    {
        bool isRunning = _virtualPlc.IsRunning;

        _virtualStartButton.Enabled = !_isBusy && !isRunning;
        _virtualStopButton.Enabled = !_isBusy && isRunning;

        // 실행 중이면 실제로 돌고 있는 응답기의 이름을 쓴다. (설정을 바꿔도 다시 시작하기 전까지는 그대로)
        string protocolName = _virtualPlc.Responder?.DisplayName
            ?? (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary ? "MC 3E Binary (Q/L)" : "Modbus TCP");
        _virtualStatusLabel.Text = isRunning
            ? $"실행 중 · 127.0.0.1:{_virtualPlc.Port} · {protocolName} · 접속 {_virtualPlc.ClientCount}"
            : "중지됨";
        _virtualStatusLabel.ForeColor = isRunning ? SuccessColor : MutedColor;
        _statusVirtualLabel.Text = isRunning ? $"가상 PLC :{_virtualPlc.Port} ({protocolName})" : "가상 PLC 중지";
    }

    private bool IsBitArea(string area)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            return area is "M" or "X" or "Y";
        }

        return area is "0x" or "1x";
    }

    private bool IsWritableArea(string area)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            return area.Length > 0 && area != "X";
        }

        return area is "4x" or "0x";
    }

    private int GetAreaCapacity(string area)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            return Enum.TryParse(area, out Mc3EDevice device) ? _mcMemory.GetCapacity(device) : 0;
        }

        return area switch
        {
            "4x" => _modbusMemory.HoldingRegisterCount,
            "3x" => _modbusMemory.InputRegisterCount,
            "0x" => _modbusMemory.CoilCount,
            "1x" => _modbusMemory.DiscreteInputCount,
            _ => 0,
        };
    }

    private ushort GetAreaWord(string area, int index)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            return Enum.TryParse(area, out Mc3EDevice device) ? _mcMemory.GetWord(device, index) : (ushort)0;
        }

        return _modbusMemory.GetRegister(ToModbusArea(area), index);
    }

    private void SetAreaWord(string area, int index, ushort value)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            if (Enum.TryParse(area, out Mc3EDevice device))
            {
                _mcMemory.SetWord(device, index, value);
            }

            return;
        }

        _modbusMemory.SetRegister(ToModbusArea(area), index, value);
    }

    private bool GetAreaBit(string area, int index)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            return Enum.TryParse(area, out Mc3EDevice device) && _mcMemory.GetBit(device, index);
        }

        return area == "0x" ? _modbusMemory.GetCoil(index) : _modbusMemory.GetBit(ToModbusArea(area), index);
    }

    private void SetAreaBit(string area, int index, bool value)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            if (Enum.TryParse(area, out Mc3EDevice device))
            {
                _mcMemory.SetBit(device, index, value);
            }

            return;
        }

        if (area == "0x")
        {
            _modbusMemory.SetCoil(index, value);
        }
    }

    private static ModbusArea ToModbusArea(string area) => area switch
    {
        "4x" => ModbusArea.HoldingRegister,
        "3x" => ModbusArea.InputRegister,
        "0x" => ModbusArea.Coil,
        "1x" => ModbusArea.DiscreteInput,
        _ => ModbusArea.HoldingRegister,
    };

    private void RefreshMemoryGrid()
    {
        if (_memoryAreaCombo.SelectedItem is not Choice<string>)
        {
            return;
        }

        string area = SelectedMemoryArea;
        bool isBitArea = IsBitArea(area);
        bool isWritable = IsWritableArea(area);
        int capacity = GetAreaCapacity(area);
        int start = (int)_memoryStartBox.Value;
        int count = (int)_memoryCountBox.Value;

        _suppressMemoryEdit = true;
        _memoryGrid.SuspendLayout();
        _memoryGrid.Rows.Clear();

        _memoryValueColumn.Visible = !isBitArea;
        _memoryBitColumn.Visible = isBitArea;
        _memoryValueColumn.ReadOnly = !isWritable;
        _memoryBitColumn.ReadOnly = !isWritable;

        for (int i = 0; i < count; i++)
        {
            int index = start + i;
            if (index >= capacity)
            {
                break;
            }

            if (isBitArea)
            {
                bool bit = GetAreaBit(area, index);
                _memoryGrid.Rows.Add(index, string.Empty, bit, string.Empty, string.Empty);
            }
            else
            {
                ushort value = GetAreaWord(area, index);
                _memoryGrid.Rows.Add(
                    index,
                    value.ToString(CultureInfo.InvariantCulture),
                    false,
                    value.ToString("X4", CultureInfo.InvariantCulture),
                    Convert.ToString(value, 2).PadLeft(16, '0'));
            }
        }

        _memoryGrid.ResumeLayout();
        _suppressMemoryEdit = false;
    }

    private void AutoRefreshMemory()
    {
        if (!_memoryAutoCheck.Checked || !_virtualPlc.IsRunning || _memoryGrid.IsCurrentCellInEditMode)
        {
            return;
        }

        RefreshMemoryGrid();
    }

    private void OnMemoryCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressMemoryEdit || e.RowIndex < 0 || e.ColumnIndex != _memoryValueColumn.Index)
        {
            return;
        }

        string area = SelectedMemoryArea;
        if (IsBitArea(area) || !IsWritableArea(area))
        {
            return;
        }

        int index = ReadMemoryIndex(e.RowIndex);
        string text = Convert.ToString(_memoryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, CultureInfo.InvariantCulture)
            ?? string.Empty;

        if (ValueTextParser.TryParseUInt16(text, out ushort value))
        {
            SetAreaWord(area, index, value);
        }
        else
        {
            SetWork($"'{text}'을(를) 16비트 값으로 해석할 수 없습니다.", isError: true);
        }

        RefreshMemoryGrid();
    }

    private void OnMemoryCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressMemoryEdit || e.RowIndex < 0 || e.ColumnIndex != _memoryBitColumn.Index)
        {
            return;
        }

        string area = SelectedMemoryArea;
        if (!IsBitArea(area) || !IsWritableArea(area))
        {
            return;
        }

        int index = ReadMemoryIndex(e.RowIndex);
        bool value = Convert.ToBoolean(_memoryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, CultureInfo.InvariantCulture);
        SetAreaBit(area, index, value);
    }

    private void OnMemoryCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_memoryGrid.IsCurrentCellDirty && _memoryGrid.CurrentCell is DataGridViewCheckBoxCell)
        {
            _memoryGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void OnMemoryPatternClicked(object? sender, EventArgs e)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            _mcMemory.FillTestPattern();
        }
        else
        {
            _modbusMemory.FillTestPattern();
        }

        RefreshMemoryGrid();
        SetWork("가상 PLC 메모리에 테스트 패턴을 채웠습니다.");
    }

    private void OnMemoryClearClicked(object? sender, EventArgs e)
    {
        if (SelectedVirtualProtocol == VirtualPlcProtocol.Mc3EBinary)
        {
            _mcMemory.Clear();
        }
        else
        {
            _modbusMemory.Clear();
        }

        RefreshMemoryGrid();
        SetWork("가상 PLC 메모리를 0으로 지웠습니다.");
    }

    private int ReadMemoryIndex(int rowIndex)
    {
        object? cellValue = _memoryGrid.Rows[rowIndex].Cells[0].Value;
        return cellValue is null ? 0 : Convert.ToInt32(cellValue, CultureInfo.InvariantCulture);
    }
}
