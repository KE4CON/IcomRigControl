using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

public partial class QsoLoggerViewModel : ViewModelBase
{
    private readonly QsoLogger _qsoLogger;
    private readonly ICallsignLookupSource? _lookupSource;
    private readonly LotwBridge? _lotwBridge;

    public ObservableCollection<QsoRecord> Qsos { get; } = new();

    /// Callsigns confirmed via LoTW download-and-match (Phase 8d). A QSO's
    /// callsign appearing here means at least one contact with that station
    /// has a matching LoTW confirmation — checked by the UI to show a
    /// confirmed indicator next to matching rows.
    public ObservableCollection<string> ConfirmedCallsigns { get; } = new();

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

    [ObservableProperty]
    private string _lotwStatus = "";

    [ObservableProperty]
    private bool _lotwOperationInProgress;

    /// Contest selection (added alongside RTTY Roundup — previously this was
    /// hardcoded to Field Day). Changing this while contest mode is active
    /// switches which contest's rules (exchange labels, scoring, dupe check)
    /// are used for the rest of the session.
    [ObservableProperty]
    private ContestDefinition _selectedContest = ContestCatalog.FieldDay;

    public List<ContestDefinition> AvailableContests { get; } = new()
    {
        ContestCatalog.FieldDay,
        ContestCatalog.RttyRoundup
    };

    private int _nextSerialNumber = 1;

    public QsoLoggerViewModel(QsoLogger qsoLogger, ICallsignLookupSource? lookupSource, LotwBridge? lotwBridge = null)
    {
        _qsoLogger = qsoLogger;
        _lookupSource = lookupSource;
        _lotwBridge = lotwBridge;

        foreach (var qso in _qsoLogger.Qsos)
        {
            Qsos.Add(qso);
        }

        UpdateScoreDisplay();

        if (_lotwBridge == null)
        {
            LotwStatus = "LoTW not configured (see Settings)";
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
            if (IsContestMode && SelectedContest.IsDuplicate(_qsoLogger.Qsos, CallsignInput, GetCurrentBand(), GetCurrentMode()))
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

    [RelayCommand]
    private async Task UploadToLotw()
    {
        if (_lotwBridge == null)
        {
            LotwStatus = "LoTW not configured (see Settings — set TQSL path).";
            return;
        }

        if (Qsos.Count == 0)
        {
            LotwStatus = "No QSOs to upload.";
            return;
        }

        LotwOperationInProgress = true;
        LotwStatus = "Signing and uploading...";

        try
        {
            var tempAdif = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"lotw_upload_{DateTime.Now:yyyyMMdd_HHmmss}.adi");

            _qsoLogger.ExportToAdif(tempAdif);

            var result = await _lotwBridge.UploadAsync(tempAdif);

            LotwStatus = result.Success
                ? $"Upload successful ({Qsos.Count} QSOs)."
                : $"Upload failed: {result.Message}";
        }
        catch (Exception ex)
        {
            LotwStatus = $"Upload error: {ex.Message}";
        }
        finally
        {
            LotwOperationInProgress = false;
        }
    }

    [RelayCommand]
    private async Task DownloadFromLotw()
    {
        if (_lotwBridge == null)
        {
            LotwStatus = "LoTW not configured (see Settings — set TQSL path).";
            return;
        }

        LotwOperationInProgress = true;
        LotwStatus = "Checking for confirmations...";

        try
        {
            // Check confirmations from the last 90 days by default — a
            // reasonable window for "recent activity" without re-fetching
            // a station's entire multi-year history every time.
            var sinceDate = DateTime.UtcNow.AddDays(-90);
            var confirmed = await _lotwBridge.DownloadConfirmedQsosAsync(sinceDate);

            int matchCount = 0;
            foreach (var confirmedQso in confirmed)
            {
                bool hasLocalMatch = Qsos.Any(q =>
                    q.Callsign.Equals(confirmedQso.Callsign, StringComparison.OrdinalIgnoreCase) &&
                    q.Band.Equals(confirmedQso.Band, StringComparison.OrdinalIgnoreCase) &&
                    q.DateTimeUtc.Date == confirmedQso.DateTimeUtc.Date);

                if (hasLocalMatch && !ConfirmedCallsigns.Contains(confirmedQso.Callsign))
                {
                    ConfirmedCallsigns.Add(confirmedQso.Callsign);
                    matchCount++;
                }
            }

            LotwStatus = $"Checked {confirmed.Count} LoTW confirmations, matched {matchCount} against local log.";
        }
        catch (Exception ex)
        {
            LotwStatus = $"Download error: {ex.Message}";
        }
        finally
        {
            LotwOperationInProgress = false;
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

    partial void OnSelectedContestChanged(ContestDefinition value)
    {
        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        if (!IsContestMode)
        {
            ContestScoreDisplay = "";
            return;
        }

        var result = ContestScoreCalculator.CalculateScore(SelectedContest, _qsoLogger.Qsos);
        ContestScoreDisplay = $"{SelectedContest.Name}: {result.QsoCount} QSOs, {result.TotalPoints} points, {result.SectionsWorked.Count} sections";
    }

    // Best-effort current band/mode for dupe checking, based on the most
    // recently logged QSO's own captured values (QsoLogger auto-fills from
    // the live radio at log time, so we don't have direct radio access here).
    private string GetCurrentBand() => _qsoLogger.Qsos.Count > 0 ? _qsoLogger.Qsos[^1].Band : "";
    private string GetCurrentMode() => _qsoLogger.Qsos.Count > 0 ? _qsoLogger.Qsos[^1].Mode : "";
}