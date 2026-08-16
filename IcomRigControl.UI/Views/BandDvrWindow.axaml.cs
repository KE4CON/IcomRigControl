using System;
using System.Collections.Generic;
using Avalonia.Controls;
using IcomRigControl.UI.ViewModels;

namespace IcomRigControl.UI.Views;

public partial class BandDvrWindow : Window
{
    private BandDvrViewModel? _vm;

    public BandDvrWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null) _vm.WaterfallReplayRequested -= ReplayWaterfallFrames;
        _vm = DataContext as BandDvrViewModel;
        if (_vm is not null) _vm.WaterfallReplayRequested += ReplayWaterfallFrames;
    }

    // Redraw the buffered scope history into the waterfall control (reusing its exact
    // live rendering). Oldest frames first, so the newest ends up at the bottom.
    private void ReplayWaterfallFrames(IReadOnlyList<int[]> frames)
    {
        foreach (var frame in frames)
            ReplayWaterfall.PushSweep(frame);
    }
}
