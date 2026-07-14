using System.Net.Http;

namespace IcomRigControl.Services;

/// <summary>
/// Result of a LoTW upload attempt.
/// </summary>
public record LotwUploadResult(bool Success, string? Message);

/// <summary>
/// Uploads signed ADIF logs to ARRL's Logbook of the World and downloads
/// QSOs confirmed since a given date. Signing is delegated to TQSL via
/// ITqslProcessRunner — this class never reimplements ARRL's certificate
/// or signing logic. See CLAUDE.md Phase 8d.
/// </summary>
public class LotwBridge
{
    private const string LotwUploadUrl = "https://lotw.arrl.org/lotwuser/upload";
    private const string LotwQueryUrl = "https://lotw.arrl.org/lotwuser/lotwreport.adi";

    private readonly ITqslProcessRunner _tqslRunner;
    private readonly HttpClient _httpClient;

    public LotwBridge(ITqslProcessRunner tqslRunner, HttpClient httpClient)
    {
        _tqslRunner = tqslRunner;
        _httpClient = httpClient;
    }

    /// Signs the given ADIF file with TQSL, then uploads the signed .tq8
    /// to LoTW. Never throws — failures at any step are returned as a
    /// non-success LotwUploadResult with an explanatory message.
    public async Task<LotwUploadResult> UploadAsync(string adifFilePath)
    {
        if (!File.Exists(adifFilePath))
        {
            return new LotwUploadResult(false, $"ADIF file not found: {adifFilePath}");
        }

        try
        {
            var signedPath = Path.Combine(
                Path.GetDirectoryName(adifFilePath) ?? Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(adifFilePath) + ".tq8");

            var signResult = await _tqslRunner.SignAdifFileAsync(adifFilePath, signedPath);
            if (!signResult.Success)
            {
                return new LotwUploadResult(false, signResult.Message ?? "TQSL signing failed.");
            }

            using var content = new MultipartFormDataContent();
            var fileBytes = await File.ReadAllBytesAsync(signedPath);
            var fileContent = new ByteArrayContent(fileBytes);
            content.Add(fileContent, "upfile", Path.GetFileName(signedPath));

            var response = await _httpClient.PostAsync(LotwUploadUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                return new LotwUploadResult(false, $"LoTW upload failed: HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync();
            return new LotwUploadResult(true, body);
        }
        catch (Exception ex)
        {
            // Never throw — network issues, file I/O errors, etc. are all
            // reported as a failed result per the never-crash pattern used
            // throughout this project's network-facing services.
            return new LotwUploadResult(false, ex.Message);
        }
    }

    /// Queries LoTW for QSOs confirmed since sinceDate, returning them as
    /// QsoRecords. Returns an empty list (never throws) on any failure.
    public async Task<List<QsoRecord>> DownloadConfirmedQsosAsync(DateTime sinceDate)
    {
        try
        {
            var dateParam = sinceDate.ToString("yyyy-MM-dd");
            var url = $"{LotwQueryUrl}?qso_query=1&qso_qsl=yes&qso_qslsince={dateParam}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new List<QsoRecord>();
            }

            var body = await response.Content.ReadAsStringAsync();
            return ParseAdifQsos(body);
        }
        catch
        {
            return new List<QsoRecord>();
        }
    }

    /// Minimal ADIF parser for LoTW's report format — extracts the fields
    /// this project's QsoRecord actually uses. LoTW's own ADIF dialect is
    /// well-formed per the ADIF spec, so simple tag scanning is sufficient
    /// here rather than needing a full ADIF parsing library.
    private static List<QsoRecord> ParseAdifQsos(string adifText)
    {
        var results = new List<QsoRecord>();

        // Skip the header (everything up to and including <APP_LoTW_EOH> or <EOH>)
        int headerEnd = adifText.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase);
        if (headerEnd < 0)
        {
            headerEnd = adifText.IndexOf("<APP_LoTW_EOH>", StringComparison.OrdinalIgnoreCase);
        }
        string body = headerEnd >= 0 ? adifText[(headerEnd)..] : adifText;

        var records = body.Split(new[] { "<eor>", "<EOR>" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var record in records)
        {
            string? call = ExtractField(record, "call");
            if (string.IsNullOrWhiteSpace(call)) continue;

            string band = ExtractField(record, "band") ?? "";
            string mode = ExtractField(record, "mode") ?? "";
            string dateStr = ExtractField(record, "qso_date") ?? "";
            string timeStr = ExtractField(record, "time_on") ?? "0000";

            DateTime qsoDate = DateTime.TryParseExact(dateStr, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsedDate)
                ? parsedDate
                : DateTime.UtcNow.Date;

            int hour = timeStr.Length >= 2 ? int.Parse(timeStr[..2]) : 0;
            int minute = timeStr.Length >= 4 ? int.Parse(timeStr[2..4]) : 0;
            var qsoDateTime = new DateTime(qsoDate.Year, qsoDate.Month, qsoDate.Day, hour, minute, 0, DateTimeKind.Utc);

            results.Add(new QsoRecord(
                Callsign: call.ToUpperInvariant(),
                FrequencyMHz: 0, // LoTW confirmation records don't carry frequency by default
                Band: band,
                Mode: mode,
                DateTimeUtc: qsoDateTime,
                RstSent: "",
                RstReceived: ""
            ));
        }

        return results;
    }

    private static string? ExtractField(string record, string fieldName)
    {
        // ADIF field format: <fieldname:length>value
        var tag = $"<{fieldName}:";
        int tagIndex = record.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
        if (tagIndex < 0) return null;

        int lengthStart = tagIndex + tag.Length;
        int closeAngle = record.IndexOf('>', lengthStart);
        if (closeAngle < 0) return null;

        if (!int.TryParse(record[lengthStart..closeAngle], out int length)) return null;

        int valueStart = closeAngle + 1;
        if (valueStart + length > record.Length) return null;

        return record.Substring(valueStart, length);
    }
}