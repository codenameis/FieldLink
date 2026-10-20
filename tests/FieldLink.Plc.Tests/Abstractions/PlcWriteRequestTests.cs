using System;
using FieldLink.Plc.Abstractions;
using Xunit;

namespace FieldLink.Plc.Tests.Abstractions;

public sealed class PlcWriteRequestTests
{
    [Fact]
    public void Constructor_CopiesData_SoCallerChangesDoNotLeak()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04 };

        var request = new PlcWriteRequest("D100", PlcDataType.Int16, data);
        data[0] = 0xFF;

        Assert.Equal(2, request.Count);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, request.GetData());
    }

    [Fact]
    public void Constructor_LengthNotMultipleOfElementSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlcWriteRequest("D100", PlcDataType.Int32, new byte[6]));
    }

    [Fact]
    public void GetData_Slice_ReturnsRequestedElements()
    {
        var request = new PlcWriteRequest("D100", PlcDataType.UInt16, new byte[] { 1, 2, 3, 4, 5, 6 });

        Assert.Equal(new byte[] { 3, 4, 5, 6 }, request.GetData(startElement: 1, elementCount: 2));
    }
}
