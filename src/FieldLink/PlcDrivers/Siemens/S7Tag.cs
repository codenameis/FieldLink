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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>노드 클래스 객체</summary>
    public class S7Tag : IS7Object
    {
        /// <summary>데이터 이름</summary>
        public string Name { get; set; }
        /// <summary>데이터 ID 정보, 첫번째는 DB 블록에 대한 정보, 두번째는 데이터 정보, 구조체라면 계속 후로 확장</summary>
        public List<uint> LID { get; set; }
        /// <summary>타입 코드</summary>
        public byte TypeCode { get; set; }
        /// <summary>0보다 작으면 스캐너 데이터를 나타냅니다.</summary>
        public int ArrayLength { get; set; } = -1;
        /// <summary>타입이 구조체일 때, 연관된 다른 구조체의 ID 정보</summary>
        public uint StructID { get; set; }
        /// <summary>타입이 구조체일 때, 태그의 이동 정보</summary>
        public int StructOffset { get; set; }

        /// <summary>타입의 텍스트 설명을 가져오기</summary>
        /// <returns>문자열 정보</returns>
        public string GetTypeText()
        {
            string tmp = null;
            switch (TypeCode)
            {
                case 0:
                    tmp = "Void";
                    break;
                case 1:
                    tmp = "Bool";
                    break;
                case 2:
                    tmp = "Byte";
                    break;
                case 3:
                    tmp = "Char";
                    break;
                case 4:
                    tmp = "Word";
                    break;
                case 5:
                    tmp = "Int";
                    break;
                case 6:
                    tmp = "DWord";
                    break;
                case 7:
                    tmp = "DInt";
                    break;
                case 8:
                    tmp = "Real";
                    break;
                case 9:
                    tmp = "Date";
                    break;
                case 10:
                    tmp = "TimeOfDay";
                    break;
                case 11:
                    tmp = "Time";
                    break;
                case 12:
                    tmp = "S5Time";
                    break;
                case 13:
                    tmp = "S5Count";
                    break;
                case 14:
                    tmp = "DateAndTime";
                    break;
                case 15:
                    tmp = "InteretTime";
                    break;
                case 16:
                    tmp = "Array";
                    break;
                case 17:
                    tmp = "Struct";
                    break;
                case 18:
                    tmp = "EndStruct";
                    break;
                case 19:
                    tmp = "String";
                    break;
                case 28:
                    tmp = "Counter";
                    break;
                case 29:
                    tmp = "Timer";
                    break;
                case 48:
                    tmp = "LReal";
                    break;
                case 49:
                    tmp = "ULInt";
                    break;
                case 50:
                    tmp = "LInt";
                    break;
                case 51:
                    tmp = "LWord";
                    break;
                case 52:
                    tmp = "USInt";
                    break;
                case 53:
                    tmp = "UInt";
                    break;
                case 54:
                    tmp = "UDInt";
                    break;
                case 55:
                    tmp = "SInt";
                    break;
                case 56:
                    tmp = "Bcd8";
                    break;
                case 61:
                    tmp = "WChar";
                    break;
                case 62:
                    tmp = "WString";
                    break;
                default:
                    tmp = $"Unknown({TypeCode})";
                    break;
            }

            return ArrayLength >= 0 ? $"{tmp}[{ArrayLength}]" : tmp;
        }

        /// <summary>LID의 텍스트 정보를 가져오기</summary>
        /// <returns></returns>
        public string GetLIDText()
        {
            StringBuilder sb = new StringBuilder();
            if (LID != null)
            {
                for (int i = 0; i < LID.Count; i++)
                {
                    sb.Append(LID[i].ToString("X"));
                    if (i != LID.Count - 1)
                        sb.Append(".");
                }
            }

            return sb.ToString();
        }

        /// <summary>WriteMessgae 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        public void WriteMessgae(MemoryStream ms)
        {
            S7Object.WriteUint32(ms, 0x00);
            S7Object.WriteUint32(ms, LID[0]);
            S7Object.WriteUint32(ms, (uint)LID.Count);
            if (LID[0] >= 0x8A0E0000)
                S7Object.WriteUint32(ms, 2550);
            else
                S7Object.WriteUint32(ms, 3736);
            for (int i = 1; i < LID.Count; i++)
            {
                S7Object.WriteUint32(ms, LID[i]);
            }
        }

        /// <summary>GetNumberOfFields 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public int GetNumberOfFields()
        {
            return 3 + LID.Count;
        }

        /// <summary>깊이 복제 객체</summary>
        /// <returns></returns>
        public S7Tag Clone()
        {
            S7Tag s7Tag = new S7Tag();
            s7Tag.Name = Name;
            if (LID != null)
            {
                s7Tag.LID = new List<uint>(LID.ToArray());
            }

            s7Tag.TypeCode = TypeCode;
            s7Tag.ArrayLength = ArrayLength;
            s7Tag.StructID = StructID;
            s7Tag.StructOffset = StructOffset;
            return s7Tag;
        }
    }
}
