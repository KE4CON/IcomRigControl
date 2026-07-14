using System;
using System.IO;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class QsoLoggerPersistenceTests
{
    [Fact]
    public async Task Constructor_WithLogDirectory_CreatesSessionFileImmediately()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        try
        {
            var logger = new QsoLogger(tx, tempDir);

            Assert.True(File.Exists(logger.SessionFilePath));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LogQso_WritesImmediatelyToSessionFile()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        try
        {
            var logger = new QsoLogger(tx, tempDir);
            logger.LogQso("W1AW", "59", "59");

            var content = File.ReadAllText(logger.SessionFilePath!);
            Assert.Contains("W1AW", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LogQso_MultipleQsos_AllPersistToSessionFile()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        try
        {
            var logger = new QsoLogger(tx, tempDir);
            logger.LogQso("W1AW", "59", "59");
            logger.LogQso("K1ABC", "59", "57");
            logger.LogQso("N0CALL", "59", "59");

            var content = File.ReadAllText(logger.SessionFilePath!);
            Assert.Contains("W1AW", content);
            Assert.Contains("K1ABC", content);
            Assert.Contains("N0CALL", content);

            var eorCount = content.Split("<EOR>").Length - 1;
            Assert.Equal(3, eorCount);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Constructor_WithoutLogDirectory_StillWorksInMemoryOnly()
    {
        // Backward compatibility: the original constructor (no persistence)
        // must still work exactly as before for existing callers/tests.
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var logger = new QsoLogger(tx);
        logger.LogQso("W1AW", "59", "59");

        Assert.Single(logger.Qsos);
        Assert.Null(logger.SessionFilePath);
    }

    [Fact]
    public async Task SessionFileName_IsTimestamped()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        try
        {
            var logger = new QsoLogger(tx, tempDir);

            var fileName = Path.GetFileName(logger.SessionFilePath);
            Assert.StartsWith("qsolog_", fileName);
            Assert.EndsWith(".adi", fileName);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}