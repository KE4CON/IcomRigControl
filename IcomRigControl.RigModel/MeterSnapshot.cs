namespace IcomRigControl.RigModel;

/// <summary>
/// A single point-in-time snapshot of all polled meter values.
/// This is the record that flows to ActivityLogger, EmmcomBridge, and AprsBridge.
/// </summary>
public record MeterSnapshot(
    DateTimeOffset Timestamp,
    long   FrequencyHz,
    string Mode,
    double SMeterDbm,
    int    SMeterS,
    double RfPowerPercent,
    double SwrRatio,
    double AlcLevel,
    double SupplyVoltage,
    double CurrentDraw
);
