using System.Net.Http;
using System.Net.Http.Json;
using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Pushes MeterSnapshot data to the EMMCOM Field Comms Server as JSON on every
/// MeterUpdated event while running. Network failures are logged internally
/// and do not stop the bridge or throw back to the caller.
/// </summary>
public class EmmcomBridge
{
    private readonly Transceiver _transceiver;
    private readonly HttpClient _httpClient;
    private readonly string _endpointUrl;

    public bool IsRunning { get; private set; }
    public string? LastError { get; private set; }

    public EmmcomBridge(Transceiver transceiver, HttpClient httpClient, string endpointUrl)
    {
        _transceiver = transceiver;
        _httpClient = httpClient;
        _endpointUrl = endpointUrl;
    }

    public void Start()
    {
        if (IsRunning) return;

        _transceiver.MeterUpdated += OnMeterUpdated;
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _transceiver.MeterUpdated -= OnMeterUpdated;
        IsRunning = false;
    }

    private async void OnMeterUpdated(object? sender, MeterSnapshot snapshot)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(_endpointUrl, snapshot);
            LastError = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            // Per CLAUDE.md: log, don't crash. Network issues (server down, no wifi,
            // wrong URL) should never take down rig polling or the rest of the app.
            LastError = ex.Message;
        }
    }
}