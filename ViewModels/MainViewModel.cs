using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileMill.Helpers;
using FileMill.Models;
using FileMill.Services;
using static FileMill.Helpers.DialogHelper;
using static FileMill.Helpers.SettingsValueReader;

namespace FileMill.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    internal static readonly string[] ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tif", ".tiff", ".gif", ".svg", ".bmp", ".heic", ".heif"
    ];

    private static readonly string[] OptimizeDocumentExtensions =
    [
        ".docx", ".xlsx", ".pptx"
    ];

    private static readonly string[] PdfExtensions =
    [
        ".pdf"
    ];

    private readonly ImageProcessingService _processingService = new();
    private readonly PdfOptimizationService _pdfOptimizationService = new();
    private CancellationTokenSource? _cts;
    private bool _suppressNotifications;
    private static readonly string[] StepShortcutPropertyNames =
    [
        nameof(GrayscaleStep),
        nameof(ExifAutoRotateStep),
        nameof(CropStep),
        nameof(RotateStep),
        nameof(ResizeStep),
        nameof(PaddingStep),
        nameof(SharpenStep),
        nameof(ColorAdjustStep),
        nameof(ToneCurveStep),
        nameof(FormatStep),
        nameof(OptimizeStep),
        nameof(PosterizeStep),
        nameof(CompositeStep)
    ];
    private static readonly (PipelineStepType Type, bool Enabled)[] DefaultSteps =
    [
        (PipelineStepType.ExifAutoRotate, true),
        (PipelineStepType.Crop, false),
        (PipelineStepType.Rotate, false),
        (PipelineStepType.Resize, true),
        (PipelineStepType.Padding, false),
        (PipelineStepType.Grayscale, false),
        (PipelineStepType.Sharpen, false),
        (PipelineStepType.ColorAdjust, false),
        (PipelineStepType.ToneCurve, false),
        (PipelineStepType.Posterize, false),
        (PipelineStepType.Composite, false),
        (PipelineStepType.FormatConvert, true),
        (PipelineStepType.Optimize, true)
    ];

    // --- ファイルリスト ---
    public ObservableCollection<ImageFile> Files { get; } = [];
    public ObservableCollection<OptimizeFile> OptimizeFiles { get; } = [];
    public ObservableCollection<OptimizeFile> PdfFiles { get; } = [];

    // --- パイプライン ---
    public ObservableCollection<PipelineStep> Steps { get; } = [];
    public ObservableCollection<string> ImagePresetNames { get; } = [];
    public ObservableCollection<string> OfficePresetNames { get; } = [];
    public ObservableCollection<string> PdfPresetNames { get; } = [];

    // 固定ステップのショートカットプロパティ
    public PipelineStep? GrayscaleStep => GetStep(PipelineStepType.Grayscale);
    public PipelineStep? ExifAutoRotateStep => GetStep(PipelineStepType.ExifAutoRotate);
    public PipelineStep? CropStep => GetStep(PipelineStepType.Crop);
    public PipelineStep? RotateStep => GetStep(PipelineStepType.Rotate);
    public PipelineStep? ResizeStep => GetStep(PipelineStepType.Resize);
    public PipelineStep? PaddingStep => GetStep(PipelineStepType.Padding);
    public PipelineStep? SharpenStep => GetStep(PipelineStepType.Sharpen);
    public PipelineStep? ColorAdjustStep => GetStep(PipelineStepType.ColorAdjust);
    public PipelineStep? ToneCurveStep => GetStep(PipelineStepType.ToneCurve);
    public PipelineStep? FormatStep => GetStep(PipelineStepType.FormatConvert);
    public PipelineStep? OptimizeStep => GetStep(PipelineStepType.Optimize);
    public PipelineStep? PosterizeStep => GetStep(PipelineStepType.Posterize);
    public PipelineStep? CompositeStep => GetStep(PipelineStepType.Composite);

    private PipelineStep? GetStep(PipelineStepType type)
        => Steps.FirstOrDefault(s => s.Type == type);

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
            OnPropertyChanged(nameof(PdfOutputSummary));
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

    private string _qpdfPath = "tools/qpdf.exe";
    public string QpdfPath
    {
        get => _qpdfPath;
        set { _qpdfPath = value; OnPropertyChanged(); }
    }

    // --- 最適化一般設定 ---
    private bool _enableOfficeOptimize = true;
    public bool EnableOfficeOptimize
    {
        get => _enableOfficeOptimize;
        set { _enableOfficeOptimize = value; OnPropertyChanged(); }
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

    private bool _resetCellSelection;
    public bool ResetCellSelection
    {
        get => _resetCellSelection;
        set { _resetCellSelection = value; OnPropertyChanged(); }
    }

    private bool _compressEmbeddedImages = true;
    public bool CompressEmbeddedImages
    {
        get => _compressEmbeddedImages;
        set { _compressEmbeddedImages = value; OnPropertyChanged(); }
    }

    private bool _resizeEmbeddedImagesByPpi;
    public bool ResizeEmbeddedImagesByPpi
    {
        get => _resizeEmbeddedImagesByPpi;
        set { _resizeEmbeddedImagesByPpi = value; OnPropertyChanged(); }
    }

    private int _targetImagePpi = 220;
    public int TargetImagePpi
    {
        get => _targetImagePpi;
        set
        {
            var next = Math.Clamp(value, 72, 600);
            if (_targetImagePpi == next) return;
            _targetImagePpi = next;
            OnPropertyChanged();
        }
    }

    private bool _convertToWebP;
    public bool ConvertToWebP
    {
        get => _convertToWebP;
        set { _convertToWebP = value; OnPropertyChanged(); }
    }

    private bool _convertOfficeToPdf;
    public bool ConvertOfficeToPdf
    {
        get => _convertOfficeToPdf && IsOfficePdfConversionAvailable;
        set
        {
            var next = value && IsOfficePdfConversionAvailable;
            if (_convertOfficeToPdf == next) return;
            _convertOfficeToPdf = next;
            OnPropertyChanged();
        }
    }

    private bool _convertOfficeToPdfA;
    public bool ConvertOfficeToPdfA
    {
        get => _convertOfficeToPdfA;
        set { _convertOfficeToPdfA = value; OnPropertyChanged(); }
    }

    public bool IsOfficePdfConversionAvailable => ImageProcessingService.IsOfficePdfConversionAvailable;
    public string OfficePdfConversionToolTip => ImageProcessingService.OfficePdfConversionAvailabilityMessage;

    // --- PDF変換詳細 ---
    private bool _pdfOptimizeImages;
    public bool PdfOptimizeImages
    {
        get => _pdfOptimizeImages;
        set { _pdfOptimizeImages = value; OnPropertyChanged(); }
    }

    private int _pdfJpegQuality = 85;
    public int PdfJpegQuality
    {
        get => _pdfJpegQuality;
        set { _pdfJpegQuality = value; OnPropertyChanged(); }
    }

    private int _pdfMinWidth = 128;
    public int PdfMinWidth
    {
        get => _pdfMinWidth;
        set { _pdfMinWidth = value; OnPropertyChanged(); }
    }

    private int _pdfMinHeight = 128;
    public int PdfMinHeight
    {
        get => _pdfMinHeight;
        set { _pdfMinHeight = value; OnPropertyChanged(); }
    }

    private int _pdfMinArea = 16384;
    public int PdfMinArea
    {
        get => _pdfMinArea;
        set { _pdfMinArea = value; OnPropertyChanged(); }
    }

    private bool _pdfKeepInlineImages;
    public bool PdfKeepInlineImages
    {
        get => _pdfKeepInlineImages;
        set { _pdfKeepInlineImages = value; OnPropertyChanged(); }
    }

    private bool _pdfExternalizeInlineImages;
    public bool PdfExternalizeInlineImages
    {
        get => _pdfExternalizeInlineImages;
        set { _pdfExternalizeInlineImages = value; OnPropertyChanged(); }
    }

    private int _pdfInlineImageMinBytes = 1024;
    public int PdfInlineImageMinBytes
    {
        get => _pdfInlineImageMinBytes;
        set { _pdfInlineImageMinBytes = value; OnPropertyChanged(); }
    }

    private bool _pdfCompressStreams = true;
    public bool PdfCompressStreams
    {
        get => _pdfCompressStreams;
        set { _pdfCompressStreams = value; OnPropertyChanged(); }
    }

    private int _pdfCompressionLevel = 6;
    public int PdfCompressionLevel
    {
        get => _pdfCompressionLevel;
        set { _pdfCompressionLevel = value; OnPropertyChanged(); }
    }

    private string _pdfDecodeLevel = "generalized";
    public string PdfDecodeLevel
    {
        get => _pdfDecodeLevel;
        set
        {
            var next = value switch
            {
                "none" or "specialized" or "all" => value,
                _ => "generalized"
            };
            if (_pdfDecodeLevel == next) return;
            _pdfDecodeLevel = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PdfDecodeLevelIndex));
        }
    }

    public int PdfDecodeLevelIndex
    {
        get => _pdfDecodeLevel switch
        {
            "none" => 0,
            "specialized" => 2,
            "all" => 3,
            _ => 1
        };
        set => PdfDecodeLevel = value switch
        {
            0 => "none",
            2 => "specialized",
            3 => "all",
            _ => "generalized"
        };
    }

    private bool _pdfRecompressFlate;
    public bool PdfRecompressFlate
    {
        get => _pdfRecompressFlate;
        set { _pdfRecompressFlate = value; OnPropertyChanged(); }
    }

    private bool _pdfStructureCleanup;
    public bool PdfStructureCleanup
    {
        get => _pdfStructureCleanup;
        set { _pdfStructureCleanup = value; OnPropertyChanged(); }
    }

    private string _pdfObjectStreamMode = "preserve";
    public string PdfObjectStreamMode
    {
        get => _pdfObjectStreamMode;
        set
        {
            var next = value switch
            {
                "disable" or "generate" => value,
                _ => "preserve"
            };
            if (_pdfObjectStreamMode == next) return;
            _pdfObjectStreamMode = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PdfObjectStreamModeIndex));
            OnPropertyChanged(nameof(PdfGenerateObjectStreams));
        }
    }

    public int PdfObjectStreamModeIndex
    {
        get => _pdfObjectStreamMode switch
        {
            "disable" => 1,
            "generate" => 2,
            _ => 0
        };
        set => PdfObjectStreamMode = value switch
        {
            1 => "disable",
            2 => "generate",
            _ => "preserve"
        };
    }

    public bool PdfGenerateObjectStreams
    {
        get => PdfObjectStreamMode == "generate";
        set
        {
            if (value)
                PdfObjectStreamMode = "generate";
            else if (PdfObjectStreamMode == "generate")
                PdfObjectStreamMode = "preserve";
            OnPropertyChanged();
        }
    }

    private string _pdfRemoveUnreferencedResources = "auto";
    public string PdfRemoveUnreferencedResources
    {
        get => _pdfRemoveUnreferencedResources;
        set
        {
            var next = value switch
            {
                "yes" or "no" => value,
                _ => "auto"
            };
            if (_pdfRemoveUnreferencedResources == next) return;
            _pdfRemoveUnreferencedResources = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PdfRemoveUnreferencedResourcesIndex));
        }
    }

    public int PdfRemoveUnreferencedResourcesIndex
    {
        get => _pdfRemoveUnreferencedResources switch
        {
            "yes" => 1,
            "no" => 2,
            _ => 0
        };
        set => PdfRemoveUnreferencedResources = value switch
        {
            1 => "yes",
            2 => "no",
            _ => "auto"
        };
    }

    private bool _pdfPreserveUnreferencedObjects;
    public bool PdfPreserveUnreferencedObjects
    {
        get => _pdfPreserveUnreferencedObjects;
        set { _pdfPreserveUnreferencedObjects = value; OnPropertyChanged(); }
    }

    private bool _pdfNormalizeContent;
    public bool PdfNormalizeContent
    {
        get => _pdfNormalizeContent;
        set { _pdfNormalizeContent = value; OnPropertyChanged(); }
    }

    private bool _pdfCoalesceContents;
    public bool PdfCoalesceContents
    {
        get => _pdfCoalesceContents;
        set { _pdfCoalesceContents = value; OnPropertyChanged(); }
    }

    private bool _pdfNewlineBeforeEndStream;
    public bool PdfNewlineBeforeEndStream
    {
        get => _pdfNewlineBeforeEndStream;
        set { _pdfNewlineBeforeEndStream = value; OnPropertyChanged(); }
    }

    private bool _pdfDistributionCompatibility;
    public bool PdfDistributionCompatibility
    {
        get => _pdfDistributionCompatibility;
        set { _pdfDistributionCompatibility = value; OnPropertyChanged(); }
    }

    private bool _pdfDecrypt;
    public bool PdfDecrypt
    {
        get => _pdfDecrypt;
        set { _pdfDecrypt = value; OnPropertyChanged(); }
    }

    private bool _pdfRemoveRestrictions;
    public bool PdfRemoveRestrictions
    {
        get => _pdfRemoveRestrictions;
        set { _pdfRemoveRestrictions = value; OnPropertyChanged(); }
    }

    private bool _pdfRestrictionRemoval;
    public bool PdfRestrictionRemoval
    {
        get => _pdfRestrictionRemoval;
        set { _pdfRestrictionRemoval = value; OnPropertyChanged(); }
    }

    private string _pdfMinVersion = "";
    public string PdfMinVersion
    {
        get => _pdfMinVersion;
        set
        {
            var next = NormalizePdfVersion(value);
            if (_pdfMinVersion == next) return;
            _pdfMinVersion = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PdfMinVersionIndex));
        }
    }

    public int PdfMinVersionIndex
    {
        get => PdfVersionToIndex(_pdfMinVersion);
        set => PdfMinVersion = PdfVersionFromIndex(value);
    }

    private string _pdfForceVersion = "";
    public string PdfForceVersion
    {
        get => _pdfForceVersion;
        set
        {
            var next = NormalizePdfVersion(value);
            if (_pdfForceVersion == next) return;
            _pdfForceVersion = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PdfForceVersionIndex));
        }
    }

    public int PdfForceVersionIndex
    {
        get => PdfVersionToIndex(_pdfForceVersion);
        set => PdfForceVersion = PdfVersionFromIndex(value);
    }

    private bool _pdfLinearize;
    public bool PdfLinearize
    {
        get => _pdfLinearize;
        set { _pdfLinearize = value; OnPropertyChanged(); }
    }

    private static int PdfVersionToIndex(string value)
        => value switch
        {
            "1.3" => 1,
            "1.4" => 2,
            "1.5" => 3,
            "1.6" => 4,
            "1.7" => 5,
            "2.0" => 6,
            _ => 0
        };

    private static string PdfVersionFromIndex(int value)
        => value switch
        {
            1 => "1.3",
            2 => "1.4",
            3 => "1.5",
            4 => "1.6",
            5 => "1.7",
            6 => "2.0",
            _ => ""
        };

    private static string NormalizePdfVersion(string? value)
        => value switch
        {
            "1.3" or "1.4" or "1.5" or "1.6" or "1.7" or "2.0" => value,
            _ => ""
        };

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

    private string _statusText = Properties.Loc.StatusReady;
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
    public bool IsPdfFileListEmpty => PdfFiles.Count == 0;

    public string OutputSummary
        => string.Format(Properties.Loc.SummaryOutput, OutputDirectory, Files.Count, Steps.Count(s => s.Enabled));

    public string OptimizeOutputSummary
        => string.Format(Properties.Loc.SummaryOptimizeOutput, OutputDirectory, OptimizeFiles.Count);

    public string PdfOutputSummary
        => string.Format(Properties.Loc.SummaryOptimizeOutput, OutputDirectory, PdfFiles.Count);

    private string _selectedImagePresetName = "";
    public string SelectedImagePresetName
    {
        get => _selectedImagePresetName;
        set
        {
            if (_selectedImagePresetName == value) return;
            _selectedImagePresetName = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged(SaveImagePresetCommand);

            if (!_suppressNotifications && ImagePresetNames.Contains(value))
                LoadPreset("Image", value, Properties.Loc.PresetTypeImage);
        }
    }

    private string _selectedOfficePresetName = "";
    public string SelectedOfficePresetName
    {
        get => _selectedOfficePresetName;
        set
        {
            if (_selectedOfficePresetName == value) return;
            _selectedOfficePresetName = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged(SaveOfficePresetCommand);

            if (!_suppressNotifications && OfficePresetNames.Contains(value))
                LoadPreset("Office", value, Properties.Loc.PresetTypeOffice);
        }
    }

    private string _selectedPdfPresetName = "";
    public string SelectedPdfPresetName
    {
        get => _selectedPdfPresetName;
        set
        {
            if (_selectedPdfPresetName == value) return;
            _selectedPdfPresetName = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged(SavePdfPresetCommand);

            if (!_suppressNotifications && PdfPresetNames.Contains(value))
                LoadPreset("Pdf", value, Properties.Loc.PresetTypePdf);
        }
    }

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
    public ICommand AddPdfFilesCommand { get; }
    public ICommand AddPdfFolderCommand { get; }
    public ICommand RemovePdfFileCommand { get; }
    public ICommand ClearPdfFilesCommand { get; }
    public ICommand ProcessPdfOptimizeCommand { get; }
    public ICommand RunOrCancelImageCommand { get; }
    public ICommand RunOrCancelOfficeCommand { get; }
    public ICommand RunOrCancelPdfCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseCompositeCommand { get; }
    public ICommand ToggleDebugCommand { get; }
    public ICommand SaveImagePresetCommand { get; }
    public ICommand SaveOfficePresetCommand { get; }
    public ICommand SavePdfPresetCommand { get; }

    public MainViewModel(string? settingsPath = null)
    {
        _suppressNotifications = true;
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
        RunOrCancelImageCommand = new RelayCommand(async _ => await RunImageOrCancelAsync(), _ => IsProcessing || Files.Count > 0);
        OpenModalCommand = new RelayCommand(OpenModal, _ => !IsProcessing);
        BrowseCompositeCommand = new RelayCommand(BrowseComposite, _ => !IsProcessing);
        ToggleDebugCommand = new RelayCommand(_ => IsDebugVisible = !IsDebugVisible);
        SaveImagePresetCommand = new RelayCommand(SaveImagePreset, _ => !IsProcessing && !string.IsNullOrWhiteSpace(SelectedImagePresetName));
        SaveOfficePresetCommand = new RelayCommand(SaveOfficePreset, _ => !IsProcessing && !string.IsNullOrWhiteSpace(SelectedOfficePresetName));
        SavePdfPresetCommand = new RelayCommand(SavePdfPreset, _ => !IsProcessing && !string.IsNullOrWhiteSpace(SelectedPdfPresetName));
        AddOptimizeFilesCommand = new RelayCommand(AddOptimizeFiles, _ => !IsProcessing);
        AddOptimizeFolderCommand = new RelayCommand(AddOptimizeFolder, _ => !IsProcessing);
        RemoveOptimizeFileCommand = new RelayCommand(RemoveOptimizeFile, _ => !IsProcessing);
        ClearOptimizeFilesCommand = new RelayCommand(ClearOptimizeFiles, _ => !IsProcessing);
        ProcessOptimizeCommand = new RelayCommand(async _ => await ProcessOptimizeAsync(), _ => !IsProcessing && OptimizeFiles.Count > 0);
        RunOrCancelOfficeCommand = new RelayCommand(async _ => await RunOfficeOrCancelAsync(), _ => IsProcessing || OptimizeFiles.Count > 0);
        AddPdfFilesCommand = new RelayCommand(AddPdfFiles, _ => !IsProcessing);
        AddPdfFolderCommand = new RelayCommand(AddPdfFolder, _ => !IsProcessing);
        RemovePdfFileCommand = new RelayCommand(RemovePdfFile, _ => !IsProcessing);
        ClearPdfFilesCommand = new RelayCommand(ClearPdfFiles, _ => !IsProcessing);
        ProcessPdfOptimizeCommand = new RelayCommand(async _ => await ProcessPdfOptimizeAsync(), _ => !IsProcessing && PdfFiles.Count > 0);
        RunOrCancelPdfCommand = new RelayCommand(async _ => await RunPdfOrCancelAsync(), _ => IsProcessing || PdfFiles.Count > 0);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsProcessing);

        // ファイルリスト変更時に空表示を更新
        Files.CollectionChanged += (_, _) => OnImageFilesChanged();

        OptimizeFiles.CollectionChanged += (_, _) => OnOfficeFilesChanged();

        PdfFiles.CollectionChanged += (_, _) => OnPdfFilesChanged();

        // デフォルトのステップを追加（処理順に登録、FormatConvert/Optimize は最後に適用）
        foreach (var (type, enabled) in DefaultSteps)
            AddDefaultStep(type, enabled);

        foreach (var step in Steps)
            step.PropertyChanged += Step_PropertyChanged;

        Steps.CollectionChanged += (_, e) =>
        {
            UpdateStepSubscriptions(e);
            NotifyStepShortcutsChanged();
            UpdateSummaries();
        };

        // 設定の読み込み
        LoadSettings(settingsPath);
        LoadPresetNames();
        LoadLastUsedPresets();
        _suppressNotifications = false;
    }

    private void AddDefaultStep(PipelineStepType type, bool enabled)
    {
        Steps.Add(new PipelineStep { Type = type, Enabled = enabled });
    }

    private void OnImageFilesChanged()
    {
        OnPropertyChanged(nameof(IsFileListEmpty));
        UpdateSummaries();
    }

    private void OnOfficeFilesChanged()
    {
        OnPropertyChanged(nameof(IsOptimizeFileListEmpty));
        OnPropertyChanged(nameof(OptimizeOutputSummary));
        RaiseCanExecuteChanged(ProcessOptimizeCommand, RunOrCancelOfficeCommand);
    }

    private void OnPdfFilesChanged()
    {
        OnPropertyChanged(nameof(IsPdfFileListEmpty));
        OnPropertyChanged(nameof(PdfOutputSummary));
        RaiseCanExecuteChanged(ProcessPdfOptimizeCommand, RunOrCancelPdfCommand);
    }

    private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PipelineStep.Enabled))
            UpdateSummaries();
    }

    private void UpdateStepSubscriptions(NotifyCollectionChangedEventArgs e)
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
    }

    private void NotifyStepShortcutsChanged()
    {
        foreach (var propertyName in StepShortcutPropertyNames)
            OnPropertyChanged(propertyName);
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
        => HasSupportedExtension(path, ImageExtensions);

    private static bool HasSupportedExtension(string path, IEnumerable<string> extensions)
        => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

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
        AddFilesFromDialog(Properties.Loc.DlgTitleSelectImages, Properties.Loc.DlgFilterImages, path => AddFileByPath(path));
    }

    private void AddFolder(object? _)
    {
        AddFolderFromDialog(Properties.Loc.DlgTitleAddImageFolder, path => AddFileByPath(path));
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
            var result = MessageBox.Show(Properties.Loc.MsgConfirmClearImageList, Properties.Loc.TitleConfirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
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
        AddFolderFromDialog(Properties.Loc.DlgTitleSelectOutputFolder, path => OutputDirectory = path);
    }

    private void BrowseToolPath(object? parameter)
    {
        if (parameter is not string toolName)
            return;

        var currentPath = GetToolPath(toolName);
        var selectedPath = SelectFileFromDialog(
            string.Format(Properties.Loc.DlgTitleSelectToolExecutable, toolName),
            Properties.Loc.DlgFilterExecutable,
            currentPath);

        if (selectedPath == null)
            return;

        SetToolPath(toolName, selectedPath);
    }

    private string GetToolPath(string toolName)
        => toolName switch
        {
            "oxipng" => OxipngPath,
            "cjpegli" => CjpegliPath,
            "ffmpeg" => FfmpegPath,
            "qpdf" => QpdfPath,
            _ => ""
        };

    private void SetToolPath(string toolName, string path)
    {
        switch (toolName)
        {
            case "ffmpeg":
                FfmpegPath = path;
                break;
            case "oxipng":
                OxipngPath = path;
                break;
            case "cjpegli":
                CjpegliPath = path;
                break;
            case "qpdf":
                QpdfPath = path;
                break;
        }
    }

    private void BrowseComposite(object? _)
    {
        var path = SelectFileFromDialog(Properties.Loc.DlgTitleSelectCompositeImage, Properties.Loc.DlgFilterCompositeImages);
        if (path != null)
        {
            if (CompositeStep != null)
            {
                CompositeStep.CompositePath = path;
            }
        }
    }

    // --- バッチ処理 ---
    private async Task ProcessAsync()
    {
        var targets = Files.Where(f => f.IsChecked).ToList();
        if (targets.Count == 0)
        {
            StatusText = Properties.Loc.StatusNoFiles;
            return;
        }

        var enabledSteps = Steps.Where(s => s.Enabled).ToList();
        if (enabledSteps.Count == 0)
        {
            StatusText = Properties.Loc.StatusNoOptions;
            return;
        }

        var token = BeginProcessing(targets.Count);

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
                    token.ThrowIfCancellationRequested();
                    var file = targets[i];
                    try
                    {
                        // FormatConvert が有効なら指定形式、無効なら元ファイルと同じ拡張子
                        var ext = formatStep != null
                            ? ImageProcessingService.GetExtension(formatStep.TargetFormat)
                            : Path.GetExtension(file.FilePath);

                        var outputPath = OutputPathHelper.GetUniqueSuffixedPath(file.FilePath, FileNameRule, ext, OutputDirectory);

                        _processingService.Process(file.FilePath, outputPath, Steps, UseOxipng, OxipngPath, OxipngLevel, UseJpegli, CjpegliPath, logAction: LogDebug);
                        success++;
                        LogDebug($"OK  {file.FileName} → {Path.GetFileName(outputPath)}");

                        UpdateImageProgress(i + 1, targets.Count, file.FileName);
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        ReportFileError(file.FileName, ex.Message);
                        UpdateProgressValue(i + 1);
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            LogDebug("変換処理がキャンセルされました。");
        }
        finally
        {
            var isCancelled = EndProcessing(token);
            if (isCancelled)
            {
                StatusText = Properties.Loc.StatusCancelled;
                LogDebug($"変換キャンセル: {success} 成功, {errors} 失敗");
            }
            else
            {
                StatusText = string.Format(Properties.Loc.StatusDoneMsg, success, errors);
                LogDebug($"変換完了: {success} 成功, {errors} 失敗");
                PlayCompletionSoundIfEnabled();
            }
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
        LogDebug("ユーザーによって処理がキャンセルされました");
        StatusText = Properties.Loc.StatusCancelling;
    }

    private async Task RunImageOrCancelAsync()
        => await RunOrCancelAsync(ProcessAsync);

    private async Task RunOfficeOrCancelAsync()
        => await RunOrCancelAsync(ProcessOptimizeAsync);

    private async Task RunPdfOrCancelAsync()
        => await RunOrCancelAsync(ProcessPdfOptimizeAsync);

    private async Task RunOrCancelAsync(Func<Task> processAsync)
    {
        if (IsProcessing)
        {
            Cancel();
            return;
        }

        await processAsync();
    }

    private void RefreshCommands()
    {
        RaiseCanExecuteChanged(
            CancelCommand,
            AddFilesCommand,
            AddFolderCommand,
            RemoveFileCommand,
            ClearFilesCommand,
            AddStepCommand,
            RemoveStepCommand,
            MoveStepUpCommand,
            MoveStepDownCommand,
            BrowseOutputCommand,
            ProcessCommand,
            RunOrCancelImageCommand,
            OpenModalCommand,
            AddOptimizeFilesCommand,
            AddOptimizeFolderCommand,
            RemoveOptimizeFileCommand,
            ClearOptimizeFilesCommand,
            ProcessOptimizeCommand,
            RunOrCancelOfficeCommand,
            AddPdfFilesCommand,
            AddPdfFolderCommand,
            RemovePdfFileCommand,
            ClearPdfFilesCommand,
            ProcessPdfOptimizeCommand,
            RunOrCancelPdfCommand,
            BrowseCompositeCommand,
            SaveImagePresetCommand,
            SaveOfficePresetCommand,
            SavePdfPresetCommand);
    }

    private void UpdateSummaries()
    {
        OnPropertyChanged(nameof(OutputSummary));
        RaiseCanExecuteChanged(ProcessCommand, RunOrCancelImageCommand);
    }

    private static void RaiseCanExecuteChanged(params ICommand[] commands)
    {
        foreach (var command in commands)
        {
            if (command is RelayCommand relayCommand)
                relayCommand.RaiseCanExecuteChanged();
        }
    }

    private void OpenModal(object? parameter)
    {
        if (parameter is string type)
        {
            ActiveModalType = type;
            ModalTitle = GetModalTitle(type);
            RequestOpenSettings?.Invoke(type);
        }
    }

    private static string GetModalTitle(string modalType)
        => modalType switch
        {
            "Grayscale" => Properties.Loc.ModalTitleGrayscale,
            "ExifAutoRotate" => Properties.Loc.ModalTitleExifAutoRotate,
            "Crop" => Properties.Loc.ModalTitleCrop,
            "Resize" => Properties.Loc.ModalTitleResize,
            "Padding" => Properties.Loc.ModalTitlePadding,
            "Sharpen" => Properties.Loc.ModalTitleSharpen,
            "ColorAdjust" => Properties.Loc.ModalTitleColorAdjust,
            "ToneCurve" => Properties.Loc.ModalTitleToneCurve,
            "Format" => Properties.Loc.ModalTitleFormat,
            "Optimize" => Properties.Loc.ModalTitleOptimize,
            "Posterize" => Properties.Loc.ModalTitlePosterize,
            "Rotate" => Properties.Loc.ModalTitleRotate,
            "Composite" => Properties.Loc.ModalTitleComposite,
            "OfficeOptimize" => Properties.Loc.ModalTitleOfficeOptimize,
            "OfficePdf" => Properties.Loc.ModalTitleOfficePdf,
            "PdfConvert" => Properties.Loc.ModalTitlePdfConvert,
            "PdfImage" => Properties.Loc.ModalTitlePdfImage,
            "PdfStream" => Properties.Loc.ModalTitlePdfStream,
            "PdfStructure" => Properties.Loc.ModalTitlePdfStructure,
            "PdfCompatibility" => Properties.Loc.ModalTitlePdfCompatibility,
            "PdfRestrictions" => Properties.Loc.ModalTitlePdfRestrictions,
            "Options" => Properties.Loc.ModalTitleOptions,
            _ => Properties.Loc.ModalTitleDefault
        };

    // --- ファイル最適化機能用メソッド ---
    public void AddOptimizeFileByPath(string path)
    {
        if (!IsSupportedOptimizeFile(path))
            return;

        AddOptimizeFileInfo(OptimizeFiles, path);
    }

    private static bool IsSupportedOptimizeFile(string path)
        => HasSupportedExtension(path, OptimizeDocumentExtensions);

    private void AddOptimizeFiles(object? _)
    {
        AddFilesFromDialog(Properties.Loc.DlgTitleSelectOfficeFiles, Properties.Loc.DlgFilterOfficeFiles, AddOptimizeFileByPath);
    }

    private void AddOptimizeFolder(object? _)
    {
        AddFolderFromDialog(Properties.Loc.DlgTitleAddFolder, AddOptimizeFolderPath);
    }

    private void AddOptimizeFolderPath(string dir)
    {
        if (!Directory.Exists(dir))
            return;

        var files = Directory.GetFiles(dir)
            .Where(IsSupportedOptimizeFile)
            .OrderBy(f => f);
        foreach (var path in files)
            AddOptimizeFileByPath(path);
    }

    private void RemoveOptimizeFile(object? parameter)
    {
        if (parameter is OptimizeFile file)
            OptimizeFiles.Remove(file);
    }

    private void ClearOptimizeFiles(object? _)
    {
        if (!ConfirmClearSharedList())
            return;

        OptimizeFiles.Clear();
    }

    public void AddPdfFileByPath(string path)
    {
        if (Directory.Exists(path))
        {
            foreach (var filePath in Directory.GetFiles(path).Where(IsSupportedPdfFile).OrderBy(f => f))
                AddPdfFileByPath(filePath);
            return;
        }

        if (!IsSupportedPdfFile(path))
            return;

        AddOptimizeFileInfo(PdfFiles, path);
    }

    private static bool IsSupportedPdfFile(string path)
        => HasSupportedExtension(path, PdfExtensions);

    private void AddPdfFiles(object? _)
    {
        AddFilesFromDialog(Properties.Loc.DlgTitleSelectPdfFiles, Properties.Loc.DlgFilterPdfFiles, AddPdfFileByPath);
    }

    private void AddPdfFolder(object? _)
    {
        AddFolderFromDialog(Properties.Loc.DlgTitleAddFolder, AddPdfFileByPath);
    }

    private void RemovePdfFile(object? parameter)
    {
        if (parameter is OptimizeFile file)
            PdfFiles.Remove(file);
    }

    private void ClearPdfFiles(object? _)
    {
        if (!ConfirmClearSharedList())
            return;

        PdfFiles.Clear();
    }

    private static void AddOptimizeFileInfo(ObservableCollection<OptimizeFile> collection, string path)
    {
        if (collection.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            return;

        var fileInfo = new FileInfo(path);
        collection.Add(new OptimizeFile
        {
            FilePath = path,
            OriginalSize = fileInfo.Length,
            Status = Properties.Loc.StatusWaiting
        });
    }

    private bool ConfirmClearSharedList()
    {
        if (!ConfirmOnClear)
            return true;

        var result = MessageBox.Show(Properties.Loc.MsgConfirmClearList, Properties.Loc.TitleConfirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    private sealed record ConversionWorkItem(OptimizeFile File, string OriginalPath, string TargetPath);

    private sealed record OfficeProcessingOptions
    {
        public bool EnableOptimize { get; init; }
        public bool StripMetadata { get; init; }
        public bool CleanUnusedObjects { get; init; }
        public bool CompressImages { get; init; }
        public bool ResizeImagesByPpi { get; init; }
        public int TargetImagePpi { get; init; }
        public bool ConvertToWebP { get; init; }
        public bool ConvertToPdf { get; init; }
        public bool ConvertToPdfA { get; init; }
        public int WebPQuality { get; init; }
        public bool CompressMedia { get; init; }
        public string FfmpegPath { get; init; } = "";
        public int MediaVideoCrf { get; init; }
        public string MediaVideoCodec { get; init; } = "";
        public string MediaAudioCodec { get; init; } = "";
        public string OutputDirectory { get; init; } = "";
        public bool ResetCellSelection { get; init; }
        public int MaxDegreeOfParallelism { get; init; }
        public bool UseOxipng { get; init; }
        public int OxipngLevel { get; init; }
        public bool UseJpegli { get; init; }
        public string OxipngPath { get; init; } = "";
        public string CjpegliPath { get; init; } = "";
        public string FileNameRule { get; init; } = "";
    }

    private async Task ProcessOptimizeAsync()
    {
        var targets = OptimizeFiles.Where(f => f.IsChecked).ToList();
        if (targets.Count == 0)
        {
            StatusText = Properties.Loc.StatusNoFiles;
            return;
        }

        LogDebug($"Office ファイル変換開始: {targets.Count} ファイル");

        var token = BeginProcessing(targets.Count);

        int success = 0;
        int errors = 0;
        int processed = 0;

        var options = CreateOfficeProcessingOptions();
        var workItems = CreateOfficeWorkItems(targets, options);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.ConvertToPdf ? 1 : GetParallelism(options.MaxDegreeOfParallelism),
            CancellationToken = token
        };

        try
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(workItems, parallelOptions, item =>
                {
                    token.ThrowIfCancellationRequested();
                    var file = item.File;
                    file.Status = Properties.Loc.StatusProcessing;
                    file.IsProcessing = true;
                    string? tempPath = null;
                    try
                    {
                        var originalPath = item.OriginalPath;
                        LogDebug($"処理開始: {file.FileName}");
                        var targetPath = item.TargetPath;
                        tempPath = OutputPathHelper.GetTemporarySiblingPath(targetPath);

                        long optimizedSize;
                        if (options.ConvertToPdf)
                        {
                            file.Status = Properties.Loc.StatusConvertingToPdf;
                            optimizedSize = _processingService.ConvertOfficeToPdf(originalPath, tempPath, options.ConvertToPdfA, LogDebug, targetPath);
                        }
                        else
                        {
                            file.Status = Properties.Loc.StatusOptimizingPackage;
                            optimizedSize = OptimizeOfficePackage(originalPath, tempPath, options);
                        }

                        FileMoveHelper.MoveWithRetries(tempPath, targetPath);

                        tempPath = null; // 正常完了したのでクリア

                        CompleteOptimizeFile(file, optimizedSize);
                        Interlocked.Increment(ref success);
                    }
                    catch (Exception ex)
                    {
                        file.Status = Properties.Loc.StatusErrorState;
                        Interlocked.Increment(ref errors);
                        ReportFileError(file.FileName, ex.Message);

                        FileMoveHelper.DeleteIfExists(tempPath);
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref processed);
                        UpdateOptimizeProgress(current, targets.Count);
                        file.IsProcessing = false;
                    }
                }
                );
            });

            // 並列処理完了後に1回だけGC
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        catch (OperationCanceledException)
        {
            LogDebug("Office ファイル変換処理がキャンセルされました。");
        }
        catch (AggregateException ae) when (ae.InnerExceptions.Any(e => e is OperationCanceledException))
        {
            LogDebug("Office ファイル変換処理がキャンセルされました。");
        }
        finally
        {
            var isCancelled = EndProcessing(token);

            if (isCancelled)
            {
                StatusText = Properties.Loc.StatusOptimizeCancelled;
                LogDebug($"Office ファイル変換キャンセル: {success} 成功, {errors} 失敗");
            }
            else
            {
                StatusText = string.Format(Properties.Loc.StatusOptimizeDone, success, errors);
                LogDebug(StatusText);
                PlayCompletionSoundIfEnabled();
            }
        }
    }

    private OfficeProcessingOptions CreateOfficeProcessingOptions()
        => new()
        {
            EnableOptimize = EnableOfficeOptimize,
            StripMetadata = StripOfficeMetadata,
            CleanUnusedObjects = CleanUnusedObjects,
            CompressImages = CompressEmbeddedImages,
            ResizeImagesByPpi = ResizeEmbeddedImagesByPpi,
            TargetImagePpi = TargetImagePpi,
            ConvertToWebP = ConvertToWebP,
            ConvertToPdf = ConvertOfficeToPdf,
            ConvertToPdfA = ConvertOfficeToPdfA,
            WebPQuality = WebPQuality,
            CompressMedia = CompressMedia,
            FfmpegPath = FfmpegPath,
            MediaVideoCrf = MediaVideoCrf,
            MediaVideoCodec = MediaVideoCodec,
            MediaAudioCodec = MediaAudioCodec,
            OutputDirectory = OutputDirectory,
            ResetCellSelection = ResetCellSelection,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            UseOxipng = UseOxipng,
            OxipngLevel = OxipngLevel,
            UseJpegli = UseJpegli,
            OxipngPath = OxipngPath,
            CjpegliPath = CjpegliPath,
            FileNameRule = FileNameRule
        };

    private static List<ConversionWorkItem> CreateOfficeWorkItems(IEnumerable<OptimizeFile> targets, OfficeProcessingOptions options)
    {
        var reservedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return targets.Select(file =>
        {
            var originalPath = file.FilePath;
            var outputExtension = options.ConvertToPdf ? ".pdf" : Path.GetExtension(originalPath);
            var targetPath = OutputPathHelper.GetUniqueSuffixedPath(
                originalPath,
                options.FileNameRule,
                outputExtension,
                options.OutputDirectory,
                reservedOutputPaths);

            reservedOutputPaths.Add(targetPath);
            return new ConversionWorkItem(file, originalPath, targetPath);
        }).ToList();
    }

    private long OptimizeOfficePackage(string originalPath, string tempPath, OfficeProcessingOptions options)
        => _processingService.Optimize(
            originalPath,
            tempPath,
            options.EnableOptimize && options.StripMetadata,
            options.EnableOptimize && options.CleanUnusedObjects,
            options.EnableOptimize && options.CompressImages,
            options.EnableOptimize && options.ConvertToWebP,
            options.WebPQuality,
            options.EnableOptimize && options.CompressMedia,
            options.FfmpegPath,
            options.MediaVideoCrf,
            options.MediaVideoCodec,
            options.MediaAudioCodec,
            options.UseOxipng,
            options.OxipngPath,
            options.OxipngLevel,
            options.UseJpegli,
            options.CjpegliPath,
            resizeImagesByPpi: options.EnableOptimize && options.ResizeImagesByPpi,
            targetImagePpi: options.TargetImagePpi,
            resetCellSelection: options.EnableOptimize && options.ResetCellSelection,
            logAction: LogDebug);

    private async Task ProcessPdfOptimizeAsync()
    {
        var targets = PdfFiles.Where(f => f.IsChecked).ToList();
        if (targets.Count == 0)
        {
            StatusText = Properties.Loc.StatusNoFiles;
            return;
        }

        LogDebug($"PDF ファイル変換開始: {targets.Count} ファイル");

        var token = BeginProcessing(targets.Count);

        int success = 0;
        int errors = 0;
        int processed = 0;

        var qpdfPath = QpdfPath;
        var options = CreatePdfOptimizationOptions();
        var workItems = CreatePdfWorkItems(targets, FileNameRule, OutputDirectory);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GetParallelism(MaxDegreeOfParallelism),
            CancellationToken = token
        };

        try
        {
            await Parallel.ForEachAsync(workItems, parallelOptions, async (item, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var file = item.File;
                file.Status = Properties.Loc.StatusProcessing;
                file.IsProcessing = true;
                string? tempPath = null;
                try
                {
                    LogDebug($"PDF処理開始: {file.FileName}");
                    tempPath = OutputPathHelper.GetTemporarySiblingPath(item.TargetPath);
                    file.Status = Properties.Loc.StatusOptimizingPdf;

                    var optimizedSize = await _pdfOptimizationService.OptimizeAsync(
                        item.OriginalPath,
                        tempPath,
                        options,
                        qpdfPath,
                        ct,
                        LogDebug,
                        item.TargetPath);

                    await FileMoveHelper.MoveWithRetriesAsync(tempPath, item.TargetPath, ct);

                    tempPath = null;
                    CompleteOptimizeFile(file, optimizedSize);
                    Interlocked.Increment(ref success);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    file.Status = Properties.Loc.StatusErrorState;
                    Interlocked.Increment(ref errors);
                    ReportFileError(file.FileName, ex.Message);

                    FileMoveHelper.DeleteIfExists(tempPath);
                }
                finally
                {
                    var current = Interlocked.Increment(ref processed);
                    UpdateOptimizeProgress(current, targets.Count);
                    file.IsProcessing = false;
                }
            });

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        catch (OperationCanceledException)
        {
            LogDebug("PDF ファイル変換処理がキャンセルされました。");
        }
        finally
        {
            var isCancelled = EndProcessing(token);

            if (isCancelled)
            {
                StatusText = Properties.Loc.StatusPdfOptimizeCancelled;
                LogDebug($"PDF ファイル変換キャンセル: {success} 成功, {errors} 失敗");
            }
            else
            {
                StatusText = string.Format(Properties.Loc.StatusPdfOptimizeDone, success, errors);
                LogDebug(StatusText);
                PlayCompletionSoundIfEnabled();
            }
        }
    }

    private static List<ConversionWorkItem> CreatePdfWorkItems(
        IEnumerable<OptimizeFile> targets,
        string fileNameRule,
        string outputDirectory)
    {
        var reservedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return targets.Select(file =>
        {
            var originalPath = file.FilePath;
            var targetPath = OutputPathHelper.GetUniqueSuffixedPath(
                originalPath,
                fileNameRule,
                ".pdf",
                outputDirectory,
                reservedOutputPaths);

            reservedOutputPaths.Add(targetPath);
            return new ConversionWorkItem(file, originalPath, targetPath);
        }).ToList();
    }

    private static int GetParallelism(int configuredParallelism)
        => configuredParallelism > 0 ? configuredParallelism : Environment.ProcessorCount;

    private CancellationToken BeginProcessing(int progressMax)
    {
        IsProcessing = true;
        ProgressValue = 0;
        ProgressMax = progressMax;

        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    private bool EndProcessing(CancellationToken token)
    {
        var isCancelled = token.IsCancellationRequested;
        _cts?.Dispose();
        _cts = null;
        IsProcessing = false;
        return isCancelled;
    }

    private void PlayCompletionSoundIfEnabled()
    {
        if (PlaySoundOnComplete)
            System.Media.SystemSounds.Asterisk.Play();
    }

    private void UpdateOptimizeProgress(int current, int total)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = string.Format(Properties.Loc.StatusOptimizingProgress, current, total);
            ProgressValue = current;
        });
    }

    private void UpdateProgressValue(int current)
    {
        Application.Current?.Dispatcher.Invoke(() => ProgressValue = current);
    }

    private void UpdateImageProgress(int current, int total, string fileName)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = string.Format(Properties.Loc.StatusProcessingProgress, current, total, fileName);
            ProgressValue = current;
        });
    }

    private void ReportFileError(string fileName, string message)
    {
        LogDebug($"ERR {fileName}: {message}");
        Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = string.Format(Properties.Loc.StatusErrorMsg, fileName, message);
        });
    }

    private void CompleteOptimizeFile(OptimizeFile file, long optimizedSize)
    {
        file.OptimizedSize = optimizedSize;
        file.Status = Properties.Loc.StatusCompleted;
        LogDebug($"OK  {file.FileName} ({file.OriginalSize} -> {optimizedSize} bytes)");
    }

    private PdfOptimizationOptions CreatePdfOptimizationOptions()
        => new()
        {
            OptimizeImages = PdfOptimizeImages,
            JpegQuality = PdfJpegQuality,
            MinWidth = PdfMinWidth,
            MinHeight = PdfMinHeight,
            MinArea = PdfMinArea,
            KeepInlineImages = PdfKeepInlineImages,
            ExternalizeInlineImages = PdfExternalizeInlineImages,
            InlineImageMinBytes = PdfInlineImageMinBytes,
            CompressStreams = PdfCompressStreams,
            CompressionLevel = PdfCompressionLevel,
            DecodeLevel = PdfDecodeLevel,
            RecompressFlate = PdfRecompressFlate,
            StructureCleanup = PdfStructureCleanup,
            ObjectStreamMode = PdfObjectStreamMode,
            RemoveUnreferencedResources = PdfRemoveUnreferencedResources,
            PreserveUnreferencedObjects = PdfPreserveUnreferencedObjects,
            NormalizeContent = PdfNormalizeContent,
            CoalesceContents = PdfCoalesceContents,
            NewlineBeforeEndStream = PdfNewlineBeforeEndStream,
            DistributionCompatibility = PdfDistributionCompatibility,
            Decrypt = PdfDecrypt,
            RemoveRestrictions = PdfRemoveRestrictions,
            RestrictionRemoval = PdfRestrictionRemoval,
            MinVersion = PdfMinVersion,
            ForceVersion = PdfForceVersion,
            Linearize = PdfLinearize
        };

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

    private void LoadPresetNames()
    {
        LoadPresetNames(ImagePresetNames, "Image");
        LoadPresetNames(OfficePresetNames, "Office");
        LoadPresetNames(PdfPresetNames, "Pdf");
    }

    private void LoadPresetNames(ObservableCollection<string> target, string presetType)
    {
        target.Clear();

        var dir = AppPathHelper.GetPresetDirectoryPath(presetType);
        if (!Directory.Exists(dir))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.ini")
                         .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to load {presetType} presets: {ex.Message}");
        }
    }

    private void LoadPreset(string presetType, string presetName, string displayName, bool updateStatus = true)
    {
        var sanitizedName = AppPathHelper.SanitizePresetName(presetName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
            return;

        var path = AppPathHelper.GetPresetFilePath(presetType, sanitizedName);
        if (!File.Exists(path))
            return;

        LoadSettings(path);
        if (updateStatus)
            StatusText = string.Format(Properties.Loc.StatusPresetApplied, displayName, sanitizedName);
    }

    private void LoadLastUsedPresets()
    {
        LoadLastUsedPreset("Image", SelectedImagePresetName, ImagePresetNames, name => SelectedImagePresetName = name, Properties.Loc.PresetTypeImage);
        LoadLastUsedPreset("Office", SelectedOfficePresetName, OfficePresetNames, name => SelectedOfficePresetName = name, Properties.Loc.PresetTypeOffice);
        LoadLastUsedPreset("Pdf", SelectedPdfPresetName, PdfPresetNames, name => SelectedPdfPresetName = name, Properties.Loc.PresetTypePdf);
    }

    private void LoadLastUsedPreset(string presetType, string selectedPresetName, ObservableCollection<string> availablePresetNames, Action<string> selectPreset, string displayName)
    {
        var presetName = AppPathHelper.SanitizePresetName(selectedPresetName);
        var matchedPresetName = string.IsNullOrWhiteSpace(presetName)
            ? null
            : availablePresetNames.FirstOrDefault(name => string.Equals(name, presetName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(matchedPresetName))
        {
            selectPreset("");
            return;
        }

        selectPreset(matchedPresetName);
        LoadPreset(presetType, matchedPresetName, displayName, updateStatus: false);
    }

    private void SaveImagePreset(object? parameter)
    {
        SavePreset(
            "Image",
            SelectedImagePresetName,
            name => SelectedImagePresetName = name,
            () => CreateSettingsData(includeGeneral: false, includeOffice: false, includePdf: false, includeImage: true),
            Properties.Loc.PresetTypeImage);
    }

    private void SaveOfficePreset(object? parameter)
    {
        SavePreset(
            "Office",
            SelectedOfficePresetName,
            name => SelectedOfficePresetName = name,
            () => CreateSettingsData(includeGeneral: false, includeOffice: true, includePdf: false, includeImage: false),
            Properties.Loc.PresetTypeOffice);
    }

    private void SavePdfPreset(object? parameter)
    {
        SavePreset(
            "Pdf",
            SelectedPdfPresetName,
            name => SelectedPdfPresetName = name,
            () => CreateSettingsData(includeGeneral: false, includeOffice: false, includePdf: true, includeImage: false),
            Properties.Loc.PresetTypePdf);
    }

    private void SavePreset(
        string presetType,
        string selectedPresetName,
        Action<string> selectPreset,
        Func<Dictionary<string, Dictionary<string, string>>> createData,
        string displayName)
    {
        var presetName = AppPathHelper.SanitizePresetName(selectedPresetName);
        if (string.IsNullOrWhiteSpace(presetName))
        {
            StatusText = Properties.Loc.StatusPresetNameRequired;
            return;
        }

        try
        {
            SettingsService.Save(AppPathHelper.GetPresetFilePath(presetType, presetName), createData());
            LoadPresetNames();
            selectPreset(presetName);
            StatusText = string.Format(Properties.Loc.StatusPresetSaved, displayName, presetName);
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to save {presetType} preset: {ex.Message}");
        }
    }

    public void LoadSettings(string? customPath = null)
    {
        try
        {
            var path = customPath ?? AppPathHelper.GetSettingsFilePath();
            if (!File.Exists(path))
            {
                return;
            }

            var data = SettingsService.Load(path);

            ApplyGeneralSettings(data);

            ApplyOfficeSettings(data);

            ApplyPdfSettings(data);

            ApplyImageSettings(data);
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to load settings: {ex.Message}");
        }
    }

    private void ApplyGeneralSettings(Dictionary<string, Dictionary<string, string>> data)
    {
        if (!data.TryGetValue("General", out var general))
            return;

        OutputDirectory = ReadString(general, "OutputDirectory", OutputDirectory, requireNonWhiteSpace: true);
        FileNameRule = ReadString(general, "FileNameRule", FileNameRule);
        AppTheme = ReadEnum(general, "ThemeMode", AppTheme);
        Language = ReadString(general, "Language", Language, requireNonWhiteSpace: true);
        ConfirmOnClear = ReadBool(general, "ConfirmOnClear", ConfirmOnClear);
        PlaySoundOnComplete = ReadBool(general, "PlaySoundOnComplete", PlaySoundOnComplete);
        MaxDegreeOfParallelism = ReadInt(general, "MaxDegreeOfParallelism", MaxDegreeOfParallelism);
        WindowWidth = ReadDouble(general, "WindowWidth", WindowWidth);
        WindowHeight = ReadDouble(general, "WindowHeight", WindowHeight);
        WindowLeft = ReadDouble(general, "WindowLeft", double.NaN);
        WindowTop = ReadDouble(general, "WindowTop", double.NaN);
        WindowState = NormalizeRestoredWindowState(ReadEnum(general, "WindowState", WindowState));
        SelectedTabIndex = ReadInt(general, "SelectedTabIndex", SelectedTabIndex);
        SelectedImagePresetName = ReadString(general, "LastImagePresetName", SelectedImagePresetName);
        SelectedOfficePresetName = ReadString(general, "LastOfficePresetName", SelectedOfficePresetName);
        SelectedPdfPresetName = ReadString(general, "LastPdfPresetName", SelectedPdfPresetName);
    }

    private void ApplyOfficeSettings(Dictionary<string, Dictionary<string, string>> data)
    {
        if (!data.TryGetValue("Office", out var opt))
            return;

        EnableOfficeOptimize = ReadBool(opt, "EnableOfficeOptimize", EnableOfficeOptimize);
        StripOfficeMetadata = ReadBool(opt, "StripOfficeMetadata", StripOfficeMetadata);
        CleanUnusedObjects = ReadBool(opt, "CleanUnusedObjects", CleanUnusedObjects);
        ResetCellSelection = ReadBool(opt, "ResetCellSelection", ResetCellSelection);
        ConvertToWebP = ReadBool(opt, "ConvertToWebP", ConvertToWebP);
        ConvertOfficeToPdf = ReadBool(opt, "ConvertOfficeToPdf", ConvertOfficeToPdf);
        ConvertOfficeToPdfA = ReadBool(opt, "ConvertOfficeToPdfA", ConvertOfficeToPdfA);
        WebPQuality = ReadInt(opt, "WebPQuality", WebPQuality);
        CompressEmbeddedImages = ReadBool(opt, "CompressEmbeddedImages", CompressEmbeddedImages);
        ResizeEmbeddedImagesByPpi = ReadBool(opt, "ResizeEmbeddedImagesByPpi", ResizeEmbeddedImagesByPpi);
        TargetImagePpi = ReadInt(opt, "TargetImagePpi", TargetImagePpi);
        CompressMedia = ReadBool(opt, "CompressMedia", CompressMedia);
        MediaVideoCrf = ReadInt(opt, "MediaVideoCrf", MediaVideoCrf);
        MediaVideoCodec = ReadString(opt, "MediaVideoCodec", MediaVideoCodec);
        MediaAudioCodec = ReadString(opt, "MediaAudioCodec", MediaAudioCodec);
        FfmpegPath = ReadString(opt, "FfmpegPath", FfmpegPath);
        OxipngPath = ReadString(opt, "OxipngPath", OxipngPath);
        UseOxipng = ReadBool(opt, "UseOxipng", UseOxipng);
        OxipngLevel = ReadInt(opt, "OxipngLevel", OxipngLevel);
        UseJpegli = ReadBool(opt, "UseJpegli", UseJpegli);
        CjpegliPath = ReadString(opt, "CjpegliPath", CjpegliPath);
    }

    private void ApplyPdfSettings(Dictionary<string, Dictionary<string, string>> data)
    {
        if (!data.TryGetValue("Pdf", out var pdf))
            return;

        var hasStructureCleanupSetting = false;
        var hasDistributionCompatibilitySetting = false;
        var hasRestrictionRemovalSetting = false;

        QpdfPath = ReadString(pdf, "QpdfPath", QpdfPath);
        PdfOptimizeImages = ReadBool(pdf, "OptimizeImages", PdfOptimizeImages);
        PdfJpegQuality = ReadInt(pdf, "JpegQuality", PdfJpegQuality);
        PdfMinWidth = ReadInt(pdf, "MinWidth", PdfMinWidth);
        PdfMinHeight = ReadInt(pdf, "MinHeight", PdfMinHeight);
        PdfMinArea = ReadInt(pdf, "MinArea", PdfMinArea);
        PdfKeepInlineImages = ReadBool(pdf, "KeepInlineImages", PdfKeepInlineImages);
        PdfExternalizeInlineImages = ReadBool(pdf, "ExternalizeInlineImages", PdfExternalizeInlineImages);
        PdfInlineImageMinBytes = ReadInt(pdf, "InlineImageMinBytes", PdfInlineImageMinBytes);
        PdfCompressStreams = ReadBool(pdf, "CompressStreams", PdfCompressStreams);
        PdfCompressionLevel = ReadInt(pdf, "CompressionLevel", PdfCompressionLevel);
        PdfDecodeLevel = ReadString(pdf, "DecodeLevel", PdfDecodeLevel);
        PdfRecompressFlate = ReadBool(pdf, "RecompressFlate", PdfRecompressFlate);
        if (TryReadBool(pdf, "StructureCleanup", out var parsedStructureCleanup))
        {
            PdfStructureCleanup = parsedStructureCleanup;
            hasStructureCleanupSetting = true;
        }

        PdfObjectStreamMode = ReadString(pdf, "ObjectStreamMode", PdfObjectStreamMode);
        if (!pdf.ContainsKey("ObjectStreamMode"))
            PdfGenerateObjectStreams = ReadBool(pdf, "GenerateObjectStreams", PdfGenerateObjectStreams);

        PdfRemoveUnreferencedResources = ReadString(pdf, "RemoveUnreferencedResources", PdfRemoveUnreferencedResources);
        PdfPreserveUnreferencedObjects = ReadBool(pdf, "PreserveUnreferencedObjects", PdfPreserveUnreferencedObjects);
        PdfNormalizeContent = ReadBool(pdf, "NormalizeContent", PdfNormalizeContent);
        PdfCoalesceContents = ReadBool(pdf, "CoalesceContents", PdfCoalesceContents);
        PdfNewlineBeforeEndStream = ReadBool(pdf, "NewlineBeforeEndStream", PdfNewlineBeforeEndStream);
        if (TryReadBool(pdf, "DistributionCompatibility", out var parsedDistributionCompatibility))
        {
            PdfDistributionCompatibility = parsedDistributionCompatibility;
            hasDistributionCompatibilitySetting = true;
        }

        PdfDecrypt = ReadBool(pdf, "Decrypt", PdfDecrypt);
        PdfRemoveRestrictions = ReadBool(pdf, "RemoveRestrictions", PdfRemoveRestrictions);
        if (TryReadBool(pdf, "RestrictionRemoval", out var parsedRestrictionRemoval))
        {
            PdfRestrictionRemoval = parsedRestrictionRemoval;
            hasRestrictionRemovalSetting = true;
        }

        PdfMinVersion = ReadString(pdf, "MinVersion", PdfMinVersion);
        PdfForceVersion = ReadString(pdf, "ForceVersion", PdfForceVersion);
        PdfLinearize = ReadBool(pdf, "Linearize", PdfLinearize);

        if (!hasStructureCleanupSetting)
            PdfStructureCleanup = InferPdfStructureCleanup();

        if (!hasDistributionCompatibilitySetting)
            PdfDistributionCompatibility = InferPdfDistributionCompatibility();

        if (!hasRestrictionRemovalSetting)
            PdfRestrictionRemoval = InferPdfRestrictionRemoval();
    }

    private bool InferPdfStructureCleanup()
        => PdfExternalizeInlineImages
           || PdfObjectStreamMode != "preserve"
           || PdfRemoveUnreferencedResources != "auto"
           || PdfPreserveUnreferencedObjects
           || PdfNormalizeContent
           || PdfCoalesceContents
           || PdfNewlineBeforeEndStream;

    private bool InferPdfDistributionCompatibility()
        => PdfLinearize
           || !string.IsNullOrWhiteSpace(PdfMinVersion)
           || !string.IsNullOrWhiteSpace(PdfForceVersion);

    private bool InferPdfRestrictionRemoval()
        => PdfDecrypt || PdfRemoveRestrictions;

    private void ApplyImageSettings(Dictionary<string, Dictionary<string, string>> data)
    {
        if (!data.TryGetValue("Image", out var pipeline) || !pipeline.TryGetValue("Enabled", out var enabledList))
            return;

        foreach (var step in Steps)
            step.Enabled = false;

        foreach (var typeName in enabledList.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedTypeName = typeName.Trim();
            if (Enum.TryParse<PipelineStepType>(trimmedTypeName, out var type) &&
                data.TryGetValue("Image." + trimmedTypeName, out var stepData))
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

    private static WindowState NormalizeRestoredWindowState(WindowState state)
        => state == WindowState.Minimized ? WindowState.Normal : state;

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
                data["SharpenSigma"] = step.SharpenSigma.ToString(CultureInfo.InvariantCulture);
                break;
            case PipelineStepType.ColorAdjust:
                data["Brightness"] = step.Brightness.ToString();
                data["Contrast"] = step.Contrast.ToString();
                break;
            case PipelineStepType.ToneCurve:
                data["ToneGamma"] = step.ToneGamma.ToString(CultureInfo.InvariantCulture);
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
        switch (step.Type)
        {
            case PipelineStepType.Crop:
                step.CropWidth = ReadInt(data, "CropWidth", step.CropWidth);
                step.CropHeight = ReadInt(data, "CropHeight", step.CropHeight);
                break;
            case PipelineStepType.Rotate:
                step.RotateTarget = ReadEnum(data, "RotateTarget", step.RotateTarget);
                step.RotationDegrees = ReadInt(data, "RotationDegrees", step.RotationDegrees);
                break;
            case PipelineStepType.Resize:
                step.TargetWidth = ReadInt(data, "TargetWidth", step.TargetWidth);
                step.TargetHeight = ReadInt(data, "TargetHeight", step.TargetHeight);
                step.FitMode = ReadEnum(data, "FitMode", step.FitMode);
                step.AllowUpscale = ReadBool(data, "AllowUpscale", step.AllowUpscale);
                break;
            case PipelineStepType.Padding:
                step.PaddingSize = ReadInt(data, "PaddingSize", step.PaddingSize);
                step.PaddingRed = ReadInt(data, "PaddingRed", step.PaddingRed);
                step.PaddingGreen = ReadInt(data, "PaddingGreen", step.PaddingGreen);
                step.PaddingBlue = ReadInt(data, "PaddingBlue", step.PaddingBlue);
                break;
            case PipelineStepType.Sharpen:
                step.SharpenSigma = ReadDouble(data, "SharpenSigma", step.SharpenSigma);
                break;
            case PipelineStepType.ColorAdjust:
                step.Brightness = ReadInt(data, "Brightness", step.Brightness);
                step.Contrast = ReadInt(data, "Contrast", step.Contrast);
                break;
            case PipelineStepType.ToneCurve:
                step.ToneGamma = ReadDouble(data, "ToneGamma", step.ToneGamma);
                break;
            case PipelineStepType.FormatConvert:
                step.TargetFormat = ReadEnum(data, "TargetFormat", step.TargetFormat);
                step.Quality = ReadInt(data, "Quality", step.Quality);
                step.CompressionLevel = ReadInt(data, "CompressionLevel", step.CompressionLevel);
                break;
            case PipelineStepType.Optimize:
                step.StripMetadata = ReadBool(data, "StripMetadata", step.StripMetadata);
                step.OptimizeCoding = ReadBool(data, "OptimizeCoding", step.OptimizeCoding);
                step.TrellisQuant = ReadBool(data, "TrellisQuant", step.TrellisQuant);
                step.ReductionEffort = ReadInt(data, "ReductionEffort", step.ReductionEffort);
                step.Lossless = ReadBool(data, "Lossless", step.Lossless);
                break;
            case PipelineStepType.Posterize:
                step.BitsPerChannel = ReadInt(data, "BitsPerChannel", step.BitsPerChannel);
                break;
            case PipelineStepType.Composite:
                step.CompositePath = ReadString(data, "CompositePath", step.CompositePath ?? "");
                step.CompositeX = ReadInt(data, "CompositeX", step.CompositeX);
                step.CompositeY = ReadInt(data, "CompositeY", step.CompositeY);
                break;
            // Grayscale, ExifAutoRotate: 追加プロパティなし
        }
    }

    private Dictionary<string, Dictionary<string, string>> CreateSettingsData(
        bool includeGeneral,
        bool includeOffice = true,
        bool includePdf = true,
        bool includeImage = true)
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (includeGeneral)
            data["General"] = CreateGeneralSettingsSection();

        if (includeOffice)
            data["Office"] = CreateOfficeSettingsSection();

        if (includePdf)
            data["Pdf"] = CreatePdfSettingsSection();

        if (includeImage)
            AddImageSettingsSections(data);

        return data;
    }

    private Dictionary<string, string> CreateGeneralSettingsSection()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputDirectory"] = OutputDirectory ?? "",
            ["FileNameRule"] = FileNameRule ?? "",
            ["ThemeMode"] = AppTheme.ToString(),
            ["Language"] = Language,
            ["ConfirmOnClear"] = ConfirmOnClear.ToString(),
            ["PlaySoundOnComplete"] = PlaySoundOnComplete.ToString(),
            ["MaxDegreeOfParallelism"] = MaxDegreeOfParallelism.ToString(),
            ["WindowWidth"] = WindowWidth.ToString(CultureInfo.InvariantCulture),
            ["WindowHeight"] = WindowHeight.ToString(CultureInfo.InvariantCulture),
            ["WindowLeft"] = WindowLeft.ToString(CultureInfo.InvariantCulture),
            ["WindowTop"] = WindowTop.ToString(CultureInfo.InvariantCulture),
            ["WindowState"] = (WindowState == WindowState.Minimized ? WindowState.Normal : WindowState).ToString(),
            ["SelectedTabIndex"] = SelectedTabIndex.ToString(),
            ["LastImagePresetName"] = SelectedImagePresetName ?? "",
            ["LastOfficePresetName"] = SelectedOfficePresetName ?? "",
            ["LastPdfPresetName"] = SelectedPdfPresetName ?? ""
        };

    private Dictionary<string, string> CreateOfficeSettingsSection()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["EnableOfficeOptimize"] = EnableOfficeOptimize.ToString(),
            ["StripOfficeMetadata"] = StripOfficeMetadata.ToString(),
            ["CleanUnusedObjects"] = CleanUnusedObjects.ToString(),
            ["ResetCellSelection"] = ResetCellSelection.ToString(),
            ["ConvertToWebP"] = ConvertToWebP.ToString(),
            ["ConvertOfficeToPdf"] = ConvertOfficeToPdf.ToString(),
            ["ConvertOfficeToPdfA"] = ConvertOfficeToPdfA.ToString(),
            ["WebPQuality"] = WebPQuality.ToString(),
            ["CompressEmbeddedImages"] = CompressEmbeddedImages.ToString(),
            ["ResizeEmbeddedImagesByPpi"] = ResizeEmbeddedImagesByPpi.ToString(),
            ["TargetImagePpi"] = TargetImagePpi.ToString(),
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

    private Dictionary<string, string> CreatePdfSettingsSection()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["QpdfPath"] = QpdfPath ?? "",
            ["OptimizeImages"] = PdfOptimizeImages.ToString(),
            ["JpegQuality"] = PdfJpegQuality.ToString(),
            ["MinWidth"] = PdfMinWidth.ToString(),
            ["MinHeight"] = PdfMinHeight.ToString(),
            ["MinArea"] = PdfMinArea.ToString(),
            ["KeepInlineImages"] = PdfKeepInlineImages.ToString(),
            ["ExternalizeInlineImages"] = PdfExternalizeInlineImages.ToString(),
            ["InlineImageMinBytes"] = PdfInlineImageMinBytes.ToString(),
            ["CompressStreams"] = PdfCompressStreams.ToString(),
            ["CompressionLevel"] = PdfCompressionLevel.ToString(),
            ["DecodeLevel"] = PdfDecodeLevel,
            ["RecompressFlate"] = PdfRecompressFlate.ToString(),
            ["StructureCleanup"] = PdfStructureCleanup.ToString(),
            ["ObjectStreamMode"] = PdfObjectStreamMode,
            ["GenerateObjectStreams"] = PdfGenerateObjectStreams.ToString(),
            ["RemoveUnreferencedResources"] = PdfRemoveUnreferencedResources,
            ["PreserveUnreferencedObjects"] = PdfPreserveUnreferencedObjects.ToString(),
            ["NormalizeContent"] = PdfNormalizeContent.ToString(),
            ["CoalesceContents"] = PdfCoalesceContents.ToString(),
            ["NewlineBeforeEndStream"] = PdfNewlineBeforeEndStream.ToString(),
            ["DistributionCompatibility"] = PdfDistributionCompatibility.ToString(),
            ["Decrypt"] = PdfDecrypt.ToString(),
            ["RemoveRestrictions"] = PdfRemoveRestrictions.ToString(),
            ["RestrictionRemoval"] = PdfRestrictionRemoval.ToString(),
            ["MinVersion"] = PdfMinVersion,
            ["ForceVersion"] = PdfForceVersion,
            ["Linearize"] = PdfLinearize.ToString()
        };

    private void AddImageSettingsSections(Dictionary<string, Dictionary<string, string>> data)
    {
        var enabledSteps = Steps.Where(s => s.Enabled).ToList();
        data["Image"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Enabled"] = string.Join(",", enabledSteps.Select(s => s.Type.ToString()))
        };

        foreach (var step in enabledSteps)
        {
            var stepData = CreateSettingsSection();
            SaveStepProperties(step, stepData);
            data["Image." + step.Type.ToString()] = stepData;
        }
    }

    private static Dictionary<string, string> CreateSettingsSection()
        => new(StringComparer.OrdinalIgnoreCase);

    public void SaveSettings(string? customPath = null)
    {
        try
        {
            var path = customPath ?? AppPathHelper.GetSettingsFilePath();
            SettingsService.Save(path, CreateSettingsData(includeGeneral: true));
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to save settings: {ex.Message}");
        }
    }

    // --- INotifyPropertyChanged ---
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        if (!_suppressNotifications)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
