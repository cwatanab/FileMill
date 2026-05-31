using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileMill.Models;
using FileMill.Services;

namespace FileMill.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly string[] ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tif", ".tiff", ".gif", ".svg", ".bmp", ".heic", ".heif"
    ];

    private static readonly string[] OptimizeDocumentExtensions =
    [
        ".docx", ".xlsx", ".pptx"
    ];

    private readonly ImageProcessingService _processingService = new();

    // --- ファイルリスト ---
    public ObservableCollection<ImageFile> Files { get; } = [];
    public ObservableCollection<OptimizeFile> OptimizeFiles { get; } = [];

    // --- パイプライン ---
    public ObservableCollection<PipelineStep> Steps { get; } = [];

    // 固定ステップのショートカットプロパティ
    public PipelineStep? GrayscaleStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Grayscale);
    public PipelineStep? ExifAutoRotateStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.ExifAutoRotate);
    public PipelineStep? CropStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Crop);
    public PipelineStep? RotateStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Rotate);
    public PipelineStep? ResizeStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Resize);
    public PipelineStep? PaddingStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Padding);
    public PipelineStep? SharpenStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Sharpen);
    public PipelineStep? ColorAdjustStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.ColorAdjust);
    public PipelineStep? ToneCurveStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.ToneCurve);
    public PipelineStep? FormatStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.FormatConvert);
    public PipelineStep? OptimizeStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Optimize);
    public PipelineStep? PosterizeStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Posterize);
    public PipelineStep? CompositeStep => Steps.FirstOrDefault(s => s.Type == PipelineStepType.Composite);

    // --- 出力先 ---
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            _outputDirectory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputSummary));
            OnPropertyChanged(nameof(OptimizeOutputSummary));
        }
    }

    // --- ファイル名生成規則（元のファイル名の末尾に付与するサフィックス） ---
    private string _fileNameRule = "_converted";
    public string FileNameRule
    {
        get => _fileNameRule;
        set { _fileNameRule = value; OnPropertyChanged(); }
    }

    // --- 設定一般 ---
    private bool _playSoundOnComplete = true;
    public bool PlaySoundOnComplete
    {
        get => _playSoundOnComplete;
        set { _playSoundOnComplete = value; OnPropertyChanged(); }
    }

    private bool _confirmOnClear = true;
    public bool ConfirmOnClear
    {
        get => _confirmOnClear;
        set { _confirmOnClear = value; OnPropertyChanged(); }
    }

    private AppTheme _appTheme = AppTheme.Auto;
    public AppTheme AppTheme
    {
        get => _appTheme;
        set { _appTheme = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThemeModeIndex)); }
    }

    public int ThemeModeIndex
    {
        get => (int)_appTheme;
        set => AppTheme = (AppTheme)value;
    }

    private string _language = "ja";
    public string Language
    {
        get => _language;
        set { _language = value; OnPropertyChanged(); OnPropertyChanged(nameof(LanguageIndex)); }
    }

    public int LanguageIndex
    {
        get => _language == "en" ? 1 : 0;
        set => Language = value == 1 ? "en" : "ja";
    }

    // --- 外部ツール ---
    private bool _useOxipng;
    public bool UseOxipng
    {
        get => _useOxipng;
        set { _useOxipng = value; OnPropertyChanged(); }
    }

    private int _oxipngLevel = 2;
    public int OxipngLevel
    {
        get => _oxipngLevel;
        set { _oxipngLevel = value; OnPropertyChanged(); }
    }

    private bool _useJpegli;
    public bool UseJpegli
    {
        get => _useJpegli;
        set { _useJpegli = value; OnPropertyChanged(); }
    }

    private string _oxipngPath = "tools/oxipng.exe";
    public string OxipngPath
    {
        get => _oxipngPath;
        set { _oxipngPath = value; OnPropertyChanged(); }
    }

    private string _cjpegliPath = "tools/cjpegli.exe";
    public string CjpegliPath
    {
        get => _cjpegliPath;
        set { _cjpegliPath = value; OnPropertyChanged(); }
    }

    // --- 最適化一般設定 ---
    private bool _enableOfficeOptimize = true;
    public bool EnableOfficeOptimize
    {
        get => _enableOfficeOptimize;
        set { _enableOfficeOptimize = value; OnPropertyChanged(); }
    }

    private bool _enableImageOptimize = true;
    public bool EnableImageOptimize
    {
        get => _enableImageOptimize;
        set { _enableImageOptimize = value; OnPropertyChanged(); }
    }

    // --- Office最適化詳細 ---
    private bool _stripOfficeMetadata = true;
    public bool StripOfficeMetadata
    {
        get => _stripOfficeMetadata;
        set { _stripOfficeMetadata = value; OnPropertyChanged(); }
    }

    private bool _cleanUnusedObjects = true;
    public bool CleanUnusedObjects
    {
        get => _cleanUnusedObjects;
        set { _cleanUnusedObjects = value; OnPropertyChanged(); }
    }

    private bool _compressEmbeddedImages = true;
    public bool CompressEmbeddedImages
    {
        get => _compressEmbeddedImages;
        set { _compressEmbeddedImages = value; OnPropertyChanged(); }
    }

    // --- 画像最適化詳細 ---
    private bool _compressImages = true;
    public bool CompressImages
    {
        get => _compressImages;
        set { _compressImages = value; OnPropertyChanged(); }
    }

    private bool _convertToWebP;
    public bool ConvertToWebP
    {
        get => _convertToWebP;
        set { _convertToWebP = value; OnPropertyChanged(); }
    }

    private int _webpQuality = 85;
    public int WebPQuality
    {
        get => _webpQuality;
        set { _webpQuality = value; OnPropertyChanged(); }
    }

    // --- メディア最適化詳細 ---
    private bool _compressMedia = true;
    public bool CompressMedia
    {
        get => _compressMedia;
        set { _compressMedia = value; OnPropertyChanged(); }
    }

    private int _mediaVideoCrf = 23;
    public int MediaVideoCrf
    {
        get => _mediaVideoCrf;
        set { _mediaVideoCrf = value; OnPropertyChanged(); }
    }

    private string _mediaVideoCodec = "libx264";
    public string MediaVideoCodec
    {
        get => _mediaVideoCodec;
        set { _mediaVideoCodec = value; OnPropertyChanged(); OnPropertyChanged(nameof(MediaVideoCodecIndex)); }
    }

    private string _mediaAudioCodec = "aac";
    public string MediaAudioCodec
    {
        get => _mediaAudioCodec;
        set { _mediaAudioCodec = value; OnPropertyChanged(); OnPropertyChanged(nameof(MediaAudioCodecIndex)); }
    }

    public int MediaVideoCodecIndex
    {
        get => _mediaVideoCodec switch
        {
            "libx265" => 1,
            "libvpx-vp9" => 2,
            "libaom-av1" => 3,
            _ => 0
        };
        set => MediaVideoCodec = value switch
        {
            1 => "libx265",
            2 => "libvpx-vp9",
            3 => "libaom-av1",
            _ => "libx264"
        };
    }

    public int MediaAudioCodecIndex
    {
        get => _mediaAudioCodec switch
        {
            "libopus" => 1,
            "copy" => 2,
            _ => 0
        };
        set => MediaAudioCodec = value switch
        {
            1 => "libopus",
            2 => "copy",
            _ => "aac"
        };
    }

    private string _ffmpegPath = "tools/ffmpeg.exe";
    public string FfmpegPath
    {
        get => _ffmpegPath;
        set { _ffmpegPath = value; OnPropertyChanged(); }
    }

    // --- 並列処理数 ---
    private int _maxDegreeOfParallelism = 4;
    public int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        set { _maxDegreeOfParallelism = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxDegreeOfParallelismIndex)); }
    }

    public int MaxDegreeOfParallelismIndex
    {
        get => _maxDegreeOfParallelism switch
        {
            1 => 0,
            2 => 1,
            4 => 2,
            8 => 3,
            _ => 2
        };
        set
        {
            MaxDegreeOfParallelism = value switch
            {
                0 => 1,
                1 => 2,
                2 => 4,
                3 => 8,
                _ => 4
            };
        }
    }

    // --- 進捗 ---
    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set { _progressValue = value; OnPropertyChanged(); }
    }

    private int _progressMax = 100;
    public int ProgressMax
    {
        get => _progressMax;
        set { _progressMax = value; OnPropertyChanged(); }
    }

    private string _statusText = "準備完了";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (_isProcessing == value) return;
            _isProcessing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEdit));
            RefreshCommands();
        }
    }

    public bool CanEdit => !IsProcessing;

    public bool IsFileListEmpty => Files.Count == 0;
    public bool IsOptimizeFileListEmpty => OptimizeFiles.Count == 0;

    public string OutputSummary
        => $"出力先: {OutputDirectory}  /  ファイル数: {Files.Count}  /  ステップ数: {Steps.Count(s => s.Enabled)}";

    public string OptimizeOutputSummary
        => $"出力先: {OutputDirectory}  /  ファイル数: {OptimizeFiles.Count}";

    // --- 設定ウィンドウの管理 ---
    // View 側がここに SettingsWindow を開く処理を登録する
    public Action<string>? RequestOpenSettings { get; set; }

    private string _activeModalType = "";
    public string ActiveModalType
    {
        get => _activeModalType;
        set { _activeModalType = value; OnPropertyChanged(); }
    }

    private string _modalTitle = "";
    public string ModalTitle
    {
        get => _modalTitle;
        set { _modalTitle = value; OnPropertyChanged(); }
    }

    // --- デバッグ ---
    public Action<string>? OnDebugLog { get; set; }

    private bool _isDebugVisible;
    public bool IsDebugVisible
    {
        get => _isDebugVisible;
        set { _isDebugVisible = value; OnPropertyChanged(); }
    }

    public void LogDebug(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        OnDebugLog?.Invoke($"[{timestamp}] {message}");
    }

    // --- コマンド ---
    public ICommand AddFilesCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand RemoveFileCommand { get; }
    public ICommand ClearFilesCommand { get; }
    public ICommand AddStepCommand { get; }
    public ICommand RemoveStepCommand { get; }
    public ICommand MoveStepUpCommand { get; }
    public ICommand MoveStepDownCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand BrowseToolPathCommand { get; }
    public ICommand ProcessCommand { get; }
    public ICommand OpenModalCommand { get; }
    public ICommand AddOptimizeFilesCommand { get; }
    public ICommand AddOptimizeFolderCommand { get; }
    public ICommand RemoveOptimizeFileCommand { get; }
    public ICommand ClearOptimizeFilesCommand { get; }
    public ICommand ProcessOptimizeCommand { get; }
    public ICommand BrowseCompositeCommand { get; }
    public ICommand ToggleDebugCommand { get; }

    public MainViewModel(string? settingsPath = null)
    {
        AddFilesCommand = new RelayCommand(AddFiles, _ => !IsProcessing);
        AddFolderCommand = new RelayCommand(AddFolder, _ => !IsProcessing);
        RemoveFileCommand = new RelayCommand(RemoveFile, _ => !IsProcessing);
        ClearFilesCommand = new RelayCommand(ClearFiles, _ => !IsProcessing);
        AddStepCommand = new RelayCommand(AddStep, _ => !IsProcessing);
        RemoveStepCommand = new RelayCommand(RemoveStep, _ => !IsProcessing);
        MoveStepUpCommand = new RelayCommand(MoveStepUp, _ => !IsProcessing);
        MoveStepDownCommand = new RelayCommand(MoveStepDown, _ => !IsProcessing);
        BrowseOutputCommand = new RelayCommand(BrowseOutput, _ => !IsProcessing);
        BrowseToolPathCommand = new RelayCommand(BrowseToolPath, _ => !IsProcessing);
        ProcessCommand = new RelayCommand(async _ => await ProcessAsync(), _ => !IsProcessing && Files.Count > 0);
        OpenModalCommand = new RelayCommand(OpenModal, _ => !IsProcessing);
        BrowseCompositeCommand = new RelayCommand(BrowseComposite, _ => !IsProcessing);
        ToggleDebugCommand = new RelayCommand(_ => IsDebugVisible = !IsDebugVisible);

        AddOptimizeFilesCommand = new RelayCommand(AddOptimizeFiles, _ => !IsProcessing);
        AddOptimizeFolderCommand = new RelayCommand(AddOptimizeFolder, _ => !IsProcessing);
        RemoveOptimizeFileCommand = new RelayCommand(RemoveOptimizeFile, _ => !IsProcessing);
        ClearOptimizeFilesCommand = new RelayCommand(ClearOptimizeFiles, _ => !IsProcessing);
        ProcessOptimizeCommand = new RelayCommand(async _ => await ProcessOptimizeAsync(), _ => !IsProcessing && OptimizeFiles.Count > 0);

        // ファイルリスト変更時に空表示を更新
        Files.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsFileListEmpty));
            UpdateSummaries();
        };

        OptimizeFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsOptimizeFileListEmpty));
            OnPropertyChanged(nameof(OptimizeOutputSummary));
            ((RelayCommand)ProcessOptimizeCommand).RaiseCanExecuteChanged();
        };

        Steps.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (PipelineStep step in e.NewItems)
                    step.PropertyChanged += Step_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (PipelineStep step in e.OldItems)
                    step.PropertyChanged -= Step_PropertyChanged;
            }

            NotifyStepShortcutsChanged();
            UpdateSummaries();
        };

        // デフォルトのステップを追加（処理順に登録、FormatConvert/Optimize は最後に適用）
        AddDefaultStep(PipelineStepType.ExifAutoRotate, true);
        AddDefaultStep(PipelineStepType.Crop, false);
        AddDefaultStep(PipelineStepType.Rotate, false);
        AddDefaultStep(PipelineStepType.Resize, true);
        AddDefaultStep(PipelineStepType.Padding, false);
        AddDefaultStep(PipelineStepType.Grayscale, false);
        AddDefaultStep(PipelineStepType.Sharpen, false);
        AddDefaultStep(PipelineStepType.ColorAdjust, false);
        AddDefaultStep(PipelineStepType.ToneCurve, false);
        AddDefaultStep(PipelineStepType.Posterize, false);
        AddDefaultStep(PipelineStepType.Composite, false);
        AddDefaultStep(PipelineStepType.FormatConvert, true);
        AddDefaultStep(PipelineStepType.Optimize, true);

        // 設定の読み込み
        LoadSettings(settingsPath);
    }

    private void AddDefaultStep(PipelineStepType type, bool enabled)
    {
        Steps.Add(new PipelineStep { Type = type, Enabled = enabled });
    }

    private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PipelineStep.Enabled))
            UpdateSummaries();
    }

    private void NotifyStepShortcutsChanged()
    {
        OnPropertyChanged(nameof(GrayscaleStep));
        OnPropertyChanged(nameof(ExifAutoRotateStep));
        OnPropertyChanged(nameof(CropStep));
        OnPropertyChanged(nameof(RotateStep));
        OnPropertyChanged(nameof(ResizeStep));
        OnPropertyChanged(nameof(PaddingStep));
        OnPropertyChanged(nameof(SharpenStep));
        OnPropertyChanged(nameof(ColorAdjustStep));
        OnPropertyChanged(nameof(ToneCurveStep));
        OnPropertyChanged(nameof(FormatStep));
        OnPropertyChanged(nameof(OptimizeStep));
        OnPropertyChanged(nameof(PosterizeStep));
        OnPropertyChanged(nameof(CompositeStep));
    }

    // --- ファイル操作 ---
    public bool AddFileByPath(string path)
    {
        var added = false;

        if (Directory.Exists(path))
        {
            foreach (var filePath in EnumerateImageFiles(path))
                added |= AddSingleFileByPath(filePath);
        }
        else
        {
            added = AddSingleFileByPath(path);
        }

        return added;
    }

    private bool AddSingleFileByPath(string path)
    {
        if (!IsSupportedImage(path) || Files.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            return false;

        var info = ImageProcessingService.GetImageInfo(path);
        Files.Add(info);
        return true;
    }

    private static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path);
        return ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateImageFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedImage)
                .OrderBy(f => f)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void AddFiles(object? _)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "画像ファイルを選択",
            Filter = "画像ファイル|*.jpg;*.jpeg;*.png;*.webp;*.avif;*.tif;*.tiff;*.gif;*.svg;*.bmp|すべてのファイル|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
        {
            foreach (var path in dlg.FileNames)
                AddFileByPath(path);
        }
    }

    private void AddFolder(object? _)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "画像フォルダを追加",
            Multiselect = false
        };

        if (dlg.ShowDialog() == true)
        {
            AddFileByPath(dlg.FolderName);
        }
    }

    private void RemoveFile(object? parameter)
    {
        if (parameter is ImageFile file)
            Files.Remove(file);
    }

    private void ClearFiles(object? _)
    {
        if (ConfirmOnClear)
        {
            var result = MessageBox.Show("リストからすべての画像ファイルを削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }
        Files.Clear();
    }

    // --- パイプライン操作 ---
    private void AddStep(object? parameter)
    {
        var type = parameter switch
        {
            string s => Enum.Parse<PipelineStepType>(s),
            PipelineStepType t => t,
            _ => PipelineStepType.FormatConvert
        };

        Steps.Add(new PipelineStep { Type = type });
        UpdateSummaries();
    }

    private void RemoveStep(object? parameter)
    {
        if (parameter is PipelineStep step)
            Steps.Remove(step);
        UpdateSummaries();
    }

    private void MoveStepUp(object? parameter)
    {
        if (parameter is PipelineStep step)
        {
            int idx = Steps.IndexOf(step);
            if (idx > 0)
                Steps.Move(idx, idx - 1);
        }
    }

    private void MoveStepDown(object? parameter)
    {
        if (parameter is PipelineStep step)
        {
            int idx = Steps.IndexOf(step);
            if (idx < Steps.Count - 1)
                Steps.Move(idx, idx + 1);
        }
    }

    // --- 出力先 ---
    private void BrowseOutput(object? _)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "出力先フォルダを選択"
        };

        if (dlg.ShowDialog() == true)
        {
            OutputDirectory = dlg.FolderName;
        }
    }

    private void BrowseToolPath(object? parameter)
    {
        if (parameter is not string toolName)
            return;

        var currentPath = toolName switch
        {
            "oxipng" => OxipngPath,
            "cjpegli" => CjpegliPath,
            "ffmpeg" => FfmpegPath,
            _ => ""
        };

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"{toolName} の実行ファイルを選択",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
            FileName = Path.GetFileName(currentPath)
        };

        var currentDirectory = Path.GetDirectoryName(currentPath);
        if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            dlg.InitialDirectory = currentDirectory;

        if (dlg.ShowDialog() != true)
            return;

        switch (toolName)
        {
            case "ffmpeg":
                FfmpegPath = dlg.FileName;
                break;
            case "oxipng":
                OxipngPath = dlg.FileName;
                break;
            case "cjpegli":
                CjpegliPath = dlg.FileName;
                break;
        }
    }

    private void BrowseComposite(object? _)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "合成用の画像ファイルを選択",
            Filter = "画像ファイル|*.jpg;*.jpeg;*.png;*.webp;*.bmp|すべてのファイル|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            if (CompositeStep != null)
            {
                CompositeStep.CompositePath = dlg.FileName;
            }
        }
    }

    // --- バッチ処理 ---
    private async Task ProcessAsync()
    {
        var targets = Files.Where(f => f.IsChecked).ToList();
        if (targets.Count == 0)
        {
            StatusText = "選択されたファイルがありません";
            return;
        }

        var enabledSteps = Steps.Where(s => s.Enabled).ToList();
        if (enabledSteps.Count == 0)
        {
            StatusText = "処理オプションが選択されていません";
            return;
        }

        IsProcessing = true;
        ProgressValue = 0;
        ProgressMax = targets.Count;

        LogDebug($"変換開始: {targets.Count} ファイル, 有効ステップ: {string.Join(", ", enabledSteps.Select(s => s.DisplayName))}");

        var formatStep = enabledSteps.LastOrDefault(s => s.Type == PipelineStepType.FormatConvert);

        int success = 0;
        int errors = 0;

        try
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var file = targets[i];
                    try
                    {
                        // FormatConvert が有効なら指定形式、無効なら元ファイルと同じ拡張子
                        var ext = formatStep != null
                            ? ImageProcessingService.GetExtension(formatStep.TargetFormat)
                            : Path.GetExtension(file.FilePath);

                        var baseName = Path.GetFileNameWithoutExtension(file.FilePath);
                        var newBaseName = baseName + FileNameRule;

                        var fileName = newBaseName + ext;
                        var outputPath = Path.Combine(OutputDirectory, fileName);

                        int counter = 1;
                        while (File.Exists(outputPath))
                        {
                            outputPath = Path.Combine(OutputDirectory,
                                $"{newBaseName}_{counter}{ext}");
                            counter++;
                        }

                        _processingService.Process(file.FilePath, outputPath, Steps, UseOxipng, OxipngPath, OxipngLevel, UseJpegli, CjpegliPath, logAction: LogDebug);
                        success++;
                        LogDebug($"OK  {file.FileName} → {Path.GetFileName(outputPath)}");

                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            StatusText = $"処理中... ({i + 1}/{targets.Count}) {file.FileName}";
                            ProgressValue = i + 1;
                        });
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        LogDebug($"ERR {file.FileName}: {ex.Message}");

                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            StatusText = $"エラー: {file.FileName} - {ex.Message}";
                        });
                    }
                }
            });
        }
        finally
        {
            IsProcessing = false;
            StatusText = $"完了: {success} 成功, {errors} 失敗";
            LogDebug($"変換完了: {success} 成功, {errors} 失敗");
            if (PlaySoundOnComplete)
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }
    }

    private void RefreshCommands()
    {
        ((RelayCommand)AddFilesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveFileCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearFilesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddStepCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveStepCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MoveStepUpCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MoveStepDownCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BrowseOutputCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenModalCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddOptimizeFilesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddOptimizeFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveOptimizeFileCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearOptimizeFilesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ProcessOptimizeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BrowseCompositeCommand).RaiseCanExecuteChanged();
    }

    private void UpdateSummaries()
    {
        OnPropertyChanged(nameof(OutputSummary));
        ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
    }

    private void OpenModal(object? parameter)
    {
        if (parameter is string type)
        {
            ActiveModalType = type;
            ModalTitle = type switch
            {
                "Grayscale" => "グレースケール化設定",
                "ExifAutoRotate" => "Exif自動回転設定",
                "Crop" => "トリミング設定",
                "Resize" => "リサイズ設定",
                "Padding" => "余白の追加設定",
                "Sharpen" => "アンシャープマスク設定",
                "ColorAdjust" => "色調補正設定",
                "ToneCurve" => "トーンカーブ設定",
                "Format" => "フォーマット変換設定",
                "Optimize" => "最適化設定",
                "Posterize" => "減色設定",
                "Rotate" => "回転",
                "Composite" => "画像合成設定",
                "OfficeOptimize" => "Office最適化設定",
                "ImageOptimize" => "画像最適化設定",
                "MediaOptimize" => "メディア最適化設定",
                "Options" => "オプション設定",
                _ => "設定"
            };
            RequestOpenSettings?.Invoke(type);
        }
    }

    // --- ファイル最適化機能用メソッド ---
    public void AddOptimizeFileByPath(string path)
    {
        if (!IsSupportedOptimizeFile(path))
            return;

        if (!OptimizeFiles.Any(f => f.FilePath == path))
        {
            var fileInfo = new FileInfo(path);
            var info = new OptimizeFile
            {
                FilePath = path,
                OriginalSize = fileInfo.Length,
                Status = "待機中"
            };
            OptimizeFiles.Add(info);
        }
    }

    private static bool IsSupportedOptimizeFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return OptimizeDocumentExtensions.Contains(ext);
    }

    private void AddOptimizeFiles(object? _)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "ファイルを選択 (Office)",
            Filter = "Office ファイル|*.docx;*.xlsx;*.pptx|すべてのファイル|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
        {
            foreach (var path in dlg.FileNames)
                AddOptimizeFileByPath(path);
        }
    }

    private void AddOptimizeFolder(object? _)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "フォルダを追加",
            Multiselect = false
        };

        if (dlg.ShowDialog() == true)
        {
            var dir = dlg.FolderName;
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir)
                    .Where(IsSupportedOptimizeFile)
                    .OrderBy(f => f);
                foreach (var path in files)
                    AddOptimizeFileByPath(path);
            }
        }
    }

    private void RemoveOptimizeFile(object? parameter)
    {
        if (parameter is OptimizeFile file)
            OptimizeFiles.Remove(file);
    }

    private void ClearOptimizeFiles(object? _)
    {
        if (ConfirmOnClear)
        {
            var result = MessageBox.Show("リストからすべてのファイルを削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }
        OptimizeFiles.Clear();
    }

    private async Task ProcessOptimizeAsync()
    {
        var targets = OptimizeFiles.Where(f => f.IsChecked).ToList();
        if (targets.Count == 0)
        {
            StatusText = "選択されたファイルがありません";
            return;
        }

        LogDebug($"最適化開始: {targets.Count} ファイル");

        IsProcessing = true;
        ProgressValue = 0;
        ProgressMax = targets.Count;

        int success = 0;
        int errors = 0;
        int processed = 0;

        // UI スレッドから読み取るオプションをローカル変数にキャプチャ
        var enableOfficeOptimize = EnableOfficeOptimize;
        var enableImageOptimize = EnableImageOptimize;
        var stripOfficeMetadata = StripOfficeMetadata;
        var cleanUnusedObjects = CleanUnusedObjects;
        var compressEmbeddedImages = CompressEmbeddedImages;
        var compressImages = CompressImages;
        var convertToWebP = ConvertToWebP;
        var webPQuality = WebPQuality;
        var compressMedia = CompressMedia;
        var ffmpegPath = FfmpegPath;
        var mediaVideoCrf = MediaVideoCrf;
        var mediaVideoCodec = MediaVideoCodec;
        var mediaAudioCodec = MediaAudioCodec;
        var outputDirectory = OutputDirectory;
        var maxParallel = MaxDegreeOfParallelism;
        var useOxipng = UseOxipng;
        var oxipngLevel = OxipngLevel;
        var useJpegli = UseJpegli;
        var oxipngPath = OxipngPath;
        var cjpegliPath = CjpegliPath;

        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tif", ".tiff", ".bmp" };
        var reservedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workItems = targets.Select(file =>
        {
            var originalPath = file.FilePath;
            var ext = Path.GetExtension(originalPath);
            var isWebPConversion = enableImageOptimize && convertToWebP && imageExtensions.Contains(ext.ToLowerInvariant());
            var outputExtension = isWebPConversion ? ".webp" : Path.GetExtension(originalPath);
            var targetPath = GetUniqueSuffixedPath(originalPath, "_optimized", outputExtension, outputDirectory, reservedOutputPaths);
            reservedOutputPaths.Add(targetPath);
            return new
            {
                File = file,
                OriginalPath = originalPath,
                Extension = ext,
                TargetPath = targetPath
            };
        }).ToList();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallel > 0 ? maxParallel : Environment.ProcessorCount
        };

        try
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(workItems, parallelOptions, item =>
                {
                    var file = item.File;
                    file.Status = "処理中";
                    file.IsProcessing = true;
                    string? tempPath = null;
                    try
                    {
                        var originalPath = item.OriginalPath;
                        LogDebug($"処理開始: {file.FileName}");
                        var ext = item.Extension;
                        var targetPath = item.TargetPath;
                        tempPath = targetPath + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";

                        var isOpenXml = ext == ".docx" || ext == ".xlsx" || ext == ".pptx";
                        var passStrip = isOpenXml && enableOfficeOptimize && stripOfficeMetadata;
                        var passClean = isOpenXml && enableOfficeOptimize && cleanUnusedObjects;
                        var shouldCompressImages = isOpenXml
                            ? (enableOfficeOptimize && compressEmbeddedImages)
                            : (enableImageOptimize && compressImages);
                        var passConvertToWebP = isOpenXml
                            ? (enableOfficeOptimize && convertToWebP)
                            : (enableImageOptimize && convertToWebP);
                        var passCompressMedia = isOpenXml && compressMedia;

                        file.Status = isOpenXml ? "パッケージ最適化中" : "処理中";
                        long optimizedSize = _processingService.Optimize(
                            originalPath, 
                            tempPath, 
                            passStrip, 
                            passClean, 
                            shouldCompressImages, 
                            passConvertToWebP, 
                            webPQuality,
                            passCompressMedia,
                            ffmpegPath,
                            mediaVideoCrf,
                            mediaVideoCodec,
                            mediaAudioCodec,
                            useOxipng,
                            oxipngPath,
                            oxipngLevel,
                            useJpegli,
                            cjpegliPath,
                            logAction: LogDebug);

                        int retries = 5;
                        while (retries > 0)
                        {
                            try
                            {
                                File.Move(tempPath, targetPath);
                                break;
                            }
                            catch (IOException)
                            {
                                retries--;
                                if (retries == 0) throw;
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                Thread.Sleep(100);
                            }
                        }

                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            file.FilePath = targetPath;
                        });

                        tempPath = null; // 正常完了したのでクリア

                        file.OptimizedSize = optimizedSize;
                        file.Status = "完了";
                        LogDebug($"OK  {file.FileName} ({file.OriginalSize} -> {optimizedSize} bytes)");
                        Interlocked.Increment(ref success);
                    }
                    catch (Exception ex)
                    {
                        file.Status = "エラー";
                        Interlocked.Increment(ref errors);
                        LogDebug($"ERR {file.FileName}: {ex.Message}");
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            StatusText = $"エラー: {file.FileName} - {ex.Message}";
                        });

                        // 残った一時ファイルがあればクリーンアップ
                        if (tempPath != null && File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch {}
                        }
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref processed);
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            StatusText = $"最適化中... ({current}/{targets.Count})";
                            ProgressValue = current;
                        });
                        file.IsProcessing = false;
                    }
                }
                );
            });

            // 並列処理完了後に1回だけGC
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        finally
        {
            IsProcessing = false;
            StatusText = $"最適化完了: {success} 成功, {errors} 失敗";
            LogDebug(StatusText);
            if (PlaySoundOnComplete)
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }
    }

    private static string GetUniqueSuffixedPath(string originalPath, string suffix, string outputExtension, string outputDirectory, ISet<string>? reservedPaths = null)
    {
        var directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(originalPath) ?? ""
            : outputDirectory;
        var fileName = Path.GetFileNameWithoutExtension(originalPath);
        var extension = string.IsNullOrWhiteSpace(outputExtension) ? Path.GetExtension(originalPath) : outputExtension;
        var candidate = Path.Combine(directory, fileName + suffix + extension);

        var counter = 1;
        while (File.Exists(candidate) || (reservedPaths?.Contains(candidate) ?? false))
        {
            candidate = Path.Combine(directory, $"{fileName}{suffix}_{counter}{extension}");
            counter++;
        }

        return candidate;
    }

    // --- Window Size and Position and State ---
    private double _windowWidth = 1000;
    public double WindowWidth
    {
        get => _windowWidth;
        set { _windowWidth = value; OnPropertyChanged(); }
    }

    private double _windowHeight = 720;
    public double WindowHeight
    {
        get => _windowHeight;
        set { _windowHeight = value; OnPropertyChanged(); }
    }

    private double _windowLeft = double.NaN;
    public double WindowLeft
    {
        get => _windowLeft;
        set { _windowLeft = value; OnPropertyChanged(); }
    }

    private double _windowTop = double.NaN;
    public double WindowTop
    {
        get => _windowTop;
        set { _windowTop = value; OnPropertyChanged(); }
    }

    private WindowState _windowState = WindowState.Normal;
    public WindowState WindowState
    {
        get => _windowState;
        set { _windowState = value; OnPropertyChanged(); }
    }

    private int _selectedTabIndex = 0;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    private static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "FileMill", "settings.ini");
    }

    public void LoadSettings(string? customPath = null)
    {
        try
        {
            var path = customPath ?? GetSettingsFilePath();
            if (!File.Exists(path))
            {
                return;
            }

            var data = SettingsService.Load(path);

            if (data.TryGetValue("General", out var general))
            {
                if (general.TryGetValue("OutputDirectory", out var outDir) && !string.IsNullOrWhiteSpace(outDir))
                    OutputDirectory = outDir;
                if (general.TryGetValue("FileNameRule", out var fnRule))
                    FileNameRule = fnRule;
                if (general.TryGetValue("ThemeMode", out var themeVal) && Enum.TryParse<AppTheme>(themeVal, out var tm))
                    AppTheme = tm;
                if (general.TryGetValue("Language", out var langVal) && !string.IsNullOrWhiteSpace(langVal))
                    Language = langVal;
                if (general.TryGetValue("ConfirmOnClear", out var val))
                    ConfirmOnClear = bool.TryParse(val, out var b) ? b : ConfirmOnClear;
                if (general.TryGetValue("PlaySoundOnComplete", out val))
                    PlaySoundOnComplete = bool.TryParse(val, out var b) ? b : PlaySoundOnComplete;
                if (general.TryGetValue("MaxDegreeOfParallelism", out val))
                    MaxDegreeOfParallelism = int.TryParse(val, out var i) ? i : MaxDegreeOfParallelism;
                if (general.TryGetValue("WindowWidth", out val) && double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dw))
                    WindowWidth = dw;
                if (general.TryGetValue("WindowHeight", out val) && double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dh))
                    WindowHeight = dh;
                if (general.TryGetValue("WindowLeft", out val))
                    WindowLeft = double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dl) ? dl : double.NaN;
                if (general.TryGetValue("WindowTop", out val))
                    WindowTop = double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dt) ? dt : double.NaN;
                if (general.TryGetValue("WindowState", out val) && Enum.TryParse<WindowState>(val, out var ws))
                    WindowState = ws == WindowState.Minimized ? WindowState.Normal : ws;
                if (general.TryGetValue("SelectedTabIndex", out val) && int.TryParse(val, out var idx))
                    SelectedTabIndex = idx;
            }

            if (data.TryGetValue("File", out var opt))
            {
                if (opt.TryGetValue("EnableOfficeOptimize", out var val))
                    EnableOfficeOptimize = bool.TryParse(val, out var b) ? b : EnableOfficeOptimize;
                if (opt.TryGetValue("EnableImageOptimize", out val))
                    EnableImageOptimize = bool.TryParse(val, out var b) ? b : EnableImageOptimize;
                if (opt.TryGetValue("StripOfficeMetadata", out val))
                    StripOfficeMetadata = bool.TryParse(val, out var b) ? b : StripOfficeMetadata;
                if (opt.TryGetValue("CleanUnusedObjects", out val))
                    CleanUnusedObjects = bool.TryParse(val, out var b) ? b : CleanUnusedObjects;
                if (opt.TryGetValue("CompressImages", out val))
                    CompressImages = bool.TryParse(val, out var b) ? b : CompressImages;
                if (opt.TryGetValue("ConvertToWebP", out val))
                    ConvertToWebP = bool.TryParse(val, out var b) ? b : ConvertToWebP;
                if (opt.TryGetValue("WebPQuality", out val))
                    WebPQuality = int.TryParse(val, out var i) ? i : WebPQuality;
                if (opt.TryGetValue("CompressEmbeddedImages", out val))
                    CompressEmbeddedImages = bool.TryParse(val, out var b) ? b : CompressEmbeddedImages;
                if (opt.TryGetValue("CompressMedia", out val))
                    CompressMedia = bool.TryParse(val, out var b) ? b : CompressMedia;
                if (opt.TryGetValue("MediaVideoCrf", out val))
                    MediaVideoCrf = int.TryParse(val, out var i) ? i : MediaVideoCrf;
                if (opt.TryGetValue("MediaVideoCodec", out var codec))
                    MediaVideoCodec = codec;
                if (opt.TryGetValue("MediaAudioCodec", out codec))
                    MediaAudioCodec = codec;
                if (opt.TryGetValue("FfmpegPath", out var fpath))
                    FfmpegPath = fpath;
                if (opt.TryGetValue("OxipngPath", out var pathVal))
                    OxipngPath = pathVal;
                if (opt.TryGetValue("UseOxipng", out val))
                    UseOxipng = bool.TryParse(val, out var b) ? b : UseOxipng;
                if (opt.TryGetValue("OxipngLevel", out val))
                    OxipngLevel = int.TryParse(val, out var i) ? i : OxipngLevel;
                if (opt.TryGetValue("UseJpegli", out val))
                    UseJpegli = bool.TryParse(val, out var b) ? b : UseJpegli;
                if (opt.TryGetValue("CjpegliPath", out var pathVal2))
                    CjpegliPath = pathVal2;

            }

            // 保存された有効ステップのプロパティをデフォルトステップに上書き適用
            if (data.TryGetValue("Image", out var pipeline) && pipeline.TryGetValue("Enabled", out var enabledList))
            {
                // 一旦全ステップを無効化（保存された有効ステップだけ再有効化する）
                foreach (var step in Steps)
                    step.Enabled = false;

                foreach (var typeName in enabledList.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Enum.TryParse<PipelineStepType>(typeName.Trim(), out var type) &&
                        data.TryGetValue("Image." + typeName.Trim(), out var stepData))
                    {
                        var step = Steps.FirstOrDefault(s => s.Type == type);
                        if (step != null)
                        {
                            step.Enabled = true;
                            LoadStepProperties(step, stepData);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to load settings: {ex.Message}");
        }
    }

    /// <summary>
    /// ステップ種別ごとに必要なプロパティだけを保存用ディクショナリに追加する。
    /// </summary>
    private static void SaveStepProperties(PipelineStep step, Dictionary<string, string> data)
    {
        switch (step.Type)
        {
            case PipelineStepType.Crop:
                data["CropWidth"] = step.CropWidth.ToString();
                data["CropHeight"] = step.CropHeight.ToString();
                break;
            case PipelineStepType.Rotate:
                data["RotateTarget"] = step.RotateTarget.ToString();
                data["RotationDegrees"] = step.RotationDegrees.ToString();
                break;
            case PipelineStepType.Resize:
                data["TargetWidth"] = step.TargetWidth.ToString();
                data["TargetHeight"] = step.TargetHeight.ToString();
                data["FitMode"] = step.FitMode.ToString();
                data["AllowUpscale"] = step.AllowUpscale.ToString();
                break;
            case PipelineStepType.Padding:
                data["PaddingSize"] = step.PaddingSize.ToString();
                data["PaddingRed"] = step.PaddingRed.ToString();
                data["PaddingGreen"] = step.PaddingGreen.ToString();
                data["PaddingBlue"] = step.PaddingBlue.ToString();
                break;
            case PipelineStepType.Sharpen:
                data["SharpenSigma"] = step.SharpenSigma.ToString(System.Globalization.CultureInfo.InvariantCulture);
                break;
            case PipelineStepType.ColorAdjust:
                data["Brightness"] = step.Brightness.ToString();
                data["Contrast"] = step.Contrast.ToString();
                break;
            case PipelineStepType.ToneCurve:
                data["ToneGamma"] = step.ToneGamma.ToString(System.Globalization.CultureInfo.InvariantCulture);
                break;
            case PipelineStepType.FormatConvert:
                data["TargetFormat"] = step.TargetFormat.ToString();
                data["Quality"] = step.Quality.ToString();
                data["CompressionLevel"] = step.CompressionLevel.ToString();
                break;
            case PipelineStepType.Optimize:
                data["StripMetadata"] = step.StripMetadata.ToString();
                data["OptimizeCoding"] = step.OptimizeCoding.ToString();
                data["TrellisQuant"] = step.TrellisQuant.ToString();
                data["ReductionEffort"] = step.ReductionEffort.ToString();
                data["Lossless"] = step.Lossless.ToString();
                break;
            case PipelineStepType.Posterize:
                data["BitsPerChannel"] = step.BitsPerChannel.ToString();
                break;
            case PipelineStepType.Composite:
                data["CompositePath"] = step.CompositePath ?? "";
                data["CompositeX"] = step.CompositeX.ToString();
                data["CompositeY"] = step.CompositeY.ToString();
                break;
            // Grayscale, ExifAutoRotate: 追加プロパティなし
        }
    }

    /// <summary>
    /// 保存データから読み取ったプロパティを、ステップ種別に応じて適用する。
    /// </summary>
    private static void LoadStepProperties(PipelineStep step, Dictionary<string, string> data)
    {
        string? v;
        switch (step.Type)
        {
            case PipelineStepType.Crop:
                if (data.TryGetValue("CropWidth", out v)) step.CropWidth = int.TryParse(v, out var i) ? i : step.CropWidth;
                if (data.TryGetValue("CropHeight", out v)) step.CropHeight = int.TryParse(v, out var i) ? i : step.CropHeight;
                break;
            case PipelineStepType.Rotate:
                if (data.TryGetValue("RotateTarget", out v)) step.RotateTarget = Enum.TryParse<RotateTarget>(v, out var r) ? r : step.RotateTarget;
                if (data.TryGetValue("RotationDegrees", out v)) step.RotationDegrees = int.TryParse(v, out var i) ? i : step.RotationDegrees;
                break;
            case PipelineStepType.Resize:
                if (data.TryGetValue("TargetWidth", out v)) step.TargetWidth = int.TryParse(v, out var i) ? i : step.TargetWidth;
                if (data.TryGetValue("TargetHeight", out v)) step.TargetHeight = int.TryParse(v, out var i) ? i : step.TargetHeight;
                if (data.TryGetValue("FitMode", out v)) step.FitMode = Enum.TryParse<FitMode>(v, out var fm) ? fm : step.FitMode;
                if (data.TryGetValue("AllowUpscale", out v)) step.AllowUpscale = bool.TryParse(v, out var b) ? b : step.AllowUpscale;
                break;
            case PipelineStepType.Padding:
                if (data.TryGetValue("PaddingSize", out v)) step.PaddingSize = int.TryParse(v, out var i) ? i : step.PaddingSize;
                if (data.TryGetValue("PaddingRed", out v)) step.PaddingRed = int.TryParse(v, out var i) ? i : step.PaddingRed;
                if (data.TryGetValue("PaddingGreen", out v)) step.PaddingGreen = int.TryParse(v, out var i) ? i : step.PaddingGreen;
                if (data.TryGetValue("PaddingBlue", out v)) step.PaddingBlue = int.TryParse(v, out var i) ? i : step.PaddingBlue;
                break;
            case PipelineStepType.Sharpen:
                if (data.TryGetValue("SharpenSigma", out v)) step.SharpenSigma = double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : step.SharpenSigma;
                break;
            case PipelineStepType.ColorAdjust:
                if (data.TryGetValue("Brightness", out v)) step.Brightness = int.TryParse(v, out var i) ? i : step.Brightness;
                if (data.TryGetValue("Contrast", out v)) step.Contrast = int.TryParse(v, out var i) ? i : step.Contrast;
                break;
            case PipelineStepType.ToneCurve:
                if (data.TryGetValue("ToneGamma", out v)) step.ToneGamma = double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : step.ToneGamma;
                break;
            case PipelineStepType.FormatConvert:
                if (data.TryGetValue("TargetFormat", out v)) step.TargetFormat = Enum.TryParse<OutputFormat>(v, out var f) ? f : step.TargetFormat;
                if (data.TryGetValue("Quality", out v)) step.Quality = int.TryParse(v, out var i) ? i : step.Quality;
                if (data.TryGetValue("CompressionLevel", out v)) step.CompressionLevel = int.TryParse(v, out var i) ? i : step.CompressionLevel;
                break;
            case PipelineStepType.Optimize:
                if (data.TryGetValue("StripMetadata", out v)) step.StripMetadata = bool.TryParse(v, out var b) ? b : step.StripMetadata;
                if (data.TryGetValue("OptimizeCoding", out v)) step.OptimizeCoding = bool.TryParse(v, out var b) ? b : step.OptimizeCoding;
                if (data.TryGetValue("TrellisQuant", out v)) step.TrellisQuant = bool.TryParse(v, out var b) ? b : step.TrellisQuant;
                if (data.TryGetValue("ReductionEffort", out v)) step.ReductionEffort = int.TryParse(v, out var i) ? i : step.ReductionEffort;
                if (data.TryGetValue("Lossless", out v)) step.Lossless = bool.TryParse(v, out var b) ? b : step.Lossless;
                break;
            case PipelineStepType.Posterize:
                if (data.TryGetValue("BitsPerChannel", out v)) step.BitsPerChannel = int.TryParse(v, out var i) ? i : step.BitsPerChannel;
                break;
            case PipelineStepType.Composite:
                if (data.TryGetValue("CompositePath", out v)) step.CompositePath = v;
                if (data.TryGetValue("CompositeX", out v)) step.CompositeX = int.TryParse(v, out var i) ? i : step.CompositeX;
                if (data.TryGetValue("CompositeY", out v)) step.CompositeY = int.TryParse(v, out var i) ? i : step.CompositeY;
                break;
            // Grayscale, ExifAutoRotate: 追加プロパティなし
        }
    }

    public void SaveSettings(string? customPath = null)
    {
        try
        {
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            var general = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OutputDirectory"] = OutputDirectory ?? "",
                ["FileNameRule"] = FileNameRule ?? "",
                ["ThemeMode"] = AppTheme.ToString(),
                ["Language"] = Language,
                ["ConfirmOnClear"] = ConfirmOnClear.ToString(),
                ["PlaySoundOnComplete"] = PlaySoundOnComplete.ToString(),
                ["MaxDegreeOfParallelism"] = MaxDegreeOfParallelism.ToString(),
                ["WindowWidth"] = WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["WindowHeight"] = WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["WindowLeft"] = WindowLeft.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["WindowTop"] = WindowTop.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["WindowState"] = (WindowState == WindowState.Minimized ? WindowState.Normal : WindowState).ToString(),
                ["SelectedTabIndex"] = SelectedTabIndex.ToString()
            };
            data["General"] = general;

            var opt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EnableOfficeOptimize"] = EnableOfficeOptimize.ToString(),
                ["EnableImageOptimize"] = EnableImageOptimize.ToString(),
                ["StripOfficeMetadata"] = StripOfficeMetadata.ToString(),
                ["CleanUnusedObjects"] = CleanUnusedObjects.ToString(),
                ["CompressImages"] = CompressImages.ToString(),
                ["ConvertToWebP"] = ConvertToWebP.ToString(),
                ["WebPQuality"] = WebPQuality.ToString(),
                ["CompressEmbeddedImages"] = CompressEmbeddedImages.ToString(),
                ["CompressMedia"] = CompressMedia.ToString(),
                ["MediaVideoCrf"] = MediaVideoCrf.ToString(),
                ["MediaVideoCodec"] = MediaVideoCodec ?? "",
                ["MediaAudioCodec"] = MediaAudioCodec ?? "",
                ["FfmpegPath"] = FfmpegPath ?? "",
                ["OxipngPath"] = OxipngPath ?? "",
                ["UseOxipng"] = UseOxipng.ToString(),
                ["OxipngLevel"] = OxipngLevel.ToString(),
                ["UseJpegli"] = UseJpegli.ToString(),
                ["CjpegliPath"] = CjpegliPath ?? ""
            };
            data["File"] = opt;

            // 有効ステップのみ種別をセクション名にして保存
            var enabledSteps = Steps.Where(s => s.Enabled).ToList();
            var pipeline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Enabled"] = string.Join(",", enabledSteps.Select(s => s.Type.ToString()))
            };
            data["Image"] = pipeline;

            foreach (var step in enabledSteps)
            {
                var stepData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                SaveStepProperties(step, stepData);
                data["Image." + step.Type.ToString()] = stepData;
            }

            var path = customPath ?? GetSettingsFilePath();
            SettingsService.Save(path, data);
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to save settings: {ex.Message}");
        }
    }

    // --- INotifyPropertyChanged ---
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
