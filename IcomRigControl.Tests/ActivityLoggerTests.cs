using System;
using System.IO;
using System.Threading.Tasks;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class ActivityLoggerTests
{
    [Fact]
    public async Task Start_CreatesFileWithHeaderRow()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        var logger = new ActivityLogger(tx, tempDir);
        logger.Start();

        Assert.True(logger.IsLogging);
        Assert.True(File.Exists(logger.CurrentFilePath));

        var lines = await File.ReadAllLinesAsync(logger.CurrentFilePath!);
        Assert.Single(lines);
        Assert.Contains("Timestamp", lines[0]);
        Assert.Contains("FrequencyHz", lines[0]);
        Assert.Contains("Mode", lines[0]);
        Assert.Contains("SMeterS", lines[0]);

        logger.Stop();
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task MeterUpdated_WhileLogging_AppendsRow()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        var logger = new ActivityLogger(tx, tempDir);
        logger.Start();

        // Simulate a meter update the same way TransceiverTests does
        var incoming = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x15, 0x02, 0x01, 0x20, 0xFD };
        transport.SimulateIncoming(incoming);
        await Task.Delay(50);

        tx.StartPolling(TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        tx.StopPolling();

        logger.Stop();

        var lines = await File.ReadAllLinesAsync(logger.CurrentFilePath!);
        Assert.True(lines.Length > 1); // header + at least one data row

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Stop_UnsubscribesFromMeterUpdated()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        var logger = new ActivityLogger(tx, tempDir);
        logger.Start();
        logger.Stop();

        Assert.False(logger.IsLogging);

        var linesBeforeMoreActivity = await File.ReadAllLinesAsync(logger.CurrentFilePath!);
        int countBefore = linesBeforeMoreActivity.Length;

        tx.StartPolling(TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        tx.StopPolling();

        var linesAfter = await File.ReadAllLinesAsync(logger.CurrentFilePath!);
        Assert.Equal(countBefore, linesAfter.Length); // no new rows after Stop

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void CurrentFilePath_IsNullBeforeStart()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());

        var logger = new ActivityLogger(tx, tempDir);

        Assert.Null(logger.CurrentFilePath);
        Assert.False(logger.IsLogging);
    }
}