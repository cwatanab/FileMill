using System;

namespace FileMill;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test-settings")
            Environment.Exit(0);

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
