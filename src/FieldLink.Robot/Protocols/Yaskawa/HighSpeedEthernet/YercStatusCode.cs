using System.Collections.Generic;
using FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA <c>YERC</c> 응답 헤더의 Status 및 AddedStatus 오류 설명 조회기입니다.</summary>
public static class YercStatusCode
{
    private const byte SuccessStatus = 0x00;
    private const byte UndefinedCommandStatus = 0x08;
    private const byte InvalidAttributeStatus = 0x09;
    private const byte NonExistentInstanceStatus = 0x28;
    private const byte ExtendedErrorStatus = 0x1F;
    private const ushort MinimumBackupSystemErrorCode = 0x6040;
    private const ushort MaximumBackupSystemErrorCode = 0x6140;
    private const int MinimumBcdAlarmCode = 1010;
    private const int MaximumBcdAlarmCode = 5480;
    private const int BcdNibbleCount = 4;
    private const int MaximumDecimalDigit = 9;
    private const int BcdDecimalBase = 10;
    private const int NibbleShiftBits = 4;
    private const int NibbleMask = 0x0F;

    private static readonly IReadOnlyDictionary<ushort, string> ExtendedErrorDescriptions = new Dictionary<ushort, string>
    {
        [0x6010] = "로봇 또는 외부축이 동작 중입니다.",
        [0x6020] = "지정 장치 용량이 부족합니다.",
        [0x6030] = "지정 장치에 접근할 수 없습니다.",
        [0xA000] = "정의되지 않은 명령입니다.",
        [0xA001] = "Instance 번호가 잘못되었습니다.",
        [0xA002] = "Attribute 번호가 잘못되었습니다.",
        [0xA100] = "응답 데이터 크기가 하드웨어 한계를 초과했습니다.",
        [0xA102] = "응답 데이터 크기가 소프트웨어 한계를 초과했습니다.",
        [0xB001] = "정의되지 않은 위치 변수입니다.",
        [0xB002] = "데이터 사용이 금지되어 있습니다.",
        [0xB003] = "요청 크기가 올바르지 않습니다.",
        [0xB004] = "데이터가 허용 범위를 벗어났습니다.",
        [0xB005] = "데이터가 설정되지 않았습니다.",
        [0xB006] = "등록되지 않은 용도입니다.",
        [0xB007] = "등록되지 않은 기종입니다.",
        [0xB008] = "제어축 그룹이 잘못되었습니다.",
        [0xB009] = "속도 설정이 잘못되었습니다.",
        [0xB00A] = "속도 값이 범위를 벗어났습니다.",
        [0xB00B] = "좌표계 지정이 잘못되었습니다.",
        [0xB00C] = "형태 지정이 잘못되었습니다.",
        [0xB00D] = "툴 번호가 잘못되었습니다.",
        [0xB00E] = "사용자 번호가 잘못되었습니다.",
    };

    /// <summary>Status와 AddedStatus 값에 대응하는 진단 메시지를 반환합니다.</summary>
    public static string GetDescription(byte status, ushort addedStatus)
    {
        if (status != ExtendedErrorStatus)
        {
            return status switch
            {
                SuccessStatus => "정상 성공입니다.",
                UndefinedCommandStatus => "정의되지 않은 명령입니다. (0x08)",
                InvalidAttributeStatus => "유효하지 않은 Attribute 번호입니다. (0x09)",
                NonExistentInstanceStatus => "존재하지 않는 Instance 번호입니다. (0x28)",
                _ => $"YERC 응답 오류가 발생했습니다. (Status=0x{status:X2})"
            };
        }

        if (ExtendedErrorDescriptions.TryGetValue(addedStatus, out string? description))
        {
            return $"{description} (0x{addedStatus:X4})";
        }

        if (addedStatus is >= MinimumBackupSystemErrorCode and <= MaximumBackupSystemErrorCode)
        {
            return $"자동/수동 백업 또는 CMOS/RAM 시스템 오류가 발생했습니다. (0x{addedStatus:X4})";
        }

        if (TryConvertBcdToDecimal(addedStatus, out int decimalCode)
            && decimalCode is >= MinimumBcdAlarmCode and <= MaximumBcdAlarmCode)
        {
            return $"{YrcErrorCode.GetDescription(decimalCode)} (0x{addedStatus:X4})";
        }

        return $"YERC 확장 오류가 발생했습니다. (AddedStatus=0x{addedStatus:X4})";
    }

    private static bool TryConvertBcdToDecimal(ushort value, out int decimalResult)
    {
        int result = 0;
        int multiplier = 1;
        ushort remaining = value;
        for (int i = 0; i < BcdNibbleCount; i++)
        {
            int nibble = remaining & NibbleMask;
            if (nibble > MaximumDecimalDigit)
            {
                decimalResult = 0;
                return false;
            }

            result += nibble * multiplier;
            multiplier *= BcdDecimalBase;
            remaining >>= NibbleShiftBits;
        }

        decimalResult = result;
        return true;
    }
}
