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
using static FieldLink.PlcDrivers.IDCard.SAMSerialCommandBuilder;

namespace FieldLink.PlcDrivers.IDCard
{
    /// <summary>SAMSerial 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SAMSerialResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "input">input에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckADSCommandCompletion(List<byte> input)
        {
            if (input?.Count < 8)
                return false;
            if ((input[5] * 256 + input[6]) > (input.Count - 7))
                return false;
            return true;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "input">input에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckADSCommandAndSum(byte[] input)
        {
            if (input?.Length < 8)
                return new OperationResult(ProtocolMessages.SAMReceiveLengthMustLargerThan8);
            if (input[0] != 0xAA || input[1] != 0xAA || input[2] != 0xAA || input[3] != 0x96 || input[4] != 0x69)
                return new OperationResult(ProtocolMessages.SAMHeadCheckFailed);
            if ((input[5] * 256 + input[6]) != (input.Length - 7))
                return new OperationResult(ProtocolMessages.SAMLengthCheckFailed);
            int count = 0;
            for (int i = 5; i < input.Length - 1; i++)
            {
                count ^= input[i];
            }

            if (count != input[input.Length - 1])
                return new OperationResult(ProtocolMessages.SAMSumCheckFailed);
            else
                return OperationResult.CreateSuccessResult();
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ExtractSafeModuleNumber(byte[] data)
        {
            try
            {
                if (data[9] != 0x90)
                    return new OperationResult<string>(GetErrorDescription(data[9]));
                StringBuilder sb = new StringBuilder();
                sb.Append(data[10].ToString("D2"));
                sb.Append(".");
                sb.Append(data[12].ToString("D2"));
                sb.Append("-");
                sb.Append(BitConverter.ToInt32(data, 14).ToString());
                sb.Append("-");
                sb.Append(BitConverter.ToInt32(data, 18).ToString("D9"));
                sb.Append("-");
                sb.Append(BitConverter.ToInt32(data, 22).ToString("D9"));
                return OperationResult.CreateSuccessResult(sb.ToString());
            }
            catch (Exception ex)
            {
                return new OperationResult<string>("Error:" + ex.Message + "  Source Data: " + ProtocolBytes.ByteToHexString(data));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<IdentityCard> ExtractIdentityCard(byte[] data)
        {
            try
            {
                if (data[9] != 0x90)
                    return new OperationResult<IdentityCard>(GetErrorDescription(data[9]));
                string strContent = Encoding.Unicode.GetString(data, 14, 256);
                byte[] imageContent = ProtocolBytes.ArraySelectMiddle(data, 270, 1024);
                IdentityCard identityCard = new IdentityCard();
                identityCard.Name = strContent.Substring(0, 15);
                identityCard.Sex = strContent.Substring(15, 1) == "1" ? "남성" : strContent.Substring(15, 1) == "2" ? "여성" : "알 수 없음";
                identityCard.Nation = GetNationText(Convert.ToInt32(strContent.Substring(16, 2)));
                identityCard.Birthday = new DateTime(int.Parse(strContent.Substring(18, 4)), int.Parse(strContent.Substring(22, 2)), int.Parse(strContent.Substring(24, 2)));
                identityCard.Address = strContent.Substring(26, 35);
                identityCard.Id = strContent.Substring(61, 18);
                identityCard.Organ = strContent.Substring(79, 15);
                identityCard.ValidityStartDate = new DateTime(int.Parse(strContent.Substring(94, 4)), int.Parse(strContent.Substring(98, 2)), int.Parse(strContent.Substring(100, 2)));
                identityCard.ValidityEndDate = new DateTime(int.Parse(strContent.Substring(102, 4)), int.Parse(strContent.Substring(106, 2)), int.Parse(strContent.Substring(108, 2)));
                identityCard.Portrait = imageContent;
                return OperationResult.CreateSuccessResult(identityCard);
            }
            catch (Exception ex)
            {
                return new OperationResult<IdentityCard>(ex.Message);
            }
        }

        /// <summary>GetNationText 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "nation">nation에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetNationText(int nation)
        {
            switch (nation)
            {
                case 01:
                    return "한족";
                case 02:
                    return "몽골족";
                case 03:
                    return "후이족";
                case 04:
                    return "티베트족";
                case 05:
                    return "위구르족";
                case 06:
                    return "먀오족";
                case 07:
                    return "이족";
                case 08:
                    return "좡족";
                case 09:
                    return "부이족";
                case 10:
                    return "조선족";
                case 11:
                    return "만주족";
                case 12:
                    return "둥족";
                case 13:
                    return "야오족";
                case 14:
                    return "바이족";
                case 15:
                    return "투자족";
                case 16:
                    return "하니족";
                case 17:
                    return "카자흐족";
                case 18:
                    return "다이족";
                case 19:
                    return "리족";
                case 20:
                    return "리수족";
                case 21:
                    return "와족";
                case 22:
                    return "서족";
                case 23:
                    return "가오산족";
                case 24:
                    return "라후족";
                case 25:
                    return "수이족";
                case 26:
                    return "둥샹족";
                case 27:
                    return "나시족";
                case 28:
                    return "징포족";
                case 29:
                    return "키르기스족";
                case 30:
                    return "투족";
                case 31:
                    return "다우르족";
                case 32:
                    return "무라오족";
                case 33:
                    return "창족";
                case 34:
                    return "부랑족";
                case 35:
                    return "살라르족";
                case 36:
                    return "마오난족";
                case 37:
                    return "거라오족";
                case 38:
                    return "시버족";
                case 39:
                    return "아창족";
                case 40:
                    return "푸미족";
                case 41:
                    return "타지크족";
                case 42:
                    return "누족";
                case 43:
                    return "우즈베크족";
                case 44:
                    return "러시아족";
                case 45:
                    return "에벤키족";
                case 46:
                    return "더앙족";
                case 47:
                    return "바오안족";
                case 48:
                    return "위구족";
                case 49:
                    return "징족";
                case 50:
                    return "타타르족";
                case 51:
                    return "두룽족";
                case 52:
                    return "오로촌족";
                case 53:
                    return "허저족";
                case 54:
                    return "먼바족";
                case 55:
                    return "뤄바족";
                case 56:
                    return "지눠족";
                case 97:
                    return "기타";
                case 98:
                    return "외국계 중국 국적자";
                default:
                    return string.Empty;
            }
        }

        /// <summary>GetNationEnumerator 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static IEnumerator<string> GetNationEnumerator()
        {
            for (int i = 1; i < 57; i++)
            {
                yield return GetNationText(i);
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDescription(int err)
        {
            switch (err)
            {
                case 0x91:
                    return ProtocolMessages.SAMStatus91;
                case 0x10:
                    return ProtocolMessages.SAMStatus10;
                case 0x11:
                    return ProtocolMessages.SAMStatus11;
                case 0x21:
                    return ProtocolMessages.SAMStatus21;
                case 0x23:
                    return ProtocolMessages.SAMStatus23;
                case 0x24:
                    return ProtocolMessages.SAMStatus24;
                case 0x31:
                    return ProtocolMessages.SAMStatus31;
                case 0x32:
                    return ProtocolMessages.SAMStatus32;
                case 0x33:
                    return ProtocolMessages.SAMStatus33;
                case 0x40:
                    return ProtocolMessages.SAMStatus40;
                case 0x41:
                    return ProtocolMessages.SAMStatus41;
                case 0x47:
                    return ProtocolMessages.SAMStatus47;
                case 0x60:
                    return ProtocolMessages.SAMStatus60;
                case 0x66:
                    return ProtocolMessages.SAMStatus66;
                case 0x80:
                    return ProtocolMessages.SAMStatus80;
                case 0x81:
                    return ProtocolMessages.SAMStatus81;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
