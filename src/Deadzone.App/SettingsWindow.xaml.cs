using System.Windows;

namespace Deadzone.App;

public partial class SettingsWindow : Window
{
    private readonly Settings.SettingsService _settingsService;
    private Settings.AppSettings _settings;

    public SettingsWindow(Settings.SettingsService settingsService, Settings.AppSettings settings)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;

        // Initialise checkboxes from loaded settings (suppress event handlers via IsLoaded)
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        StartMinimizedCheckBox.IsChecked    = settings.StartMinimizedToTray;
        MinimizeToTrayCheckBox.IsChecked    = settings.MinimizeToTray;

        // Refresh actual task-scheduler state so the checkbox reflects reality
        bool taskExists = Services.StartupTaskService.IsStartupTaskEnabled();
        StartWithWindowsCheckBox.IsChecked = taskExists;
        _settings.StartWithWindows = taskExists;
    }

    private void StartWithWindowsCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (Services.StartupTaskService.SetStartupTask(true, out string? err))
        {
            _settings.StartWithWindows = true;
            _settingsService.RequestSave(_settings);
        }
        else
        {
            // Revert checkbox if it failed
            StartWithWindowsCheckBox.IsChecked = false;
            System.Windows.MessageBox.Show(
                $"Could not create startup task:\n{err}",
                "Startup Task Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void StartWithWindowsCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        Services.StartupTaskService.SetStartupTask(false, out _);
        _settings.StartWithWindows = false;
        _settingsService.RequestSave(_settings);
    }

    private void StartMinimizedCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.StartMinimizedToTray = true;
        _settingsService.RequestSave(_settings);
    }

    private void StartMinimizedCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.StartMinimizedToTray = false;
        _settingsService.RequestSave(_settings);
    }

    private void MinimizeToTrayCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.MinimizeToTray = true;
        _settingsService.RequestSave(_settings);
    }

    private void MinimizeToTrayCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.MinimizeToTray = false;
        _settingsService.RequestSave(_settings);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
