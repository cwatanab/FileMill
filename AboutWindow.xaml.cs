using System;
using System.Windows;
using System.Windows.Input;
using FileMill.Services;

namespace FileMill;

public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        ThemeHelper.ApplyWindowTheme(this, App.IsDarkThemeActive());
        VersionText.Text = $"v{UpdateService.CurrentVersionText}";
        MessageText.Text = Properties.Loc.AboutMessage;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Wpf.Ui.Controls.Button;
        if (button != null)
        {
            button.IsEnabled = false;
        }
        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (!result.IsUpdateAvailable)
            {
                MessageBox.Show(string.Format(Properties.Loc.MsgUpdateUpToDate, result.CurrentVersion), Properties.Loc.TitleUpdateCheck, MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            else
            {
                if (MessageBox.Show(string.Format(Properties.Loc.MsgUpdateAvailable, result.LatestVersion, result.CurrentVersion), Properties.Loc.TitleUpdateCheck, MessageBoxButton.YesNo, MessageBoxImage.Asterisk) != MessageBoxResult.Yes)
                {
                    return;
                }
                if (string.IsNullOrWhiteSpace(result.PackageUrl))
                {
                    if (MessageBox.Show(Properties.Loc.MsgUpdateNoPackage, Properties.Loc.TitleUpdateCheck, MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                    {
                        UpdateService.OpenReleasePage(result.ReleaseUrl);
                    }
                }
                else
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    UpdateService.StartUpdaterProcess(await UpdateService.DownloadUpdatePackageAsync(result));
                    Application.Current.Shutdown();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Properties.Loc.MsgUpdateCheckFailed, ex.Message), Properties.Loc.TitleUpdateCheck, MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            if (button != null)
            {
                button.IsEnabled = true;
            }
        }
    }
}
