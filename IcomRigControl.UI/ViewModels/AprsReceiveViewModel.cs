using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// HF APRS window: decodes received HF APRS (300-baud) off the radio's RX audio
/// and shows a live station list (one row per callsign, updated as they beacon),
/// plus received text messages. Each station has an "aprs.fi" link (the map
/// substitute). Text messages can be sent via the shared APRS transmit path.
/// </summary>
public partial class AprsReceiveViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly Transceiver _transceiver;
    private readonly SettingsService _settingsService;
    private readonly AprsBeaconService _beaconService;
    private readonly Dictionary<string, AprsStationRow> _byCall = new();
    private AprsReceiveService? _rxService;

    public ObservableCollection<AprsStationRow> Stations { get; } = new();
    public ObservableCollection<AprsMessageRow> Messages { get; } = new();

    [ObservableProperty] private bool _isReceiving;
    [ObservableProperty] private string _status = "Not receiving. Put the radio on an HF APRS frequency (e.g. 10.1476 or 14.1000 MHz USB) and Start.";
    [ObservableProperty] private string _messageToCall = "";
    [ObservableProperty] private string _messageText = "";

    public AprsReceiveViewModel(Transceiver transceiver, SettingsService settingsService, AprsBeaconService beaconService)
    {
        _transceiver = transceiver;
        _settingsService = settingsService;
        _beaconService = beaconService;
    }

    [RelayCommand]
    private void StartReceive()
    {
        if (_rxService is not null) return;
        try
        {
            string? device = _settingsService.Load().RemoteAudioCaptureDevice;
            _rxService = new AprsReceiveService(AudioDevices.CreateCapture(), AfskProfile.Hf300Baud);
            _rxService.PacketReceived += OnPacketReceived;
            _rxService.Start(string.IsNullOrWhiteSpace(device) ? null : device);
            IsReceiving = true;
            Status = "Receiving HF APRS. Decoded stations appear below.";
        }
        catch (Exception ex)
        {
            Status = $"Could not start receive: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopReceive()
    {
        StopService();
        IsReceiving = false;
        Status = "Stopped.";
    }

    private void OnPacketReceived(object? sender, AprsReception rx) =>
        Dispatcher.UIThread.Post(() => Ingest(rx));

    private void Ingest(AprsReception rx)
    {
        string call = rx.Frame.SourceSsid == 0 ? rx.Frame.Source : $"{rx.Frame.Source}-{rx.Frame.SourceSsid}";
        var now = DateTime.Now;

        // Station list: one row per callsign, updated in place.
        if (!_byCall.TryGetValue(call, out var row))
        {
            row = new AprsStationRow(call);
            _byCall[call] = row;
            Stations.Insert(0, row);
        }
        row.LastHeard = now.ToString("HH:mm:ss");
        row.Info = rx.Packet.Type == AprsPacketType.Position ? rx.Packet.Comment : rx.Frame.InfoField;
        if (rx.Packet.Latitude is { } lat && rx.Packet.Longitude is { } lon)
            row.Position = $"{lat:F4}, {lon:F4}";

        // Received messages addressed to us (or to anyone, if we haven't set a call).
        if (rx.Packet.Type == AprsPacketType.Message)
        {
            string myCall = _settingsService.Load().AprsCallsign;
            bool forUs = string.IsNullOrWhiteSpace(myCall) ||
                         string.Equals(rx.Packet.MessageAddressee, myCall, StringComparison.OrdinalIgnoreCase);
            if (forUs)
                Messages.Insert(0, new AprsMessageRow(call, rx.Packet.MessageText ?? "", now.ToString("HH:mm:ss")));
        }
    }

    /// Opens the station on aprs.fi (the "map" — no in-app map needed).
    [RelayCommand]
    private void OpenInAprsFi(AprsStationRow? row)
    {
        if (row is null) return;
        try
        {
            Process.Start(new ProcessStartInfo($"https://aprs.fi/#!call={row.Callsign}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"Could not open aprs.fi: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        var settings = _settingsService.Load();
        if (!CallsignValidator.IsPlausibleAmateurCallsign(settings.AprsCallsign))
        {
            Status = "Set your own callsign in Settings before sending APRS messages.";
            return;
        }
        if (string.IsNullOrWhiteSpace(MessageToCall) || string.IsNullOrWhiteSpace(MessageText))
        {
            Status = "Enter a recipient callsign and a message.";
            return;
        }
        try
        {
            string? device = string.IsNullOrWhiteSpace(settings.AudioOutputDeviceName) ? null : settings.AudioOutputDeviceName;
            bool sent = await _beaconService.SendMessageAsync(
                settings.AprsCallsign, settings.AprsSsid,
                MessageToCall, MessageText, messageNumber: null,
                AfskProfile.Hf300Baud, audioDeviceName: device);

            Status = sent
                ? $"Sent message to {MessageToCall.ToUpperInvariant()}."
                : "Busy transmitting — try again in a moment.";
            if (sent) MessageText = "";
        }
        catch (Exception ex)
        {
            Status = $"Send error: {ex.Message}";
        }
    }

    private void StopService()
    {
        if (_rxService is not null)
        {
            _rxService.PacketReceived -= OnPacketReceived;
            _rxService.Stop();
            _rxService = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        StopService();
        return ValueTask.CompletedTask;
    }
}

public partial class AprsStationRow : ObservableObject
{
    public string Callsign { get; }

    [ObservableProperty] private string _lastHeard = "";
    [ObservableProperty] private string _position = "";
    [ObservableProperty] private string _info = "";

    public AprsStationRow(string callsign) => Callsign = callsign;
}

public record AprsMessageRow(string From, string Text, string Time);
