using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.RigModel;

namespace IcomRigControl.UI.ViewModels;

public partial class MemoryEditorViewModel : ViewModelBase
{
    private readonly Transceiver _transceiver;
    private CancellationTokenSource? _readCts;

    public ObservableCollection<MemoryChannel> Channels { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _progressText = "Ready";

    [ObservableProperty]
    private double _progressPercent;

    public MemoryEditorViewModel(Transceiver transceiver)
    {
        _transceiver = transceiver;
    }

    [RelayCommand]
    private async Task ReadAllChannels()
    {
        if (IsBusy) return;

        IsBusy = true;
        Channels.Clear();
        _readCts = new CancellationTokenSource();

        var progress = new Progress<(int current, int total)>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressText = $"Reading channel {p.current} of {p.total}...";
                ProgressPercent = (double)p.current / p.total * 100;
            });
        });

        try
        {
            var results = await _transceiver.ReadAllMemoriesAsync(progress, _readCts.Token);
            System.IO.File.AppendAllText("memory_debug.log", $"{DateTime.Now}: VIEWMODEL got {results.Count} results, about to dispatch\n");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                System.IO.File.AppendAllText("memory_debug.log", $"{DateTime.Now}: VIEWMODEL inside dispatcher, adding to Channels\n");
                foreach (var ch in results)
                {
                    Channels.Add(ch);
                }
                ProgressText = $"Done — {results.Count} channels found.";
                System.IO.File.AppendAllText("memory_debug.log", $"{DateTime.Now}: VIEWMODEL set ProgressText to Done\n");
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => ProgressText = "Read cancelled.");
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => ProgressText = $"Error: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                ProgressPercent = 0;
            });
        }
    }

    [RelayCommand]
    private void CancelRead()
    {
        _readCts?.Cancel();
    }

    [RelayCommand]
    private async Task WriteAllChannels()
    {
        if (IsBusy) return;

        IsBusy = true;
        int total = Channels.Count;
        int done = 0;

        try
        {
            foreach (var ch in Channels)
            {
                await _transceiver.WriteMemoryChannelAsync(ch);
                done++;
                int currentDone = done;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ProgressText = $"Writing channel {currentDone} of {total}...";
                    ProgressPercent = (double)currentDone / total * 100;
                });
            }
            await Dispatcher.UIThread.InvokeAsync(() => ProgressText = $"Done — {total} channels written.");
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => ProgressText = $"Error: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                ProgressPercent = 0;
            });
        }
    }

    [RelayCommand]
    private void AddEmptyChannel()
    {
        int nextNumber = Channels.Count > 0 ? Channels[^1].ChannelNumber + 1 : 1;
        if (nextNumber <= 99)
        {
            Channels.Add(MemoryChannel.Empty(nextNumber));
        }
    }
}