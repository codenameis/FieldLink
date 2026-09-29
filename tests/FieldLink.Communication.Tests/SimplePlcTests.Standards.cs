using System.Net;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.Modbus;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static async Task ModbusStandardsRejectsInvalidMbapBeforeWaitingForBodyAsync()
    {
        // Length includes Unit ID + PDU: 2..254. Protocol identifier must be zero.
        foreach (string header in new[] { "0001000000FF", "000100010006", "000100000001" })
        {
            var listener = new TestTcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                using var plc = Plc.ModbusTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
                    new ModbusTcpClientOptions { Timeout = TimeSpan.FromSeconds(2) });
                var accepted = listener.AcceptSocketAsync();
                TestAssert.True((await plc.OpenAsync()).IsSuccess);
                using var peer = await accepted;
                var reading = plc.ReadInt16Async("0");
                TestAssert.Bytes(H("000100000006010300000001"), await TcpFixture.ReadExactlyAsync(peer, 12));
                await TcpFixture.WriteAsync(peer, H(header));
                var result = await reading;
                TestAssert.True(!result.IsSuccess);
                TestAssert.Equal(CommunicationFailure.InvalidFrame,
                    ((CommunicationException)result.FailureDetails.Cause).Failure);
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
