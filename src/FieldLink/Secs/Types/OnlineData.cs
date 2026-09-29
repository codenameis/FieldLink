using System;
using System.IO;

namespace FieldLink.Secs.Types
{
    /// <summary>GEM 장비 모델과 소프트웨어 버전입니다.</summary>
    public sealed class OnlineData
    {
        /// <summary>장비 식별 정보를 생성합니다.</summary>
        public OnlineData(string model, string version)
        {
            ModelType = model ?? throw new ArgumentNullException(nameof(model));
            SoftVersion = version ?? throw new ArgumentNullException(nameof(version));
        }
        /// <summary>장비 모델입니다.</summary>
        public string ModelType { get; }
        /// <summary>소프트웨어 버전입니다.</summary>
        public string SoftVersion { get; }
        /// <summary>두 문자열을 포함한 List를 변환합니다. 빈 List는 식별 정보가 없음을 뜻합니다.</summary>
        public static implicit operator OnlineData(SecsValue value)
        {
            var list = SecsValue.RequireList(value);
            if (list.Length == 0)
                return new OnlineData("", "");
            if (list.Length != 2 || list[0].ItemType != SecsItemType.ASCII || list[1].ItemType != SecsItemType.ASCII)
                throw new InvalidDataException("Expected model and software version strings.");
            return new OnlineData((string)list[0].Value, (string)list[1].Value);
        }
        /// <summary>장비 식별 정보를 SECS List로 변환합니다.</summary>
        public static implicit operator SecsValue(OnlineData value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return new SecsValue(new object[] { value.ModelType, value.SoftVersion });
        }
    }
}
