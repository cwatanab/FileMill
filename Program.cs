using System;
using FileMill.Services;

namespace FileMill;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test-settings")
            Environment.Exit(0);

        if (UpdateService.TryRunUpdaterMode(args))
            return;

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
