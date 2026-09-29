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

namespace FieldLink.PlcDrivers.IDCard
{
    /// <summary>신분증의 정보</summary>
    public class IdentityCard
    {
        /// <summary>이름</summary>
        public string Name { get; set; }
        /// <summary>성별</summary>
        public string Sex { get; set; }
        /// <summary>주민등록번호</summary>
        public string Id { get; set; }
        /// <summary>민족</summary>
        public string Nation { get; set; }
        /// <summary>생일</summary>
        public DateTime Birthday { get; set; }
        /// <summary>주소</summary>
        public string Address { get; set; }
        /// <summary>발급 기관</summary>
        public string Organ { get; set; }
        /// <summary>유효 날짜의 시작 날짜</summary>
        public DateTime ValidityStartDate { get; set; }
        /// <summary>유효 날짜의 종료 날짜</summary>
        public DateTime ValidityEndDate { get; set; }
        /// <summary>헤드 이미지 정보</summary>
        public byte[] Portrait { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>문자열</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("성명: " + Name);
            sb.Append(Environment.NewLine);
            sb.Append("성별: " + Sex);
            sb.Append(Environment.NewLine);
            sb.Append("민족: " + Nation);
            sb.Append(Environment.NewLine);
            sb.Append("신분증 번호: " + Id);
            sb.Append(Environment.NewLine);
            sb.Append($"생년월일: {Birthday.Year}년 {Birthday.Month}월 {Birthday.Day}일");
            sb.Append(Environment.NewLine);
            sb.Append("주소: " + Address);
            sb.Append(Environment.NewLine);
            sb.Append("발급 기관: " + Organ);
            sb.Append(Environment.NewLine);
            sb.Append($"유효 기간: {ValidityStartDate.Year}년 {ValidityStartDate.Month}월 {ValidityStartDate.Day}일 - {ValidityEndDate.Year}년 {ValidityEndDate.Month}월 {ValidityEndDate.Day}일");
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }
    }
}
