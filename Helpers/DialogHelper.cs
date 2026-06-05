using System;
using System.IO;

namespace FileMill.Helpers;

public static class DialogHelper
{
    public static void AddFilesFromDialog(string title, string filter, Action<string> addPath)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
        {
            foreach (var path in dlg.FileNames)
                addPath(path);
        }
    }

    public static string? SelectFileFromDialog(string title, string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static string? SelectFileFromDialog(string title, string filter, string currentPath)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = Path.GetFileName(currentPath)
        };

        var currentDirectory = Path.GetDirectoryName(currentPath);
        if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            dlg.InitialDirectory = currentDirectory;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static void AddFolderFromDialog(string title, Action<string> addFolder)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (dlg.ShowDialog() == true)
            addFolder(dlg.FolderName);
    }
}
