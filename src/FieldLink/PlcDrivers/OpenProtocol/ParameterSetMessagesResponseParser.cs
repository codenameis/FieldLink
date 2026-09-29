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

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>ParameterSetMessages 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class ParameterSetMessagesResponseParser
    {
        /// <summary>MID0011 응답의 고정 위치 필드를 해석합니다.</summary>
        /// <param name = "reply">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int[]> PraseMID0011(string reply)
        {
            try
            {
                int count = Convert.ToInt32(reply.Substring(20, 3));
                int[] ints = new int[count];
                for (int i = 0; i < count; i++)
                {
                    ints[i] = Convert.ToInt32(reply.Substring(23 + i * 3, 3));
                }

                return OperationResult.CreateSuccessResult(ints);
            }
            catch (Exception ex)
            {
                return new OperationResult<int[]>("MID0011 prase failed: " + ex.Message + Environment.NewLine + "Source: " + reply);
            }
        }

        /// <summary>MID0012 응답의 고정 위치 필드를 해석합니다.</summary>
        /// <param name = "reply">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<TighteningParameterSet> PraseMID0012(string reply)
        {
            try
            {
                TighteningParameterSet parameterSetData = new TighteningParameterSet();
                parameterSetData.ParameterSetID = Convert.ToInt32(reply.Substring(22, 3));
                parameterSetData.ParameterSetName = reply.Substring(27, 25).Trim();
                parameterSetData.RotationDirection = reply[54] == '1' ? "CW" : "CCW";
                parameterSetData.BatchSize = Convert.ToInt32(reply.Substring(57, 2));
                parameterSetData.TorqueMin = Convert.ToDouble(reply.Substring(61, 6)) / 100d;
                parameterSetData.TorqueMax = Convert.ToDouble(reply.Substring(69, 6)) / 100d;
                parameterSetData.TorqueFinalTarget = Convert.ToDouble(reply.Substring(77, 6)) / 100d;
                parameterSetData.AngleMin = Convert.ToInt32(reply.Substring(85, 5));
                parameterSetData.AngleMax = Convert.ToInt32(reply.Substring(92, 5));
                parameterSetData.AngleFinalTarget = Convert.ToInt32(reply.Substring(99, 5));
                return OperationResult.CreateSuccessResult(parameterSetData);
            }
            catch (Exception ex)
            {
                return new OperationResult<TighteningParameterSet>("MID0013 prase failed: " + ex.Message + Environment.NewLine + "Source: " + reply);
            }
        }
    }
}
