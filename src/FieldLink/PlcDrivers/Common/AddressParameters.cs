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

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>AddressParameters 프로토콜 값입니다.</summary>
    public static class AddressParameters
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "paraName">paraName에 사용할 입력값입니다.</param>
        /// <param name = "defaultValue">defaultValue에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int ExtractParameter(ref string address, string paraName, int defaultValue)
        {
            OperationResult<int> extra = ExtractParameter(ref address, paraName);
            return extra.IsSuccess ? extra.Content : defaultValue;
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "paraName">paraName에 사용할 입력값입니다.</param>
        /// <param name = "defaultValue">defaultValue에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool ExtractBooleanParameter(ref string address, string paraName, bool defaultValue)
        {
            OperationResult<bool> extra = ExtractBooleanParameter(ref address, paraName);
            return extra.IsSuccess ? extra.Content : defaultValue;
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "paraName">paraName에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int> ExtractParameter(ref string address, string paraName)
        {
            try
            {
                Match match = Regex.Match(address, paraName + "=[0-9A-Fa-fxX]+;", RegexOptions.IgnoreCase);
                if (!match.Success)
                    return new OperationResult<int>($"Address [{address}] can't find [{paraName}] Parameters. for example : {paraName}=1;100");
                string number = match.Value.Substring(paraName.Length + 1, match.Value.Length - paraName.Length - 2);
                int value = (number.StartsWith("0x") || number.StartsWith("0X")) ? Convert.ToInt32(number.Substring(2), 16) : number.StartsWith("0") ? Convert.ToInt32(number, 8) : Convert.ToInt32(number);
                address = address.Replace(match.Value, "");
                return OperationResult.CreateSuccessResult(value);
            }
            catch (Exception ex)
            {
                return new OperationResult<int>($"Address [{address}] Get [{paraName}] Parameters failed: " + ex.Message);
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "paraName">paraName에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<bool> ExtractBooleanParameter(ref string address, string paraName)
        {
            try
            {
                Match match = Regex.Match(address, paraName + "=[0-1A-Za-z]+;");
                if (!match.Success)
                    return new OperationResult<bool>($"Address [{address}] can't find [{paraName}] Parameters. for example : {paraName}=True;100");
                string number = match.Value.Substring(paraName.Length + 1, match.Value.Length - paraName.Length - 2);
                bool value = false;
                if (Regex.IsMatch(number, "^[0-1]+$"))
                {
                    value = Convert.ToInt32(number) != 0;
                }
                else
                {
                    value = Convert.ToBoolean(number);
                }

                address = address.Replace(match.Value, "");
                return OperationResult.CreateSuccessResult(value);
            }
            catch (Exception ex)
            {
                return new OperationResult<bool>($"Address [{address}] Get [{paraName}] Parameters failed: " + ex.Message);
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int ExtractStartIndex(ref string address)
        {
            try
            {
                Match match = Regex.Match(address, "\\[[0-9]+\\]$");
                if (!match.Success)
                    return -1;
                string number = match.Value.Substring(1, match.Value.Length - 2);
                int value = Convert.ToInt32(number);
                address = address.Remove(address.Length - match.Value.Length);
                return value;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "defaultTransform">defaultTransform에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static IProtocolValueConverter ExtractTransformParameter(ref string address, IProtocolValueConverter defaultTransform)
        {
            try
            {
                string paraName = "format";
                Match match = Regex.Match(address, paraName + "=(ABCD|BADC|DCBA|CDAB);", RegexOptions.IgnoreCase);
                if (!match.Success)
                    return defaultTransform;
                string format = match.Value.Substring(paraName.Length + 1, match.Value.Length - paraName.Length - 2);
                ByteOrder dataFormat = defaultTransform.ByteOrder;
                switch (format.ToUpper())
                {
                    case "ABCD":
                        dataFormat = ByteOrder.BigEndian;
                        break;
                    case "BADC":
                        dataFormat = ByteOrder.BigEndianWithByteSwap;
                        break;
                    case "DCBA":
                        dataFormat = ByteOrder.LittleEndian;
                        break;
                    case "CDAB":
                        dataFormat = ByteOrder.LittleEndianWithByteSwap;
                        break;
                    default:
                        break;
                }

                address = address.Replace(match.Value, "");
                if (dataFormat != defaultTransform.ByteOrder)
                    return defaultTransform.WithByteOrder(dataFormat);
                return defaultTransform;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>읽기 길이를 양수 segment 이하로 분할하고 각 시작 주소를 계산합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "segment">segment에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int[], int[]> SplitReadLength(int address, ushort length, int segment)
        {
            if (segment <= 0)
                throw new ArgumentOutOfRangeException(nameof(segment), "Segment length must be positive.");

            int[] segmentLengths = ProtocolBytes.SplitIntegerToArray(length, segment);
            int[] startAddresses = new int[segmentLengths.Length];
            for (int segmentIndex = 0; segmentIndex < startAddresses.Length; segmentIndex++)
            {
                if (segmentIndex == 0)
                {
                    startAddresses[segmentIndex] = address;
                }
                else
                {
                    startAddresses[segmentIndex] = startAddresses[segmentIndex - 1] + segmentLengths[segmentIndex - 1];
                }
            }

            return OperationResult.CreateSuccessResult(startAddresses, segmentLengths);
        }

        /// <summary>주소당 원소 수(addressLength)와 양수 segment로 쓰기 데이터를 분할합니다. 두 값의 곱은 양수 Int32 범위여야 합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "segment">segment에 사용할 입력값입니다.</param>
        /// <param name = "addressLength">addressLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<int[], List<T[]>> SplitWriteData<T>(int address, T[] value, ushort segment, int addressLength)
        {
            if (segment == 0)
                throw new ArgumentOutOfRangeException(nameof(segment), "Segment length must be positive.");

            // 곱셈하기 전에 나눗셈으로 검사해야 정수 범위 초과를 막을 수 있습니다.
            if (addressLength <= 0 || addressLength > int.MaxValue / segment)
                throw new ArgumentOutOfRangeException(nameof(addressLength), "The segment size in array elements must fit a positive Int32.");

            int maximumSegmentElements = segment * addressLength;
            List<T[]> dataSegments = ProtocolBytes.ArraySplitByLength(value, maximumSegmentElements);
            int[] startAddresses = new int[dataSegments.Count];
            for (int segmentIndex = 0; segmentIndex < startAddresses.Length; segmentIndex++)
            {
                if (segmentIndex == 0)
                {
                    startAddresses[segmentIndex] = address;
                }
                else
                {
                    // 배열 원소 수를 장치 주소 개수로 환산해서 다음 시작 주소를 구합니다.
                    int previousAddressCount = dataSegments[segmentIndex - 1].Length / addressLength;
                    startAddresses[segmentIndex] = startAddresses[segmentIndex - 1] + previousAddressCount;
                }
            }

            return OperationResult.CreateSuccessResult(startAddresses, dataSegments);
        }

        /// <summary>GetBitIndexInformation 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetBitIndexInformation(ref string address)
        {
            int bitIndex = 0;
            int lastIndex = address.LastIndexOf('.');
            if (lastIndex > 0 && lastIndex < address.Length - 1)
            {
                string bit = address.Substring(lastIndex + 1);
                if (bit.Contains(new string[] { "A", "B", "C", "D", "E", "F" }))
                {
                    bitIndex = Convert.ToInt32(bit, 16);
                }
                else
                {
                    bitIndex = Convert.ToInt32(bit);
                }

                address = address.Substring(0, lastIndex);
            }

            return bitIndex;
        }

        /// <summary>CalculateStartBitIndexAndLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "addressStart">addressStart에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "newStart">newStart에 사용할 입력값입니다.</param>
        /// <param name = "byteLength">byteLength에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        public static void CalculateStartBitIndexAndLength(int addressStart, ushort length, out int newStart, out ushort byteLength, out int offset)
        {
            byteLength = (ushort)((addressStart + length - 1) / 8 - addressStart / 8 + 1);
            offset = addressStart % 8;
            newStart = addressStart - offset;
        }

        /// <summary>CalculateBitStartIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "bit">bit에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int CalculateBitStartIndex(string bit)
        {
            if (Regex.IsMatch(bit, @"[ABCDEF]", RegexOptions.IgnoreCase))
            {
                return Convert.ToInt32(bit, 16);
            }
            else
            {
                return Convert.ToInt32(bit);
            }
        }

        /// <summary>CreateTwoArrayFromOneArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <param name = "row">row에 사용할 입력값입니다.</param>
        /// <param name = "col">col에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[, ] CreateTwoArrayFromOneArray<T>(T[] array, int row, int col)
        {
            T[, ] twoArray = new T[row, col];
            int count = 0;
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    twoArray[i, j] = array[count];
                    count++;
                }
            }

            return twoArray;
        }

        /// <summary>IsAddressEndWithIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool IsAddressEndWithIndex(string address)
        {
            return Regex.IsMatch(address, @"\[[0-9]+\]$");
        }

        /// <summary>CalculateOccupyLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "hex">hex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int CalculateOccupyLength(int address, int length, int hex = 8)
        {
            // 100을 M 10로 나누면 13 - 12 + 1
            return (address + length - 1) / hex - (address / hex) + 1;
        }
    }
}
