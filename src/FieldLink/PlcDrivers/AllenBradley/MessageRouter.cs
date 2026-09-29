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
    /// <summary>사용자 정의 메시지 라우팅 클래스, CIP 프로토콜 사용자 정의 라우팅 메시지를 구현할 수 있는</summary>
    public class MessageRouter
    {
        /// <summary>기본 인스턴스 객체를 인스턴스화합니다.</summary>
        public MessageRouter()
        {
            _router[0] = 0x01;
            new byte[]
            {
                0x0f,
                0x02,
                0x12,
                0x01
            }.CopyTo(_router, 1);
            _router[5] = 0x0c;
        }

        /// <summary>파이를 지정하여 객체를 인스턴스화하고, 문자열을 사용하여 표현하는 방법</summary>
        /// <param name = "router">경로 정보</param>
        /// <remarks>로이 메시지는 두 가지 형식을 지원합니다. 형식 1: 1.15.2.18.1.12 형식 2: 1.1.2.130.133.139.61.1.0<br/>
        /// 채널 메시지는 두 가지 형식을 지원합니다. 형식 1: 1.15.2.18.1.12, 형식 2: 1.1.2.130.133.139.61.1.0</remarks>
        public MessageRouter(string router)
        {
            string[] splits = router.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length <= 6)
            {
                if (splits.Length > 0)
                    _router[0] = byte.Parse(splits[0]);
                if (splits.Length > 1)
                    _router[1] = byte.Parse(splits[1]);
                if (splits.Length > 2)
                    _router[2] = byte.Parse(splits[2]);
                if (splits.Length > 3)
                    _router[3] = byte.Parse(splits[3]);
                if (splits.Length > 4)
                    _router[4] = byte.Parse(splits[4]);
                if (splits.Length > 5)
                    _router[5] = byte.Parse(splits[5]);
            }
            else if (splits.Length == 9)
            {
                string ip = splits[3] + "." + splits[4] + "." + splits[5] + "." + splits[6];
                _router = new byte[6 + ip.Length];
                _router[0] = byte.Parse(splits[0]);
                _router[1] = byte.Parse(splits[1]);
                _router[2] = (byte)(0x10 + byte.Parse(splits[2]));
                _router[3] = (byte)ip.Length;
                Encoding.ASCII.GetBytes(ip).CopyTo(_router, 4);
                _router[_router.Length - 2] = byte.Parse(splits[7]);
                _router[_router.Length - 1] = byte.Parse(splits[8]);
            }
        }

        /// <summary>완전히 사용자 정의 된 프레임 경로를 사용하여 초기화 데이터</summary>
        /// <param name = "router">완전히 사용자 정의된 라우팅 메시지</param>
        public MessageRouter(byte[] router)
        {
            _router = router;
        }

        /// <summary>경로 정보를 얻으십시오</summary>
        /// <returns>라우팅 메시지의 바이트 정보</returns>
        public byte[] GetRouter() => _router;
        /// <summary>전송되는 CIP 라우팅 메시지를 가져오기</summary>
        /// <returns>경로 정보</returns>
        public byte[] GetRouterCIP()
        {
            byte[] router = this.GetRouter();
            if (router.Length % 2 == 1)
                router = ProtocolBytes.SpliceArray(router, new byte[] { 0x00 }); // 홀수 길이의 경우 0 연산
            byte[] routerCip = new byte[46 + router.Length];
            "54022006240105f70200 00800100fe8002001b05 28a7fd03020000008084 1e00f44380841e00f443 a305".ToHexBytes().CopyTo(routerCip, 0);
            router.CopyTo(routerCip, 42);
            "20022401".ToHexBytes().CopyTo(routerCip, 42 + router.Length);
            routerCip[41] = (byte)(router.Length / 2);
            return routerCip;
        }

        /// <summary>뒷면 정보</summary>
        public byte Backplane { get => _router[0]; set => _router[0] = value; }
        /// <summary>슬롯 번호 정보</summary>
        public byte Slot { get => _router[5]; set => _router[5] = value; }

        /// <summary>_router 프로토콜 값입니다.</summary>
        private byte[] _router = new byte[6];
    }
}
