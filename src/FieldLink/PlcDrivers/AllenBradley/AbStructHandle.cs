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
    /// <summary>구조의 핸들 정보</summary>
    public class AbStructHandle
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public AbStructHandle()
        {
        }

        /// <summary>원본 바이트의 데이터를 사용하여 정보를 인덱스하여 객체를 인스턴스화합니다.</summary>
        /// <param name = "source">원시 바이트 데이터</param>
        /// <param name = "index">초기 편향 인덱스</param>
        public AbStructHandle(byte[] source, int index)
        {
            ReturnCount = BitConverter.ToUInt16(source, index + 0); // 반환된 항목 수
            TemplateObjectDefinitionSize = BitConverter.ToUInt32(source, index + 6); // 템플릿 정의 구조의 크기 32비트 워드
            TemplateStructureSize = BitConverter.ToUInt32(source, index + 14); // Read Tag 서비스를 사용하여 구조를 읽을 때 와이어에서 전송되는 바이트의 수
            MemberCount = BitConverter.ToUInt16(source, index + 22); // 구조에 정의된 구성원 수
            StructureHandle = BitConverter.ToUInt16(source, index + 28); // 구조물Handle(Calculated CRC value for members of the structure. 구조물의 구성원들을 위한 계산된 CRC 값.
        }

        /// <summary>복귀 항목</summary>
        /// <remarks>반환된 항목 수</remarks>
        public ushort ReturnCount { get; set; }
        /// <summary>구조 정의 크기</summary>
        /// <remarks>이것은 구조 구성원의 수입니다.</remarks>
        public uint TemplateObjectDefinitionSize { get; set; }
        /// <summary>레이저 서비스를 사용하여 구조를 읽을 때 라인에서 전송되는 바이트 수</summary>
        /// <remarks>이것은 구조 데이터의 바이트 수입니다.</remarks>
        public uint TemplateStructureSize { get; set; }
        /// <summary>멤버수</summary>
        /// <remarks>이것은 구조 구성원의 수입니다.</remarks>
        public ushort MemberCount { get; set; }
        /// <summary>구조의 핸들</summary>
        /// <remarks>이것은 읽기/쓰기 태그 서비스에 사용되는 태그 타입 파라미터입니다.</remarks>
        public ushort StructureHandle { get; set; }
    }
}
