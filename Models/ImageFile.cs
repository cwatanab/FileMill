using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FileMill.Models;

public class ImageFile : INotifyPropertyChanged
{
    private bool _isChecked = true;
    private string _filePath = "";
    private long _fileSize;
    private string _format = "";
    private int _width;
    private int _height;
    private DateTime _dateModified;
    private DateTime? _dateTaken;

    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileName)); }
    }

    public string FileName => Path.GetFileName(FilePath);

    public long FileSize
    {
        get => _fileSize;
        set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); }
    }

    public string Format
    {
        get => _format;
        set { _format = value; OnPropertyChanged(); }
    }

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

    public string DateModifiedDisplay => DateModified.ToString("yyyy/MM/dd HH:mm");
    public string DateTakenDisplay => DateTaken?.ToString("yyyy/MM/dd HH:mm") ?? DateModifiedDisplay;

    public string FileSizeDisplay => FileMill.Helpers.FormatHelper.FormatFileSize(FileSize);

    public string DimensionsDisplay => Width > 0 && Height > 0
        ? $"{Width} × {Height}"
        : "?";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
