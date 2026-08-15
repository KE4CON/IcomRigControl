using System.Net;
using System.Net.Sockets;

namespace IcomRigControl.Services;

/// <summary>
/// The full-duplex remote-audio engine (Phase 12 milestone 3). One of these runs
/// on each end (the Pi server at the radio and the remote client); the two are
/// symmetric:
///   capture -> reframe -> Opus encode -> AudioPacket -> UDP send  (outbound)
///   UDP recv -> parse -> JitterBuffer -> Opus decode -> stream output (inbound)
/// The play-out loop pulls the jitter buffer at the codec frame rate, concealing
/// losses with Opus PLC. SendEnabled gates the outbound path (e.g. a client only
/// streams its mic while transmitting; the server always streams radio audio).
///
/// Capture and output are injected so the network/codec/jitter path is testable
/// with fakes; production uses AudioDevices.CreateCapture()/CreateStreamOutput().
/// Runs alongside the Phase 9 CI-V link; it does not carry CI-V itself.
/// </summary>
public class RemoteAudioLink : IAsyncDisposable
{
    private readonly IAudioCapture _capture;
    private readonly IAudioStreamOutput _output;
    private readonly OpusAudioCodec _codec;
    private readonly JitterBuffer _jitter;
    private readonly FrameAccumulator _accumulator;
    private readonly object _jitterLock = new();
    private readonly object _sendLock = new();

    private UdpClient? _udp;
    private IPEndPoint? _remote;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _playoutTask;
    private ushort _sequence;
    private uint _timestamp;

    /// Gate on the outbound (capture -> network) path. True = stream captured
    /// audio; false = capture is discarded (e.g. client mic muted / not transmitting).
    public bool SendEnabled { get; set; } = true;

    public string? LastError { get; private set; }

    public RemoteAudioLink(IAudioCapture capture, IAudioStreamOutput output,
                           int sampleRateHz = 16000, int jitterDepth = 3)
    {
        _capture = capture;
        _output = output;
        _codec = new OpusAudioCodec(sampleRateHz);
        _jitter = new JitterBuffer(jitterDepth);
        _accumulator = new FrameAccumulator(_codec.FrameSize);
    }

    public void Start(int localPort, string remoteHost, int remotePort)
    {
        _udp = new UdpClient(localPort);
        _remote = new IPEndPoint(Resolve(remoteHost), remotePort);

        _capture.SamplesCaptured += OnSamplesCaptured;
        _capture.Start(_codec.SampleRate);
        _output.Start(_codec.SampleRate);

        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        _playoutTask = Task.Run(() => PlayoutLoopAsync(_cts.Token));
    }

    private void OnSamplesCaptured(object? sender, short[] samples)
    {
        if (!SendEnabled || _udp is null || _remote is null) return;
        try
        {
            foreach (var frame in _accumulator.Add(samples))
            {
                byte[] opus = _codec.Encode(frame);
                var packet = new AudioPacket(_sequence++, _timestamp, opus);
                _timestamp += (uint)_codec.FrameSize;

                byte[] datagram = packet.Serialize();
                lock (_sendLock)
                    _udp.Send(datagram, datagram.Length, _remote);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udp is not null)
        {
            UdpReceiveResult result;
            try { result = await _udp.ReceiveAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch { break; }

            var packet = AudioPacket.Parse(result.Buffer);
            if (packet is not null)
            {
                lock (_jitterLock)
                    _jitter.Add(packet.Value.Sequence, packet.Value.Payload);
            }
        }
    }

    private async Task PlayoutLoopAsync(CancellationToken ct)
    {
        int frameMs = _codec.FrameSize * 1000 / _codec.SampleRate; // e.g. 20 ms
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(frameMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                byte[]? payload;
                lock (_jitterLock)
                    payload = _jitter.GetNext();

                // payload == null => underrun/priming/gap: Opus PLC fills it.
                short[] frame = _codec.Decode(payload);
                _output.Write(frame);
            }
        }
        catch (OperationCanceledException) { /* stopping */ }
    }

    private static IPAddress Resolve(string host)
    {
        if (IPAddress.TryParse(host, out var ip)) return ip;
        var addresses = Dns.GetHostAddresses(host);
        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
               ?? addresses[0];
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _capture.SamplesCaptured -= OnSamplesCaptured;
        try { _capture.Stop(); } catch { }
        try { _output.Stop(); } catch { }
        _udp?.Close();

        try { if (_receiveTask is not null) await _receiveTask; } catch { }
        try { if (_playoutTask is not null) await _playoutTask; } catch { }

        _udp?.Dispose();
        _cts?.Dispose();
    }
}
