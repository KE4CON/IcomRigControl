using System;
using System.IO;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Load_NoFileExists_ReturnsDefaultSettings()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var service = new SettingsService(tempPath);

        var settings = service.Load();

        Assert.NotNull(settings);
        Assert.Equal("Callook", settings.CallsignLookupSource);
        Assert.False(settings.HrdBridgeEnabled);
        Assert.False(settings.N1mmSendEnabled);
        Assert.False(settings.N1mmReceiveEnabled);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllValues()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var service = new SettingsService(tempPath);
            var settings = new AppSettings
            {
                CallsignLookupSource = "QRZ",
                QrzUsername = "testuser",
                QrzPassword = "testpass",
                HamQthUsername = "hquser",
                HamQthPassword = "hqpass",
                TqslExecutablePath = @"C:\Program Files\TQSL\tqsl.exe",
                HrdBridgeEnabled = true,
                HrdDatabasePath = @"C:\HRD\logbook.db",
                N1mmSendEnabled = true,
                N1mmReceiveEnabled = true,
                N1mmDestinations = new() { ("127.0.0.1", 12060) },
                ContactListenPort = 12070
            };

            service.Save(settings);
            var loaded = service.Load();

            Assert.Equal("QRZ", loaded.CallsignLookupSource);
            Assert.Equal("testuser", loaded.QrzUsername);
            Assert.Equal("testpass", loaded.QrzPassword);
            Assert.Equal("hquser", loaded.HamQthUsername);
            Assert.True(loaded.HrdBridgeEnabled);
            Assert.Equal(@"C:\HRD\logbook.db", loaded.HrdDatabasePath);
            Assert.True(loaded.N1mmSendEnabled);
            Assert.Single(loaded.N1mmDestinations);
            Assert.Equal(12070, loaded.ContactListenPort);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsWithoutThrowing()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(tempPath, "not valid json {{{");
        try
        {
            var service = new SettingsService(tempPath);
            var settings = service.Load();

            Assert.NotNull(settings);
            Assert.Equal("Callook", settings.CallsignLookupSource); // fell back to defaults
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlSettingsTest_" + Guid.NewGuid());
        var tempPath = Path.Combine(tempDir, "settings.json");
        try
        {
            var service = new SettingsService(tempPath);
            service.Save(new AppSettings());

            Assert.True(File.Exists(tempPath));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}