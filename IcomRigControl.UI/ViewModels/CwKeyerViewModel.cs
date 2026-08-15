using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// CW keyer (command 17) and voice TX memory (command 28) sender. CW macros are
/// editable and persisted to settings; the radio transmits them as CW (it must
/// be in a CW mode). Voice memories are recorded on the radio itself — this just
/// triggers T1-T8.
/// </summary>
public partial class CwKeyerViewModel : ViewModelBase
{
    private readonly Transceiver _transceiver;
    private readonly SettingsService _settingsService;

    public ObservableCollection<CwMessageSlot> CwMessages { get; } = new();

    /// Voice TX memory slots T1-T8, for the fire buttons.
    public List<int> VoiceSlots { get; } = Enumerable.Range(1, 8).ToList();

    [ObservableProperty]
    private string _status = "Ready. The radio must be connected (and in CW mode for CW).";

    public CwKeyerViewModel(Transceiver transceiver, SettingsService settingsService)
    {
        _transceiver = transceiver;
        _settingsService = settingsService;

        var messages = _settingsService.Load().CwMessages;
        for (int i = 0; i < 8; i++)
            CwMessages.Add(new CwMessageSlot(i + 1, i < messages.Count ? messages[i] : ""));
    }

    [RelayCommand]
    private async Task SendCw(CwMessageSlot? slot)
    {
        if (slot is null || string.IsNullOrWhiteSpace(slot.Text))
        {
            Status = "That memory is empty.";
            return;
        }
        try
        {
            await _transceiver.SendCwMessageAsync(slot.Text);
            Status = $"Sent M{slot.Index} (up to 30 chars): {slot.Text}";
        }
        catch (Exception ex)
        {
            Status = $"CW send error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopCw()
    {
        try
        {
            await _transceiver.AbortCwAsync();
            Status = "CW transmission stopped.";
        }
        catch (Exception ex)
        {
            Status = $"CW stop error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SendVoice(int slot)
    {
        try
        {
            await _transceiver.SendVoiceMemoryAsync(slot);
            Status = $"Sent voice memory T{slot} (must be recorded on the radio first).";
        }
        catch (Exception ex)
        {
            Status = $"Voice send error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopVoice()
    {
        try
        {
            await _transceiver.StopVoiceMemoryAsync();
            Status = "Voice transmission stopped.";
        }
        catch (Exception ex)
        {
            Status = $"Voice stop error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveMessages()
    {
        try
        {
            var settings = _settingsService.Load();
            settings.CwMessages = CwMessages.Select(m => m.Text ?? "").ToList();
            _settingsService.Save(settings);
            Status = "CW messages saved.";
        }
        catch (Exception ex)
        {
            Status = $"Save error: {ex.Message}";
        }
    }
}

/// A single editable CW macro slot (M1-M8).
public partial class CwMessageSlot : ObservableObject
{
    public int Index { get; }

    [ObservableProperty]
    private string _text;

    public CwMessageSlot(int index, string text)
    {
        Index = index;
        _text = text;
    }
}
