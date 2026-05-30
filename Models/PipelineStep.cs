using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileMill.Models;

public enum PipelineStepType
{
    Grayscale,
    ExifAutoRotate,
    Crop,
    Rotate,
    Resize,
    Padding,
    Sharpen,
    ColorAdjust,
    ToneCurve,
    FormatConvert,
    Optimize,
    Posterize,
    Composite
}

public enum OutputFormat
{
    Jpeg,
    Png,
    WebP,
    Avif,
    Tiff
}

public enum FitMode
{
    Inside,   // 内包（はみ出さないよう縮小）
    Cover,    // 外接（はみ出しクロップ）
    Fill      // 引き伸ばし（縦横比無視）
}

public enum RotateTarget
{
    All,
    Landscape,
    Portrait
}

public class PipelineStep : INotifyPropertyChanged
{
    private PipelineStepType _type;
    private bool _enabled = true;

    // Crop params
    private int _cropWidth;
    private int _cropHeight;

    // Rotate params
    private RotateTarget _rotateTarget = RotateTarget.All;
    private int _rotationDegrees = 90;

    // FormatConvert params
    private OutputFormat _targetFormat = OutputFormat.Jpeg;
    private int _quality = 85;
    private int _compressionLevel = 6;

    // Resize params
    private int _targetWidth = 1920;
    private int _targetHeight = 1080;
    private FitMode _fitMode = FitMode.Inside;
    private bool _allowUpscale;

    // Padding params
    private int _paddingSize;
    private int _paddingRed = 255;
    private int _paddingGreen = 255;
    private int _paddingBlue = 255;

    // Sharpen params
    private double _sharpenSigma = 1.0;

    // Color adjustment params
    private int _brightness;
    private int _contrast;

    // Tone curve params
    private double _toneGamma = 1.0;

    // Composite params
    private string _compositePath = "";
    private int _compositeX;
    private int _compositeY;

    // Optimize params
    private bool _stripMetadata = true;
    private bool _optimizeCoding = true;
    private bool _trellisQuant;
    private int _reductionEffort = 4;
    private bool _lossless;

    // Posterize params
    private int _bitsPerChannel = 6;

    public PipelineStepType Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(); }
    }

    public string DisplayName => Type switch
    {
        PipelineStepType.Grayscale => "グレースケール化",
        PipelineStepType.ExifAutoRotate => "Exif自動回転",
        PipelineStepType.Crop => "トリミング",
        PipelineStepType.Rotate => "回転",
        PipelineStepType.Padding => "余白の追加",
        PipelineStepType.Sharpen => "アンシャープマスク",
        PipelineStepType.ColorAdjust => "色調補正",
        PipelineStepType.ToneCurve => "トーンカーブ",
        PipelineStepType.FormatConvert => "フォーマット変換",
        PipelineStepType.Resize => "リサイズ",
        PipelineStepType.Optimize => "最適化",
        PipelineStepType.Posterize => "減色",
        PipelineStepType.Composite => "画像合成",
        _ => Type.ToString()
    };

    // --- Crop ---
    public int CropWidth { get => _cropWidth; set { _cropWidth = value; OnPropertyChanged(); } }
    public int CropHeight { get => _cropHeight; set { _cropHeight = value; OnPropertyChanged(); } }

    // --- Rotate ---
    public RotateTarget RotateTarget { get => _rotateTarget; set { _rotateTarget = value; OnPropertyChanged(); OnPropertyChanged(nameof(RotateTargetIndex)); } }
    public int RotationDegrees { get => _rotationDegrees; set { _rotationDegrees = value; OnPropertyChanged(); OnPropertyChanged(nameof(RotationAngleIndex)); } }

    public int RotateTargetIndex
    {
        get => (int)_rotateTarget;
        set { RotateTarget = (RotateTarget)value; }
    }

    public int RotationAngleIndex
    {
        get => _rotationDegrees switch
        {
            180 => 1,
            270 => 2,
            _ => 0
        };
        set
        {
            RotationDegrees = value switch
            {
                1 => 180,
                2 => 270,
                _ => 90
            };
        }
    }

    // --- FormatConvert ---
    public OutputFormat TargetFormat { get => _targetFormat; set { _targetFormat = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetFormatIndex)); } }
    public int Quality { get => _quality; set { _quality = value; OnPropertyChanged(); } }
    public int CompressionLevel { get => _compressionLevel; set { _compressionLevel = value; OnPropertyChanged(); } }

    // ComboBox SelectedIndex binding helper
    public int TargetFormatIndex
    {
        get => (int)_targetFormat;
        set { TargetFormat = (OutputFormat)value; }
    }

    // --- Resize ---
    public int TargetWidth { get => _targetWidth; set { _targetWidth = value; OnPropertyChanged(); } }
    public int TargetHeight { get => _targetHeight; set { _targetHeight = value; OnPropertyChanged(); } }
    public FitMode FitMode { get => _fitMode; set { _fitMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(FitModeIndex)); } }
    public bool AllowUpscale { get => _allowUpscale; set { _allowUpscale = value; OnPropertyChanged(); } }

    // ComboBox SelectedIndex binding helper
    public int FitModeIndex
    {
        get => (int)_fitMode;
        set { FitMode = (FitMode)value; }
    }

    // --- Padding ---
    public int PaddingSize { get => _paddingSize; set { _paddingSize = value; OnPropertyChanged(); } }
    public int PaddingRed { get => _paddingRed; set { _paddingRed = value; OnPropertyChanged(); } }
    public int PaddingGreen { get => _paddingGreen; set { _paddingGreen = value; OnPropertyChanged(); } }
    public int PaddingBlue { get => _paddingBlue; set { _paddingBlue = value; OnPropertyChanged(); } }

    // --- Sharpen ---
    public double SharpenSigma { get => _sharpenSigma; set { _sharpenSigma = value; OnPropertyChanged(); } }

    // --- Color adjustment ---
    public int Brightness { get => _brightness; set { _brightness = value; OnPropertyChanged(); } }
    public int Contrast { get => _contrast; set { _contrast = value; OnPropertyChanged(); } }

    // --- Tone curve ---
    public double ToneGamma { get => _toneGamma; set { _toneGamma = value; OnPropertyChanged(); } }

    // --- Composite ---
    public string CompositePath { get => _compositePath; set { _compositePath = value; OnPropertyChanged(); } }
    public int CompositeX { get => _compositeX; set { _compositeX = value; OnPropertyChanged(); } }
    public int CompositeY { get => _compositeY; set { _compositeY = value; OnPropertyChanged(); } }

    // --- Optimize ---
    public bool StripMetadata { get => _stripMetadata; set { _stripMetadata = value; OnPropertyChanged(); } }
    public bool OptimizeCoding { get => _optimizeCoding; set { _optimizeCoding = value; OnPropertyChanged(); } }
    public bool TrellisQuant { get => _trellisQuant; set { _trellisQuant = value; OnPropertyChanged(); } }
    public int ReductionEffort { get => _reductionEffort; set { _reductionEffort = value; OnPropertyChanged(); } }
    public bool Lossless { get => _lossless; set { _lossless = value; OnPropertyChanged(); } }

    // --- Posterize ---
    public int BitsPerChannel { get => _bitsPerChannel; set { _bitsPerChannel = value; OnPropertyChanged(); OnPropertyChanged(nameof(LevelsPerChannel)); OnPropertyChanged(nameof(BitsIndex)); } }
    public int LevelsPerChannel => 1 << _bitsPerChannel;

    // ComboBox SelectedIndex binding helper (0=8bit ... 7=1bit)
    public int BitsIndex
    {
        get => 8 - _bitsPerChannel;
        set => BitsPerChannel = 8 - value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public PipelineStep Clone()
    {
        return new PipelineStep
        {
            Type = Type,
            Enabled = Enabled,
            CropWidth = CropWidth,
            CropHeight = CropHeight,
            RotateTarget = RotateTarget,
            RotationDegrees = RotationDegrees,
            TargetFormat = TargetFormat,
            Quality = Quality,
            CompressionLevel = CompressionLevel,
            TargetWidth = TargetWidth,
            TargetHeight = TargetHeight,
            FitMode = FitMode,
            AllowUpscale = AllowUpscale,
            PaddingSize = PaddingSize,
            PaddingRed = PaddingRed,
            PaddingGreen = PaddingGreen,
            PaddingBlue = PaddingBlue,
            SharpenSigma = SharpenSigma,
            Brightness = Brightness,
            Contrast = Contrast,
            ToneGamma = ToneGamma,
            CompositePath = CompositePath,
            CompositeX = CompositeX,
            CompositeY = CompositeY,
            StripMetadata = StripMetadata,
            OptimizeCoding = OptimizeCoding,
            TrellisQuant = TrellisQuant,
            ReductionEffort = ReductionEffort,
            Lossless = Lossless,
            BitsPerChannel = BitsPerChannel
        };
    }
}
