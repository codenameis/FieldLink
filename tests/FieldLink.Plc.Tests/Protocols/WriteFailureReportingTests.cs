using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.LsElectric.FEnet;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols;

/// <summary>분할 쓰기 중 장치가 거절해도 장치 오류 코드와 완료 수가 함께 보존되는지 확인합니다.</summary>
public sealed class WriteFailureReportingTests
{
    [Fact]
    public async Task ModbusTcp_DeviceRejectsSecondChunk_KeepsDeviceErrorCodeAndCompletedCount()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FromHex("00 01 00 00 00 06 01 10 00 00 00 02"));
        transport.EnqueueResponse(FromHex("00 02 00 00 00 03 01 90 02"));
        var protocol = new ModbusTcpProtocol(transport, new ModbusTcpOptions { MaxWriteRegisters = 2 });
        var request = new PlcWriteRequest("40001", PlcDataType.Int16, new byte[8]);

        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, CancellationToken.None));

        Assert.Equal(0x02, exception.DeviceErrorCode);
        Assert.Equal(2, exception.CompletedCount);
    }

    [Fact]
    public async Task FEnet_DeviceRejectsSecondChunk_KeepsDeviceErrorCodeAndCompletedCount()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FEnetWriteResponse(invokeId: 1, errorState: 0, errorCode: 0));
        transport.EnqueueResponse(FEnetWriteResponse(invokeId: 2, errorState: 1, errorCode: (ushort)FEnetErrorCode.DeviceAreaError));
        var protocol = new FEnetProtocol(transport, new FEnetOptions { MaxWriteBytes = 2 });
        var request = new PlcWriteRequest("%DW100", PlcDataType.Int16, new byte[4]);

        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, CancellationToken.None));

        Assert.Equal((int)FEnetErrorCode.DeviceAreaError, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
    }

    private static byte[] FEnetWriteResponse(ushort invokeId, ushort errorState, ushort errorCode)
    {
        var frame = new byte[30];
        Encoding.ASCII.GetBytes("LSIS-XGT").CopyTo(frame, 0);
        frame[12] = 0xA0;
        frame[13] = 0x11;
        frame[14] = (byte)invokeId;
        frame[15] = (byte)(invokeId >> 8);
        frame[16] = 10;

        byte checksum = 0;
        for (int index = 0; index < 19; index++)
        {
            checksum = (byte)(checksum + frame[index]);
        }

        frame[19] = checksum;
        frame[20] = 0x59;
        frame[22] = 0x14;
        frame[26] = (byte)errorState;
        frame[27] = (byte)(errorState >> 8);
        frame[28] = (byte)errorCode;
        frame[29] = (byte)(errorCode >> 8);
        return frame;
    }

    private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", string.Empty));
}
