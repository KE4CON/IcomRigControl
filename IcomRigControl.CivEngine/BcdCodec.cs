namespace IcomRigControl.CivEngine;

public static class BcdCodec
{
    public static byte[] EncodeFrequency(long frequencyHz)
    {
        var result = new byte[5];
        long remaining = frequencyHz;
        for (int i = 0; i < 5; i++)
        {
            int lo = (int)(remaining % 10); remaining /= 10;
            int hi = (int)(remaining % 10); remaining /= 10;
            result[i] = (byte)((hi << 4) | lo);
        }
        return result;
    }

    public static long DecodeFrequency(ReadOnlySpan<byte> bcd)
    {
        long freq = 0;
        long multiplier = 1;
        for (int i = 0; i < 5; i++)
        {
            freq += (bcd[i] & 0x0F) * multiplier;
            multiplier *= 10;
            freq += ((bcd[i] >> 4) & 0x0F) * multiplier;
            multiplier *= 10;
        }
        return freq;
    }
}
