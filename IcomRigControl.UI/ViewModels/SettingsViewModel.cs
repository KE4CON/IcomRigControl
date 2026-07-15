using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string _callsignLookupSource = "Callook";

    public List<string> AvailableLookupSources { get; } = new() { "Callook", "QRZ", "HamQTH" };

    [ObservableProperty]
    private string _qrzUsername = "";

    [ObservableProperty]
    private string _qrzPassword = "";

    [ObservableProperty]
    private string _hamQthUsername = "";

    [ObservableProperty]
    private string _hamQthPassword = "";

    [ObservableProperty]
    private string _tqslExecutablePath = "";

    [ObservableProperty]
    private bool _hrdBridgeEnabled;

    [ObservableProperty]
    private string _hrdDatabasePath = "";

    [ObservableProperty]
    private bool _n1mmSendEnabled;

    [ObservableProperty]
    private bool _n1mmReceiveEnabled;

    [ObservableProperty]
    private string _n1mmDestinationInput = "127.0.0.1:12060";

    [ObservableProperty]
    private int _contactListenPort = 12070;

    [ObservableProperty]
    private string _statusMessage = "";

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromSettings(_settingsService.Load());
    }

    private void LoadFromSettings(AppSettings settings)
    {
        CallsignLookupSource = settings.CallsignLookupSource;
        QrzUsername = settings.QrzUsername;
        QrzPassword = settings.QrzPassword;
        HamQthUsername = settings.HamQthUsername;
        HamQthPassword = settings.HamQthPassword;
        TqslExecutablePath = settings.TqslExecutablePath;
        HrdBridgeEnabled = settings.HrdBridgeEnabled;
        HrdDatabasePath = settings.HrdDatabasePath;
        N1mmSendEnabled = settings.N1mmSendEnabled;
        N1mmReceiveEnabled = settings.N1mmReceiveEnabled;
        ContactListenPort = settings.ContactListenPort;

        if (settings.N1mmDestinations.Count > 0)
        {
            var (ip, port) = settings.N1mmDestinations[0];
            N1mmDestinationInput = $"{ip}:{port}";
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            var destinations = new List<(string Ip, int Port)>();
            var parts = N1mmDestinationInput.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int port))
            {
                destinations.Add((parts[0], port));
            }

            var settings = new AppSettings
            {
                CallsignLookupSource = CallsignLookupSource,
                QrzUsername = QrzUsername,
                QrzPassword = QrzPassword,
                HamQthUsername = HamQthUsername,
                HamQthPassword = HamQthPassword,
                TqslExecutablePath = TqslExecutablePath,
                HrdBridgeEnabled = HrdBridgeEnabled,
                HrdDatabasePath = HrdDatabasePath,
                N1mmSendEnabled = N1mmSendEnabled,
                N1mmReceiveEnabled = N1mmReceiveEnabled,
                N1mmDestinations = destinations,
                ContactListenPort = ContactListenPort
            };

            _settingsService.Save(settings);
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }
}