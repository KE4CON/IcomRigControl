using System.Net;
using System.Net.Sockets;
using System.Text;
using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Sends RadioInfo-format XML UDP packets (frequency, mode, PTT state)
/// whenever the Transceiver's state changes, to a configurable list of
/// destination IP:port targets. This is the "send" half of the shared
/// N1MM/WSJT-X/HRD protocol integration (CLAUDE.md Phase 8f, Direction 1) —
/// it lets those programs treat IcomRigControl as a rig-status source
/// without needing their own CAT connection to the radio.
/// </summary>
public class RadioInfoUdpBroadcaster
{
    private readonly Transceiver _transceiver;
    private readonly List<(string Ip, int Port)> _destinations = new();
    private UdpClient? _udpClient;

    public bool IsRunning { get; private set; }
    public IReadOnlyList<(string Ip, int Port)> Destinations => _destinations;
    public string? LastError { get; private set; }

    public RadioInfoUdpBroadcaster(Transceiver transceiver)
    {
        _transceiver = transceiver;
    }

    public void AddDestination(string ip, int port)
    {
        if (!_destinations.Contains((ip, port)))
        {
            _destinations.Add((ip, port));
        }
    }

    public void RemoveDestination(string ip, int port)
    {
        _destinations.Remove((ip, port));
    }

    public void Start()
    {
        if (IsRunning) return;

        _udpClient = new UdpClient();
        _transceiver.FrequencyChanged += OnStateChanged;
        _transceiver.ModeChanged += OnModeChanged;
        _transceiver.PttChanged += OnPttChanged;
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _transceiver.FrequencyChanged -= OnStateChanged;
        _transceiver.ModeChanged -= OnModeChanged;
        _transceiver.PttChanged -= OnPttChanged;
        _udpClient?.Dispose();
        _udpClient = null;
        IsRunning = false;
    }

    private void OnStateChanged(object? sender, long hz) => BroadcastCurrentState();
    private void OnModeChanged(object? sender, string mode) => BroadcastCurrentState();
    private void OnPttChanged(object? sender, bool transmitting) => BroadcastCurrentState();

    private async void BroadcastCurrentState()
    {
        if (_udpClient == null || _destinations.Count == 0) return;

        string xml = GenerateRadioInfoXml(_transceiver.FrequencyHz, _transceiver.Mode, _transceiver.PttActive);
        byte[] bytes = Encoding.UTF8.GetBytes(xml);

        foreach (var (ip, port) in _destinations)
        {
            try
            {
                await _udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse(ip), port));
            }
            catch (Exception ex)
            {
                // Per CLAUDE.md: never crash on a network hiccup — record and continue
                // trying other destinations / future broadcasts.
                LastError = ex.Message;
            }
        }
    }

    /// Builds a RadioInfo-format XML packet matching the schema shared by
    /// N1MM Logger+, WSJT-X, and HRD Logbook's UDP Receive feature.
    public static string GenerateRadioInfoXml(long frequencyHz, string mode, bool isTransmitting)
    {
        return $"<RadioInfo>" +
               $"<Freq>{frequencyHz}</Freq>" +
               $"<Mode>{mode}</Mode>" +
               $"<IsTransmitting>{isTransmitting}</IsTransmitting>" +
               $"</RadioInfo>";
    }
}