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

namespace FileMill;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tif", ".tiff", ".gif", ".svg", ".bmp", ".heic", ".heif"
    };

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
            var win = new SettingsWindow
            {
                DataContext = vm,
                Owner = this
            };
            win.ShowDialog();
            vm.SaveSettings();
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
            FileListDropBorder.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE));
            FileListDropBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
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
        // ディレクトリは除外
        if (Directory.Exists(path)) return false;
        var ext = Path.GetExtension(path);
        return SupportedExtensions.Contains(ext);
    }

    /// <summary>
    /// ファイルリスト領域のハイライトを元に戻す。
    /// </summary>
    private void ResetFileListHighlight()
    {
        FileListDropBorder.Background = Brushes.White;
        FileListDropBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
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
        var cols = new[] { ColName, ColDim, ColSize, ColFormat, ColDateModified, ColDateTaken, ColPath };
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
        var message = "FileMill v1.0\n\n" +
                      "本アプリは、画像一括変換ソフト「Ralpha」およびOfficeファイル軽量化ツール「OptiOpenXML」の思想を取り入れ、現代の画像フォーマット（WebP, AVIF等）に対応させて統合したバッチ処理アプリです。\n\n" +
                      "【謝辞】\n" +
                      "・Ralpha / RalphaPlus (にるぽ / Nilposoft 氏)\n" +
                      "  画像変換の画面レイアウトおよび機能デザインの参考にさせていただきました。\n" +
                      "  http://nilposoft.info/ralpha/ralphaplus64.html\n\n" +
                      "・OptiOpenXML\n" +
                      "  Office Open XML文書の軽量化・最適化処理のアイデアの参考にさせていただきました。\n" +
                      "  https://www.hiskip.com/free/freesoft/doc/office/14978.html\n\n" +
                      "素晴らしいソフトウェアの開発者および関係者の皆様に心より感謝申し上げます。";

        MessageBox.Show(message, "バージョン情報", MessageBoxButton.OK, MessageBoxImage.Information);
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
            OptimizeFileListDropBorder.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE));
            OptimizeFileListDropBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
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
            var allowedExts = new[] { ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tif", ".tiff", ".gif", ".svg", ".bmp" };
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
                    catch {}
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
            var allowedExts = new[] { ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tif", ".tiff", ".gif", ".svg", ".bmp" };
            return paths.Any(p => Directory.Exists(p) || allowedExts.Contains(Path.GetExtension(p).ToLowerInvariant()));
        }
        return false;
    }

    private void ResetOptimizeFileListHighlight()
    {
        if (OptimizeFileListDropBorder != null)
        {
            OptimizeFileListDropBorder.Background = Brushes.White;
            OptimizeFileListDropBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
        }
    }

    private void OptimizeFileListHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
        {
            var propName = GetSortPropertyForOptimizeHeader(header.Column);
            if (string.IsNullOrEmpty(propName)) return;

            var dir = ListSortDirection.Ascending;
            if (_lastSortColumn == propName && _lastSortDir == ListSortDirection.Ascending)
                dir = ListSortDirection.Descending;

            _lastSortColumn = propName;
            _lastSortDir = dir;

            SortOptimizeFiles(propName, dir);
        }
    }

    private static string GetSortPropertyForOptimizeHeader(GridViewColumn column)
    {
        if (column.Header is string headerText)
        {
            return headerText switch
            {
                "名前" => "FileName",
                "種類" => "Format",
                "元のサイズ" => "OriginalSize",
                "最適化後" => "OptimizedSize",
                "状態" => "Status",
                "パス" => "FilePath",
                _ => ""
            };
        }
        return "";
    }

    private void SortOptimizeFiles(string propName, ListSortDirection direction)
    {
        if (VM?.OptimizeFiles == null) return;

        var list = VM.OptimizeFiles.ToList();
        VM.OptimizeFiles.Clear();

        IOrderedEnumerable<OptimizeFile> sorted = propName switch
        {
            "FileName" => direction == ListSortDirection.Ascending ? list.OrderBy(f => f.FileName) : list.OrderByDescending(f => f.FileName),
            "Format" => direction == ListSortDirection.Ascending ? list.OrderBy(f => f.Format) : list.OrderByDescending(f => f.Format),
            "OriginalSize" => direction == ListSortDirection.Ascending ? list.OrderBy(f => f.OriginalSize) : list.OrderByDescending(f => f.OriginalSize),
            "OptimizedSize" => direction == ListSortDirection.Ascending ? list.OrderBy(f => f.OptimizedSize ?? 0) : list.OrderByDescending(f => f.OptimizedSize ?? 0),
            "Status" => direction == ListSortDirection.Ascending ? list.OrderBy(f => f.Status) : list.OrderByDescending(f => f.Status),
            "FilePath" => direction == ListSortDirection.Ascending ? list.OrderBy(f => f.FilePath) : list.OrderByDescending(f => f.FilePath),
            _ => direction == ListSortDirection.Ascending ? list.OrderBy(f => f.FileName) : list.OrderByDescending(f => f.FileName)
        };

        foreach (var item in sorted)
            VM.OptimizeFiles.Add(item);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        VM.SaveSettings();
        base.OnClosing(e);
    }
}
