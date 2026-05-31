using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FileMill.Views;

public partial class DebugWindow : UserControl
{
    public DebugWindow()
    {
        InitializeComponent();
    }

    public void AddLog(string line)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText(line + Environment.NewLine);
        });
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        LogTextBox.Clear();
    }

    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        LogTextBox.ScrollToEnd();
    }
}
