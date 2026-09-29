using FieldLink.Communication;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Turck
{
    /// <summary>Turck 리더의 길이·시그니처·중간 ACK 판정입니다.</summary>
    public class TurckReaderFrameRules : ProtocolFrameRules
    {
        /// <inheritdoc />
        public override int HeaderLength => 3;
        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request) => header[0] == 0xAA;
        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request) => header[2] > 3 ? header[2] - 3 : 0;
        /// <inheritdoc />
        protected override ResponseDisposition ClassifyResponseCore(byte[] request, byte[] response)
        {
            if (!IsHeaderValid(response) || response.Length < response[2])
                return ResponseDisposition.Reject;
            if (response[1] == 7 && response[2] == 7)
            {
                byte command = response[3];
                if (command == 0x68 || command == 0x70 ||
                    (command == 0x69 && (response[4] == 0x89 || response[4] == 0x81)))
                    return ResponseDisposition.Ignore;
            }
            return ResponseDisposition.Accept;
        }
    }
}
