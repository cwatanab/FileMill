using System.Windows;
using FileMill.Services;

namespace FileMill;

public partial class AboutWindow : Window
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
}
