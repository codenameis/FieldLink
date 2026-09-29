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
    /// <summary>ToolMessages 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class ToolMessagesResponseParser
    {
        /// <summary>MID0041 응답의 고정 위치 필드를 해석합니다.</summary>
        /// <param name = "reply">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<TighteningToolInfo> PraseMID0041(string reply)
        {
            try
            {
                int revision = Convert.ToInt32(reply.Substring(8, 3));
                TighteningToolInfo toolData = new TighteningToolInfo();
                toolData.ToolSerialNumber = reply.Substring(22, 14);
                toolData.ToolNumberOfTightening = Convert.ToUInt32(reply.Substring(38, 10));
                toolData.LastCalibrationDate = DateTime.ParseExact(reply.Substring(50, 19), "yyyy-MM-dd:HH:mm:ss", null);
                toolData.ControllerSerialNumber = reply.Substring(71, 10);
                if (revision > 1)
                {
                    toolData.CalibrationValue = Convert.ToDouble(reply.Substring(83, 6)) / 100d;
                    toolData.LastServiceDate = DateTime.ParseExact(reply.Substring(91, 19), "yyyy-MM-dd:HH:mm:ss", null);
                    toolData.TighteningsSinceService = Convert.ToUInt32(reply.Substring(112, 10));
                    toolData.ToolType = Convert.ToInt32(reply.Substring(124, 2));
                    toolData.MotorSize = Convert.ToInt32(reply.Substring(128, 2));
                    toolData.UseOpenEnd = reply[132] == '1';
                    toolData.TighteningDirection = reply[133] == '1' ? "CCW" : "CW";
                    toolData.MotorRotation = Convert.ToInt32(reply.Substring(134, 1));
                    toolData.ControllerSoftwareVersion = reply.Substring(137, 19);
                }

                return OperationResult.CreateSuccessResult(toolData);
            }
            catch (Exception ex)
            {
                return new OperationResult<TighteningToolInfo>("MID0031 prase failed: " + ex.Message + Environment.NewLine + "Source: " + reply);
            }
        }
    }
}
