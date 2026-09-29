using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Siemens;
using FieldLink.PlcDrivers.FATEK;
using FieldLink.PlcDrivers.GE;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Keyence;
using FieldLink.PlcDrivers.AllenBradley;
using FieldLink.PlcDrivers.Yokogawa;

namespace FieldLink.Communication.Tests;

internal static class AddressContractTests
{
    internal static async Task InvalidParseCannotSilentlyReusePreviousAddressAsync()
    {
        DeviceAddress[] addresses = [new McDeviceAddress(), new S7DeviceAddress(), new FatekProgramAddress(),
            new GeSrtpAddress(), new OmronFinsAddress(), new KeyenceNanoAddress(), new AllenBradleySlcAddress(), new YokogawaLinkAddress()];
        foreach (DeviceAddress address in addresses)
        {
            address.AddressStart = 123;
            address.Length = 7;
            await TestAssert.ThrowsAsync<FormatException>(() => { address.Parse("?invalid", 99); return Task.CompletedTask; });
            TestAssert.Equal(123, address.AddressStart);
            TestAssert.Equal((ushort)7, address.Length);
        }
    }
}
