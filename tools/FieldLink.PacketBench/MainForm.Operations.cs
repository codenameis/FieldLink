using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FieldLink.PacketBench.Connections;
using FieldLink.PacketBench.Diagnostics;
using FieldLink.PacketBench.Operations;
using FieldLink.PacketBench.Ui;
using FieldLink.Plc;

namespace FieldLink.PacketBench;

internal sealed partial class MainForm
{
    private void UpdateReadInfo()
    {
        if (_updatingReadUnit || _readInfoLabel is null || _readTypeCombo.SelectedItem is not Choice<ReadInterpretation> typeChoice)
        {
            return;
        }

        ReadInterpretation interpretation = typeChoice.Value;
        CountUnit unit = _readUnitCombo.SelectedItem is Choice<CountUnit> unitChoice ? unitChoice.Value : CountUnit.Word;

        if (interpretation == ReadInterpretation.Bit)
        {
            _updatingReadUnit = true;
            _readUnitCombo.SelectedIndex = (int)CountUnit.Bit;
            _updatingReadUnit = false;
            _readUnitCombo.Enabled = false;
            unit = CountUnit.Bit;
        }
        else
        {
            _readUnitCombo.Enabled = true;
            if (unit == CountUnit.Bit)
            {
                _updatingReadUnit = true;
                _readUnitCombo.SelectedIndex = (int)CountUnit.Word;
                _updatingReadUnit = false;
                unit = CountUnit.Word;
            }
        }

        try
        {
            (int elementCount, int byteCount) = ReadExecutor.ResolveLayout(interpretation, unit, (int)_readCountBox.Value);
            _readInfoLabel.ForeColor = MutedColor;
            _readInfoLabel.Text = $"→ {OperationText.Describe(interpretation)} {elementCount}개 · {byteCount}바이트 요청";
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            _readInfoLabel.ForeColor = ErrorColor;
            _readInfoLabel.Text = exception.Message;
        }
    }

    private async void OnReadClickedAsync(object? sender, EventArgs e)
    {
        PlcClient? current = _client;
        if (current is null)
        {
            SetWork("먼저 연결하세요.", isError: true);
            return;
        }

        if (_isBusy || _readTypeCombo.SelectedItem is not Choice<ReadInterpretation> typeChoice
            || _readUnitCombo.SelectedItem is not Choice<CountUnit> unitChoice)
        {
            return;
        }

        string address = _readAddressBox.Text.Trim();
        int count = (int)_readCountBox.Value;
        SetBusy(true);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            PlcResult<ReadOutcome> result = await ReadExecutor.ExecuteAsync(
                current,
                address,
                typeChoice.Value,
                unitChoice.Value,
                count,
                current.ByteOrder,
                CancellationToken.None);
            stopwatch.Stop();

            if (!result.IsSuccess)
            {
                SetWork(DescribeError("읽기 실패", result.Error), isError: true);
                return;
            }

            FillReadGrid(result.Value);
            _readRawBox.Text = HexText.Format(result.Value.RawBytes);
            SetWork($"읽기 성공 · {result.Value.Summary} · {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
        }
        catch (Exception exception)
        {
            SetWork($"읽기 오류: {exception.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnWriteClickedAsync(object? sender, EventArgs e)
    {
        PlcClient? current = _client;
        if (current is null)
        {
            SetWork("먼저 연결하세요.", isError: true);
            return;
        }

        if (_isBusy || _writeTypeCombo.SelectedItem is not Choice<WriteInterpretation> typeChoice
            || _writeModeCombo.SelectedItem is not Choice<WriteMode> modeChoice)
        {
            return;
        }

        string address = _writeAddressBox.Text.Trim();
        bool verify = _writeVerifyCheck.Checked;
        string[] texts = _writeValuesBox.Lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        SetBusy(true);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            WriteOutcome outcome = await WriteExecutor.ExecuteAsync(
                current,
                address,
                typeChoice.Value,
                modeChoice.Value,
                texts,
                verify,
                CancellationToken.None);
            stopwatch.Stop();

            FillWriteGrid(outcome);

            if (!outcome.IsSuccess)
            {
                _writeResultLabel.Text = outcome.Message;
                _writeResultLabel.ForeColor = ErrorColor;
                SetWork(outcome.Message, isError: true);
                return;
            }

            string verification = outcome.IsVerified is null ? string.Empty : " · " + outcome.VerificationMessage;
            bool verifyFailed = outcome.IsVerified == false;

            _writeResultLabel.Text = outcome.Message + verification;
            _writeResultLabel.ForeColor = verifyFailed ? ErrorColor : SuccessColor;
            SetWork($"{outcome.Message}{verification} · {stopwatch.Elapsed.TotalMilliseconds:F0} ms", isError: verifyFailed);

            RefreshMemoryGrid();
        }
        catch (Exception exception)
        {
            SetWork($"쓰기 오류: {exception.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnGenerateValuesClicked(object? sender, EventArgs e)
    {
        if (_writeTypeCombo.SelectedItem is not Choice<WriteInterpretation> typeChoice)
        {
            return;
        }

        int count = (int)_genCountBox.Value;
        long start = (long)_genStartBox.Value;
        long step = (long)_genStepBox.Value;
        bool isBit = typeChoice.Value == WriteInterpretation.Bit;

        var lines = new string[count];
        for (int i = 0; i < count; i++)
        {
            long value = start + (i * step);
            lines[i] = isBit
                ? (value != 0 ? "1" : "0")
                : value.ToString(CultureInfo.InvariantCulture);
        }

        _writeValuesBox.Lines = lines;
        SetWork($"{count}개 값을 생성했습니다.");
    }

    private void FillReadGrid(ReadOutcome outcome)
    {
        _readGrid.SuspendLayout();
        _readGrid.Rows.Clear();

        foreach (ReadRow row in outcome.Rows)
        {
            _readGrid.Rows.Add(row.Address, row.Value, row.Hex, row.Binary, row.Ascii);
        }

        _readGrid.ResumeLayout();
    }

    private void FillWriteGrid(WriteOutcome outcome)
    {
        _writeGrid.SuspendLayout();
        _writeGrid.Rows.Clear();

        foreach (WriteRow row in outcome.Rows)
        {
            int index = _writeGrid.Rows.Add(row.Address, row.Value, row.Result);
            if (row.Result != "성공")
            {
                _writeGrid.Rows[index].DefaultCellStyle.ForeColor = ErrorColor;
            }
        }

        _writeGrid.ResumeLayout();
    }
    /// <summary>주소가 비트 디바이스인지 알 수 있으면 해석 형식을 비트/워드로 맞춰 줍니다.</summary>
    private void AutoSelectInterpretation(TextBox addressBox, ComboBox typeCombo)
    {
        if (_clientCombo.SelectedItem is not ClientDescriptor descriptor)
        {
            return;
        }

        if (!TryDetectBitDevice(descriptor.Id, addressBox.Text, out bool isBit))
        {
            return;
        }

        int wantedIndex = isBit ? 0 : 1;
        if (typeCombo.SelectedIndex != wantedIndex)
        {
            typeCombo.SelectedIndex = wantedIndex;
        }
    }

    private static bool TryDetectBitDevice(string clientId, string address, out bool isBit)
    {
        isBit = false;
        string trimmed = address.Trim().ToUpperInvariant();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (clientId.StartsWith("mc-", StringComparison.Ordinal))
        {
            // 두 글자 접두사를 먼저 본다. (SM 을 S 로 읽지 않도록)
            string[] bitDevices = { "SM", "SB", "TS", "TC", "CS", "CC", "M", "X", "Y", "L", "F", "V", "B", "S" };
            string[] wordDevices = { "SD", "SW", "TN", "CN", "ZR", "D", "W", "R", "Z" };

            foreach (string device in bitDevices)
            {
                if (trimmed.StartsWith(device, StringComparison.Ordinal))
                {
                    isBit = true;
                    return true;
                }
            }

            foreach (string device in wordDevices)
            {
                if (trimmed.StartsWith(device, StringComparison.Ordinal))
                {
                    isBit = false;
                    return true;
                }
            }

            return false;
        }

        if (clientId.StartsWith("modbus-", StringComparison.Ordinal) && trimmed.Length is 5 or 6 && trimmed.All(char.IsDigit))
        {
            isBit = trimmed[0] is '0' or '1';
            return true;
        }

        return false;
    }
}
