using Avalonia.Controls;
using Avalonia.Threading;
using IcomRigControl.UI.ViewModels;

namespace IcomRigControl.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.TransceiverInstance.WaveformUpdated += (_, waveform) =>
            {
                Dispatcher.UIThread.Post(() => Waterfall.PushSweep(waveform));
            };
        }
    }
}