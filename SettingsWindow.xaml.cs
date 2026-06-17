using System.Windows;
using FileMill.Services;

namespace FileMill;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    public SettingsWindow()
    {
        InitializeComponent();
        ThemeHelper.ApplyWindowTheme(this, App.IsDarkThemeActive());
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
