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
    /// <summary>AB PLC의 라벨 노드 데이터 정보</summary>
    public class AllenBradleyItemValue
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public AllenBradleyItemValue()
        {
        }

        /// <summary>XML 요소의 리소스를 지정하여 객체 정보를 인스턴스화합니다.</summary>
        /// <param name = "element">XML 요소 정보</param>
        public AllenBradleyItemValue(XElement element)
        {
            LoadByXml(element);
        }

        /// <summary>현재 라벨의 이름 정보</summary>
        public string Name { get; set; }
        /// <summary>실제 배열 버퍼</summary>
        public byte[] Buffer { get; set; }
        /// <summary>그리고 여기 있는 것은</summary>
        public bool IsArray { get; set; }
        /// <summary>단위 데이터 길이 정보</summary>
        public int TypeLength { get; set; } = 1;
        /// <summary>데이터 타입 정보</summary>
        public ushort TypeCode { get; set; } = AllenBradleyDefinitions.CIP_Type_Bool;

        /// <summary>값을 동일하게 설명하는 일련 문자열 정보로 변환합니다.</summary>
        /// <returns>xml 요소</returns>
        public XElement ToXml()
        {
            XElement element = new XElement(nameof(AllenBradleyItemValue));
            element.SetAttributeValue(nameof(Name), Name);
            element.SetAttributeValue(nameof(TypeCode), TypeCode);
            element.SetAttributeValue(nameof(IsArray), IsArray);
            element.SetAttributeValue(nameof(TypeLength), TypeLength);
            if (Buffer != null)
                element.SetAttributeValue(nameof(Buffer), Buffer.ToHexString());
            return element;
        }

        /// <summary>GetXmlValue 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "element">element에 사용할 입력값입니다.</param>
        /// <param name = "name">name에 사용할 입력값입니다.</param>
        /// <param name = "defaultValue">defaultValue에 사용할 입력값입니다.</param>
        /// <param name = "trans">trans에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        private T GetXmlValue<T>(XElement element, string name, T defaultValue, Func<string, T> trans)
        {
            XAttribute attribute = element.Attribute(name);
            if (attribute == null)
                return defaultValue;
            try
            {
                return trans(attribute.Value);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>xml 요소에서 현재 노드 데이터 정보를 로드합니다</summary>
        /// <param name = "element">요소 정보</param>
        public void LoadByXml(XElement element)
        {
            if (element.Name == nameof(AllenBradleyItemValue))
            {
                Name = GetXmlValue(element, nameof(Name), Name, m => m);
                TypeCode = GetXmlValue(element, nameof(TypeCode), TypeCode, ushort.Parse);
                IsArray = GetXmlValue(element, nameof(IsArray), IsArray, bool.Parse);
                TypeLength = GetXmlValue(element, nameof(TypeLength), TypeLength, int.Parse);
                Buffer = GetXmlValue(element, nameof(Buffer), "", m => m).ToHexBytes();
            }
        }
    }
}
