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

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>베이포 PLC의 명령 코드</summary>
    public class BeckhoffCommandId
    {
        /// <summary>PLC의 이름과 버전 번호와 같은 정보를 읽습니다.</summary>
        public const ushort ReadDeviceInfo = 0x0001;
        /// <summary>ADS를 읽을 수 있는 장치에서 데이터를 읽습니다.</summary>
        public const ushort Read = 0x0002;
        /// <summary>ADS 데이터를 ADS의 장치에 입력합니다.</summary>
        public const ushort Write = 0x0003;
        /// <summary>ADS 장치의 장치 상태 정보와 ADS 상태를 읽습니다.</summary>
        public const ushort ReadState = 0x0004;
        /// <summary>ADS 장치의 장치 상태 정보와 ADS 상태를 변경합니다.</summary>
        public const ushort WriteControl = 0x0005;
        /// <summary>ADS 장치에서 알림 객체를 생성합니다.</summary>
        public const ushort AddDeviceNotification = 0x0006;
        /// <summary>ADS 장치에서 알림 객체를 삭제합니다.</summary>
        public const ushort DeleteDeviceNotification = 0x0007;
        /// <summary>ADS 장치에서 데이터에 대한 알림이 클라이언트에게 전송됩니다.</summary>
        public const ushort DeviceNotification = 0x0008;
        /// <summary>쓰면서 동시에 읽는다.</summary>
        public const ushort ReadWrite = 0x0009;
    }
}
