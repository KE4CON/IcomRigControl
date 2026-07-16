using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class Ax25FrameBuilderTests
{
    [Fact]
    public void EncodeAddress_ShortCallsignNoSsid_PadsWithSpacesAndShiftsLeft()
    {
        // "APRS" with SSID 0, not the last address in the chain
        byte[] result = Ax25FrameBuilder.EncodeAddress("APRS", ssid: 0, isLastAddress: false);

        Assert.Equal(7, result.Length);
        // 'A' = 0x41, left-shifted by 1 = 0x82
        Assert.Equal(0x82, result[0]);
    }

    [Fact]
    public void EncodeAddress_SsidByte_HasCorrectBitsForNonLastAddress()
    {
        byte[] result = Ax25FrameBuilder.EncodeAddress("KE4CON", ssid: 1, isLastAddress: false);

        // SSID 1, not last: 0b011_00010 pattern region -- extension bit (bit 0) must be 0
        Assert.Equal(0, result[6] & 0x01);
    }

    [Fact]
    public void EncodeAddress_SsidByte_HasExtensionBitSetForLastAddress()
    {
        byte[] result = Ax25FrameBuilder.EncodeAddress("KE4CON", ssid: 1, isLastAddress: true);

        // Last address in the chain: extension bit (bit 0) must be 1
        Assert.Equal(1, result[6] & 0x01);
    }

    [Fact]
    public void EncodeAddress_CallsignLongerThan6Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Ax25FrameBuilder.EncodeAddress("TOOLONGCALL", ssid: 0, isLastAddress: true));
    }

    [Fact]
    public void EncodeAddress_SsidOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Ax25FrameBuilder.EncodeAddress("KE4CON", ssid: 16, isLastAddress: true));
    }

    [Fact]
    public void BuildUiFrame_ContainsDestinationThenSourceThenControlThenPid()
    {
        byte[] frame = Ax25FrameBuilder.BuildUiFrame(
            sourceCallsign: "KE4CON", sourceSsid: 1,
            destinationCallsign: "APRS", destinationSsid: 0,
            infoField: "!0000.00N/00000.00WrTest");

        // Destination address (7 bytes) + source address (7 bytes) = 14 bytes,
        // then Control (0x03) then PID (0xF0)
        Assert.Equal(0x03, frame[14]);
        Assert.Equal(0xF0, frame[15]);
    }

    [Fact]
    public void BuildUiFrame_InfoFieldAppearsUnshiftedAtEnd()
    {
        const string info = "!0000.00N/00000.00WrTest";
        byte[] frame = Ax25FrameBuilder.BuildUiFrame(
            sourceCallsign: "KE4CON", sourceSsid: 1,
            destinationCallsign: "APRS", destinationSsid: 0,
            infoField: info);

        // Info field starts right after Control+PID (byte 16 onward) and is
        // plain, un-shifted ASCII.
        var infoBytes = frame[16..];
        string decoded = System.Text.Encoding.ASCII.GetString(infoBytes);
        Assert.Equal(info, decoded);
    }

    [Fact]
    public void BuildUiFrame_DestinationAddressIsNotMarkedAsLast()
    {
        byte[] frame = Ax25FrameBuilder.BuildUiFrame(
            sourceCallsign: "KE4CON", sourceSsid: 1,
            destinationCallsign: "APRS", destinationSsid: 0,
            infoField: "test");

        // Destination is address byte index 6 (end of first 7-byte block);
        // extension bit must be 0 since source address follows.
        Assert.Equal(0, frame[6] & 0x01);
    }

    [Fact]
    public void BuildUiFrame_SourceAddressIsMarkedAsLast()
    {
        byte[] frame = Ax25FrameBuilder.BuildUiFrame(
            sourceCallsign: "KE4CON", sourceSsid: 1,
            destinationCallsign: "APRS", destinationSsid: 0,
            infoField: "test");

        // Source is address byte index 13 (end of second 7-byte block);
        // extension bit must be 1 since no digipeater path follows.
        Assert.Equal(1, frame[13] & 0x01);
    }
}