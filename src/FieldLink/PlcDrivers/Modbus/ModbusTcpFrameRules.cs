using FieldLink.PlcDrivers.Common;
using System.IO;
using FieldLink.Communication;

namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>Modbus/TCP MBAP과 기능 코드를 포함한 8바이트 헤더의 프레임 규칙입니다.</summary>
    public class ModbusTcpFrameRules : ProtocolFrameRules
    {
        /// <summary>응답의 트랜잭션 식별자 검사 여부를 고정합니다.</summary>
        public ModbusTcpFrameRules(bool validateTransactionId = true)
        {
            ValidateTransactionId = validateTransactionId;
        }

        /// <summary>요청과 응답의 트랜잭션 식별자가 일치해야 하는지 나타냅니다.</summary>
        public bool ValidateTransactionId { get; }
        /// <inheritdoc />
        public override int HeaderLength => 8;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            int length = (header[4] << 8) | header[5];
            // Length includes Unit Identifier and PDU. Maximum PDU is 253 bytes.
            if (length < 2 || length > 254)
                throw new InvalidDataException("Modbus/TCP Length must be between 2 and 254 bytes.");
            return length - 2;
        }

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request) =>
            header[2] == 0 && header[3] == 0 &&
            ((header[4] << 8) | header[5]) >= 2 && ((header[4] << 8) | header[5]) <= 254;

        /// <inheritdoc />
        protected override ResponseDisposition ClassifyResponseCore(byte[] request, byte[] response)
        {
            if (request.Length < HeaderLength || !IsHeaderValid(response))
                return ResponseDisposition.Reject;
            if (!ValidateTransactionId || (request[0] == response[0] && request[1] == response[1]))
                return ResponseDisposition.Accept;
            return ResponseDisposition.Ignore;
        }

        /// <inheritdoc />
        protected override int? ReadSequenceId(byte[] header) => (header[0] << 8) | header[1];
    }
}
