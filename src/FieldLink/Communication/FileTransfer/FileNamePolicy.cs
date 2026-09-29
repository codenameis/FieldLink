using System;

namespace FieldLink.Communication.FileTransfer
{
    internal static class FileNamePolicy
    {
        internal static void Validate(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 128 || name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".", StringComparison.Ordinal))
                throw new ArgumentException("파일 이름은 1~128자의 단일 이름이어야 합니다.", nameof(name));
            foreach (char value in name)
                if (!((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') ||
                    (value >= '0' && value <= '9') || value == '_' || value == '-' || value == '.'))
                    throw new ArgumentException("파일 이름에는 영문자, 숫자, _, -, .만 사용할 수 있습니다.", nameof(name));
            string stem = name.Split('.')[0];
            string reserved = "|CON|PRN|AUX|NUL|COM1|COM2|COM3|COM4|COM5|COM6|COM7|COM8|COM9|LPT1|LPT2|LPT3|LPT4|LPT5|LPT6|LPT7|LPT8|LPT9|";
            if (reserved.IndexOf("|" + stem.ToUpperInvariant() + "|", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("예약된 파일 이름은 사용할 수 없습니다.", nameof(name));
        }
    }
}
