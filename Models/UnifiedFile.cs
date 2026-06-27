using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FileMill.Models;

public enum FileType
{
    Image,
    Office,
    Pdf
}

public class UnifiedFile : INotifyPropertyChanged
{
    private bool _isChecked = true;
    private string _filePath = "";
    private string _format = "";
    private string _status = Properties.Loc.StatusWaiting;
    private bool _isProcessing;

    // サイズ関連 (Office / PDF で主に使用)
    private long _originalSize;
    private long? _optimizedSize;

    // 画像用プロパティ
    private int _width;
    private int _height;
    private DateTime _dateModified;
    private DateTime? _dateTaken;

    public FileType FileType { get; set; }

    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set 
        { 
            _filePath = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(FileName)); 
            if (!string.IsNullOrEmpty(value))
            {
                Format = Path.GetExtension(value).TrimStart('.').ToUpper();
            }
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public string Format
    {
        get => _format;
        set { _format = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { if (_status == value) return; _status = value; OnPropertyChanged(); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); }
    }

    public long OriginalSize
    {
        get => _originalSize;
        set { _originalSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(OriginalSizeDisplay)); OnPropertyChanged(nameof(SavingDisplay)); }
    }

    public long? OptimizedSize
    {
        get => _optimizedSize;
        set { _optimizedSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(OptimizedSizeDisplay)); OnPropertyChanged(nameof(SavingDisplay)); }
    }

    // 画像用
    public int Width
    {
        get => _width;
        set { _width = value; OnPropertyChanged(); OnPropertyChanged(nameof(DimensionsDisplay)); }
    }

    public int Height
    {
        get => _height;
        set { _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(DimensionsDisplay)); }
    }

    public DateTime DateModified
    {
        get => _dateModified;
        set { _dateModified = value; OnPropertyChanged(); OnPropertyChanged(nameof(DateModifiedDisplay)); }
    }

    public DateTime? DateTaken
    {
        get => _dateTaken;
        set { _dateTaken = value; OnPropertyChanged(); OnPropertyChanged(nameof(DateTakenDisplay)); }
    }

    public string OriginalSizeDisplay => FileMill.Helpers.FormatHelper.FormatFileSize(OriginalSize);
    public string OptimizedSizeDisplay => OptimizedSize.HasValue ? FileMill.Helpers.FormatHelper.FormatFileSize(OptimizedSize.Value) : "-";

    public string SavingDisplay
    {
        get
        {
            if (!OptimizedSize.HasValue || OriginalSize <= 0) return "-";
            long saved = OriginalSize - OptimizedSize.Value;
            double percent = (double)saved / OriginalSize * 100.0;
            return $"{percent:F1}%";
        }
    }

    public string DateModifiedDisplay => DateModified.ToString("yyyy/MM/dd HH:mm");
    public string DateTakenDisplay => DateTaken?.ToString("yyyy/MM/dd HH:mm") ?? DateModifiedDisplay;
    public string DimensionsDisplay => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "-";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
