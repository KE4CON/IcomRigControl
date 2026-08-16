namespace IcomRigControl.Services;

/// <summary>
/// Maps a frequency to its amateur band name (e.g. "20M"). Shared so the logger and
/// the DX-cluster "needed spot" analyzer classify bands identically. See CLAUDE.md.
/// </summary>
public static class Bands
{
    public static string FromHz(long hz)
    {
        double mhz = hz / 1_000_000.0;
        return mhz switch
        {
            >= 1.8 and < 2.0 => "160M",
            >= 3.5 and < 4.0 => "80M",
            >= 5.3 and < 5.5 => "60M",
            >= 7.0 and < 7.3 => "40M",
            >= 10.1 and < 10.15 => "30M",
            >= 14.0 and < 14.35 => "20M",
            >= 18.068 and < 18.168 => "17M",
            >= 21.0 and < 21.45 => "15M",
            >= 24.89 and < 24.99 => "12M",
            >= 28.0 and < 29.7 => "10M",
            >= 50.0 and < 54.0 => "6M",
            >= 144.0 and < 148.0 => "2M",
            >= 420.0 and < 450.0 => "70CM",
            _ => "UNKNOWN"
        };
    }
}
