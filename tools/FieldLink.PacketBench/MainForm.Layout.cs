using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using FieldLink.PacketBench.Connections;
using FieldLink.PacketBench.Operations;
using FieldLink.PacketBench.Ui;
using FieldLink.PacketBench.VirtualPlc;
using FieldLink.Plc;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.PacketBench;

internal sealed partial class MainForm
{
    private void BuildUi()
    {
        Text = "FieldLink 패킷 벤치";
        StartPosition = FormStartPosition.CenterScreen;

        // 작은 화면에서도 창이 화면 밖으로 나가지 않게 작업 영역에 맞춘다.
        Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1360, 900);
        int width = Math.Max(900, Math.Min(1360, workingArea.Width - 24));
        int height = Math.Max(620, Math.Min(900, workingArea.Height - 48));
        ClientSize = new Size(width, height);
        MinimumSize = new Size(900, 620);

        Font = new Font("Malgun Gothic", 9F);
        BackColor = Color.FromArgb(247, 248, 250);
        AutoScaleMode = AutoScaleMode.Dpi;

        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(228, 230, 235),
        };

        _sideSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(228, 230, 235),
        };

        _sideSplit.Panel1.Controls.Add(BuildSidePanel());
        _sideSplit.Panel2.Controls.Add(BuildOperationTabs());
        _mainSplit.Panel1.Controls.Add(_sideSplit);
        _mainSplit.Panel2.Controls.Add(BuildPacketPanel());

        Controls.Add(_mainSplit);
        Controls.Add(BuildStatusStrip());
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // SplitterDistance 는 컨트롤에 실제 크기가 생긴 뒤에만 설정할 수 있다.
        // 최소 크기는 분할 위치를 먼저 잡은 다음에 걸어야 예외가 나지 않는다.
        try
        {
            int packetHeight = Math.Max(180, Math.Min(320, (int)(_mainSplit.Height * 0.32)));
            int mainDistance = _mainSplit.Height - packetHeight - _mainSplit.SplitterWidth;
            mainDistance = Math.Max(300, Math.Min(mainDistance, _mainSplit.Height - 160 - _mainSplit.SplitterWidth));
            _mainSplit.SplitterDistance = mainDistance;
            _mainSplit.Panel1MinSize = 300;
            _mainSplit.Panel2MinSize = 160;

            int sideDistance = Math.Max(320, Math.Min(420, (int)(_sideSplit.Width * 0.29)));
            sideDistance = Math.Min(sideDistance, Math.Max(320, _sideSplit.Width - 480 - _sideSplit.SplitterWidth));
            _sideSplit.SplitterDistance = sideDistance;
            _sideSplit.Panel1MinSize = 320;
            _sideSplit.Panel2MinSize = 480;
        }
        catch (InvalidOperationException)
        {
            // 창이 너무 작으면 기본 분할 위치를 그대로 쓴다.
        }

        ApplyClientSelection();
    }

    private Control BuildSidePanel()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8, 8, 8, 8),
            BackColor = Color.FromArgb(247, 248, 250),
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(BuildConnectionCard(), 0, 0);
        table.Controls.Add(BuildVirtualPlcCard(), 0, 1);
        table.Controls.Add(BuildHelpCard(), 0, 2);

        scroll.Controls.Add(table);
        return scroll;
    }

    private GroupBox BuildConnectionCard()
    {
        GroupBox card = CreateCard("연결");
        TableLayoutPanel table = CreateCardTable(card);

        _clientCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (ClientDescriptor descriptor in ClientCatalog.All)
        {
            _clientCombo.Items.Add(descriptor);
        }

        _clientCombo.SelectedIndex = 0;
        _clientCombo.SelectedIndexChanged += (_, _) => ApplyClientSelection();
        AddField(table, "클라이언트", _clientCombo);

        _addressHintLabel = new Label
        {
            AutoSize = false,
            Height = 30,
            Dock = DockStyle.Fill,
            ForeColor = MutedColor,
            AutoEllipsis = true,
            Text = string.Empty,
        };
        _toolTip.SetToolTip(_addressHintLabel, "이 프로토콜의 주소 표기 예");
        AddField(table, "주소 예", _addressHintLabel);

        _hostBox = new TextBox { Text = "127.0.0.1" };
        AddField(table, "호스트", _hostBox);

        _portBox = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 502 };
        AddField(table, "포트", _portBox);

        _serialPortCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
        foreach (string portName in SerialPort.GetPortNames())
        {
            _serialPortCombo.Items.Add(portName);
        }

        _serialPortCombo.Text = "COM1";
        AddSerialField(table, "COM 포트", _serialPortCombo);

        _baudCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
        foreach (int baudRate in new[] { 9600, 19200, 38400, 57600, 115200 })
        {
            _baudCombo.Items.Add(baudRate);
        }

        _baudCombo.Text = "9600";
        AddSerialField(table, "속도(baud)", _baudCombo);

        _parityCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (Parity parity in Enum.GetValues<Parity>())
        {
            _parityCombo.Items.Add(parity);
        }

        _parityCombo.SelectedItem = Parity.Even;
        AddSerialField(table, "패리티", _parityCombo);

        _dataBitsCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _dataBitsCombo.Items.Add(7);
        _dataBitsCombo.Items.Add(8);
        _dataBitsCombo.SelectedItem = 8;
        AddSerialField(table, "데이터 비트", _dataBitsCombo);

        _stopBitsCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _stopBitsCombo.Items.Add(StopBits.One);
        _stopBitsCombo.Items.Add(StopBits.Two);
        _stopBitsCombo.Items.Add(StopBits.OnePointFive);
        _stopBitsCombo.SelectedItem = StopBits.One;
        AddSerialField(table, "정지 비트", _stopBitsCombo);

        _stationBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 1 };
        AddField(table, "국번/유닛", _stationBox);

        _timeoutBox = new NumericUpDown { Minimum = 100, Maximum = 60000, Increment = 100, Value = 3000 };
        AddField(table, "제한 시간(ms)", _timeoutBox);

        _byteOrderCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _byteOrderCombo.Items.Add(new Choice<ByteOrder?>(null, "프로토콜 기본"));
        _byteOrderCombo.Items.Add(new Choice<ByteOrder?>(ByteOrder.BigEndian, "ABCD (BigEndian)"));
        _byteOrderCombo.Items.Add(new Choice<ByteOrder?>(ByteOrder.BigEndianWordSwap, "CDAB (WordSwap)"));
        _byteOrderCombo.Items.Add(new Choice<ByteOrder?>(ByteOrder.LittleEndian, "DCBA (LittleEndian)"));
        _byteOrderCombo.Items.Add(new Choice<ByteOrder?>(ByteOrder.LittleEndianWordSwap, "BADC (LittleEndian WordSwap)"));
        _byteOrderCombo.SelectedIndex = 0;
        AddField(table, "바이트 순서", _byteOrderCombo);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
        };

        _connectButton = CreatePrimaryButton("연결");
        _connectButton.Click += OnConnectClickedAsync;

        _disconnectButton = CreateButton("해제");
        _disconnectButton.Click += OnDisconnectClickedAsync;

        buttons.Controls.Add(_connectButton);
        buttons.Controls.Add(_disconnectButton);
        AddSpan(table, buttons);

        _connectionStatusLabel = new Label
        {
            AutoSize = false,
            Height = 30,
            Dock = DockStyle.Fill,
            Text = "연결 안 됨",
            ForeColor = MutedColor,
        };
        AddSpan(table, _connectionStatusLabel);

        return card;
    }

    private GroupBox BuildVirtualPlcCard()
    {
        GroupBox card = CreateCard("가상 PLC");
        TableLayoutPanel table = CreateCardTable(card);

        _virtualProtocolCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _virtualProtocolCombo.Items.Add(new Choice<VirtualPlcProtocol>(VirtualPlcProtocol.ModbusTcp, "Modbus TCP"));
        _virtualProtocolCombo.Items.Add(new Choice<VirtualPlcProtocol>(VirtualPlcProtocol.Mc3EBinary, "MC 3E Binary (Q/L)"));
        _virtualProtocolCombo.SelectedIndex = 0;
        _virtualProtocolCombo.SelectedIndexChanged += (_, _) => OnVirtualProtocolChanged();
        AddField(table, "프로토콜", _virtualProtocolCombo);

        _virtualPortBox = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 1502 };
        AddField(table, "포트", _virtualPortBox);

        _virtualUnitBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 1 };
        _virtualUnitBox.ValueChanged += (_, _) => ApplyVirtualPlcSettings();
        AddField(table, "유닛 ID", _virtualUnitBox);

        _virtualDelayBox = new NumericUpDown { Minimum = 0, Maximum = 10000, Increment = 10, Value = 0 };
        _virtualDelayBox.ValueChanged += (_, _) => ApplyVirtualPlcSettings();
        AddField(table, "응답 지연(ms)", _virtualDelayBox);

        _virtualExceptionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _virtualExceptionCombo.Items.Add(new Choice<byte>(0, "없음"));
        foreach (ModbusExceptionCode code in Enum.GetValues<ModbusExceptionCode>())
        {
            _virtualExceptionCombo.Items.Add(new Choice<byte>((byte)code, $"0x{(byte)code:X2} {code}"));
        }

        _virtualExceptionCombo.SelectedIndex = 0;
        _virtualExceptionCombo.SelectedIndexChanged += (_, _) => ApplyVirtualPlcSettings();
        AddField(table, "강제 예외", _virtualExceptionCombo);

        _virtualSuppressCheck = new CheckBox { Text = "응답 없음 (제한 시간 시험)", AutoSize = true };
        _virtualSuppressCheck.CheckedChanged += (_, _) => ApplyVirtualPlcSettings();
        AddSpan(table, _virtualSuppressCheck);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 0),
        };

        _virtualStartButton = CreatePrimaryButton("시작");
        _virtualStartButton.Click += OnVirtualStartClicked;

        _virtualStopButton = CreateButton("중지");
        _virtualStopButton.Click += OnVirtualStopClicked;

        _virtualConnectButton = CreateButton("여기에 연결");
        _virtualConnectButton.Width = 108;
        _virtualConnectButton.Click += OnVirtualConnectClickedAsync;

        buttons.Controls.Add(_virtualStartButton);
        buttons.Controls.Add(_virtualStopButton);
        buttons.Controls.Add(_virtualConnectButton);
        AddSpan(table, buttons);

        _virtualStatusLabel = new Label
        {
            AutoSize = false,
            Height = 30,
            Dock = DockStyle.Fill,
            Text = "중지됨",
            ForeColor = MutedColor,
        };
        AddSpan(table, _virtualStatusLabel);

        return card;
    }

    private GroupBox BuildHelpCard()
    {
        GroupBox card = CreateCard("사용법");
        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = MutedColor,
            Text = "1. 클라이언트를 고르고 [연결]을 누릅니다.\r\n"
                + "2. 가상 PLC는 [시작] 후 127.0.0.1, 포트 1502로 연결합니다.\r\n"
                + "3. 읽기·쓰기 탭에서 주소와 개수를 지정합니다.\r\n"
                + "4. 아래 패킷 로그에서 TX/RX 프레임을 확인합니다.\r\n"
                + "※ 가상 PLC는 Modbus TCP · MC 3E 서버 역할을 하며, 해당 프로토콜 클라이언트로 시험할 수 있습니다.",
        };
        label.MaximumSize = new Size(card.Width - 28, 0);
        card.SizeChanged += (_, _) => label.MaximumSize = new Size(card.ClientSize.Width - 20, 0);
        card.Controls.Add(label);
        return card;
    }

    private TabControl BuildOperationTabs() => new TabControl
    {
        Dock = DockStyle.Fill,
        Padding = new Point(14, 6),
        TabPages =
        {
            BuildReadTab(),
            BuildWriteTab(),
            BuildMemoryTab(),
        },
    };

    private TabPage BuildReadTab()
    {
        var page = new TabPage("읽기") { BackColor = Color.White, Padding = new Padding(8) };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(4),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));

        // 좁은 창에서도 컨트롤이 잘리지 않도록 두 줄로 나눈다.
        var addressRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };

        _readAddressBox = new TextBox { Width = 160, Text = "40001" };
        AddToolbarField(addressRow, "주소", _readAddressBox);

        _readTypeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        foreach (ReadInterpretation interpretation in Enum.GetValues<ReadInterpretation>())
        {
            _readTypeCombo.Items.Add(new Choice<ReadInterpretation>(interpretation, OperationText.Describe(interpretation)));
        }

        _readTypeCombo.SelectedIndex = 1;
        _readTypeCombo.SelectedIndexChanged += (_, _) => UpdateReadInfo();
        _readAddressBox.TextChanged += (_, _) => AutoSelectInterpretation(_readAddressBox, _readTypeCombo);
        AddToolbarField(addressRow, "해석", _readTypeCombo);
        table.Controls.Add(addressRow, 0, 0);

        var countRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };

        _readUnitCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 86 };
        foreach (CountUnit unit in Enum.GetValues<CountUnit>())
        {
            _readUnitCombo.Items.Add(new Choice<CountUnit>(unit, OperationText.Describe(unit)));
        }

        _readUnitCombo.SelectedIndex = 0;
        _readUnitCombo.SelectedIndexChanged += (_, _) => UpdateReadInfo();
        AddToolbarField(countRow, "단위", _readUnitCombo);

        _readCountBox = new NumericUpDown { Minimum = 1, Maximum = 2000, Value = 10, Width = 76 };
        _readCountBox.ValueChanged += (_, _) => UpdateReadInfo();
        AddToolbarField(countRow, "개수", _readCountBox);

        _readButton = CreatePrimaryButton("읽기");
        _readButton.Margin = new Padding(8, 3, 0, 0);
        _readButton.Click += OnReadClickedAsync;
        countRow.Controls.Add(_readButton);
        table.Controls.Add(countRow, 0, 1);

        _readInfoLabel = new Label { AutoSize = true, ForeColor = MutedColor, Text = string.Empty, Margin = new Padding(2, 2, 0, 4) };
        table.Controls.Add(_readInfoLabel, 0, 2);

        _readGrid = CreateGrid();
        AddColumn(_readGrid, "주소", 110);
        AddColumn(_readGrid, "값", 130);
        AddColumn(_readGrid, "16진", 100);
        AddColumn(_readGrid, "2진", 170);
        DataGridViewTextBoxColumn readAsciiColumn = AddColumn(_readGrid, "ASCII", 70);
        readAsciiColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        table.Controls.Add(_readGrid, 0, 3);

        _readRawBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(250, 251, 253),
            Margin = new Padding(0, 6, 0, 0),
        };
        table.Controls.Add(_readRawBox, 0, 4);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildWriteTab()
    {
        var page = new TabPage("쓰기") { BackColor = Color.White, Padding = new Padding(8) };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(4),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var addressRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };

        _writeAddressBox = new TextBox { Width = 160, Text = "40001" };
        AddToolbarField(addressRow, "주소", _writeAddressBox);

        _writeTypeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        foreach (WriteInterpretation interpretation in Enum.GetValues<WriteInterpretation>())
        {
            _writeTypeCombo.Items.Add(new Choice<WriteInterpretation>(interpretation, OperationText.Describe(interpretation)));
        }

        _writeTypeCombo.SelectedIndex = 1;
        _writeAddressBox.TextChanged += (_, _) => AutoSelectInterpretation(_writeAddressBox, _writeTypeCombo);
        AddToolbarField(addressRow, "해석", _writeTypeCombo);
        table.Controls.Add(addressRow, 0, 0);

        var modeRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };

        _writeModeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        foreach (WriteMode mode in Enum.GetValues<WriteMode>())
        {
            _writeModeCombo.Items.Add(new Choice<WriteMode>(mode, OperationText.Describe(mode)));
        }

        _writeModeCombo.SelectedIndex = 0;
        AddToolbarField(modeRow, "모드", _writeModeCombo);

        _writeVerifyCheck = new CheckBox { Text = "쓰고 검증", AutoSize = true, Margin = new Padding(6, 8, 6, 0) };
        modeRow.Controls.Add(_writeVerifyCheck);

        _writeButton = CreatePrimaryButton("쓰기");
        _writeButton.Margin = new Padding(4, 3, 0, 0);
        _writeButton.Click += OnWriteClickedAsync;
        modeRow.Controls.Add(_writeButton);
        table.Controls.Add(modeRow, 0, 1);

        var generator = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        _genStartBox = new NumericUpDown { Minimum = -1000000, Maximum = 1000000, Value = 1, Width = 76 };
        AddToolbarField(generator, "시작", _genStartBox);

        _genStepBox = new NumericUpDown { Minimum = -1000, Maximum = 1000, Value = 1, Width = 66 };
        AddToolbarField(generator, "증가", _genStepBox);

        _genCountBox = new NumericUpDown { Minimum = 1, Maximum = 500, Value = 10, Width = 66 };
        AddToolbarField(generator, "개수", _genCountBox);

        _generateButton = CreateButton("값 채우기");
        _generateButton.Width = 92;
        _generateButton.Margin = new Padding(6, 3, 6, 0);
        _generateButton.Click += OnGenerateValuesClicked;
        generator.Controls.Add(_generateButton);

        _clearValuesButton = CreateButton("지우기");
        _clearValuesButton.Margin = new Padding(0, 3, 0, 0);
        _clearValuesButton.Click += (_, _) => _writeValuesBox.Clear();
        generator.Controls.Add(_clearValuesButton);

        table.Controls.Add(generator, 0, 2);

        _writeValuesBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            PlaceholderText = "값을 한 줄에 하나씩 입력합니다. 16진수는 0x 접두사 (예: 0x00FF)",
            Margin = new Padding(0, 4, 0, 6),
        };
        table.Controls.Add(_writeValuesBox, 0, 3);

        _writeResultLabel = new Label { AutoSize = true, ForeColor = MutedColor, Text = string.Empty, Margin = new Padding(2, 0, 0, 4) };
        table.Controls.Add(_writeResultLabel, 0, 4);

        _writeGrid = CreateGrid();
        AddColumn(_writeGrid, "주소", 140);
        AddColumn(_writeGrid, "값", 180);
        AddColumn(_writeGrid, "결과", 120);
        table.Controls.Add(_writeGrid, 0, 5);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildMemoryTab()
    {
        var page = new TabPage("가상 PLC 메모리") { BackColor = Color.White, Padding = new Padding(8) };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var areaRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };

        _memoryAreaCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _memoryAreaCombo.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingMemoryAreas)
            {
                RefreshMemoryGrid();
            }
        };

        AddToolbarField(areaRow, "영역", _memoryAreaCombo);

        _memoryStartBox = new NumericUpDown { Minimum = 0, Maximum = 65535, Value = 0, Width = 76 };
        _memoryStartBox.ValueChanged += (_, _) => RefreshMemoryGrid();
        AddToolbarField(areaRow, "시작", _memoryStartBox);

        _memoryCountBox = new NumericUpDown { Minimum = 1, Maximum = 256, Value = 32, Width = 66 };
        _memoryCountBox.ValueChanged += (_, _) => RefreshMemoryGrid();
        AddToolbarField(areaRow, "개수", _memoryCountBox);
        table.Controls.Add(areaRow, 0, 0);

        var actionRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };

        _memoryRefreshButton = CreateButton("새로고침");
        _memoryRefreshButton.Width = 92;
        _memoryRefreshButton.Margin = new Padding(0, 3, 6, 0);
        _memoryRefreshButton.Click += (_, _) => RefreshMemoryGrid();
        actionRow.Controls.Add(_memoryRefreshButton);

        _memoryPatternButton = CreateButton("패턴 채우기");
        _memoryPatternButton.Width = 104;
        _memoryPatternButton.Margin = new Padding(0, 3, 6, 0);
        _memoryPatternButton.Click += OnMemoryPatternClicked;
        actionRow.Controls.Add(_memoryPatternButton);

        _memoryClearButton = CreateButton("0으로");
        _memoryClearButton.Margin = new Padding(0, 3, 6, 0);
        _memoryClearButton.Click += OnMemoryClearClicked;
        actionRow.Controls.Add(_memoryClearButton);

        _memoryAutoCheck = new CheckBox { Text = "자동 새로고침", AutoSize = true, Margin = new Padding(6, 8, 0, 0) };
        actionRow.Controls.Add(_memoryAutoCheck);
        table.Controls.Add(actionRow, 0, 1);

        _memoryGrid = CreateGrid(readOnly: false);
        _memoryGrid.EditMode = DataGridViewEditMode.EditOnEnter;
        _memoryGrid.CellEndEdit += OnMemoryCellEndEdit;
        _memoryGrid.CellValueChanged += OnMemoryCellValueChanged;
        _memoryGrid.CurrentCellDirtyStateChanged += OnMemoryCellDirtyStateChanged;

        AddColumn(_memoryGrid, "번호", 90);
        _memoryValueColumn = AddColumn(_memoryGrid, "값 (10진)", 120);
        _memoryValueColumn.ReadOnly = false;

        _memoryBitColumn = new DataGridViewCheckBoxColumn
        {
            HeaderText = "값",
            Width = 60,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        _memoryGrid.Columns.Add(_memoryBitColumn);

        AddColumn(_memoryGrid, "16진", 100);
        AddColumn(_memoryGrid, "2진", 220);
        table.Controls.Add(_memoryGrid, 0, 2);

        page.Controls.Add(table);
        return page;
    }

    private Control BuildPacketPanel()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 6, 8, 8),
            BackColor = Color.White,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));

        var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };

        var title = new Label
        {
            Text = "패킷 로그",
            AutoSize = true,
            Font = new Font("Malgun Gothic", 9.5F, FontStyle.Bold),
            Margin = new Padding(0, 9, 12, 0),
        };
        toolbar.Controls.Add(title);

        _packetPauseButton = CreateButton("일시정지");
        _packetPauseButton.Click += OnPacketPauseClicked;
        toolbar.Controls.Add(_packetPauseButton);

        _packetClearButton = CreateButton("지우기");
        _packetClearButton.Click += OnPacketClearClicked;
        toolbar.Controls.Add(_packetClearButton);

        _packetCopyButton = CreateButton("복사");
        _packetCopyButton.Click += OnPacketCopyClicked;
        toolbar.Controls.Add(_packetCopyButton);

        _packetSaveButton = CreateButton("CSV 저장");
        _packetSaveButton.Width = 92;
        _packetSaveButton.Click += OnPacketSaveClicked;
        toolbar.Controls.Add(_packetSaveButton);

        _packetServerCheck = new CheckBox { Text = "서버 프레임도 기록", AutoSize = true, Margin = new Padding(8, 9, 8, 0) };
        _packetServerCheck.CheckedChanged += OnPacketServerCheckChanged;
        toolbar.Controls.Add(_packetServerCheck);

        _packetCountLabel = new Label { AutoSize = true, ForeColor = MutedColor, Margin = new Padding(4, 9, 0, 0), Text = "TX 0 / RX 0" };
        toolbar.Controls.Add(_packetCountLabel);

        table.Controls.Add(toolbar, 0, 0);

        _packetGrid = CreateGrid();
        AddColumn(_packetGrid, "No", 56);
        AddColumn(_packetGrid, "시각", 96);
        AddColumn(_packetGrid, "방향", 64);
        AddColumn(_packetGrid, "출처", 76);
        AddColumn(_packetGrid, "길이", 60);
        DataGridViewTextBoxColumn frameColumn = AddColumn(_packetGrid, "프레임 (HEX)", 420);
        frameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        AddColumn(_packetGrid, "지연", 90);
        _packetGrid.Font = new Font("Consolas", 9F);
        _packetGrid.SelectionChanged += OnPacketSelectionChanged;
        table.Controls.Add(_packetGrid, 0, 1);

        _packetDetailBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(250, 251, 253),
            Margin = new Padding(0, 6, 0, 0),
        };
        table.Controls.Add(_packetDetailBox, 0, 2);

        return table;
    }

    private StatusStrip BuildStatusStrip()
    {
        var strip = new StatusStrip { BackColor = Color.FromArgb(240, 242, 245), SizingGrip = true };

        _statusConnectionLabel = new ToolStripStatusLabel("연결 안 됨");
        _statusVirtualLabel = new ToolStripStatusLabel("가상 PLC 중지");
        _statusCounterLabel = new ToolStripStatusLabel("TX 0 / RX 0");
        _statusWorkLabel = new ToolStripStatusLabel(string.Empty) { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

        strip.Items.Add(_statusConnectionLabel);
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(_statusVirtualLabel);
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(_statusCounterLabel);
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(_statusWorkLabel);

        return strip;
    }

    private static GroupBox CreateCard(string title) => new()
    {
        Text = title,
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(10, 6, 10, 10),
        Margin = new Padding(0, 0, 0, 10),
        BackColor = Color.White,
    };

    private static TableLayoutPanel CreateCardTable(GroupBox card)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.Controls.Add(table);
        return table;
    }

    private static Label AddField(TableLayoutPanel table, string text, Control control)
    {
        int row = table.RowCount;
        table.RowCount = row + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 4, 0),
            ForeColor = MutedColor,
        };

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);

        table.Controls.Add(label, 0, row);
        table.Controls.Add(control, 1, row);
        return label;
    }

    private void AddSerialField(TableLayoutPanel table, string text, Control control)
    {
        Label label = AddField(table, text, control);
        _serialRows.Add((label, control));
    }

    private static void AddSpan(TableLayoutPanel table, Control control)
    {
        int row = table.RowCount;
        table.RowCount = row + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);

        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
    }

    private static void AddToolbarField(FlowLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(6, 8, 2, 0),
            ForeColor = MutedColor,
        });

        control.Margin = new Padding(0, 4, 6, 0);
        panel.Controls.Add(control);
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 84,
        Height = 30,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        Margin = new Padding(0, 3, 6, 0),
    };

    private static Button CreatePrimaryButton(string text)
    {
        Button button = CreateButton(text);
        button.BackColor = AccentColor;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static DataGridView CreateGrid(bool readOnly = true)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = readOnly,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 28,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 242, 245);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Malgun Gothic", 9F, FontStyle.Bold);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(212, 232, 252);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.RowTemplate.Height = 22;

        return grid;
    }

    private static DataGridViewTextBoxColumn AddColumn(DataGridView grid, string header, int width)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
        };

        grid.Columns.Add(column);
        return column;
    }
}
