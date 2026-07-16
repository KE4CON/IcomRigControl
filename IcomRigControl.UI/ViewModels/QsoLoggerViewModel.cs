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

    [ObservableProperty]
    private bool _isContestMode;

    [ObservableProperty]
    private string _contestExchangeSentInput = "";

    [ObservableProperty]
    private string _contestExchangeReceivedInput = "";

    [ObservableProperty]
    private string _serialNumberSentInput = "";

    [ObservableProperty]
    private string _serialNumberReceivedInput = "";

    [ObservableProperty]
    private string _contestScoreDisplay = "";

    private readonly ContestDefinition _activeContest = ContestCatalog.FieldDay;
    private int _nextSerialNumber = 1;

    public QsoLoggerViewModel(QsoLogger qsoLogger, ICallsignLookupSource? lookupSource)
    {
        _qsoLogger = qsoLogger;
        _lookupSource = lookupSource;

        foreach (var qso in _qsoLogger.Qsos)
        {
            Qsos.Add(qso);
        }

        UpdateScoreDisplay();
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
            if (IsContestMode && _activeContest.IsDuplicate(_qsoLogger.Qsos, CallsignInput, GetCurrentBand(), GetCurrentMode()))
            {
                LogStatus = $"DUPE: {CallsignInput.ToUpperInvariant()} already worked on this band.";
                return;
            }

            int? serialSent = IsContestMode && int.TryParse(SerialNumberSentInput, out int ss) ? ss : null;
            int? serialReceived = IsContestMode && int.TryParse(SerialNumberReceivedInput, out int sr) ? sr : null;

            var qso = _qsoLogger.LogQso(
                CallsignInput,
                RstSentInput,
                RstReceivedInput,
                string.IsNullOrWhiteSpace(NameInput) ? null : NameInput,
                string.IsNullOrWhiteSpace(GridSquareInput) ? null : GridSquareInput,
                string.IsNullOrWhiteSpace(NotesInput) ? null : NotesInput,
                IsContestMode && !string.IsNullOrWhiteSpace(ContestExchangeSentInput) ? ContestExchangeSentInput : null,
                IsContestMode && !string.IsNullOrWhiteSpace(ContestExchangeReceivedInput) ? ContestExchangeReceivedInput : null,
                serialSent,
                serialReceived);

            Qsos.Insert(0, qso);
            LogStatus = $"Logged {qso.Callsign} at {qso.DateTimeUtc:HH:mm} UTC.";

            CallsignInput = "";
            NameInput = "";
            GridSquareInput = "";
            NotesInput = "";
            ContestExchangeReceivedInput = "";
            LookupStatus = "";

            if (IsContestMode)
            {
                _nextSerialNumber++;
                SerialNumberSentInput = _nextSerialNumber.ToString();
                UpdateScoreDisplay();
            }
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

    partial void OnIsContestModeChanged(bool value)
    {
        if (value)
        {
            SerialNumberSentInput = _nextSerialNumber.ToString();
            RstSentInput = "59";
            UpdateScoreDisplay();
        }
    }

    private void UpdateScoreDisplay()
    {
        if (!IsContestMode)
        {
            ContestScoreDisplay = "";
            return;
        }

        var result = ContestScoreCalculator.CalculateScore(_activeContest, _qsoLogger.Qsos);
        ContestScoreDisplay = $"{_activeContest.Name}: {result.QsoCount} QSOs, {result.TotalPoints} points, {result.SectionsWorked.Count} sections";
    }

    // Best-effort current band/mode for dupe checking, based on the most
    // recently logged QSO's own captured values (QsoLogger auto-fills from
    // the live radio at log time, so we don't have direct radio access here).
    private string GetCurrentBand() => _qsoLogger.Qsos.Count > 0 ? _qsoLogger.Qsos[^1].Band : "";
    private string GetCurrentMode() => _qsoLogger.Qsos.Count > 0 ? _qsoLogger.Qsos[^1].Mode : "";
}