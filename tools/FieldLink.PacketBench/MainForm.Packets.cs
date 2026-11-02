using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FieldLink.PacketBench.Diagnostics;

namespace FieldLink.PacketBench;

internal sealed partial class MainForm
{
    private void OnPacketTimerTick()
    {
        DrainPackets();
        UpdateStatusCounters();
        UpdateVirtualPlcUi();
    }

    private void DrainPackets()
    {
        if (_packetPaused)
        {
            return;
        }

        bool appended = false;
        _packetGrid.SuspendLayout();

        while (_packetLog.TryDequeue(out PacketRecord? record))
        {
            if (_packetGrid.Rows.Count >= MaxPacketRows)
            {
                _packetGrid.Rows.RemoveAt(0);
            }

            int index = _packetGrid.Rows.Add(
                record.Sequence,
                record.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                record.DirectionText,
                record.SourceText,
                record.Length,
                record.Hex,
                record.ElapsedText);

            _packetGrid.Rows[index].Tag = record;
            appended = true;
        }

        _packetGrid.ResumeLayout();

        if (appended && _packetAutoScroll && _packetGrid.Rows.Count > 0 && _packetGrid.IsHandleCreated)
        {
            _packetGrid.FirstDisplayedScrollingRowIndex = _packetGrid.Rows.Count - 1;
        }
    }

    private void OnPacketSelectionChanged(object? sender, EventArgs e)
    {
        if (_packetGrid.CurrentRow?.Tag is PacketRecord record)
        {
            _packetDetailBox.Text = HexText.FormatDetailed(record.Frame);
        }
    }

    private void OnPacketPauseClicked(object? sender, EventArgs e)
    {
        _packetPaused = !_packetPaused;
        _packetPauseButton.Text = _packetPaused ? "재개" : "일시정지";
        SetWork(_packetPaused ? "패킷 표시를 잠시 멈췄습니다. 기록은 계속 쌓입니다." : "패킷 표시를 다시 시작했습니다.");
    }

    private void OnPacketClearClicked(object? sender, EventArgs e)
    {
        _packetGrid.Rows.Clear();
        _packetDetailBox.Clear();
        _packetLog.Reset();
        UpdateStatusCounters();
        SetWork("패킷 로그를 지웠습니다.");
    }

    private void OnPacketCopyClicked(object? sender, EventArgs e)
    {
        var builder = new StringBuilder();
        bool onlySelected = _packetGrid.SelectedRows.Count > 0;

        foreach (DataGridViewRow row in _packetGrid.Rows)
        {
            if (onlySelected && !row.Selected)
            {
                continue;
            }

            builder.AppendLine(string.Join(
                "\t",
                row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value?.ToString() ?? string.Empty)));
        }

        if (builder.Length == 0)
        {
            SetWork("복사할 패킷이 없습니다.", isError: true);
            return;
        }

        Clipboard.SetText(builder.ToString());
        SetWork(onlySelected
            ? $"선택한 패킷 {_packetGrid.SelectedRows.Count}개를 클립보드에 복사했습니다."
            : $"패킷 {_packetGrid.Rows.Count}개를 클립보드에 복사했습니다.");
    }

    private void OnPacketSaveClicked(object? sender, EventArgs e)
    {
        if (_packetGrid.Rows.Count == 0)
        {
            SetWork("저장할 패킷이 없습니다.", isError: true);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*",
            FileName = $"fieldlink-packets-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("No,Time,Direction,Source,Length,Frame,Elapsed");

        foreach (DataGridViewRow row in _packetGrid.Rows)
        {
            builder.AppendLine(string.Join(
                ",",
                row.Cells.Cast<DataGridViewCell>().Select(cell => EscapeCsv(cell.Value?.ToString() ?? string.Empty))));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        SetWork($"패킷 {_packetGrid.Rows.Count}개를 저장했습니다: {dialog.FileName}");
    }

    private void OnPacketServerCheckChanged(object? sender, EventArgs e)
    {
        _packetLog.RecordServerFrames = _packetServerCheck.Checked;
        SetWork(_packetServerCheck.Checked
            ? "가상 PLC 서버가 주고받은 프레임도 함께 기록합니다."
            : "클라이언트가 주고받은 프레임만 기록합니다.");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
