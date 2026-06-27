using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FileMill.Models;
using FileMill.Properties;
using FileMill.Services;
using FileMill.ViewModels;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MenuItem = System.Windows.Controls.MenuItem;

namespace FileMill;

public partial class MainWindow : FluentWindow
{
    private static readonly HashSet<string> SupportedExtensions = new(MainViewModel.ImageExtensions, StringComparer.OrdinalIgnoreCase);

    private string _lastSortColumn = "";
    private ListSortDirection _lastSortDir;

    private static readonly string[] ImageExts = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp", ".heic"];
    private static readonly string[] OfficeExts = [".docx", ".xlsx", ".pptx"];
    private static readonly string[] PdfExts = [".pdf"];

    private MainViewModel VM => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;

        // WndProc フックを Loaded 時の Dispatcher で登録することで TitleBar の後に登録されるようにし、LIFO順で先にフックされるようにする
        Loaded += (s, e) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(hwnd);
                source?.AddHook(WndProc);
            }), DispatcherPriority.Background);
        };

        vm.RequestOpenSettings = _ =>
        {
            var language = vm.Language;
            var settingsWindow = new SettingsWindow
            {
                DataContext = vm,
                Owner = this
            };
            settingsWindow.ShowDialog();
            vm.SaveSettings();
            
            if (vm.Language != language && 
                System.Windows.MessageBox.Show(Loc.MsgLanguageRestart, "FileMill", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Process.Start(Environment.ProcessPath ?? "");
                Application.Current.Shutdown();
            }
        };

        App.ApplyTheme(vm.AppTheme);
        ThemeHelper.ApplyWindowTheme(this, App.IsDarkThemeActive());

        vm.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == "AppTheme")
            {
                App.ApplyTheme(vm.AppTheme);
            }
        };

        vm.OnDebugLog = line =>
        {
            DebugConsole.AddLog(line);
        };

        KeyDown += (sender, e) =>
        {
            if ((int)e.Key == 101) // F23 or custom log toggle key
            {
                VM.IsDebugVisible = !VM.IsDebugVisible;
                e.Handled = true;
            }
        };

        Loaded += (sender, e) =>
        {
            vm.WizardStep = 0;
        };
    }

    private static bool IsImageExtension(string ext) => ImageExts.Contains(ext, StringComparer.OrdinalIgnoreCase);
    private static bool IsOfficeExtension(string ext) => OfficeExts.Contains(ext, StringComparer.OrdinalIgnoreCase);
    private static bool IsPdfExtension(string ext) => PdfExts.Contains(ext, StringComparer.OrdinalIgnoreCase);

    private static bool IsSupportedExtension(string ext)
    {
        return IsImageExtension(ext) || IsOfficeExtension(ext) || IsPdfExtension(ext);
    }

    private void FileList_DragEnter(object sender, DragEventArgs e)
    {
        if (HasSupportedFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
            FileListDropBorder.Background = (Brush)TryFindResource("DragOverBackground");
            FileListDropBorder.BorderBrush = (Brush)TryFindResource("DragOverBorderBrush");
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void FileList_DragOver(object sender, DragEventArgs e)
    {
        if (HasSupportedFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void FileList_DragLeave(object sender, DragEventArgs e)
    {
        ResetFileListHighlight();
        e.Handled = true;
    }

    private void FileList_Drop(object sender, DragEventArgs e)
    {
        ResetFileListHighlight();
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (var path in paths)
            {
                AddDroppedPath(path);
            }
        }
        e.Handled = true;
    }

    private void AddDroppedPath(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    AddDroppedFile(file);
                }
                return;
            }
            catch
            {
                return;
            }
        }
        AddDroppedFile(path);
    }

    private void AddDroppedFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(ext))
        {
            if (IsImageExtension(ext))
            {
                VM.AddFileByPath(path);
            }
            else if (IsOfficeExtension(ext))
            {
                VM.AddOptimizeFileByPath(path);
            }
            else if (IsPdfExtension(ext))
            {
                VM.AddPdfFileByPath(path);
            }
        }
    }

    private static bool HasSupportedFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            return paths.Any(p => Directory.Exists(p) || IsSupportedExtension(Path.GetExtension(p)));
        }
        return false;
    }

    private void ResetFileListHighlight()
    {
        if (FileListDropBorder != null)
        {
            FileListDropBorder.Background = (Brush)TryFindResource("CardBackgroundFillColorDefaultBrush");
            FileListDropBorder.BorderBrush = (Brush)TryFindResource("CardStrokeColorDefaultBrush");
        }
    }

    private void FileListView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete && (int)e.Key != 32 || VM.IsProcessing) // Del or Backspace/Space
        {
            return;
        }
        var list = FileListView.SelectedItems.Cast<UnifiedFile>().ToList();
        foreach (var item in list)
        {
            VM.Files.Remove(item);
        }
        e.Handled = list.Count > 0;
    }

    private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void FileListHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column: not null } header)
        {
            return;
        }

        string? sortProp = null;
        if (header.Column == ColName) sortProp = "FileName";
        else if (header.Column == ColDim) sortProp = "Width";
        else if (header.Column == ColSize) sortProp = "OriginalSize";
        else if (header.Column == ColFormat) sortProp = "Format";
        else if (header.Column == ColPath) sortProp = "FilePath";

        if (sortProp != null)
        {
            if (_lastSortColumn == sortProp)
            {
                _lastSortDir = _lastSortDir == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            }
            else
            {
                _lastSortDir = ListSortDirection.Ascending;
            }
            _lastSortColumn = sortProp;
            
            var view = CollectionViewSource.GetDefaultView(VM.Files);
            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(sortProp, _lastSortDir));
            }
            UpdateHeaderArrows(header);
        }
    }

    private void UpdateHeaderArrows(GridViewColumnHeader active)
    {
        var arrow = _lastSortDir == ListSortDirection.Ascending ? " ▲" : " ▼";
        var columns = new[] { ColName, ColDim, ColSize, ColFormat, ColPath };
        foreach (var col in columns)
        {
            if (col != null)
            {
                var headerText = col.Header?.ToString() ?? "";
                headerText = headerText.Replace(" ▲", "").Replace(" ▼", "");
                col.Header = col == active.Column ? (headerText + arrow) : headerText;
            }
        }
    }

    private void HeaderSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || VM?.Files == null)
        {
            return;
        }
        foreach (var file in VM.Files)
        {
            file.IsChecked = checkBox.IsChecked == true;
        }
    }

    private void HeaderAllCheck_Checked(object sender, RoutedEventArgs e)
    {
        if (VM?.Files == null)
        {
            return;
        }
        foreach (var file in VM.Files)
        {
            file.IsChecked = true;
        }
    }

    private void HeaderAllCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        if (VM?.Files == null)
        {
            return;
        }
        foreach (var file in VM.Files)
        {
            file.IsChecked = false;
        }
    }

    private void MenuItem_About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private async void MenuItem_CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuItem;
        if (menuItem != null)
        {
            menuItem.IsEnabled = false;
        }
        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (!result.IsUpdateAvailable)
            {
                System.Windows.MessageBox.Show(string.Format(Loc.MsgUpdateUpToDate, result.CurrentVersion), Loc.TitleUpdateCheck, MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            else
            {
                if (System.Windows.MessageBox.Show(string.Format(Loc.MsgUpdateAvailable, result.LatestVersion, result.CurrentVersion), Loc.TitleUpdateCheck, MessageBoxButton.YesNo, MessageBoxImage.Asterisk) != MessageBoxResult.Yes)
                {
                    return;
                }
                if (string.IsNullOrWhiteSpace(result.PackageUrl))
                {
                    if (System.Windows.MessageBox.Show(Loc.MsgUpdateNoPackage, Loc.TitleUpdateCheck, MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
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
            System.Windows.MessageBox.Show(string.Format(Loc.MsgUpdateCheckFailed, ex.Message), Loc.TitleUpdateCheck, MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            if (menuItem != null)
            {
                menuItem.IsEnabled = true;
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        VM.SaveSettings();
        base.OnClosing(e);
    }

    private void Button_OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && Directory.Exists(vm.OutputDirectory))
        {
            try
            {
                Process.Start("explorer.exe", vm.OutputDirectory);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("フォルダを開けませんでした: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;

        if (msg == WM_NCHITTEST && SettingsButton is { IsVisible: true, ActualWidth: > 0 })
        {
            int screenX = unchecked((short)(long)lParam);
            int screenY = unchecked((short)((long)lParam >> 16));
            try
            {
                Point pt = SettingsButton.PointFromScreen(new Point(screenX, screenY));
                if (pt.X >= 0.0 && pt.X <= SettingsButton.ActualWidth && pt.Y >= 0.0 && pt.Y <= SettingsButton.ActualHeight)
                {
                    handled = true;
                    return (IntPtr)HTCLIENT;
                }
            }
            catch
            {
                // PointFromScreen can throw if the visual is not connected
            }
        }
        return IntPtr.Zero;
    }
}
