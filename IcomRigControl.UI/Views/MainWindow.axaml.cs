using Avalonia.Controls;
using Avalonia.Input;
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

    /// Handles a click on the waterfall: computes the click position as a
    /// 0.0-1.0 fraction of the control's width and asks the ViewModel to
    /// tune the radio there (Phase 7 click-to-tune).
    private void Waterfall_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is not Control control) return;

        var point = e.GetPosition(control);
        double fraction = control.Bounds.Width > 0 ? point.X / control.Bounds.Width : 0.5;
        fraction = System.Math.Clamp(fraction, 0.0, 1.0);

        if (vm.TuneToWaterfallPositionCommand.CanExecute(fraction))
        {
            vm.TuneToWaterfallPositionCommand.Execute(fraction);
        }
    }
}