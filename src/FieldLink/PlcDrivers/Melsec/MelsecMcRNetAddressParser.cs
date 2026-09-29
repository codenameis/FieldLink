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
using static FieldLink.PlcDrivers.Melsec.MelsecMcRNetCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecMcRNet 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class MelsecMcRNetAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<MelsecMcDataType, int> AnalysisAddress(string address)
        {
            try
            {
                if (address.StartsWith("LSTS"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LSTS, Convert.ToInt32(address.Substring(4), MelsecMcDataType.R_LSTS.FromBase));
                else if (address.StartsWith("LSTC"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LSTC, Convert.ToInt32(address.Substring(4), MelsecMcDataType.R_LSTC.FromBase));
                else if (address.StartsWith("LSTN"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LSTN, Convert.ToInt32(address.Substring(4), MelsecMcDataType.R_LSTN.FromBase));
                else if (address.StartsWith("STS"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_STS, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_STS.FromBase));
                else if (address.StartsWith("STC"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_STC, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_STC.FromBase));
                else if (address.StartsWith("STN"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_STN, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_STN.FromBase));
                else if (address.StartsWith("LTS"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LTS, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_LTS.FromBase));
                else if (address.StartsWith("LTC"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LTC, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_LTC.FromBase));
                else if (address.StartsWith("LTN"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LTN, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_LTN.FromBase));
                else if (address.StartsWith("LCS"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LCS, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_LCS.FromBase));
                else if (address.StartsWith("LCC"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LCC, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_LCC.FromBase));
                else if (address.StartsWith("LCN"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_LCN, Convert.ToInt32(address.Substring(3), MelsecMcDataType.R_LCN.FromBase));
                else if (address.StartsWith("TS"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_TS, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_TS.FromBase));
                else if (address.StartsWith("TC"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_TC, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_TC.FromBase));
                else if (address.StartsWith("TN"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_TN, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_TN.FromBase));
                else if (address.StartsWith("CS"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_CS, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_CS.FromBase));
                else if (address.StartsWith("CC"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_CC, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_CC.FromBase));
                else if (address.StartsWith("CN"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_CN, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_CN.FromBase));
                else if (address.StartsWith("SM"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_SM, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_SM.FromBase));
                else if (address.StartsWith("SB"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_SB, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_SB.FromBase));
                else if (address.StartsWith("DX"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_DX, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_DX.FromBase));
                else if (address.StartsWith("DY"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_DY, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_DY.FromBase));
                else if (address.StartsWith("SD"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_SD, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_SD.FromBase));
                else if (address.StartsWith("SW"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_SW, Convert.ToInt32(address.Substring(2), MelsecMcDataType.R_SW.FromBase));
                else if (address.StartsWith("X"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_X, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_X.FromBase));
                else if (address.StartsWith("Y"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_Y, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_Y.FromBase));
                else if (address.StartsWith("M"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_M, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_M.FromBase));
                else if (address.StartsWith("L"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_L, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_L.FromBase));
                else if (address.StartsWith("F"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_F, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_F.FromBase));
                else if (address.StartsWith("V"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_V, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_V.FromBase));
                else if (address.StartsWith("S"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_S, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_S.FromBase));
                else if (address.StartsWith("B"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_B, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_B.FromBase));
                else if (address.StartsWith("D"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_D, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_D.FromBase));
                else if (address.StartsWith("W"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_W, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_W.FromBase));
                else if (address.StartsWith("R"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_R, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_R.FromBase));
                else if (address.StartsWith("Z"))
                    return OperationResult.CreateSuccessResult(MelsecMcDataType.R_Z, Convert.ToInt32(address.Substring(1), MelsecMcDataType.R_Z.FromBase));
                else
                    return new OperationResult<MelsecMcDataType, int>(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<MelsecMcDataType, int>(ex.Message);
            }
        }
    }
}
