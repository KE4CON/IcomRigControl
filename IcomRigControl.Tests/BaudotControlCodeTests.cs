using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class BaudotControlCodeTests
{
    [Fact]
    public void CrAndLf_MatchIta2_MsbFirst()
    {
        // ITA2, written MSB-first (unit 5 → unit 1): CR is unit-4 mark = 01000,
        // LF is unit-2 mark = 00010. These were previously swapped, which put a
        // stray blank line / no line break in decoded and transmitted RTTY.
        Assert.Equal("01000", BaudotCode.Cr);
        Assert.Equal("00010", BaudotCode.Lf);
        Assert.NotEqual(BaudotCode.Cr, BaudotCode.Lf);
    }

    [Fact]
    public void Newline_EncodesAsCrThenLf()
    {
        var codes = BaudotCode.Encode("\n");
        Assert.Equal(new[] { BaudotCode.Cr, BaudotCode.Lf }, codes);
    }

    [Fact]
    public void LettersAndFigures_RoundTripThroughEncodeDecode()
    {
        // "R" is a letter; decoding its bits in LETTERS state returns 'R'.
        var codes = BaudotCode.Encode("R");
        Assert.Single(codes);
        Assert.Equal('R', BaudotCode.Decode(codes[0], figures: false));

        // "5" is a figure; encoding inserts a FIGS shift, then the T/5 pattern.
        var five = BaudotCode.Encode("5");
        Assert.Equal(BaudotCode.Figs, five[0]);
        Assert.Equal('5', BaudotCode.Decode(five[1], figures: true));
    }
}
