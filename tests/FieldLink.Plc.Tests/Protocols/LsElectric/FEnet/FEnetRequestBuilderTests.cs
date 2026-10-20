using System;
using System.Text;
using FieldLink.Plc.Protocols.LsElectric.FEnet;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.LsElectric.FEnet;

public sealed class FEnetRequestBuilderTests
{
    [Fact]
    public void BuildContinuousRead_MatchesSpecVectorT1()
    {
        // Vector T1: %DW100에서 4바이트(2워드) 읽기, Invoke ID = 0x0001
        byte[] frame = FEnetRequestBuilder.BuildContinuousRead(1, "%DW100", 4, cpuInfo: 0xA0);

        Assert.Equal(38, frame.Length); // 20 header + 18 instruction

        // Company ID
        Assert.Equal("LSIS-XGT", Encoding.ASCII.GetString(frame, 0, 8));

        // CPU Info
        Assert.Equal(0xA0, frame[12]);

        // Source: 0x33
        Assert.Equal(0x33, frame[13]);

        // Invoke ID: 0x0001
        Assert.Equal(0x01, frame[14]);
        Assert.Equal(0x00, frame[15]);

        // Instruction Length: 18 (0x0012)
        Assert.Equal(0x12, frame[16]);
        Assert.Equal(0x00, frame[17]);

        // BCC verification
        byte bcc = 0;
        for (int i = 0; i < 19; i++)
        {
            bcc = (byte)(bcc + frame[i]);
        }

        Assert.Equal(frame[19], bcc);

        // Command: 0x0054
        Assert.Equal(0x54, frame[20]);
        Assert.Equal(0x00, frame[21]);

        // Data Type: 0x0014
        Assert.Equal(0x14, frame[22]);
        Assert.Equal(0x00, frame[23]);

        // Blocks: 0x0001
        Assert.Equal(0x01, frame[26]);
        Assert.Equal(0x00, frame[27]);

        // Variable length: 6
        Assert.Equal(0x06, frame[28]);
        Assert.Equal(0x00, frame[29]);

        // Variable: "%DW100"
        Assert.Equal("%DW100", Encoding.ASCII.GetString(frame, 30, 6));

        // Read byte count: 4
        Assert.Equal(0x04, frame[36]);
        Assert.Equal(0x00, frame[37]);
    }

    [Fact]
    public void BuildContinuousWrite_CreatesValidFrame()
    {
        byte[] dataToWrite = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        byte[] frame = FEnetRequestBuilder.BuildContinuousWrite(2, "%MW50", dataToWrite);

        Assert.Equal(20 + 12 + 5 + 4, frame.Length); // 20 header + 12 + 5 (len of "%MW50") + 4 bytes

        // Command: 0x0058
        Assert.Equal(0x58, frame[20]);
        Assert.Equal(0x00, frame[21]);

        // Data Type: 0x0014
        Assert.Equal(0x14, frame[22]);

        // Variable length: 5
        Assert.Equal(0x05, frame[28]);
        Assert.Equal("%MW50", Encoding.ASCII.GetString(frame, 30, 5));

        // Write byte count: 4
        Assert.Equal(0x04, frame[35]);
        Assert.Equal(0x00, frame[36]);

        // Payload
        Assert.Equal(dataToWrite, new ReadOnlySpan<byte>(frame, 37, 4).ToArray());
    }
}
