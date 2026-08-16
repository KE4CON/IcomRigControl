namespace IcomRigControl.Services;

/// <summary>
/// A single logged contact (QSO), covering both general logging and contest
/// fields. Optional fields are null when not applicable/not entered.
/// </summary>
public record QsoRecord(
    string Callsign,
    double FrequencyMHz,
    string Band,
    string Mode,
    DateTime DateTimeUtc,
    string RstSent,
    string RstReceived,
    string? Name = null,
    string? GridSquare = null,
    string? Notes = null,
    string? ContestExchangeSent = null,
    string? ContestExchangeReceived = null,
    int? SerialNumberSent = null,
    int? SerialNumberReceived = null,
    // App-local path to a WAV recording of this contact's audio, when the Band DVR
    // was monitoring at log time. Not part of ADIF (it's a local convenience).
    string? AudioFile = null,
    // US state (2-letter) for Worked All States tracking; null/empty if not a US QSO.
    string? State = null
)
{
    /// True when a per-QSO audio clip is attached — drives the log's play button.
    public bool HasAudio => !string.IsNullOrEmpty(AudioFile);
}