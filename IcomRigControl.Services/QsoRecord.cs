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
    int? SerialNumberReceived = null
);