using System;
using System.Text;

namespace FieldLink.Plc.Abstractions;

/// <summary>문자열을 PLC 메모리 형식으로 바꾸고 되돌리는 규칙입니다.</summary>
/// <remarks>
/// 제조사마다 형식이 다릅니다. 예: 워드 단위 0 채움(MC, Modbus), 길이 머리말이 붙는 Logix STRING,
/// 최대 길이·실제 길이 2바이트가 붙는 S7 STRING. 프로토콜이 자신에게 맞는 구현을 제공합니다.
/// </remarks>
public interface IPlcStringFormat
{
    /// <summary>문자 수 용량을 저장하는 데 필요한 PLC 바이트 수입니다. 머리말·채움 바이트를 포함합니다.</summary>
    /// <param name="characterCapacity">최대 문자 수(인코딩 후 바이트 수)입니다.</param>
    int GetStorageSize(int characterCapacity);

    /// <summary>문자열을 PLC 메모리 바이트로 바꿉니다.</summary>
    /// <param name="value">쓸 문자열입니다.</param>
    /// <param name="encoding">문자 인코딩입니다. 표현할 수 없는 문자는 예외를 던져야 합니다.</param>
    /// <param name="characterCapacity">최대 문자 수입니다.</param>
    /// <exception cref="ArgumentException">인코딩 결과가 용량을 넘을 때.</exception>
    byte[] Encode(string value, Encoding encoding, int characterCapacity);

    /// <summary>PLC 메모리 바이트를 문자열로 바꿉니다.</summary>
    /// <param name="storage">PLC에서 읽은 바이트입니다.</param>
    /// <param name="encoding">문자 인코딩입니다.</param>
    string Decode(byte[] storage, Encoding encoding);
}
