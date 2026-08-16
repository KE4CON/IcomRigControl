using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Live propagation / band-conditions display (from hamqsl.com): solar flux, A/K
/// index, sunspots, and the modeled day/night opening for each band — an operating
/// assistant that pairs with the DX cluster. See CLAUDE.md propagation.
/// </summary>
public partial class PropagationViewModel : ViewModelBase
{
    private readonly SolarDataService _service = new(new HttpClient());

    [ObservableProperty] private string _solarFlux = "…";
    [ObservableProperty] private string _aIndex = "…";
    [ObservableProperty] private string _kIndex = "…";
    [ObservableProperty] private string _sunspots = "…";
    [ObservableProperty] private string _status = "Loading…";

    public ObservableCollection<BandCondition> Bands { get; } = new();

    public PropagationViewModel() => _ = RefreshAsync();

    [RelayCommand]
    private async Task Refresh() => await RefreshAsync();

    private async Task RefreshAsync()
    {
        Status = "Loading propagation data…";
        SolarData? d = await _service.FetchAsync();
        if (d is null) { Status = "Couldn't fetch propagation data — check your internet connection."; return; }

        SolarFlux = d.SolarFlux;
        AIndex = d.AIndex;
        KIndex = d.KIndex;
        Sunspots = d.Sunspots;

        Bands.Clear();
        foreach (var b in d.Bands) Bands.Add(b);
        Status = string.IsNullOrWhiteSpace(d.Updated) ? "Updated." : $"Updated {d.Updated}. Source: hamqsl.com";
    }
}
