using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FileMill.Models;
using FileMill.ViewModels;
using FileMill.Services;

namespace FileMill;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(MainViewModel.ImageExtensions, StringComparer.OrdinalIgnoreCase);

    private string _lastSortColumn = "";
    private ListSortDirection _lastSortDir = ListSortDirection.Ascending;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;

        // ViewModel から設定ウィンドウの表示を要求されたら別ウィンドウとして開く
        vm.RequestOpenSettings = _ =>
        {
            var previousLanguage = vm.Language;
            var win = new SettingsWindow
            {
                DataContext = vm,
                Owner = this
            };
            win.ShowDialog();
            vm.SaveSettings();

            // 言語が変更されたら再起動を促す
            if (vm.Language != previousLanguage)
            {
                var result = MessageBox.Show(
                    "Language changed. Restart now to apply?",
                    "FileMill",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(Environment.ProcessPath!);
                    Application.Current.Shutdown();
                }
            }
        };

        // テーマの初期適用と変更監視
        App.ApplyTheme(vm.AppTheme);
        ThemeHelper.ApplyWindowTheme(this, App.IsDarkThemeActive());
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.AppTheme))
                App.ApplyTheme(vm.AppTheme);
        };

        // デバッグコンソールの配線
        vm.OnDebugLog = line => DebugConsole.AddLog(line);

        // F12 でデバッグコンソールをトグル
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F12)
            {
                VM.IsDebugVisible = !VM.IsDebugVisible;
                e.Handled = true;
            }
        };
    }

    private MainViewModel VM => (MainViewModel)DataContext;

    /// <summary>
    /// ドラッグがファイルリスト領域に入ったときの処理。
    /// 画像ファイルが含まれていればハイライト表示。
    /// </summary>
    private void FileList_DragEnter(object sender, DragEventArgs e)
    {
        if (HasImageFiles(e))
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

    /// <summary>
    /// ドラッグ中も継続的にハイライトを維持。
    /// </summary>
    private void FileList_DragOver(object sender, DragEventArgs e)
    {
        if (HasImageFiles(e))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// ドラッグがファイルリスト領域から出たとき、ハイライトを解除。
    /// </summary>
    private void FileList_DragLeave(object sender, DragEventArgs e)
    {
        ResetFileListHighlight();
        e.Handled = true;
    }

    /// <summary>
    /// ファイルがドロップされたとき、画像ファイルのみを追加。
    /// </summary>
    private void FileList_Drop(object sender, DragEventArgs e)
    {
        ResetFileListHighlight();

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var imageFiles = paths.Where(p => IsImageFile(p)).ToArray();
            foreach (var path in imageFiles)
                VM.AddFileByPath(path);
        }
        e.Handled = true;
    }

    /// <summary>
    /// ドロップされたデータに画像ファイルが含まれているか。
    /// </summary>
    private static bool HasImageFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        return e.Data.GetData(DataFormats.FileDrop) is string[] paths
               && paths.Any(p => IsImageFile(p));
    }

    /// <summary>
    /// パスが対応画像ファイルかどうか（ファイル実在チェックはしない）。
    /// </summary>
    private static bool IsImageFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (Directory.Exists(path)) return true;
        var ext = Path.GetExtension(path);
        return SupportedExtensions.Contains(ext);
    }

    /// <summary>
    /// ファイルリスト領域のハイライトを元に戻す。
    /// </summary>
    private void ResetFileListHighlight()
    {
        FileListDropBorder.Background = (Brush)TryFindResource("ControlBackground");
        FileListDropBorder.BorderBrush = (Brush)TryFindResource("BorderBrush");
    }

    private void FileListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Delete || VM.IsProcessing)
            return;

        var selectedFiles = FileListView.SelectedItems.Cast<ImageFile>().ToList();
        foreach (var file in selectedFiles)
            VM.Files.Remove(file);

        e.Handled = selectedFiles.Count > 0;
    }

    private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    /// <summary>
    /// リストのヘッダークリックでソート切替。
    /// </summary>
    private void FileListHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Column == null)
            return;

        // クリックされたカラムに対応するソートプロパティ名を決定
        string? sortProp = header.Column switch
        {
            _ when header.Column == ColName  => nameof(ImageFile.FileName),
            _ when header.Column == ColDim   => nameof(ImageFile.Width),
            _ when header.Column == ColSize  => nameof(ImageFile.FileSize),
            _ when header.Column == ColFormat => nameof(ImageFile.Format),
            _ when header.Column == ColDateModified => nameof(ImageFile.DateModified),
            _ when header.Column == ColDateTaken => nameof(ImageFile.DateTaken),
            _ when header.Column == ColPath  => nameof(ImageFile.FilePath),
            _ => null
        };

        if (sortProp == null) return;

        // 同じ列を再度クリック → 昇順/降順切替
        if (_lastSortColumn == sortProp)
            _lastSortDir = _lastSortDir == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        else
            _lastSortDir = ListSortDirection.Ascending;

        _lastSortColumn = sortProp;

        var view = CollectionViewSource.GetDefaultView(VM.Files);
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortProp, _lastSortDir));
        }

        // ヘッダーに矢印表示（簡易）
        UpdateHeaderArrows(header);
    }

    private void UpdateHeaderArrows(GridViewColumnHeader active)
    {
        var arrow = _lastSortDir == ListSortDirection.Ascending ? " ▲" : " ▼";
        var cols = new[] { ColName, ColDim, ColSize, ColFormat, ColDateModified, ColDateTaken, ColPath, ColOptName, ColOptFormat, ColOptOriginalSize, ColOptOptimizedSize, ColOptSavings, ColOptStatus, ColOptPath, ColPdfName, ColPdfFormat, ColPdfOriginalSize, ColPdfOptimizedSize, ColPdfSavings, ColPdfStatus, ColPdfPath };
        foreach (var col in cols)
        {
            if (col == null) continue;
            var hdr = col.Header?.ToString() ?? "";
            hdr = hdr.Replace(" ▲", "").Replace(" ▼", "");
            col.Header = col == active.Column ? hdr + arrow : hdr;
        }
    }

    private void HeaderAllCheck_Checked(object sender, RoutedEventArgs e)
    {
        if (VM?.Files == null) return;
        foreach (var file in VM.Files)
        {
            file.IsChecked = true;
        }
    }

    private void HeaderAllCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        if (VM?.Files == null) return;
        foreach (var file in VM.Files)
        {
            file.IsChecked = false;
        }
    }

    private void MenuItem_About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWin = new AboutWindow { Owner = this };
        aboutWin.ShowDialog();
    }

    private async void MenuItem_CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuItem;
        if (menuItem != null)
            menuItem.IsEnabled = false;

        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (!result.IsUpdateAvailable)
            {
                MessageBox.Show(
                    $"現在のバージョン {result.CurrentVersion} は最新です。",
                    "アップデート確認",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var answer = MessageBox.Show(
                $"新しいバージョン {result.LatestVersion} が利用できます。\n\n現在のバージョン: {result.CurrentVersion}\n最新バージョン: {result.LatestVersion}\n\nダウンロードして更新しますか？\nFileMill は終了し、更新後に再起動します。",
                "アップデート確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer != MessageBoxResult.Yes)
                return;

            if (string.IsNullOrWhiteSpace(result.PackageUrl))
            {
                var openReleasePage = MessageBox.Show(
                    "更新用 ZIP が見つかりませんでした。リリースページを開きますか？",
                    "アップデート確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (openReleasePage == MessageBoxResult.Yes)
                    UpdateService.OpenReleasePage(result.ReleaseUrl);
                return;
            }

            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            var packagePath = await UpdateService.DownloadUpdatePackageAsync(result);
            UpdateService.StartUpdaterProcess(packagePath);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"アップデート情報を確認できませんでした。\n\n{ex.Message}",
                "アップデート確認",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
            if (menuItem != null)
                menuItem.IsEnabled = true;
        }
    }

    // --- ファイル最適化用イベントハンドラー ---
    private void HeaderAllCheckOptimize_Checked(object sender, RoutedEventArgs e)
    {
        if (VM?.OptimizeFiles == null) return;
        foreach (var file in VM.OptimizeFiles)
        {
            file.IsChecked = true;
        }
    }

    private void HeaderAllCheckOptimize_Unchecked(object sender, RoutedEventArgs e)
    {
        if (VM?.OptimizeFiles == null) return;
        foreach (var file in VM.OptimizeFiles)
        {
            file.IsChecked = false;
        }
    }

    private void OptimizeFileList_DragEnter(object sender, DragEventArgs e)
    {
        if (HasOptimizeFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
            OptimizeFileListDropBorder.Background = (Brush)TryFindResource("DragOverBackground");
            OptimizeFileListDropBorder.BorderBrush = (Brush)TryFindResource("DragOverBorderBrush");
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OptimizeFileList_DragOver(object sender, DragEventArgs e)
    {
        if (HasOptimizeFiles(e))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OptimizeFileList_DragLeave(object sender, DragEventArgs e)
    {
        ResetOptimizeFileListHighlight();
        e.Handled = true;
    }

    private void OptimizeFileList_Drop(object sender, DragEventArgs e)
    {
        ResetOptimizeFileListHighlight();

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var allowedExts = new[] { ".docx", ".xlsx", ".pptx" };
            var matchedFiles = paths.Where(p => Directory.Exists(p) || allowedExts.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray();
            
            foreach (var path in matchedFiles)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        var files = Directory.GetFiles(path)
                            .Where(f => allowedExts.Contains(Path.GetExtension(f).ToLowerInvariant()));
                        foreach (var f in files)
                            VM.AddOptimizeFileByPath(f);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to read files in dropped directory {path}: {ex.Message}");
                    }
                }
                else
                {
                    VM.AddOptimizeFileByPath(path);
                }
            }
        }
        e.Handled = true;
    }

    private static bool HasOptimizeFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var allowedExts = new[] { ".docx", ".xlsx", ".pptx" };
            return paths.Any(p => Directory.Exists(p) || allowedExts.Contains(Path.GetExtension(p).ToLowerInvariant()));
        }
        return false;
    }

    private void ResetOptimizeFileListHighlight()
    {
        if (OptimizeFileListDropBorder != null)
        {
            OptimizeFileListDropBorder.Background = (Brush)TryFindResource("ControlBackground");
            OptimizeFileListDropBorder.BorderBrush = (Brush)TryFindResource("BorderBrush");
        }
    }

    private void OptimizeFileListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Delete || VM.IsProcessing)
            return;

        var selectedFiles = OptimizeFileListView.SelectedItems.Cast<OptimizeFile>().ToList();
        foreach (var file in selectedFiles)
            VM.OptimizeFiles.Remove(file);

        e.Handled = selectedFiles.Count > 0;
    }

    private void OptimizeFileListHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Column == null)
            return;

        string? sortProp = header.Column switch
        {
            _ when header.Column == ColOptName => nameof(OptimizeFile.FileName),
            _ when header.Column == ColOptFormat => nameof(OptimizeFile.Format),
            _ when header.Column == ColOptOriginalSize => nameof(OptimizeFile.OriginalSize),
            _ when header.Column == ColOptOptimizedSize => nameof(OptimizeFile.OptimizedSize),
            _ when header.Column == ColOptStatus => nameof(OptimizeFile.Status),
            _ when header.Column == ColOptPath => nameof(OptimizeFile.FilePath),
            _ => null
        };

        if (sortProp == null) return;

        if (_lastSortColumn == sortProp)
            _lastSortDir = _lastSortDir == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        else
            _lastSortDir = ListSortDirection.Ascending;

        _lastSortColumn = sortProp;

        var view = CollectionViewSource.GetDefaultView(VM.OptimizeFiles);
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortProp, _lastSortDir));
        }

        UpdateHeaderArrows(header);
    }

    private void HeaderAllCheckPdf_Checked(object sender, RoutedEventArgs e)
    {
        if (VM?.PdfFiles == null) return;
        foreach (var file in VM.PdfFiles)
        {
            file.IsChecked = true;
        }
    }

    private void HeaderAllCheckPdf_Unchecked(object sender, RoutedEventArgs e)
    {
        if (VM?.PdfFiles == null) return;
        foreach (var file in VM.PdfFiles)
        {
            file.IsChecked = false;
        }
    }

    private void PdfFileList_DragEnter(object sender, DragEventArgs e)
    {
        if (HasPdfFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
            PdfFileListDropBorder.Background = (Brush)TryFindResource("DragOverBackground");
            PdfFileListDropBorder.BorderBrush = (Brush)TryFindResource("DragOverBorderBrush");
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void PdfFileList_DragOver(object sender, DragEventArgs e)
    {
        if (HasPdfFiles(e))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void PdfFileList_DragLeave(object sender, DragEventArgs e)
    {
        ResetPdfFileListHighlight();
        e.Handled = true;
    }

    private void PdfFileList_Drop(object sender, DragEventArgs e)
    {
        ResetPdfFileListHighlight();

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (var path in paths.Where(p => Directory.Exists(p) || IsPdfFile(p)))
                VM.AddPdfFileByPath(path);
        }
        e.Handled = true;
    }

    private static bool HasPdfFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        return e.Data.GetData(DataFormats.FileDrop) is string[] paths
               && paths.Any(p => Directory.Exists(p) || IsPdfFile(p));
    }

    private static bool IsPdfFile(string path)
        => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    private void ResetPdfFileListHighlight()
    {
        if (PdfFileListDropBorder != null)
        {
            PdfFileListDropBorder.Background = (Brush)TryFindResource("ControlBackground");
            PdfFileListDropBorder.BorderBrush = (Brush)TryFindResource("BorderBrush");
        }
    }

    private void PdfFileListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Delete || VM.IsProcessing)
            return;

        var selectedFiles = PdfFileListView.SelectedItems.Cast<OptimizeFile>().ToList();
        foreach (var file in selectedFiles)
            VM.PdfFiles.Remove(file);

        e.Handled = selectedFiles.Count > 0;
    }

    private void PdfFileListHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Column == null)
            return;

        string? sortProp = header.Column switch
        {
            _ when header.Column == ColPdfName => nameof(OptimizeFile.FileName),
            _ when header.Column == ColPdfFormat => nameof(OptimizeFile.Format),
            _ when header.Column == ColPdfOriginalSize => nameof(OptimizeFile.OriginalSize),
            _ when header.Column == ColPdfOptimizedSize => nameof(OptimizeFile.OptimizedSize),
            _ when header.Column == ColPdfStatus => nameof(OptimizeFile.Status),
            _ when header.Column == ColPdfPath => nameof(OptimizeFile.FilePath),
            _ => null
        };

        if (sortProp == null) return;

        if (_lastSortColumn == sortProp)
            _lastSortDir = _lastSortDir == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        else
            _lastSortDir = ListSortDirection.Ascending;

        _lastSortColumn = sortProp;

        var view = CollectionViewSource.GetDefaultView(VM.PdfFiles);
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortProp, _lastSortDir));
        }

        UpdateHeaderArrows(header);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        VM.SaveSettings();
        base.OnClosing(e);
    }
}
