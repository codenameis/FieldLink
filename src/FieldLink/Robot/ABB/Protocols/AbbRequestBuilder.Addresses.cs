using System;
using System.Collections.Generic;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.ABB.Protocols
{
    /// <summary>ABB의 논리 주소와 조회 경로를 연결합니다.</summary>
    public static partial class AbbRequestBuilder
    {
        /// <summary>논리 주소 별칭 또는 url= 경로를 요청으로 구성합니다. 알 수 없는 논리 주소는 실패합니다.</summary>
        public static OperationResult<AbbRequest<string>> BuildByAddress(string address)
        {
            var request = address.StartsWith("url=", StringComparison.OrdinalIgnoreCase) ? Create(address.Substring(4)) : FindByAddress(address);
            return request == null ? new OperationResult<AbbRequest<string>>(ProtocolMessages.NotSupportedFunction) : OperationResult.CreateSuccessResult(request);
        }

        private static AbbRequest<string> FindByAddress(string address)
        {
            if (address.ToUpper() == "ErrorState".ToUpper())
                return GetErrorState();
            else if (address.ToUpper() == "jointtarget".ToUpper())
                return GetJointTarget();
            else if (address.ToUpper() == "PhysicalJoints".ToUpper())
                return GetJointTarget();
            else if (address.ToUpper() == "SpeedRatio".ToUpper())
                return GetSpeedRatio();
            else if (address.ToUpper() == "OperationMode".ToUpper())
                return GetOperationMode();
            else if (address.ToUpper() == "CtrlState".ToUpper())
                return GetCtrlState();
            else if (address.ToUpper() == "ioin".ToUpper())
                return GetIOIn();
            else if (address.ToUpper() == "ioout".ToUpper())
                return GetIOOut();
            else if (address.ToUpper() == "io2in".ToUpper())
                return GetIO2In();
            else if (address.ToUpper() == "io2out".ToUpper())
                return GetIO2Out();
            else if (address.ToUpper().StartsWith("log".ToUpper()))
            {
                if (address.Length > 3)
                {
                    if (int.TryParse(address.Substring(3), out int length))
                        return GetLog(length);
                }

                return GetLog();
            }
            else if (address.ToUpper() == "system".ToUpper())
                return GetSystem();
            else if (address.ToUpper() == "robtarget".ToUpper())
                return GetRobotTarget();
            else if (address.ToUpper() == "ServoEnable".ToUpper())
                return GetServoEnable();
            else if (address.ToUpper() == "RapidExecution".ToUpper())
                return GetRapidExecution();
            else if (address.ToUpper() == "RapidTasks".ToUpper())
                return GetRapidTasks();
            else
                return null;
        }

        /// <summary>원본에서 제공하는 논리 조회 주소 목록입니다.</summary>
        public static List<string> GetSelectStrings()
        {
            return new List<string>()
            {
                "ErrorState",
                "jointtarget",
                "PhysicalJoints",
                "SpeedRatio",
                "OperationMode",
                "CtrlState",
                "ioin",
                "ioout",
                "io2in",
                "io2out",
                "log",
                "system",
                "robtarget",
                "ServoEnable",
                "RapidExecution",
                "RapidTasks"
            };
        }
    }
}
