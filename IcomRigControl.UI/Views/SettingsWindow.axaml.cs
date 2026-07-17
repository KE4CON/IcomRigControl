using Avalonia.Controls;
using IcomRigControl.UI.ViewModels;

namespace IcomRigControl.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.RequestClose += (_, _) => Close();
            }
        };
    }
}