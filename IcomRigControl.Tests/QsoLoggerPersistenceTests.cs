using System;
using System.IO;
using System.Linq;
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
    public async Task LogQso_WhenDurableWriteFails_DoesNotLeavePhantomInMemory()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "IcomRigControlTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = new QsoLogger(tx, tempDir);

            // Sabotage the durable session file so the write-through append is
            // guaranteed to fail on every OS: replace the file with a directory
            // of the same name. Appending text to a directory path throws.
            string sessionFile = logger.SessionFilePath!;
            File.Delete(sessionFile);
            Directory.CreateDirectory(sessionFile);

            // The durable append MUST fail...
            Assert.ThrowsAny<Exception>(() => logger.LogQso("KE4CON", "59", "59"));

            // ...and because the durable file is the backup of record, a failed
            // write must NOT leave a phantom QSO that lives only in memory and
            // would be lost on the next restart. Before the fix the record was
            // added to the in-memory list BEFORE the (failing) write.
            Assert.Empty(logger.Qsos);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LogReceivedQso_ConcurrentCallsFromManyThreads_AllRecorded()
    {
        // The UI thread (LogQso) and the UDP contact listener thread
        // (LogReceivedQso) can log at the same instant. The shared list must be
        // synchronized or QSOs are silently lost / the list corrupts — a direct
        // violation of the backup-of-record principle.
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        var logger = new QsoLogger(tx); // in-memory only, to isolate the list race

        const int count = 300;
        var tasks = Enumerable.Range(0, count).Select(i => Task.Run(() =>
            logger.LogReceivedQso(new QsoRecord(
                Callsign: $"CALL{i}",
                FrequencyMHz: 14.074,
                Band: "20M",
                Mode: "USB",
                DateTimeUtc: DateTime.UtcNow,
                RstSent: "59",
                RstReceived: "59")))).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(count, logger.Qsos.Count);
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