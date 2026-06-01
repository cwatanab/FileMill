using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FileMill.Models;

public class OptimizeFile : INotifyPropertyChanged
{
    private bool _isChecked = true;
    private string _filePath = "";
    private long _originalSize;
    private long? _optimizedSize;
    private string _format = "";
    private string _status = Properties.Loc.StatusWaiting;
    private bool _isProcessing;

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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
