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
using static FieldLink.PlcDrivers.Beckhoff.AdsCommandBuilder;
using static FieldLink.PlcDrivers.Beckhoff.AdsAddressParser;
using static FieldLink.PlcDrivers.Beckhoff.AdsValueConverter;

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>Ads 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class AdsResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int> CheckResponse(byte[] response)
        {
            // Beckhoff AMS/TCP: 6-byte TCP header, 32-byte AMS header, then ADS data.
            if (response == null || response.Length < 38)
                return new OperationResult<int>("Incomplete AMS/TCP response header.");
            if (response[0] != 0 || response[1] != 0 ||
                BitConverter.ToUInt32(response, 2) != response.Length - 6 ||
                BitConverter.ToUInt32(response, 26) != response.Length - 38)
                return new OperationResult<int>("Invalid AMS/TCP response length or reserved field.");
            if (BitConverter.ToUInt16(response, 24) != 0x0005)
                return new OperationResult<int>("Expected an ADS response over TCP.");
            try
            {
                int ams = BitConverter.ToInt32(response, 30);
                if (ams != 0)
                    return new OperationResult<int>(ams, GetErrorCodeText(ams) + Environment.NewLine + "Source:" + response.ToHexString(' '));
                if (response.Length < 42)
                    return new OperationResult<int>("Missing ADS result code.");
                int status = BitConverter.ToInt32(response, 38);
                if (status != 0)
                    return new OperationResult<int>(status, GetErrorCodeText(status) + Environment.NewLine + "Source:" + response.ToHexString(' '));
            }
            catch (Exception ex)
            {
                return new OperationResult<int>(ex.Message + " Source:" + response.ToHexString(' '));
            }

            return OperationResult.CreateSuccessResult(0);
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "error">error에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorCodeText(int error)
        {
            switch (error)
            {
                case 0:
                    return "NO ERROR";
                case 1:
                    return "InternalError";
                case 2:
                    return "NO RTIME";
                case 3:
                    return "Allocation locked – memory error.";
                case 4:
                    return "Mailbox full – the ADS message could not be sent. Reducing the number of ADS messages per cycle will help.";
                case 5:
                    return "WRONG RECEIVEH MSG";
                case 6:
                    return "Target port not found – ADS server is not started or is not reachable.";
                case 7:
                    return "Target computer not found – AMS route was not found.";
                case 8:
                    return "Unknown command ID.";
                case 9:
                    return "Invalid task ID.";
                case 10:
                    return "No IO.";
                case 11:
                    return "Unknown AMS command.";
                case 12:
                    return "Win32 error.";
                case 13:
                    return "Port not connected.";
                case 14:
                    return "Invalid AMS length.";
                case 15:
                    return "Invalid AMS Net ID.";
                case 16:
                    return "Installation level is too low –TwinCAT 2 license error.";
                case 17:
                    return "No debugging available.";
                case 18:
                    return "Port disabled – TwinCAT system service not started.";
                case 19:
                    return "Port already connected.";
                case 20:
                    return "AMS Sync Win32 error.";
                case 21:
                    return "AMS Sync Timeout.";
                case 22:
                    return "AMS Sync error.";
                case 23:
                    return "No index map for AMS Sync available.";
                case 24:
                    return "Invalid AMS port.";
                case 25:
                    return "No memory.";
                case 26:
                    return "TCP send error.";
                case 27:
                    return "Host unreachable.";
                case 28:
                    return "Invalid AMS fragment.";
                case 29:
                    return "TLS send error – secure ADS connection failed.";
                case 30:
                    return "Access denied – secure ADS access denied.";
                case 1280:
                    return "Locked memory cannot be allocated.";
                case 1281:
                    return "The router memory size could not be changed.";
                case 1282:
                    return "The mailbox has reached the maximum number of possible messages.";
                case 1283:
                    return "The Debug mailbox has reached the maximum number of possible messages.";
                case 1284:
                    return "The port type is unknown.";
                case 1285:
                    return "The router is not initialized.";
                case 1286:
                    return "The port number is already assigned.";
                case 1287:
                    return "The port is not registered.";
                case 1288:
                    return "The maximum number of ports has been reached.";
                case 1289:
                    return "The port is invalid.";
                case 1290:
                    return "The router is not active.";
                case 1291:
                    return "The mailbox has reached the maximum number for fragmented messages.";
                case 1292:
                    return "A fragment timeout has occurred.";
                case 1293:
                    return "The port is removed.";
                case 1792:
                    return "General device error.";
                case 1793:
                    return "Service is not supported by the server.";
                case 1794:
                    return "Invalid index group.";
                case 1795:
                    return "Invalid index offset.";
                case 1796:
                    return "Reading or writing not permitted.";
                case 1797:
                    return "Parameter size not correct. Commonly found in batch processing, check the calculation command length";
                case 1798:
                    return "Invalid data values.";
                case 1799:
                    return "Device is not ready to operate. It is possible that the TSM configuration is incorrect, reactivate the configuration";
                case 1800:
                    return "Device Busy";
                case 1801:
                    return "Invalid operating system context. This can result from use of ADS blocks in different tasks. It may be possible to resolve this through multitasking synchronization in the PLC.";
                case 1802:
                    return "Insufficient memory.";
                case 1803:
                    return "Invalid parameter values.";
                case 1804:
                    return "Device Not Found";
                case 1805:
                    return "Device Syntax Error";
                case 1806:
                    return "Objects do not match.";
                case 1807:
                    return "Object already exists.";
                case 1808:
                    return "Symbol not found. Check whether the variable name is correct, Note: the global variables in some PLC equipment are: .[Variable Name]";
                case 1809:
                    return "Invalid symbol version. This can occur due to an online change. Create a new handle.";
                case 1810:
                    return "Device (server) is in invalid state.";
                case 1811:
                    return "AdsTransMode not supported.";
                case 1812:
                    return "Device Notify Handle Invalid";
                case 1813:
                    return "Notification client not registered.";
                case 1814:
                    return "Device No More Handles";
                case 1815:
                    return "Device Invalid Watch size";
                case 1816:
                    return "Device Not Initialized";
                case 1817:
                    return "Device TimeOut";
                case 1818:
                    return "Device No Interface";
                case 1819:
                    return "Device Invalid Interface";
                case 1820:
                    return "Device Invalid CLSID";
                case 1821:
                    return "Device Invalid Object ID";
                case 1822:
                    return "Device Request Is Pending";
                case 1823:
                    return "Device Request Is Aborted";
                case 1824:
                    return "Device Signal Warning";
                case 1825:
                    return "Device Invalid Array Index";
                case 1826:
                    return "Device Symbol Not Active";
                case 1827:
                    return "Device Access Denied";
                case 1828:
                    return "Device Missing License";
                case 1829:
                    return "Device License Expired";
                case 1830:
                    return "Device License Exceeded";
                case 1831:
                    return "Device License Invalid";
                case 1832:
                    return "Device License System Id";
                case 1833:
                    return "Device License No Time Limit";
                case 1834:
                    return "Device License Future Issue";
                case 1835:
                    return "Device License Time To Long";
                case 1836:
                    return "Device Exception During Startup";
                case 1837:
                    return "Device License Duplicated";
                case 1838:
                    return "Device Signature Invalid";
                case 1839:
                    return "Device Certificate Invalid";
                case 1840:
                    return "Device License Oem Not Found";
                case 1841:
                    return "Device License Restricted";
                case 1842:
                    return "Device License Demo Denied";
                case 1843:
                    return "Device Invalid Function Id";
                case 1844:
                    return "Device Out Of Range";
                case 1845:
                    return "Device Invalid Alignment";
                case 1846:
                    return "Device License Platform";
                case 1847:
                    return "Device Context Forward Passive Level";
                case 1848:
                    return "Device Context Forward Dispatch Level";
                case 1849:
                    return "Device Context Forward RealTime";
                case 1850:
                    return "Device Certificate Entrust";
                case 1856:
                    return "ClientError";
                case 1857:
                    return "Client Invalid Parameter";
                case 1858:
                    return "Client List Empty";
                case 1859:
                    return "Client Variable In Use";
                case 1860:
                    return "Client Duplicate InvokeID";
                case 1861:
                    return "Timeout has occurred – the remote terminal is not responding in the specified ADS timeout. The route setting of the remote terminal may be configured incorrectly.";
                case 1862:
                    return "ClientW32OR";
                case 1863:
                    return "Client Timeout Invalid";
                case 1864:
                    return "Client Port Not Open";
                case 1865:
                    return "Client No Ams Addr";
                case 1872:
                    return "Client Sync Internal";
                case 1873:
                    return "Client Add Hash";
                case 1874:
                    return "Client Remove Hash";
                case 1875:
                    return "Client No More Symbols";
                case 1876:
                    return "Client Response Invalid";
                case 1877:
                    return "Client Port Locked";
                case 32768:
                    return "ClientQueueFull";
                case 10060:
                    return "A connection timeout has occurred - error while establishing the connection, because the remote terminal did not respond properly after a certain period of time";
                case 10061:
                    return "WSA_ConnRefused";
                case 10065:
                    return "No route to host - a socket operation referred to an unavailable host.";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
