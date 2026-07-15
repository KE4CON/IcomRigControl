using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

public partial class QsoLoggerViewModel : ViewModelBase
{
    private readonly QsoLogger _qsoLogger;
    private readonly ICallsignLookupSource? _lookupSource;

    public ObservableCollection<QsoRecord> Qsos { get; } = new();

    [ObservableProperty]
    private string _callsignInput = "";

    [ObservableProperty]
    private string _rstSentInput = "59";

    [ObservableProperty]
    private string _rstReceivedInput = "59";

    [ObservableProperty]
    private string _nameInput = "";

    [ObservableProperty]
    private string _gridSquareInput = "";

    [ObservableProperty]
    private string _notesInput = "";

    [ObservableProperty]
    private string _lookupStatus = "";

    [ObservableProperty]
    private string _logStatus = "";

    public QsoLoggerViewModel(QsoLogger qsoLogger, ICallsignLookupSource? lookupSource)
    {
        _qsoLogger = qsoLogger;
        _lookupSource = lookupSource;

        foreach (var qso in _qsoLogger.Qsos)
        {
            Qsos.Add(qso);
        }
    }

    [RelayCommand]
    private async Task LookupCallsign()
    {
        if (string.IsNullOrWhiteSpace(CallsignInput))
        {
            LookupStatus = "Enter a callsign first.";
            return;
        }

        if (_lookupSource == null)
        {
            LookupStatus = "No lookup source configured (see Settings).";
            return;
        }

        LookupStatus = "Looking up...";
        var result = await _lookupSource.LookupAsync(CallsignInput);

        if (result == null)
        {
            LookupStatus = "Not found or lookup unavailable.";
            return;
        }

        NameInput = result.Name ?? NameInput;
        GridSquareInput = result.GridSquare ?? GridSquareInput;
        LookupStatus = $"Found via {_lookupSource.SourceName}.";
    }

    [RelayCommand]
    private void LogQso()
    {
        if (string.IsNullOrWhiteSpace(CallsignInput))
        {
            LogStatus = "Enter a callsign first.";
            return;
        }

        try
        {
            var qso = _qsoLogger.LogQso(
                CallsignInput,
                RstSentInput,
                RstReceivedInput,
                string.IsNullOrWhiteSpace(NameInput) ? null : NameInput,
                string.IsNullOrWhiteSpace(GridSquareInput) ? null : GridSquareInput,
                string.IsNullOrWhiteSpace(NotesInput) ? null : NotesInput);

            Qsos.Insert(0, qso);
            LogStatus = $"Logged {qso.Callsign} at {qso.DateTimeUtc:HH:mm} UTC.";

            // Clear entry fields for the next QSO, but keep RST defaults.
            CallsignInput = "";
            NameInput = "";
            GridSquareInput = "";
            NotesInput = "";
            LookupStatus = "";
        }
        catch (Exception ex)
        {
            LogStatus = $"Error logging QSO: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportAdif()
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "IcomRigControl", "Logs", $"export_{DateTime.Now:yyyyMMdd_HHmmss}.adi");

            _qsoLogger.ExportToAdif(path);
            LogStatus = $"Exported to {System.IO.Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            LogStatus = $"Export error: {ex.Message}";
        }
    }
}