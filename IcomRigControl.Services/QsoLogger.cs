using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Manages the in-memory log of contacts for the current session, auto-filling
/// frequency, mode, and band from the live Transceiver at the moment each QSO
/// is logged. Exports the full log to a standard ADIF file via AdifWriter.
/// </summary>
public class QsoLogger
{
    private readonly Transceiver _transceiver;
    private readonly List<QsoRecord> _qsos = new();

    public IReadOnlyList<QsoRecord> Qsos => _qsos;

    public QsoLogger(Transceiver transceiver)
    {
        _transceiver = transceiver;
    }

    /// Log a new QSO, auto-filling frequency/mode/band from the transceiver's
    /// current state at the moment of the call.
    public QsoRecord LogQso(
        string callsign,
        string rstSent,
        string rstReceived,
        string? name = null,
        string? gridSquare = null,
        string? notes = null,
        string? contestExchangeSent = null,
        string? contestExchangeReceived = null,
        int? serialNumberSent = null,
        int? serialNumberReceived = null)
    {
        var qso = new QsoRecord(
            Callsign: callsign.ToUpperInvariant(),
            FrequencyMHz: _transceiver.FrequencyHz / 1_000_000.0,
            Band: FrequencyToBand(_transceiver.FrequencyHz),
            Mode: _transceiver.Mode,
            DateTimeUtc: DateTime.UtcNow,
            RstSent: rstSent,
            RstReceived: rstReceived,
            Name: name,
            GridSquare: gridSquare,
            Notes: notes,
            ContestExchangeSent: contestExchangeSent,
            ContestExchangeReceived: contestExchangeReceived,
            SerialNumberSent: serialNumberSent,
            SerialNumberReceived: serialNumberReceived
        );

        _qsos.Add(qso);
        return qso;
    }

    public void ExportToAdif(string path)
    {
        AdifWriter.WriteToFile(path, _qsos);
    }

    public void ClearLog()
    {
        _qsos.Clear();
    }

    /// Map a frequency in Hz to its amateur radio band designation.
    /// Uses standard US/IARU band edges.
    private static string FrequencyToBand(long hz)
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