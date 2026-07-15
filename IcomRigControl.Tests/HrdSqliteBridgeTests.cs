using System;
using System.IO;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace IcomRigControl.Tests;

public class HrdSqliteBridgeTests
{
    private static string CreateTempHrdDatabase(bool withCorrectSchema)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();

        if (withCorrectSchema)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE TABLE_HRD_CONTACTS_V01 (
                    col_call TEXT,
                    col_time_on TEXT,
                    col_qso_date TEXT,
                    col_mode TEXT,
                    col_band TEXT,
                    col_freq TEXT
                );";
            cmd.ExecuteNonQuery();
        }
        else
        {
            // A database that exists but has the wrong / no matching table
            var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE SomeOtherTable (id INTEGER);";
            cmd.ExecuteNonQuery();
        }

        return path;
    }

    [Fact]
    public void IsAvailable_DatabaseFileMissing_ReturnsFalse()
    {
        var bridge = new HrdSqliteBridge(@"C:\this\path\does\not\exist.db");
        Assert.False(bridge.IsAvailable());
    }

    [Fact]
    public void IsAvailable_DatabaseExistsButWrongSchema_ReturnsFalse()
    {
        var dbPath = CreateTempHrdDatabase(withCorrectSchema: false);
        try
        {
            var bridge = new HrdSqliteBridge(dbPath);
            Assert.False(bridge.IsAvailable());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    [Fact]
    public void IsAvailable_DatabaseExistsWithCorrectSchema_ReturnsTrue()
    {
        var dbPath = CreateTempHrdDatabase(withCorrectSchema: true);
        try
        {
            var bridge = new HrdSqliteBridge(dbPath);
            Assert.True(bridge.IsAvailable());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task WriteQsoAsync_ValidDatabase_InsertsRow()
    {
        var dbPath = CreateTempHrdDatabase(withCorrectSchema: true);
        try
        {
            var bridge = new HrdSqliteBridge(dbPath);
            var qso = new QsoRecord("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59");

            var result = await bridge.WriteQsoAsync(qso);

            Assert.True(result);

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT col_call FROM TABLE_HRD_CONTACTS_V01";
            var call = cmd.ExecuteScalar() as string;
            Assert.Equal("W1AW", call);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task WriteQsoAsync_DatabaseMissing_ReturnsFalseWithoutThrowing()
    {
        var bridge = new HrdSqliteBridge(@"C:\this\path\does\not\exist.db");
        var qso = new QsoRecord("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59");

        var result = await bridge.WriteQsoAsync(qso);

        Assert.False(result);
    }

    [Fact]
    public async Task WriteQsoAsync_WrongSchema_ReturnsFalseWithoutThrowingOrCorrupting()
    {
        var dbPath = CreateTempHrdDatabase(withCorrectSchema: false);
        try
        {
            var bridge = new HrdSqliteBridge(dbPath);
            var qso = new QsoRecord("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59");

            var result = await bridge.WriteQsoAsync(qso);

            Assert.False(result);

            // Confirm the original (unrelated) table is untouched
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM SomeOtherTable";
            var count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(0, count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }
}