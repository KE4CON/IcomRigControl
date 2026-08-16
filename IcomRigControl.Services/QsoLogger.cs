using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Manages the in-memory log of contacts for the current session, auto-filling
/// frequency, mode, and band from the live Transceiver at the moment each QSO
/// is logged. Exports the full log to a standard ADIF file via AdifWriter.
///
/// If constructed with a log directory, writes each QSO through to a persistent
/// timestamped session file immediately as it's logged (not just on manual
/// export), so a crash does not lose the session. See CLAUDE.md's Core Design
/// Principle: this local log is the resilient backup of record, independent of
/// any external program's (HRD, N1MM, etc.) availability.
/// </summary>
public class QsoLogger
{
    private readonly Transceiver _transceiver;
    private readonly List<QsoRecord> _qsos = new();
    private readonly object _logLock = new();
    private bool _sessionFileHeaderWritten;

    /// A point-in-time snapshot of the logged QSOs. Returns a copy taken under
    /// the log lock so callers (UI binding, dupe checks, ADIF export) can never
    /// enumerate the list while another thread — e.g. the UDP contact listener —
    /// is appending to it.
    public IReadOnlyList<QsoRecord> Qsos
    {
        get { lock (_logLock) return _qsos.ToList(); }
    }

    /// The persistent session file path, or null if this logger was constructed
    /// without a log directory (in-memory only — used by existing tests/callers
    /// that don't need write-through persistence).
    public string? SessionFilePath { get; }

    /// Optional Band DVR (or similar) that saves each contact's audio. When set and
    /// monitoring, LogQso attaches a WAV clip to the QSO. See CLAUDE.md per-QSO audio.
    public IQsoAudioSource? AudioSource { get; set; }

    /// In-memory-only constructor (no write-through persistence).
    public QsoLogger(Transceiver transceiver)
    {
        _transceiver = transceiver;
        SessionFilePath = null;
    }

    /// Persistent constructor: creates a timestamped session file immediately
    /// in logDirectory, and writes each QSO through to it as LogQso is called.
    public QsoLogger(Transceiver transceiver, string logDirectory)
    {
        _transceiver = transceiver;

        Directory.CreateDirectory(logDirectory);
        var fileName = $"qsolog_{DateTime.Now:yyyyMMdd_HHmmss}.adi";
        SessionFilePath = Path.Combine(logDirectory, fileName);

        File.WriteAllText(SessionFilePath, AdifWriter.GenerateHeader());
        _sessionFileHeaderWritten = true;
    }

    /// Log a new QSO, auto-filling frequency/mode/band from the transceiver's
    /// current state at the moment of the call. If this logger was constructed
    /// with a log directory, also writes through to the session file immediately.
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
        int? serialNumberReceived = null,
        string? state = null)
    {
        // Attach a recording of the contact if the Band DVR is monitoring. Never let
        // an audio-save failure block the log write (backup-of-record discipline).
        string? audioFile = null;
        try { audioFile = AudioSource?.SaveQsoAudio(callsign); } catch { }

        var qso = new QsoRecord(
            Callsign: callsign.ToUpperInvariant(),
            FrequencyMHz: _transceiver.FrequencyHz / 1_000_000.0,
            Band: Bands.FromHz(_transceiver.FrequencyHz),
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
            SerialNumberReceived: serialNumberReceived,
            AudioFile: audioFile,
            State: string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant()
        );

        Commit(qso);

        return qso;
    }

    /// Adds a QsoRecord that was already fully constructed elsewhere (e.g. received
    /// over UDP from N1MM/WSJT-X/HRD via ContactUdpListener) directly into the log,
    /// without auto-filling frequency/mode from this instance's own Transceiver —
    /// the received record already carries its own correct frequency/mode/timestamp.
    public void LogReceivedQso(QsoRecord qso)
    {
        Commit(qso);
    }

    /// Commits a QSO to the log. The durable session file is the backup of
    /// record, so it is written FIRST; only if that append succeeds is the QSO
    /// added to the in-memory list. If the append throws (disk full, file locked
    /// by a cloud-sync client or antivirus), the exception surfaces to the caller
    /// and nothing is half-committed — the in-memory list and the durable file
    /// stay consistent, so the caller can retry without creating a phantom entry
    /// that lives only in RAM. The whole operation is serialized by _logLock so
    /// the UI thread and the UDP listener thread can log concurrently without
    /// corrupting the list or interleaving file writes.
    private void Commit(QsoRecord qso)
    {
        lock (_logLock)
        {
            if (SessionFilePath != null && _sessionFileHeaderWritten)
            {
                File.AppendAllText(SessionFilePath, AdifWriter.FormatQso(qso) + Environment.NewLine);
            }

            _qsos.Add(qso);
        }
    }

    public void ExportToAdif(string path)
    {
        lock (_logLock)
        {
            AdifWriter.WriteToFile(path, _qsos);
        }
    }

    public void ClearLog()
    {
        lock (_logLock)
        {
            _qsos.Clear();
        }
    }

    /// Map a frequency in Hz to its amateur radio band designation.
    /// Uses standard US/IARU band edges.
}