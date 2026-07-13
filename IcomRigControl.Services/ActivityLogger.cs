using System.Globalization;
using System.Text;
using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Logs a CSV row for every MeterUpdated event fired by a Transceiver while
/// logging is active. Each Start() call creates a new timestamped file;
/// Stop() unsubscribes and closes out the session.
/// </summary>
public class ActivityLogger
{
    private readonly Transceiver _transceiver;
    private readonly string _logDirectory;

    public bool IsLogging { get; private set; }
    public string? CurrentFilePath { get; private set; }

    public ActivityLogger(Transceiver transceiver, string logDirectory)
    {
        _transceiver = transceiver;
        _logDirectory = logDirectory;
    }

    public void Start()
    {
        if (IsLogging) return;

        Directory.CreateDirectory(_logDirectory);

        var fileName = $"activity_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        CurrentFilePath = Path.Combine(_logDirectory, fileName);

        WriteHeader();

        _transceiver.MeterUpdated += OnMeterUpdated;
        IsLogging = true;
    }

    public void Stop()
    {
        if (!IsLogging) return;

        _transceiver.MeterUpdated -= OnMeterUpdated;
        IsLogging = false;
    }

    private void WriteHeader()
    {
        var header = "Timestamp,FrequencyHz,Mode,SMeterS,SMeterDbm,RfPowerPercent,SwrRatio,AlcLevel,SupplyVoltage,CurrentDraw";
        File.WriteAllText(CurrentFilePath!, header + Environment.NewLine);
    }

    private void OnMeterUpdated(object? sender, MeterSnapshot snapshot)
    {
        if (CurrentFilePath == null) return;

        var row = new StringBuilder();
        row.Append(snapshot.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append(',');
        row.Append(snapshot.FrequencyHz).Append(',');
        row.Append(snapshot.Mode).Append(',');
        row.Append(snapshot.SMeterS).Append(',');
        row.Append(snapshot.SMeterDbm.ToString("F1", CultureInfo.InvariantCulture)).Append(',');
        row.Append(snapshot.RfPowerPercent.ToString("F1", CultureInfo.InvariantCulture)).Append(',');
        row.Append(snapshot.SwrRatio.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
        row.Append(snapshot.AlcLevel.ToString("F1", CultureInfo.InvariantCulture)).Append(',');
        row.Append(snapshot.SupplyVoltage.ToString("F1", CultureInfo.InvariantCulture)).Append(',');
        row.Append(snapshot.CurrentDraw.ToString("F1", CultureInfo.InvariantCulture));

        File.AppendAllText(CurrentFilePath, row + Environment.NewLine);
    }
}