using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;
using NetVips;
using FileMill.Models;

namespace FileMill.Services;

public class ImageProcessingService
{
    private const int WordPdfExportFormat = 17;
    private const int ExcelPdfExportType = 0;
    private const int PowerPointPdfFixedFormatType = 2;
    private const int PowerPointSaveAsPdfFileType = 32;
    private const double EmusPerInch = 914400d;

    private static readonly Lazy<OfficePdfInteropAvailability> CachedOfficePdfInteropAvailability = new(() => new OfficePdfInteropAvailability(
        HasComProgId("Word.Application"),
        HasComProgId("Excel.Application"),
        HasComProgId("PowerPoint.Application")));

    public static bool IsOfficePdfConversionAvailable => CachedOfficePdfInteropAvailability.Value.IsAnyAvailable;

    public static bool IsOfficePdfConversionAvailableForExtension(string extension)
    {
        var availability = CachedOfficePdfInteropAvailability.Value;
        return extension.ToLowerInvariant() switch
        {
            ".docx" => availability.Word,
            ".xlsx" => availability.Excel,
            ".pptx" => availability.PowerPoint,
            _ => false
        };
    }

    public static string OfficePdfConversionAvailabilityMessage
    {
        get
        {
            var availability = CachedOfficePdfInteropAvailability.Value;
            if (!availability.IsAnyAvailable)
                return Properties.Loc.TooltipOfficePdfUnavailable;

            var availableApps = new List<string>();
            if (availability.Word) availableApps.Add("Word");
            if (availability.Excel) availableApps.Add("Excel");
            if (availability.PowerPoint) availableApps.Add("PowerPoint");
            return string.Format(Properties.Loc.TooltipOfficePdfAvailable, string.Join(", ", availableApps));
        }
    }

    /// <summary>
    /// 画像を処理し、出力先に保存する。
    /// </summary>
    public void Process(string inputPath, string outputPath, IReadOnlyList<PipelineStep> steps,
        bool useOxipng = false, string oxipngPath = "oxipng", int oxipngLevel = 2,
        bool useJpegli = false, string cjpegliPath = "cjpegli", Action<string>? logAction = null)
    {
        var enabled = steps.Where(s => s.Enabled).ToList();
        
        if (logAction != null)
        {
            var stepInfo = string.Join(", ", enabled.Select(s => s.Type.ToString()));
            logAction($"[Process] 開始: {Path.GetFileName(inputPath)} -> {Path.GetFileName(outputPath)}");
            logAction($"[Process] 引数: steps=[{stepInfo}], useOxipng={useOxipng}, oxipngPath=\"{oxipngPath}\", oxipngLevel={oxipngLevel}, useJpegli={useJpegli}, cjpegliPath=\"{cjpegliPath}\"");
        }

        var formatStep = enabled.LastOrDefault(s => s.Type == PipelineStepType.FormatConvert);
        var optimizeStep = enabled.LastOrDefault(s => s.Type == PipelineStepType.Optimize);
        if (enabled.Count == 0)
        {
            File.Copy(inputPath, outputPath, true);
            return;
        }

        using var image = Image.NewFromFile(inputPath, memory: true);

        Image? processed = null;
        bool owned = false;

        try
        {
            processed = image;

            foreach (var step in enabled)
            {
                if (step.Type is PipelineStepType.FormatConvert or PipelineStepType.Optimize)
                    continue;

                var next = ApplyStep(processed, step);
                if (!ReferenceEquals(next, processed))
                {
                    if (owned) processed.Dispose();
                    processed = next;
                    owned = true;
                }
            }

            var fmt = formatStep?.TargetFormat ?? GetFormatFromExtension(inputPath);

            // 出力ディレクトリがなければ作成
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            SaveWithFormat(processed, outputPath, fmt, formatStep, optimizeStep,
                useOxipng, oxipngPath, oxipngLevel, useJpegli, cjpegliPath, logAction);
        }
        finally
        {
            if (owned && processed != null)
                processed.Dispose();
        }
    }

    private static Image ApplyStep(Image image, PipelineStep step)
        => step.Type switch
        {
            PipelineStepType.ExifAutoRotate => image.Autorot(),
            PipelineStepType.Grayscale => image.Colourspace(Enums.Interpretation.Bw),
            PipelineStepType.Crop => ApplyCrop(image, step),
            PipelineStepType.Rotate => ApplyRotate(image, step),
            PipelineStepType.Resize => ApplyResize(image, step),
            PipelineStepType.Padding => ApplyPadding(image, step),
            PipelineStepType.Sharpen => step.SharpenSigma > 0 ? image.Sharpen(sigma: step.SharpenSigma) : image,
            PipelineStepType.ColorAdjust => ApplyColorAdjust(image, step),
            PipelineStepType.ToneCurve => step.ToneGamma > 0 && Math.Abs(step.ToneGamma - 1.0) > 0.001 ? image.Gamma(exponent: step.ToneGamma) : image,
            PipelineStepType.Posterize => ApplyPosterize(image, step),
            PipelineStepType.Composite => ApplyComposite(image, step),
            _ => image
        };

    private static Image ApplyCrop(Image image, PipelineStep step)
    {
        if (step.CropWidth <= 0 && step.CropHeight <= 0)
            return image;

        var width = step.CropWidth > 0 ? Math.Min(step.CropWidth, image.Width) : image.Width;
        var height = step.CropHeight > 0 ? Math.Min(step.CropHeight, image.Height) : image.Height;
        var left = Math.Max(0, (image.Width - width) / 2);
        var top = Math.Max(0, (image.Height - height) / 2);

        return image.ExtractArea(left, top, width, height);
    }

    private static Image ApplyRotate(Image image, PipelineStep step)
    {
        var isLandscape = image.Width >= image.Height;
        if (step.RotateTarget == RotateTarget.Landscape && !isLandscape)
            return image;
        if (step.RotateTarget == RotateTarget.Portrait && isLandscape)
            return image;

        var angle = step.RotationDegrees switch
        {
            180 => Enums.Angle.D180,
            270 => Enums.Angle.D270,
            _ => Enums.Angle.D90
        };

        return image.Rot(angle);
    }

    /// <summary>
    /// ビット深度を落として減色（ポスタリゼーション）。
    /// 例: bitsPerChannel=6 → 64階調/ch, bitsPerChannel=4 → 16階調/ch
    /// </summary>
    private static Image ApplyPosterize(Image image, PipelineStep step)
    {
        int bits = step.BitsPerChannel;
        if (bits < 1 || bits >= 8) return image;

        int shift = 8 - bits;
        // >> と << 演算子で下位ビットをゼロ埋め（ポスタリゼーション）
        return (image >> shift) << shift;
    }

    private static Image ApplyResize(Image image, PipelineStep step)
    {
        int w = step.TargetWidth;
        int h = step.TargetHeight;
        if (w <= 0 && h <= 0) return image;
        if (w <= 0)
            w = Math.Max(1, (int)Math.Round(image.Width * (double)h / image.Height));
        if (h <= 0)
            h = Math.Max(1, (int)Math.Round(image.Height * (double)w / image.Width));

        switch (step.FitMode)
        {
            case FitMode.Inside:
                // 内包：指定サイズに収まるよう縮小（拡大は AllowUpscale 次第）
                return image.ThumbnailImage(w, h,
                    size: step.AllowUpscale ? Enums.Size.Both : Enums.Size.Down,
                    noRotate: true);

            case FitMode.Cover:
                // 外接：指定サイズを覆うよう縮小＋中央クロップ
                return image.ThumbnailImage(w, h,
                    crop: Enums.Interesting.Centre,
                    size: step.AllowUpscale ? Enums.Size.Both : Enums.Size.Down,
                    noRotate: true);

            case FitMode.Fill:
                // 引き伸ばし：縦横比無視で正確に指定サイズへ
                return image.Resize(
                    (double)w / image.Width,
                    vscale: (double)h / image.Height);

            default:
                return image;
        }
    }

    private static Image ApplyPadding(Image image, PipelineStep step)
    {
        var padding = Math.Max(0, step.PaddingSize);
        if (padding == 0)
            return image;

        return image.Embed(
            padding,
            padding,
            image.Width + padding * 2,
            image.Height + padding * 2,
            extend: Enums.Extend.Background,
            background: BackgroundForImage(image, step));
    }

    private static Image ApplyColorAdjust(Image image, PipelineStep step)
    {
        var contrast = Math.Clamp(1.0 + step.Contrast / 100.0, 0.01, 3.0);
        var brightness = Math.Clamp(step.Brightness, -100, 100) * 2.55;

        if (Math.Abs(contrast - 1.0) < 0.001 && Math.Abs(brightness) < 0.001)
            return image;

        return image.Linear([contrast], [brightness], uchar: true);
    }

    private static Image ApplyComposite(Image image, PipelineStep step)
    {
        if (string.IsNullOrWhiteSpace(step.CompositePath) || !File.Exists(step.CompositePath))
            return image;

        using var overlay = Image.NewFromFile(step.CompositePath, memory: true);
        return image.Composite2(
            overlay,
            Enums.BlendMode.Over,
            x: Math.Max(0, step.CompositeX),
            y: Math.Max(0, step.CompositeY));
    }

    private static double[] BackgroundForImage(Image image, PipelineStep step)
    {
        var red = ClampByte(step.PaddingRed);
        var green = ClampByte(step.PaddingGreen);
        var blue = ClampByte(step.PaddingBlue);

        return image.Bands switch
        {
            <= 1 => [0.299 * red + 0.587 * green + 0.114 * blue],
            2 => [0.299 * red + 0.587 * green + 0.114 * blue, 255],
            >= 4 => [red, green, blue, 255],
            _ => [red, green, blue]
        };
    }

    private static int ClampByte(int value) => Math.Clamp(value, 0, 255);

    private static void SaveWithFormat(Image image, string path, OutputFormat format,
        PipelineStep? formatStep, PipelineStep? optimizeStep,
        bool useOxipng = false, string oxipngPath = "oxipng", int oxipngLevel = 2,
        bool useJpegli = false, string cjpegliPath = "cjpegli", Action<string>? logAction = null)
    {
        int quality = formatStep?.Quality ?? 85;
        int compression = formatStep?.CompressionLevel ?? 6;
        bool strip = optimizeStep?.StripMetadata ?? true;
        bool lossless = optimizeStep?.Lossless ?? false;
        var keep = strip ? Enums.ForeignKeep.None : Enums.ForeignKeep.Exif;

        switch (format)
        {
            case OutputFormat.Jpeg:
                if (useJpegli)
                {
                    var tempPng = Path.Combine(Path.GetTempPath(), "FileMill_temp_" + Guid.NewGuid().ToString("N") + ".png");
                    try
                    {
                        image.Pngsave(tempPng, compression: 1);
                        var ok = RunCjpegli(tempPng, path, cjpegliPath, quality, logAction);
                        if (!ok)
                        {
                            image.Jpegsave(path,
                                q: quality,
                                optimizeCoding: optimizeStep?.OptimizeCoding ?? true,
                                trellisQuant: optimizeStep?.TrellisQuant ?? false,
                                keep: keep);
                        }
                    }
                    finally
                    {
                        try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch {}
                    }
                }
                else
                {
                    image.Jpegsave(path,
                        q: quality,
                        optimizeCoding: optimizeStep?.OptimizeCoding ?? true,
                        trellisQuant: optimizeStep?.TrellisQuant ?? false,
                        keep: keep);
                }
                break;

            case OutputFormat.Png:
                image.Pngsave(path,
                    compression: compression,
                    filter: Enums.ForeignPngFilter.All,
                    keep: keep);
                if (useOxipng)
                {
                    RunOxipng(path, oxipngPath, oxipngLevel, strip, logAction);
                }
                break;

            case OutputFormat.WebP:
                image.Webpsave(path,
                    q: quality,
                    effort: optimizeStep?.ReductionEffort ?? 4,
                    lossless: lossless,
                    keep: keep);
                break;

            case OutputFormat.Avif:
                image.Heifsave(path,
                    q: quality,
                    lossless: lossless,
                    compression: Enums.ForeignHeifCompression.Av1,
                    effort: optimizeStep?.ReductionEffort ?? 4,
                    keep: keep);
                break;

            case OutputFormat.Tiff:
                image.Tiffsave(path,
                    q: quality,
                    keep: keep);
                break;
        }
    }

    /// <summary>
    /// 画像の基本情報（サイズ・フォーマット）を取得する。
    /// </summary>
    public static ImageFile GetImageInfo(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var dateModified = fileInfo.LastWriteTime;
        DateTime? dateTaken = null;

        try
        {
            using var image = Image.NewFromFile(filePath, memory: true);
            try
            {
                // libvips は EXIF IFD を "exif-ifd2-*" として公開し、値には
                // " (ASCII, 20 components, 20 bytes)" のような注釈が付くため、
                // フィールド名は末尾一致で探し、先頭 19 文字だけをパースする。
                var dateField = image.GetFields()?.FirstOrDefault(
                    f => f.EndsWith("-DateTimeOriginal", StringComparison.Ordinal));
                if (dateField != null)
                {
                    var val = image.Get(dateField)?.ToString()?.Trim('\0').Trim();
                    if (val?.Length >= 19 && DateTime.TryParseExact(val[..19], "yyyy:MM:dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        dateTaken = dt;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse EXIF date taken: {ex.Message}");
            }

            var info = new ImageFile
            {
                FilePath = filePath,
                FileSize = fileInfo.Length,
                Width = image.Width,
                Height = image.Height,
                Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant(),
                DateModified = dateModified,
                DateTaken = dateTaken
            };
            return info;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load image info for {filePath}: {ex.Message}");
            return new ImageFile
            {
                FilePath = filePath,
                FileSize = fileInfo.Length,
                Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant(),
                DateModified = dateModified,
                DateTaken = dateModified
            };
        }
    }

    /// <summary>
    /// 出力形式に対応する拡張子を返す。
    /// </summary>
    public static string GetExtension(OutputFormat format) => format switch
    {
        OutputFormat.Jpeg => ".jpg",
        OutputFormat.Png => ".png",
        OutputFormat.WebP => ".webp",
        OutputFormat.Avif => ".avif",
        OutputFormat.Tiff => ".tif",
        _ => ".jpg"
    };

    /// <summary>
    /// 入力ファイルの拡張子から出力形式を推測する。
    /// FormatConvert ステップが無効な場合に元の形式を維持するために使用。
    /// </summary>
    private static OutputFormat GetFormatFromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => OutputFormat.Png,
            ".webp" => OutputFormat.WebP,
            ".avif" => OutputFormat.Avif,
            ".tif" or ".tiff" => OutputFormat.Tiff,
            _ => OutputFormat.Jpeg
        };
    }

    private sealed record PackageImageReplacement(string EntryName, byte[] Data);
    private readonly record struct PackageImageTargetSize(int Width, int Height);

    private static long OptimizeOpenXmlPackage(string inputPath, string outputPath, bool stripMetadata, bool repackPackage, bool compressImages, bool convertImagesToWebP, int imageQuality, bool resizeImagesByPpi, int targetImagePpi, bool compressMedia, string ffmpegPath, int videoCrf, string videoCodec, string audioCodec,
        bool useOxipng = false, string oxipngPath = "oxipng", int oxipngLevel = 2,
        bool useJpegli = false, string cjpegliPath = "cjpegli", bool resetCellSelection = false, Action<string>? logAction = null)
    {
        if (!stripMetadata && !repackPackage && !compressImages && !resizeImagesByPpi && !compressMedia && !resetCellSelection)
        {
            return CopyFile(inputPath, outputPath);
        }

        if (stripMetadata && !repackPackage && !compressImages && !resizeImagesByPpi && !compressMedia && !resetCellSelection)
        {
            using var probe = ZipFile.OpenRead(inputPath);
            if (!probe.Entries.Any(entry => IsOpenXmlMetadataEntry(entry.FullName)))
                return CopyFile(inputPath, outputPath);
        }

        // 再パック無効でもパッケージを書き直す以上は Deflate を維持する
        // （NoCompression だと元より大きいファイルになってしまう）
        var compressionLevel = repackPackage ? CompressionLevel.SmallestSize : CompressionLevel.Optimal;

        using (var source = ZipFile.OpenRead(inputPath))
        using (var output = File.Create(outputPath))
        using (var destination = new ZipArchive(output, ZipArchiveMode.Create))
        {
            var imageReplacements = compressImages || resizeImagesByPpi
                ? BuildPackageImageReplacements(source, imageQuality, compressImages, convertImagesToWebP, resizeImagesByPpi, targetImagePpi, useOxipng, oxipngPath, oxipngLevel, useJpegli, cjpegliPath, logAction)
                : new Dictionary<string, PackageImageReplacement>(StringComparer.OrdinalIgnoreCase);
            var imageEntryRenames = imageReplacements
                .Where(kvp => !PackagePathEquals(kvp.Key, kvp.Value.EntryName))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.EntryName, StringComparer.OrdinalIgnoreCase);

            var mediaReplacements = compressMedia
                ? BuildMediaReplacements(source, ffmpegPath, videoCrf, videoCodec, audioCodec, logAction)
                : new Dictionary<string, PackageImageReplacement>(StringComparer.OrdinalIgnoreCase);
            var mediaEntryRenames = mediaReplacements
                .Where(kvp => !PackagePathEquals(kvp.Key, kvp.Value.EntryName))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.EntryName, StringComparer.OrdinalIgnoreCase);

            var allReplacements = imageReplacements
                .Concat(mediaReplacements)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            var allRenames = imageEntryRenames
                .Concat(mediaEntryRenames)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in source.Entries)
            {
                var entryName = NormalizePackagePath(entry.FullName);
                // OPC 仕様上、mimetype は必ず無圧縮 (STORED) でなければならない
                var entryCompressionLevel = IsMimetypeEntry(entry.FullName)
                    ? CompressionLevel.NoCompression
                    : compressionLevel;

                if (allReplacements.TryGetValue(entryName, out var replacement))
                {
                    WriteEntry(destination, replacement.EntryName, entryCompressionLevel, entry.LastWriteTime, replacement.Data);
                    continue;
                }

                var destinationEntry = destination.CreateEntry(entry.FullName, entryCompressionLevel);
                destinationEntry.LastWriteTime = entry.LastWriteTime;
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                using var sourceStream = entry.Open();
                using var destinationStream = destinationEntry.Open();

                byte[]? xmlReplacement = null;
                if (stripMetadata && IsOpenXmlMetadataEntry(entry.FullName))
                {
                    xmlReplacement = SanitizeOpenXmlProperties(sourceStream, entry.FullName);
                }
                else if (allRenames.Count > 0 && IsContentTypesEntry(entry.FullName))
                {
                    xmlReplacement = UpdateContentTypes(sourceStream, allRenames);
                }
                else if (allRenames.Count > 0 && IsRelationshipEntry(entry.FullName))
                {
                    xmlReplacement = UpdateRelationshipTargets(sourceStream, entry.FullName, allRenames);
                }
                else if (resetCellSelection && IsExcelWorksheetEntry(entry.FullName))
                {
                    xmlReplacement = ResetSheetSelections(sourceStream);
                }

                if (xmlReplacement != null)
                    destinationStream.Write(xmlReplacement, 0, xmlReplacement.Length);
                else
                    sourceStream.CopyTo(destinationStream);
            }
        }

        return new FileInfo(outputPath).Length;
    }

    private static long CopyFile(string inputPath, string outputPath) { File.Copy(inputPath, outputPath, true); return new FileInfo(outputPath).Length; }

    private static Dictionary<string, PackageImageReplacement> BuildPackageImageReplacements(ZipArchive source, int quality, bool compressImages, bool convertToWebP, bool resizeImagesByPpi, int targetImagePpi,
        bool useOxipng = false, string oxipngPath = "oxipng", int oxipngLevel = 2,
        bool useJpegli = false, string cjpegliPath = "cjpegli", Action<string>? logAction = null)
    {
        var replacements = new Dictionary<string, PackageImageReplacement>(StringComparer.OrdinalIgnoreCase);
        var usedEntryNames = source.Entries
            .Select(entry => NormalizePackagePath(entry.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetSizes = resizeImagesByPpi
            ? BuildPackageImageTargetSizes(source, targetImagePpi)
            : new Dictionary<string, PackageImageTargetSize>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in source.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || !IsPackageImage(entry.FullName))
                continue;

            // サムネイル画像は Office が JPEG/PNG を期待するため WebP 変換・圧縮をスキップ
            if (IsThumbnailEntry(entry.FullName))
                continue;

            var entryName = NormalizePackagePath(entry.FullName);
            var outputEntryName = convertToWebP && Path.GetExtension(entryName).ToLowerInvariant() != ".webp"
                ? GetUniqueWebPEntryName(entryName, usedEntryNames)
                : entryName;

            using var stream = entry.Open();
            var imageTargetSize = targetSizes.TryGetValue(entryName, out var targetSize)
                ? targetSize
                : (PackageImageTargetSize?)null;
            var replacement = TryOptimizePackageImage(stream, entryName, outputEntryName, quality, compressImages, convertToWebP, imageTargetSize, useOxipng, oxipngPath, oxipngLevel, useJpegli, cjpegliPath, logAction);
            if (replacement != null)
            {
                replacements[entryName] = replacement;
                usedEntryNames.Add(replacement.EntryName);
            }
        }

        return replacements;
    }

    private static Dictionary<string, PackageImageTargetSize> BuildPackageImageTargetSizes(ZipArchive source, int targetPpi)
    {
        targetPpi = Math.Clamp(targetPpi, 72, 600);
        var targetSizes = new Dictionary<string, PackageImageTargetSize>(StringComparer.OrdinalIgnoreCase);
        var unknownSizeTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in source.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || !IsOfficeXmlPart(entry.FullName))
                continue;

            var entryName = NormalizePackagePath(entry.FullName);
            var relationships = LoadRelationshipTargets(source, entryName);
            if (relationships.Count == 0)
                continue;

            try
            {
                using var stream = entry.Open();
                var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
                foreach (var blip in document.Descendants().Where(element => element.Name.LocalName.Equals("blip", StringComparison.OrdinalIgnoreCase)))
                {
                    var relationId = blip.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals("embed", StringComparison.OrdinalIgnoreCase))?.Value;
                    if (string.IsNullOrWhiteSpace(relationId) || !relationships.TryGetValue(relationId, out var imageEntryName))
                        continue;

                    if (!TryGetBlipDisplaySizePixels(blip, targetPpi, out var width, out var height))
                    {
                        unknownSizeTargets.Add(imageEntryName);
                        continue;
                    }

                    if (targetSizes.TryGetValue(imageEntryName, out var current))
                    {
                        width = Math.Max(width, current.Width);
                        height = Math.Max(height, current.Height);
                    }

                    targetSizes[imageEntryName] = new PackageImageTargetSize(width, height);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BuildPackageImageTargetSizes failed for {entryName}: {ex.Message}");
            }
        }

        foreach (var imageEntryName in unknownSizeTargets)
            targetSizes.Remove(imageEntryName);

        return targetSizes;
    }

    private static void WriteEntry(ZipArchive destination, string entryName, CompressionLevel compressionLevel, DateTimeOffset lastWriteTime, byte[] data)
    {
        var destinationEntry = destination.CreateEntry(entryName, compressionLevel);
        destinationEntry.LastWriteTime = lastWriteTime;
        using var destinationStream = destinationEntry.Open();
        destinationStream.Write(data, 0, data.Length);
    }

    private static bool IsOpenXmlMetadataEntry(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        return normalized.Equals("docProps/core.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("docProps/app.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("docProps/custom.xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackageImage(string entryName)
    {
        var ext = Path.GetExtension(entryName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".tif" or ".tiff";
    }

    private static bool IsOfficeXmlPart(string entryName)
    {
        var normalized = NormalizePackagePath(entryName);
        return normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
               && !normalized.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> LoadRelationshipTargets(ZipArchive source, string sourceEntryName)
    {
        var relationships = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var relationshipEntryName = GetRelationshipEntryName(sourceEntryName);
        var relationshipEntry = source.GetEntry(relationshipEntryName);
        if (relationshipEntry == null)
            return relationships;

        var baseDirectory = GetPackageEntryDirectory(sourceEntryName);
        try
        {
            using var stream = relationshipEntry.Open();
            var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            foreach (var relationship in document.Descendants().Where(element => element.Name.LocalName.Equals("Relationship", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.Equals(relationship.Attribute("TargetMode")?.Value, "External", StringComparison.OrdinalIgnoreCase))
                    continue;

                var id = relationship.Attribute("Id")?.Value;
                var target = relationship.Attribute("Target")?.Value;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(target))
                    continue;

                var (targetPath, _) = SplitPackageTarget(target);
                var resolvedTarget = ResolvePackageTarget(baseDirectory, targetPath);
                if (!string.IsNullOrEmpty(resolvedTarget) && IsPackageImage(resolvedTarget))
                    relationships[id] = resolvedTarget;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadRelationshipTargets failed for {relationshipEntryName}: {ex.Message}");
        }

        return relationships;
    }

    private static bool TryGetBlipDisplaySizePixels(XElement blip, int targetPpi, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!TryGetBlipDisplaySizeEmu(blip, out var cx, out var cy))
            return false;

        width = Math.Max(1, (int)Math.Ceiling(cx / EmusPerInch * targetPpi));
        height = Math.Max(1, (int)Math.Ceiling(cy / EmusPerInch * targetPpi));
        return width > 0 && height > 0;
    }

    private static bool TryGetBlipDisplaySizeEmu(XElement blip, out long cx, out long cy)
    {
        cx = 0;
        cy = 0;

        foreach (var ancestor in blip.Ancestors())
        {
            var sizeElement = FindDisplaySizeElement(ancestor);
            if (TryReadEmuSize(sizeElement, out cx, out cy))
                return true;
        }

        return false;
    }

    private static XElement? FindDisplaySizeElement(XElement container)
    {
        var directExtent = container.Elements().FirstOrDefault(IsSizeElement);
        if (directExtent != null)
            return directExtent;

        var transformExtent = container.Descendants().FirstOrDefault(element =>
            element.Name.LocalName.Equals("ext", StringComparison.OrdinalIgnoreCase)
            && element.Parent?.Name.LocalName.Equals("xfrm", StringComparison.OrdinalIgnoreCase) == true
            && HasEmuSize(element));
        if (transformExtent != null)
            return transformExtent;

        return container.Descendants().FirstOrDefault(IsSizeElement);
    }

    private static bool IsSizeElement(XElement element)
    {
        return (element.Name.LocalName.Equals("extent", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("ext", StringComparison.OrdinalIgnoreCase))
               && HasEmuSize(element);
    }

    private static bool HasEmuSize(XElement element)
        => element.Attribute("cx") != null && element.Attribute("cy") != null;

    private static bool TryReadEmuSize(XElement? element, out long cx, out long cy)
    {
        cx = 0;
        cy = 0;
        if (element == null)
            return false;

        return long.TryParse(element.Attribute("cx")?.Value, out cx)
               && long.TryParse(element.Attribute("cy")?.Value, out cy)
               && cx > 0
               && cy > 0;
    }

    private static readonly HashSet<string> MediaVideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mov", ".avi", ".mp4" };
    private static readonly HashSet<string> MediaAudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav" };
    private static readonly HashSet<string> MediaAllExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mov", ".avi", ".mp4", ".mp3", ".wav" };

    private static bool IsPackageMedia(string entryName)
    {
        var ext = Path.GetExtension(entryName);
        return MediaAllExtensions.Contains(ext);
    }

    private static string GetMediaOutputExtension(string entryName)
    {
        var ext = Path.GetExtension(entryName);
        if (MediaVideoExtensions.Contains(ext))
            return ".mp4";
        if (MediaAudioExtensions.Contains(ext))
            return ".mp3";
        return ext;
    }

    private static readonly Lazy<string?> CachedFfmpegPath = new(() =>
    {
        var candidates = new[]
        {
            "ffmpeg",
            "ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin", "ffmpeg.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (TryRunFfmpegVersion(candidate))
                return candidate;
        }

        return null;
    });

    private static string? FindFfmpeg(string ffmpegPath)
    {
        ffmpegPath = (ffmpegPath ?? "").Trim();

        // User-specified path takes priority
        if (!string.IsNullOrEmpty(ffmpegPath) && ffmpegPath != "ffmpeg")
        {
            if (TryRunFfmpegVersion(ffmpegPath))
                return ffmpegPath;
        }

        return CachedFfmpegPath.Value;
    }

    private static bool TryRunFfmpegVersion(string ffmpegPath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, PackageImageReplacement> BuildMediaReplacements(ZipArchive source, string ffmpegPath, int videoCrf, string videoCodec, string audioCodec, Action<string>? logAction = null)
    {
        var replacements = new Dictionary<string, PackageImageReplacement>(StringComparer.OrdinalIgnoreCase);
        var usedEntryNames = source.Entries
            .Select(entry => NormalizePackagePath(entry.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resolvedFfmpeg = FindFfmpeg(ffmpegPath);
        if (resolvedFfmpeg == null)
            return replacements;

        foreach (var entry in source.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || !IsPackageMedia(entry.FullName))
                continue;

            var entryName = NormalizePackagePath(entry.FullName);
            var outputExt = GetMediaOutputExtension(entryName);
            var baseName = Path.GetFileNameWithoutExtension(entryName);
            var dir = entryName.Contains('/') ? entryName[..entryName.LastIndexOf('/')] : "";
            var candidatePath = JoinPackagePath(dir, baseName + outputExt);

            var outputEntryName = PackagePathEquals(candidatePath, entryName)
                ? entryName
                : GetUniqueEntryName(candidatePath, usedEntryNames);

            using var stream = entry.Open();
            var replacement = TryCompressMediaWithFfmpeg(stream, entryName, outputEntryName, resolvedFfmpeg, videoCrf, videoCodec, audioCodec, logAction);
            if (replacement != null)
            {
                replacements[entryName] = replacement;
                usedEntryNames.Add(replacement.EntryName);
            }
        }

        return replacements;
    }

    private static PackageImageReplacement? TryCompressMediaWithFfmpeg(Stream stream, string entryName, string outputEntryName, string ffmpegPath, int videoCrf, string videoCodec, string audioCodec, Action<string>? logAction = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FileMill_Media_" + Guid.NewGuid().ToString("N"));
        try
        {
            videoCodec = NormalizeCodecName(videoCodec);
            audioCodec = NormalizeCodecName(audioCodec);

            Directory.CreateDirectory(tempDir);
            var ext = Path.GetExtension(entryName);
            var inputFile = Path.Combine(tempDir, "input" + ext);
            var outputFile = Path.Combine(tempDir, "output" + Path.GetExtension(outputEntryName));

            // Read original bytes
            byte[] original;
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                original = ms.ToArray();
            }

            File.WriteAllBytes(inputFile, original);

            var args = new List<string>
            {
                "-i", inputFile,
                "-y"
            };

            bool isVideo = MediaVideoExtensions.Contains(ext);

            if (isVideo)
            {
                if (!string.IsNullOrEmpty(videoCodec))
                {
                    args.Add("-codec:v");
                    args.Add(videoCodec);
                }
                args.Add("-crf");
                args.Add(videoCrf.ToString());
            }

            if (!string.IsNullOrEmpty(audioCodec))
            {
                args.Add("-codec:a");
                args.Add(audioCodec);
            }

            args.Add(outputFile);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            foreach (var arg in args)
                process.StartInfo.ArgumentList.Add(arg);

            if (logAction != null)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logAction($"[ffmpeg] {e.Data}");
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logAction($"[ffmpeg] {e.Data}");
                };
            }

            process.Start();

            // Drain stdout/stderr to prevent deadlock when ffmpeg fills the buffer
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = process.WaitForExit(120000); // 2 min timeout for large media
            if (!completed)
            {
                try { process.Kill(); } catch { }
                logAction?.Invoke("[ffmpeg] タイムアウトまたは処理強制終了");
                return null;
            }

            if (process.ExitCode != 0 || !File.Exists(outputFile))
            {
                logAction?.Invoke($"[ffmpeg] エラー (終了コード: {process.ExitCode})");
                return null;
            }

            var compressed = File.ReadAllBytes(outputFile);
            if (compressed.Length >= original.Length)
                return null;

            return new PackageImageReplacement(outputEntryName, compressed);
        }
        catch (Exception ex)
        {
            logAction?.Invoke($"[ffmpeg] 例外エラー: {ex.Message}");
            return null;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static string NormalizeCodecName(string codec)
    {
        codec = (codec ?? "").Trim();
        var parenIndex = codec.IndexOf('(');
        if (parenIndex >= 0)
            codec = codec[..parenIndex].Trim();
        var spaceIndex = codec.IndexOf(' ');
        if (spaceIndex >= 0)
            codec = codec[..spaceIndex].Trim();
        return codec;
    }

    private static string GetUniqueEntryName(string candidate, ISet<string> usedEntryNames)
    {
        if (!usedEntryNames.Contains(candidate))
            return candidate;

        var dir = candidate.Contains('/') ? candidate[..candidate.LastIndexOf('/')] : "";
        var fileName = candidate.Contains('/') ? candidate[(candidate.LastIndexOf('/') + 1)..] : candidate;
        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);

        var suffix = 1;
        string candidate2;
        do
        {
            candidate2 = JoinPackagePath(dir, $"{baseName}_{suffix}{ext}");
            suffix++;
        } while (usedEntryNames.Contains(candidate2));

        return candidate2;
    }

    private static bool IsContentTypesEntry(string entryName)
        => NormalizePackagePath(entryName).Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsMimetypeEntry(string entryName)
        => NormalizePackagePath(entryName).Equals("mimetype", StringComparison.OrdinalIgnoreCase);

    private static bool IsThumbnailEntry(string entryName)
        => NormalizePackagePath(entryName).StartsWith("docProps/thumbnail.", StringComparison.OrdinalIgnoreCase);

    private static bool IsRelationshipEntry(string entryName)
        => NormalizePackagePath(entryName).EndsWith(".rels", StringComparison.OrdinalIgnoreCase);

    private static bool IsExcelWorksheetEntry(string entryName)
    {
        var normalized = NormalizePackagePath(entryName);
        return normalized.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
               && normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Excel ワークシート XML 内の sheetView/selection を A1 にリセットする。
    /// </summary>
    private static byte[]? ResetSheetSelections(Stream stream)
    {
        byte[]? original = null;
        try
        {
            using var input = new MemoryStream();
            stream.CopyTo(input);
            original = input.ToArray();
            using var documentStream = new MemoryStream(original);
            var document = XDocument.Load(documentStream, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null)
                return original;

            var ns = root.Name.Namespace;
            bool modified = false;

            foreach (var sheetView in document.Descendants(ns + "sheetView"))
            {
                // topLeftCell をリセット
                var topLeftCell = sheetView.Attribute("topLeftCell");
                if (topLeftCell != null && !string.Equals(topLeftCell.Value, "A1", StringComparison.OrdinalIgnoreCase))
                {
                    topLeftCell.Value = "A1";
                    modified = true;
                }

                // 既存の selection 要素をリセットまたは追加
                var selections = sheetView.Elements(ns + "selection").ToList();
                if (selections.Count > 0)
                {
                    // 最初の selection を A1 にリセットし、余分な selection を削除
                    var first = selections[0];
                    if (first.Attribute("activeCell")?.Value != "A1" || first.Attribute("sqref")?.Value != "A1")
                    {
                        first.SetAttributeValue("activeCell", "A1");
                        first.SetAttributeValue("sqref", "A1");
                        modified = true;
                    }
                    // pane 属性があれば削除（分割ペインの選択状態をクリア）
                    var paneAttr = first.Attribute("pane");
                    if (paneAttr != null)
                    {
                        paneAttr.Remove();
                        modified = true;
                    }
                    for (int i = 1; i < selections.Count; i++)
                    {
                        selections[i].Remove();
                        modified = true;
                    }
                }
            }

            if (!modified)
                return original;

            return SaveXmlWithoutBom(document);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ResetSheetSelections failed: {ex.Message}");
            return original;
        }
    }

    private static byte[]? SanitizeOpenXmlProperties(Stream stream, string entryName)
    {
        byte[]? original = null;
        try
        {
            using var input = new MemoryStream();
            stream.CopyTo(input);
            original = input.ToArray();
            using var documentStream = new MemoryStream(original);
            var document = XDocument.Load(documentStream, LoadOptions.PreserveWhitespace);

            var normalizedEntryName = NormalizePackagePath(entryName);
            if (normalizedEntryName.Equals("docProps/custom.xml", StringComparison.OrdinalIgnoreCase))
            {
                RemoveCustomProperties(document);
            }
            else if (normalizedEntryName.Equals("docProps/app.xml", StringComparison.OrdinalIgnoreCase))
            {
                SanitizeExtendedProperties(document);
            }
            else
            {
                SanitizeCoreProperties(document);
            }

            return SaveXmlWithoutBom(document);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SanitizeOpenXmlProperties failed for {entryName}: {ex.Message}");
            return original;
        }
    }

    private static byte[] SaveXmlWithoutBom(XDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, SaveOptions.DisableFormatting);
        var bytes = output.ToArray();
        // XDocument.Save は UTF-8 BOM (EF BB BF) を付加するため除去
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return bytes[3..];
        return bytes;
    }

    private static void SanitizeCoreProperties(XDocument document)
    {
        foreach (var element in document.Descendants().Where(e => !e.HasElements).ToList())
        {
            if (element.Name.LocalName.Equals("creator", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("lastModifiedBy", StringComparison.OrdinalIgnoreCase))
            {
                element.Value = "";
            }
            else if (element.Name.LocalName.Equals("revision", StringComparison.OrdinalIgnoreCase))
            {
                element.Value = "0";
            }
            else if (element.Name.LocalName.Equals("lastPrinted", StringComparison.OrdinalIgnoreCase))
            {
                element.Remove();
            }
        }
    }

    private static void SanitizeExtendedProperties(XDocument document)
    {
        foreach (var element in document.Descendants().Where(e => !e.HasElements).ToList())
        {
            if (element.Name.LocalName.Equals("company", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("manager", StringComparison.OrdinalIgnoreCase))
            {
                element.Value = "";
            }
            else if (element.Name.LocalName.Equals("totalTime", StringComparison.OrdinalIgnoreCase))
            {
                element.Value = "0";
            }
            else if (element.Name.LocalName.Equals("lastPrinted", StringComparison.OrdinalIgnoreCase))
            {
                element.Remove();
            }
        }
    }

    private static void RemoveCustomProperties(XDocument document)
    {
        foreach (var property in document.Descendants().Where(e => e.Name.LocalName.Equals("property", StringComparison.OrdinalIgnoreCase)).ToList())
            property.Remove();
    }

    private static PackageImageReplacement? TryOptimizePackageImage(Stream stream, string entryName, string outputEntryName, int quality, bool compressImage, bool convertToWebP, PackageImageTargetSize? targetSize,
        bool useOxipng = false, string oxipngPath = "oxipng", int oxipngLevel = 2,
        bool useJpegli = false, string cjpegliPath = "cjpegli", Action<string>? logAction = null)
    {
        try
        {
            using var input = new MemoryStream();
            stream.CopyTo(input);
            var original = input.ToArray();
            using var image = Image.NewFromBuffer(original, "");
            Image? resizedImage = null;
            var processed = image;
            var resized = false;

            if (targetSize is { } size && size.Width > 0 && size.Height > 0 && image.Width > 0 && image.Height > 0)
            {
                var scale = Math.Min(size.Width / (double)image.Width, size.Height / (double)image.Height);
                if (scale < 0.999)
                {
                    resizedImage = image.Resize(scale);
                    processed = resizedImage;
                    resized = true;
                    logAction?.Invoke($"[Office画像] {Path.GetFileName(entryName)}: {image.Width}x{image.Height} -> {processed.Width}x{processed.Height}");
                }
            }

            if (!compressImage && !convertToWebP && !resized)
            {
                resizedImage?.Dispose();
                return null;
            }

            byte[] optimized;
            var ext = Path.GetExtension(entryName).ToLowerInvariant();
            if (convertToWebP)
            {
                optimized = processed.WebpsaveBuffer(
                    q: Math.Clamp(quality, 10, 100),
                    effort: 4,
                    keep: Enums.ForeignKeep.None);
                resizedImage?.Dispose();
                return new PackageImageReplacement(outputEntryName, optimized);
            }

            if (ext is ".jpg" or ".jpeg")
            {
                if (useJpegli)
                {
                    var tempPng = Path.Combine(Path.GetTempPath(), "FileMill_temp_" + Guid.NewGuid().ToString("N") + ".png");
                    var tempJpg = Path.Combine(Path.GetTempPath(), "FileMill_temp_" + Guid.NewGuid().ToString("N") + ".jpg");
                    try
                    {
                        processed.Pngsave(tempPng, compression: 1);
                        var ok = RunCjpegli(tempPng, tempJpg, cjpegliPath, Math.Clamp(quality, 10, 100), logAction);
                        if (ok && File.Exists(tempJpg))
                        {
                            optimized = File.ReadAllBytes(tempJpg);
                        }
                        else
                        {
                            optimized = processed.JpegsaveBuffer(
                                q: Math.Clamp(quality, 10, 100),
                                optimizeCoding: true,
                                keep: Enums.ForeignKeep.None);
                        }
                    }
                    finally
                    {
                        try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch {}
                        try { if (File.Exists(tempJpg)) File.Delete(tempJpg); } catch {}
                    }
                }
                else
                {
                    optimized = processed.JpegsaveBuffer(
                        q: Math.Clamp(quality, 10, 100),
                        optimizeCoding: true,
                        keep: Enums.ForeignKeep.None);
                }
            }
            else if (ext == ".png")
            {
                if (useOxipng)
                {
                    var tempPng = Path.Combine(Path.GetTempPath(), "FileMill_temp_" + Guid.NewGuid().ToString("N") + ".png");
                    try
                    {
                        processed.Pngsave(tempPng, compression: 1, keep: Enums.ForeignKeep.None);
                        RunOxipng(tempPng, oxipngPath, oxipngLevel, true, logAction);
                        optimized = File.ReadAllBytes(tempPng);
                    }
                    finally
                    {
                        try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch {}
                    }
                }
                else
                {
                    optimized = processed.PngsaveBuffer(
                        compression: 9,
                        filter: Enums.ForeignPngFilter.All,
                        keep: Enums.ForeignKeep.None);
                }
            }
            else if (ext == ".webp")
            {
                optimized = processed.WebpsaveBuffer(
                    q: Math.Clamp(quality, 10, 100),
                    effort: 4,
                    keep: Enums.ForeignKeep.None);
            }
            else
            {
                resizedImage?.Dispose();
                return null;
            }

            resizedImage?.Dispose();
            return resized || (compressImage && optimized.Length < original.Length)
                ? new PackageImageReplacement(outputEntryName, optimized)
                : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TryOptimizePackageImage failed for {entryName}: {ex.Message}");
            return null;
        }
    }

    private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".webp"] = "image/webp",
        [".mp4"] = "video/mp4",
        [".mp3"] = "audio/mpeg",
    };

    private static byte[] UpdateContentTypes(Stream stream, IReadOnlyDictionary<string, string> entryRenames)
    {
        var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidDataException("[Content_Types].xml is missing a root element.");
        var ns = root.Name.Namespace;

        foreach (var element in root.Elements(ns + "Override"))
        {
            var partName = element.Attribute("PartName");
            if (partName == null)
                continue;

            var normalizedPartName = NormalizePackagePath(partName.Value);
            if (entryRenames.TryGetValue(normalizedPartName, out var replacementName))
            {
                partName.Value = "/" + replacementName;
                var newExt = Path.GetExtension(replacementName);
                if (ExtensionToContentType.TryGetValue(newExt, out var contentType))
                    element.SetAttributeValue("ContentType", contentType);
            }
        }

        // リネームで実際に導入された拡張子だけ Default を保証する
        var introducedExtensions = entryRenames.Values
            .Select(Path.GetExtension)
            .Where(ext => !string.IsNullOrEmpty(ext))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (ext, contentType) in ExtensionToContentType)
        {
            if (!introducedExtensions.Contains(ext))
                continue;

            var existingDefault = root.Elements(ns + "Default").FirstOrDefault(element =>
                string.Equals(element.Attribute("Extension")?.Value, ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
            if (existingDefault == null)
            {
                root.Add(new XElement(ns + "Default",
                    new XAttribute("Extension", ext.TrimStart('.')),
                    new XAttribute("ContentType", contentType)));
            }
            else
            {
                existingDefault.SetAttributeValue("ContentType", contentType);
            }
        }

        return SaveXmlWithoutBom(document);
    }

    private static byte[]? UpdateRelationshipTargets(Stream stream, string relationshipEntryName, IReadOnlyDictionary<string, string> imageEntryRenames)
    {
        using var input = new MemoryStream();
        stream.CopyTo(input);
        var original = input.ToArray();
        using var documentStream = new MemoryStream(original);
        var document = XDocument.Load(documentStream, LoadOptions.PreserveWhitespace);
        var baseDirectory = GetRelationshipSourceDirectory(relationshipEntryName);
        var modified = false;

        foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "Relationship"))
        {
            if (string.Equals(element.Attribute("TargetMode")?.Value, "External", StringComparison.OrdinalIgnoreCase))
                continue;

            var target = element.Attribute("Target");
            if (target == null)
                continue;

            var (targetPath, suffix) = SplitPackageTarget(target.Value);
            var resolvedTarget = ResolvePackageTarget(baseDirectory, targetPath);
            if (!imageEntryRenames.TryGetValue(resolvedTarget, out var replacementName))
                continue;

            var updatedTarget = targetPath.StartsWith("/", StringComparison.Ordinal)
                ? "/" + replacementName
                : GetRelativePackagePath(baseDirectory, replacementName);
            target.Value = updatedTarget + suffix;
            modified = true;
        }

        return modified ? SaveXmlWithoutBom(document) : original;
    }

    private static string GetUniqueWebPEntryName(string entryName, ISet<string> usedEntryNames)
    {
        var baseName = Path.GetFileNameWithoutExtension(entryName);
        var dir = entryName.Contains('/') ? entryName[..entryName.LastIndexOf('/')] : "";
        return GetUniqueEntryName(JoinPackagePath(dir, baseName + ".webp"), usedEntryNames);
    }

    private static string GetRelationshipEntryName(string sourceEntryName)
    {
        var normalized = NormalizePackagePath(sourceEntryName);
        var slash = normalized.LastIndexOf('/');
        var directory = slash >= 0 ? normalized[..slash] : "";
        var fileName = slash >= 0 ? normalized[(slash + 1)..] : normalized;
        return JoinPackagePath(JoinPackagePath(directory, "_rels"), fileName + ".rels");
    }

    private static string GetPackageEntryDirectory(string entryName)
    {
        var normalized = NormalizePackagePath(entryName);
        var slash = normalized.LastIndexOf('/');
        return slash >= 0 ? normalized[..slash] : "";
    }

    private static string GetRelationshipSourceDirectory(string relationshipEntryName)
    {
        var normalized = NormalizePackagePath(relationshipEntryName);
        if (normalized.Equals("_rels/.rels", StringComparison.OrdinalIgnoreCase))
            return "";

        var marker = "/_rels/";
        var markerIndex = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return "";

        var prefix = normalized[..markerIndex];
        var relationshipFileName = normalized[(markerIndex + marker.Length)..];
        var sourceFileName = relationshipFileName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
            ? relationshipFileName[..^5]
            : relationshipFileName;
        var sourcePart = JoinPackagePath(prefix, sourceFileName);
        var slash = sourcePart.LastIndexOf('/');
        return slash >= 0 ? sourcePart[..slash] : "";
    }

    private static (string Path, string Suffix) SplitPackageTarget(string target)
    {
        var queryIndex = target.IndexOf('?');
        var fragmentIndex = target.IndexOf('#');
        var splitIndex = (queryIndex, fragmentIndex) switch
        {
            (>= 0, >= 0) => Math.Min(queryIndex, fragmentIndex),
            (>= 0, _) => queryIndex,
            (_, >= 0) => fragmentIndex,
            _ => -1
        };

        return splitIndex < 0
            ? (target, "")
            : (target[..splitIndex], target[splitIndex..]);
    }

    private static string ResolvePackageTarget(string baseDirectory, string targetPath)
    {
        if (Uri.TryCreate(targetPath, UriKind.Absolute, out _))
            return "";

        if (targetPath.StartsWith("/", StringComparison.Ordinal))
            return NormalizePackagePath(targetPath);

        return NormalizePackagePath(JoinPackagePath(baseDirectory, targetPath));
    }

    private static string GetRelativePackagePath(string baseDirectory, string targetEntryName)
    {
        var from = SplitPackagePath(baseDirectory);
        var to = SplitPackagePath(targetEntryName);

        var common = 0;
        while (common < from.Length && common < to.Length && string.Equals(from[common], to[common], StringComparison.OrdinalIgnoreCase))
            common++;

        var segments = Enumerable.Repeat("..", from.Length - common)
            .Concat(to.Skip(common));
        return string.Join("/", segments);
    }

    private static string NormalizePackagePath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        var segments = new List<string>();
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }

        return string.Join("/", segments);
    }

    private static string[] SplitPackagePath(string path)
        => NormalizePackagePath(path).Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string JoinPackagePath(string directory, string relativePath)
        => string.IsNullOrEmpty(directory) ? relativePath : directory.TrimEnd('/') + "/" + relativePath;

    private static bool PackagePathEquals(string left, string right)
        => string.Equals(NormalizePackagePath(left), NormalizePackagePath(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Microsoft Office COM Interop を使って Office Open XML ファイルを PDF に変換する。
    /// </summary>
    public long ConvertOfficeToPdf(string inputPath, string outputPath, bool usePdfA, Action<string>? logAction = null, string? displayOutputPath = null)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        if (!IsOfficePdfConversionAvailableForExtension(ext))
            throw new NotSupportedException(string.Format(Properties.Loc.StatusOfficePdfInteropUnavailable, ext));

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                ConvertOfficeToPdfCore(inputPath, outputPath, ext, usePdfA, logAction, displayOutputPath);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
            throw new Exception($"PDF変換エラー: {error.Message}", error);

        return new FileInfo(outputPath).Length;
    }

    private static void ConvertOfficeToPdfCore(string inputPath, string outputPath, string extension, bool usePdfA, Action<string>? logAction, string? displayOutputPath)
    {
        inputPath = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);
        var logOutputPath = string.IsNullOrWhiteSpace(displayOutputPath) ? outputPath : displayOutputPath;
        logAction?.Invoke($"PDF変換: {Path.GetFileName(inputPath)} -> {Path.GetFileName(logOutputPath)}{(usePdfA ? " (PDF/A)" : "")}");

        switch (extension)
        {
            case ".docx":
                ConvertWordToPdf(inputPath, outputPath, usePdfA);
                break;
            case ".xlsx":
                ConvertExcelToPdf(inputPath, outputPath, usePdfA);
                break;
            case ".pptx":
                ConvertPowerPointToPdf(inputPath, outputPath, usePdfA);
                break;
            default:
                throw new NotSupportedException($"PDF変換対象外の形式です: {extension}");
        }
    }

    private static void ConvertWordToPdf(string inputPath, string outputPath, bool usePdfA)
    {
        object? app = null;
        object? documents = null;
        object? document = null;
        try
        {
            app = CreateOfficeApplication("Word.Application", "Word");
            dynamic word = app;
            word.Visible = false;
            word.DisplayAlerts = 0;

            documents = word.Documents;
            document = ((dynamic)documents).Open(inputPath, false, true, false);
            ((dynamic)document).ExportAsFixedFormat(OutputFileName: outputPath, ExportFormat: WordPdfExportFormat, OpenAfterExport: false, UseISO19005_1: usePdfA);
        }
        finally
        {
            TryCloseComDocument(document);
            TryQuitComApplication(app);
            ReleaseComObject(document);
            ReleaseComObject(documents);
            ReleaseComObject(app);
        }
    }

    private static void ConvertExcelToPdf(string inputPath, string outputPath, bool usePdfA)
    {
        if (usePdfA)
            throw new NotSupportedException(Properties.Loc.StatusOfficePdfAUnsupportedForExcel);

        object? app = null;
        object? workbooks = null;
        object? workbook = null;
        try
        {
            app = CreateOfficeApplication("Excel.Application", "Excel");
            dynamic excel = app;
            excel.Visible = false;
            excel.DisplayAlerts = false;

            workbooks = excel.Workbooks;
            workbook = ((dynamic)workbooks).Open(inputPath, 0, true);
            ((dynamic)workbook).ExportAsFixedFormat(ExcelPdfExportType, outputPath);
        }
        finally
        {
            TryCloseComDocument(workbook);
            TryQuitComApplication(app);
            ReleaseComObject(workbook);
            ReleaseComObject(workbooks);
            ReleaseComObject(app);
        }
    }

    private static void ConvertPowerPointToPdf(string inputPath, string outputPath, bool usePdfA)
    {
        object? app = null;
        object? presentations = null;
        object? presentation = null;
        try
        {
            app = CreateOfficeApplication("PowerPoint.Application", "PowerPoint");
            dynamic powerPoint = app;

            presentations = powerPoint.Presentations;
            presentation = ((dynamic)presentations).Open(inputPath, -1, 0, 0);
            ExportPowerPointPresentationToPdf((dynamic)presentation, outputPath, usePdfA);
        }
        finally
        {
            TryCloseComDocument(presentation);
            TryQuitComApplication(app);
            ReleaseComObject(presentation);
            ReleaseComObject(presentations);
            ReleaseComObject(app);
        }
    }

    private static void ExportPowerPointPresentationToPdf(dynamic presentation, string outputPath, bool usePdfA)
    {
        try
        {
            if (usePdfA)
            {
                presentation.ExportAsFixedFormat(
                    outputPath,
                    PowerPointPdfFixedFormatType,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    true);
            }
            else
            {
                presentation.ExportAsFixedFormat(outputPath, PowerPointPdfFixedFormatType);
            }
        }
        catch (Exception) when (!usePdfA)
        {
            presentation.SaveAs(outputPath, PowerPointSaveAsPdfFileType, 0);
        }
    }

    private static object CreateOfficeApplication(string progId, string appName)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: false)
            ?? throw new InvalidOperationException($"{appName} の Microsoft Office Interop が利用できません。");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"{appName} を起動できませんでした。");
    }

    private static bool HasComProgId(string progId)
    {
        try
        {
            return Type.GetTypeFromProgID(progId, throwOnError: false) != null;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCloseComDocument(object? document)
    {
        if (document == null)
            return;

        try
        {
            ((dynamic)document).Close(false);
        }
        catch
        {
            try
            {
                ((dynamic)document).Close();
            }
            catch
            {
            }
        }
    }

    private static void TryQuitComApplication(object? app)
    {
        if (app == null)
            return;

        try
        {
            ((dynamic)app).Quit();
        }
        catch
        {
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject == null)
            return;

        try
        {
            if (Marshal.IsComObject(comObject))
                Marshal.FinalReleaseComObject(comObject);
        }
        catch
        {
        }
    }

    /// <summary>
    /// ファイルを最適化（Office Open XMLのクリーンアップ、画像圧縮、WebP変換）し、処理後のファイルサイズを返す。
    /// </summary>
    public long Optimize(string inputPath, string outputPath, bool stripMetadata, bool cleanUnused, bool compressImages, bool convertToWebP, int webpQuality, bool compressMedia = false, string ffmpegPath = "ffmpeg", int videoCrf = 23, string videoCodec = "libx264", string audioCodec = "libmp3lame",
        bool useOxipng = false, string oxipngPath = "oxipng", int oxipngLevel = 2,
        bool useJpegli = false, string cjpegliPath = "cjpegli", bool resizeImagesByPpi = false, int targetImagePpi = 220, bool resetCellSelection = false, Action<string>? logAction = null)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // 1. Office Open XML ファイルの場合
        if (ext == ".docx" || ext == ".xlsx" || ext == ".pptx")
        {
            return OptimizeOpenXmlPackage(inputPath, outputPath, stripMetadata, cleanUnused, compressImages, convertToWebP, webpQuality, resizeImagesByPpi, targetImagePpi, compressMedia, ffmpegPath, videoCrf, videoCodec, audioCodec, useOxipng, oxipngPath, oxipngLevel, useJpegli, cjpegliPath, resetCellSelection: resetCellSelection, logAction: logAction);
        }

        // 2. 画像ファイルの場合 (JPEG, PNG, WEBP, AVIF 等)
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tif", ".tiff", ".bmp" };
        if (imageExtensions.Contains(ext))
        {
            try
            {
                using var image = Image.NewFromFile(inputPath, memory: true);
                var finalPath = outputPath;
                var keep = stripMetadata ? Enums.ForeignKeep.None : Enums.ForeignKeep.Exif;

                if (convertToWebP)
                {
                    // 渡された一時ファイルパスに直接保存する
                    image.Webpsave(finalPath, q: webpQuality, keep: keep);
                }
                else if (compressImages)
                {
                    // 画像の再圧縮
                    if (ext == ".jpg" || ext == ".jpeg")
                    {
                        if (useJpegli)
                        {
                            var tempPng = Path.Combine(Path.GetTempPath(), "FileMill_temp_" + Guid.NewGuid().ToString("N") + ".png");
                            try
                            {
                                image.Pngsave(tempPng, compression: 1);
                                var ok = RunCjpegli(tempPng, finalPath, cjpegliPath, webpQuality, logAction);
                                if (!ok)
                                {
                                    image.Jpegsave(finalPath, q: webpQuality, optimizeCoding: true, keep: keep);
                                }
                            }
                            finally
                            {
                                try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch {}
                            }
                        }
                        else
                        {
                            image.Jpegsave(finalPath, q: webpQuality, optimizeCoding: true, keep: keep);
                        }
                    }
                    else if (ext == ".png")
                    {
                        if (useOxipng)
                        {
                            image.Pngsave(finalPath, compression: 1, keep: keep);
                            RunOxipng(finalPath, oxipngPath, oxipngLevel, stripMetadata, logAction);
                        }
                        else
                        {
                            image.Pngsave(finalPath, compression: 8, keep: keep);
                        }
                    }
                    else if (ext == ".webp")
                    {
                        image.Webpsave(finalPath, q: webpQuality, keep: keep);
                    }
                    else
                    {
                        File.Copy(inputPath, finalPath, true);
                    }
                }
                else
                {
                    File.Copy(inputPath, finalPath, true);
                }

                return new FileInfo(finalPath).Length;
            }
            catch (Exception ex)
            {
                throw new Exception($"最適化エラー: {ex.Message}", ex);
            }
        }

        // 3. その他のファイル形式
        throw new NotSupportedException($"このファイル形式は最適化対象外です: {ext}");
    }

    private static string ResolveToolPath(string configuredPath, string defaultName)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return defaultName;
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;
        if (!configuredPath.Contains('/') && !configuredPath.Contains("\\"))
            return configuredPath;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    private static void RunOxipng(string filePath, string oxipngPath, int level, bool stripMetadata, Action<string>? logAction = null)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = ResolveToolPath(oxipngPath, "oxipng"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            processInfo.ArgumentList.Add("-o");
            processInfo.ArgumentList.Add(level.ToString());
            processInfo.ArgumentList.Add("--strip");
            processInfo.ArgumentList.Add(stripMetadata ? "all" : "none");
            processInfo.ArgumentList.Add(filePath);

            using var process = new Process { StartInfo = processInfo };
            if (logAction != null)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logAction($"[oxipng] {e.Data}");
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logAction($"[oxipng] {e.Data}");
                };
            }

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (process.WaitForExit(30000)) // 30 seconds timeout
                {
                    process.WaitForExit(); // 非同期出力をフラッシュ
                }
                else
                {
                    try { process.Kill(); } catch {}
                    logAction?.Invoke("[oxipng] 処理がタイムアウトしたためプロセスを終了しました。");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"oxipng failed: {ex.Message}");
            logAction?.Invoke($"[oxipng] エラー: {ex.Message}");
        }
    }

    private static bool RunCjpegli(string inputPath, string outputPath, string cjpegliPath, int quality, Action<string>? logAction = null)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = ResolveToolPath(cjpegliPath, "cjpegli"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            processInfo.ArgumentList.Add("-q");
            processInfo.ArgumentList.Add(quality.ToString());
            processInfo.ArgumentList.Add(inputPath);
            processInfo.ArgumentList.Add(outputPath);

            using var process = new Process { StartInfo = processInfo };
            if (logAction != null)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logAction($"[cjpegli] {e.Data}");
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logAction($"[cjpegli] {e.Data}");
                };
            }

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var completed = process.WaitForExit(30000); // 30 seconds timeout
                if (completed)
                    process.WaitForExit(); // 非同期出力をフラッシュ
                else
                    try { process.Kill(); } catch {}
                return completed && process.ExitCode == 0 && File.Exists(outputPath);
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"cjpegli failed: {ex.Message}");
            logAction?.Invoke($"[cjpegli] エラー: {ex.Message}");
            return false;
        }
    }

    private sealed record OfficePdfInteropAvailability(bool Word, bool Excel, bool PowerPoint)
    {
        public bool IsAnyAvailable => Word || Excel || PowerPoint;
    }
}
