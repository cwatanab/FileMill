namespace FileMill.Models;

public class PdfOptimizationOptions
{
    public bool OptimizeImages { get; set; }
    public int JpegQuality { get; set; } = 85;
    public int MinWidth { get; set; } = 128;
    public int MinHeight { get; set; } = 128;
    public int MinArea { get; set; } = 16384;
    public bool KeepInlineImages { get; set; }
    public bool ExternalizeInlineImages { get; set; }
    public int InlineImageMinBytes { get; set; } = 1024;

    public bool CompressStreams { get; set; } = true;
    public int CompressionLevel { get; set; } = 6;
    public string DecodeLevel { get; set; } = "generalized";
    public bool RecompressFlate { get; set; }
    public bool StructureCleanup { get; set; }
    public string ObjectStreamMode { get; set; } = "preserve";
    public string RemoveUnreferencedResources { get; set; } = "auto";
    public bool PreserveUnreferencedObjects { get; set; }
    public bool NormalizeContent { get; set; }
    public bool CoalesceContents { get; set; }
    public bool NewlineBeforeEndStream { get; set; }
    public bool DistributionCompatibility { get; set; }
    public bool Decrypt { get; set; }
    public bool RemoveRestrictions { get; set; }
    public bool RestrictionRemoval { get; set; }
    public string MinVersion { get; set; } = "";
    public string ForceVersion { get; set; } = "";
    public bool Linearize { get; set; }
}
