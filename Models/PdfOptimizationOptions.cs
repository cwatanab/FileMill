namespace FileMill.Models;

public class PdfOptimizationOptions
{
    public bool OptimizeImages { get; set; }
    public int JpegQuality { get; set; } = 85;
    public int MinWidth { get; set; } = 128;
    public int MinHeight { get; set; } = 128;
    public int MinArea { get; set; } = 16384;
    public bool KeepInlineImages { get; set; }

    public bool CompressStreams { get; set; } = true;
    public int CompressionLevel { get; set; } = 6;
    public bool GenerateObjectStreams { get; set; }
    public bool Linearize { get; set; }
}
