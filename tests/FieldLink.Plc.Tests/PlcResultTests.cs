using System;
using Xunit;

namespace FieldLink.Plc.Tests;

// 테스트 이름 규칙: 대상_조건_기대결과
public sealed class PlcResultTests
{
    [Fact]
    public void Success_WithValue_ExposesValue()
    {
        PlcResult<short> result = PlcResult.Success((short)123);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(123, result.Value);
    }

    [Fact]
    public void Failure_ReadingValue_ThrowsInvalidOperation()
    {
        var error = new PlcError(PlcErrorKind.DeviceError, "Illegal data address", deviceErrorCode: 0x02);

        PlcResult<short> result = PlcResult.Failure<short>(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_WithNullError_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => PlcResult.Failure(null!));
    }
}
