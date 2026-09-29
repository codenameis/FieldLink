using FieldLink.PlcDrivers.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AB PLC의 데이터 레이블 엔티티 클래스</summary>
    public class AbTagItem
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public AbTagItem()
        {
            ArrayLength = new int[]
            {
                -1,
                -1,
                -1
            };
        }

        /// <summary>인스턴스 ID</summary>
        public uint InstanceID { get; set; }
        /// <summary>현재 라벨 이름</summary>
        public string Name { get; set; }
        /// <summary>예를 들어, 0x0C1은 bool 타입을 나타냅니다.<c>IsStruct</c>값: <c>True</c>그러면, 이 속성은 구조체의 인스턴스 ID를 나타냅니다.</summary>
        public ushort SymbolType { get; set; }
        /// <summary>데이터의 차원 정보, 기본값은 0, 스펙트럼 데이터, 1은 1차원 배열, 2는 2차원 배열</summary>
        public int ArrayDimension { get; set; }
        /// <summary>현재 태그가 구조 데이터인지</summary>
        public bool IsStruct { get; set; }
        /// <summary>현재는 배열인 경우, 배열의 길이를 나타내는 구조체의 변수 정보를 읽었을 때만 유효하며, -1은 무효이다.</summary>
        public int[] ArrayLength { get; set; }

        /// <summary>현재 태그가 구조체의 태그라면, 구조체의 구성원 정보를 나타냅니다.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public AbTagItem[] Members { get; set; }

        /// <summary>사용자 정의 추가 객체</summary>
        [Newtonsoft.Json.JsonIgnore]
        public object Tag { get; set; }
        /// <summary>구조체에서 실제 데이터의 오차 위치 정보를 가져오거나 설정합니다.</summary>
        public int ByteOffset { get; set; }

        /// <summary>타입의 텍스트 설명 정보를 가져오기</summary>
        /// <returns>텍스트 정보</returns>
        public string GetTypeText()
        {
            string array = string.Empty;
            if (ArrayDimension == 1)
                array = ArrayLength[0] >= 0 ? $"[{ArrayLength[0]}]" : "[]";
            else if (ArrayDimension == 2)
                array = $"[{ArrayLength[0]},{ArrayLength[1]}]";
            else if (ArrayDimension == 3)
                array = $"[{ArrayLength[0]},{ArrayLength[1]},{ArrayLength[2]}]";
            if (IsStruct)
                return "struct" + array;
            if (SymbolType == 0x08)
                return "date" + array;
            if (SymbolType == 0x09)
                return "time" + array;
            if (SymbolType == 0x0A)
                return "timeAndDate" + array;
            if (SymbolType == 0x0B)
                return "timeOfDate" + array;
            if (SymbolType == 0xC1)
                return "bool" + array;
            if (SymbolType == 0xC2)
                return "sbyte" + array;
            if (SymbolType == 0xC3)
                return "short" + array;
            if (SymbolType == 0xC4)
                return "int" + array;
            if (SymbolType == 0xC5)
                return "long" + array;
            if (SymbolType == 0xC6)
                return "byte" + array;
            if (SymbolType == 0xC7)
                return "ushort" + array;
            if (SymbolType == 0xC8)
                return "uint" + array;
            if (SymbolType == 0xC9)
                return "ulong" + array;
            if (SymbolType == 0xCA)
                return "float" + array;
            if (SymbolType == 0xCB)
                return "double" + array;
            if (SymbolType == 0xCC)
                return "struct";
            if (SymbolType == 0xD0)
                return "string";
            if (SymbolType == 0xD1)
                return "byte-str";
            if (SymbolType == 0xD2)
                return "word-str";
            if (SymbolType == AllenBradleyDefinitions.CIP_Type_D3)
            {
                if (ArrayDimension == 0)
                    return "bool[32]";
                else if (ArrayDimension == 1)
                    return "bool" + $"[{ArrayLength[0] * 32}]";
                else
                    return "bool-str" + array;
            }

            if ((SymbolType | 0x0f00) == 0x0fc1)
                return "bool";
            return "";
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => Name;
        /// <summary>SetSymbolType 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        private void SetSymbolType(ushort value)
        {
            ArrayDimension = (value & 0x4000) == 0x4000 ? 2 : 0 + (value & 0x2000) == 0x2000 ? 1 : 0;
            IsStruct = (value & 0x8000) == 0x8000;
            // 가장 높은 4자리
            SymbolType = (ushort)(value & 0x0FFF);
        }

        /// <summary>개별 레이블 데이터 정보를 복제합니다.</summary>
        /// <param name = "abTagItem">표지 정보</param>
        /// <returns>새로운 사례의 태그</returns>
        public static AbTagItem CloneBy(AbTagItem abTagItem)
        {
            if (abTagItem == null)
                return null;
            AbTagItem abTag = new AbTagItem();
            abTag.InstanceID = abTagItem.InstanceID;
            abTag.Name = abTagItem.Name;
            abTag.ByteOffset = abTagItem.ByteOffset;
            abTag.SymbolType = abTagItem.SymbolType;
            abTag.ArrayDimension = abTagItem.ArrayDimension;
            abTag.ArrayLength[0] = abTagItem.ArrayLength[0];
            abTag.ArrayLength[1] = abTagItem.ArrayLength[1];
            abTag.ArrayLength[2] = abTagItem.ArrayLength[2];
            abTag.IsStruct = abTagItem.IsStruct;
            return abTag;
        }

        /// <summary>전체 태그 배열 정보를 복제</summary>
        /// <param name = "abTagItems">표기된 배열 정보</param>
        /// <returns>표기 대열</returns>
        public static AbTagItem[] CloneBy(AbTagItem[] abTagItems)
        {
            AbTagItem[] abTags = new AbTagItem[abTagItems.Length];
            for (int i = 0; i < abTagItems.Length; i++)
            {
                abTags[i] = CloneBy(abTagItems[i]);
            }

            return abTags;
        }

        /// <summary>정해진 원본 바이트의 데이터에서 실제 노드 정보를 분석합니다.</summary>
        /// <param name = "source">원시 바이트 데이터</param>
        /// <param name = "index">초기 인덱스</param>
        /// <returns>표지 정보</returns>
        public static AbTagItem PraseAbTagItem(byte[] source, ref int index)
        {
            AbTagItem td = new AbTagItem();
            td.InstanceID = BitConverter.ToUInt32(source, index);
            index += 4;
            ushort nameLen = BitConverter.ToUInt16(source, index);
            index += 2;
            td.Name = Encoding.ASCII.GetString(source, index, nameLen);
            index += nameLen;
            // SymbolType는 스펙트럼 데이터의 타입이고, 구조물에서는 주소입니다.
            td.SetSymbolType(BitConverter.ToUInt16(source, index));
            index += 2;
            // 차원 정보
            td.ArrayLength[0] = BitConverter.ToInt32(source, index);
            index += 4;
            td.ArrayLength[1] = BitConverter.ToInt32(source, index);
            index += 4;
            td.ArrayLength[2] = BitConverter.ToInt32(source, index);
            index += 4;
            return td;
        }

        /// <summary>지정된 원본 바이트의 데이터에서 실제 레이거 배열을 파싱하고, 시스템으로 보존되는 배열이거나__로 시작하는 경우 자동으로 무시한다.</summary>
        /// <param name = "source">원시 바이트 데이터</param>
        /// <param name = "index">초기 인덱스</param>
        /// <param name = "isGlobalVariable">지역 변수인지</param>
        /// <param name = "instance">마지막 태그를 출력하는 인스턴스 ID</param>
        /// <returns>표지 정보</returns>
        public static List<AbTagItem> PraseAbTagItems(byte[] source, int index, bool isGlobalVariable, out uint instance)
        {
            List<AbTagItem> array = new List<AbTagItem>();
            instance = 0;
            while (index < source.Length)
            {
                AbTagItem td = PraseAbTagItem(source, ref index);
                instance = td.InstanceID;
                // 시스템에서 저장된 데이터 정보를 삭제합니다.
                if ((td.SymbolType & 0x1000) != 0x1000)
                    //  && !td.Name.Contains(":")
                    if (!td.Name.StartsWith("__") && !td.Name.Contains(":")) // __로 시작하는 변수 이름을 제거하고, 코멘트를 포함할 수 없습니다. 자세한 내용은 1756-pm020-en-p.pdf page51를 참조하십시오.
                    {
                        if (!isGlobalVariable)
                            td.Name = "Program:MainProgram." + td.Name;
                        array.Add(td);
                    }
            }

            return array;
        }

        /// <summary>지정된 바이트에 도달하는 길이 정보를 계산하여 고정 분기 기호의 바이트 길이를 계산할 수 있습니다.</summary>
        /// <param name = "source">원시 바이트 데이터</param>
        /// <param name = "index">인덱스 위치</param>
        /// <param name = "value">판단을 기다리는 바이트</param>
        /// <returns>문자열 길이는 존재하지 않으면 -1을 반환합니다.</returns>
        private static int CalculatesSpecifiedCharacterLength(byte[] source, int index, byte value)
        {
            for (int i = index; i < source.Length; i++)
            {
                if (source[i] == value)
                    return i - index;
            }

            return -1;
        }

        /// <summary>CalculatesString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "source">source에 사용할 입력값입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        private static string CalculatesString(byte[] source, ref int index, byte value)
        {
            if (index >= source.Length)
                return string.Empty;
            int length = CalculatesSpecifiedCharacterLength(source, index, value);
            if (length < 0)
            {
                index = source.Length;
                return string.Empty;
            }

            string name = Encoding.ASCII.GetString(source, index, length);
            index += length + 1;
            return name;
        }

        /// <summary>구조의 데이터에서 실제 서브 태그 정보를 파싱</summary>
        /// <param name = "source">원본 바이트</param>
        /// <param name = "index">이동 인덱스</param>
        /// <param name = "structHandle">구조 문절</param>
        /// <returns>결과 내용</returns>
        public static List<AbTagItem> PraseAbTagItemsFromStruct(byte[] source, int index, AbStructHandle structHandle)
        {
            List<AbTagItem> array = new List<AbTagItem>();
            int offset = structHandle.MemberCount * 8 + index;
            string structName = CalculatesString(source, ref offset, 0x00);
            for (int i = 0; i < structHandle.MemberCount; i++)
            {
                AbTagItem abTagItem = new AbTagItem();
                abTagItem.ArrayLength[0] = BitConverter.ToUInt16(source, 8 * i + index + 0);
                abTagItem.SetSymbolType(BitConverter.ToUInt16(source, 8 * i + index + 2));
                abTagItem.ByteOffset = BitConverter.ToInt32(source, 8 * i + index + 4) + 2;
                abTagItem.Name = CalculatesString(source, ref offset, 0x00);
                //if (!abTagItem.Name.StartsWith( "ZZZZZZZZZZ" ))
                array.Add(abTagItem);
            }

            return array;
        }
    }
}
