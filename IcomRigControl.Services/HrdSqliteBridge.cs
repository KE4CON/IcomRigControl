using Microsoft.Data.Sqlite;

namespace IcomRigControl.Services;

/// <summary>
/// Best-effort direct write into HRD Logbook's SQLite database, so QSOs
/// logged in IcomRigControl appear in HRD Logbook without a manual ADIF
/// export/import step. This is a bonus convenience layer on top of the
/// always-reliable ADIF export path (Phase 8e Layer 2) — never a
/// replacement for it. HRD's schema (table TABLE_HRD_CONTACTS_V01) is
/// reverse-engineered from community sources, not officially documented,
/// so every operation defensively checks the schema exists before writing
/// and never throws or corrupts HRD's live database. See CLAUDE.md Phase 8e.
/// </summary>
public class HrdSqliteBridge
{
    private const string ExpectedTableName = "TABLE_HRD_CONTACTS_V01";

    private readonly string _databasePath;

    public HrdSqliteBridge(string databasePath)
    {
        _databasePath = databasePath;
    }

    /// Checks whether the HRD database file exists and contains the
    /// expected contacts table, without writing anything. Used to decide
    /// whether to offer this bridge as an option at all.
    public bool IsAvailable()
    {
        try
        {
            if (!File.Exists(_databasePath)) return false;

            using var connection = new SqliteConnection($"Data Source={_databasePath};Mode=ReadOnly");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName";
            cmd.Parameters.AddWithValue("@tableName", ExpectedTableName);

            var result = cmd.ExecuteScalar();
            return result != null;
        }
        catch
        {
            // Any failure (locked file, corrupt database, permissions,
            // unexpected schema) means "not available" — never throw.
            return false;
        }
    }

    /// Writes a single QSO into HRD's database. Returns false (never
    /// throws) if the database or expected schema isn't found, or if
    /// the write fails for any reason — this bridge must never be the
    /// reason a QSO goes unrecorded (the local QsoLogger already has it
    /// per the Core Design Principle; this is purely a bonus convenience).
    public async Task<bool> WriteQsoAsync(QsoRecord qso)
    {
        if (!IsAvailable()) return false;

        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO {ExpectedTableName}
                    (col_call, col_time_on, col_qso_date, col_mode, col_band, col_freq)
                VALUES
                    (@call, @time_on, @qso_date, @mode, @band, @freq)";

            cmd.Parameters.AddWithValue("@call", qso.Callsign);
            cmd.Parameters.AddWithValue("@time_on", qso.DateTimeUtc.ToString("HHmm"));
            cmd.Parameters.AddWithValue("@qso_date", qso.DateTimeUtc.ToString("yyyyMMdd"));
            cmd.Parameters.AddWithValue("@mode", qso.Mode);
            cmd.Parameters.AddWithValue("@band", qso.Band);
            cmd.Parameters.AddWithValue("@freq", qso.FrequencyMHz.ToString("0.######"));

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            // Schema mismatch, locked database, disk error, etc. — never
            // throw or risk corrupting HRD's live data.
            return false;
        }
    }
}