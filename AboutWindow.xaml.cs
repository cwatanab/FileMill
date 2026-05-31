using System.Windows;
using FileMill.Services;

namespace FileMill;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        ThemeHelper.ApplyWindowTheme(this, App.IsDarkThemeActive());
        MessageText.Text = Properties.Loc.AboutMessage;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
