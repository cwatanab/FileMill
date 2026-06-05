using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileMill.Helpers;

public static class OutputPathHelper
{
    public static string GetUniqueSuffixedPath(
        string originalPath,
        string suffix,
        string outputExtension,
        string outputDirectory,
        ISet<string>? reservedPaths = null)
    {
        var directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(originalPath) ?? ""
            : outputDirectory;
        var fileName = Path.GetFileNameWithoutExtension(originalPath);
        var extension = string.IsNullOrWhiteSpace(outputExtension) ? Path.GetExtension(originalPath) : outputExtension;
        var suffixedFileName = GetSuffixedFileName(fileName, suffix);
        var candidate = Path.Combine(directory, suffixedFileName + extension);

        var counter = 1;
        while (File.Exists(candidate) || (reservedPaths?.Contains(candidate) ?? false))
        {
            candidate = Path.Combine(directory, $"{suffixedFileName}_{counter}{extension}");
            counter++;
        }

        return candidate;
    }

    public static string GetTemporarySiblingPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}{extension}");
    }

    private static string GetSuffixedFileName(string fileName, string? suffix)
    {
        if (string.IsNullOrEmpty(suffix))
            return fileName;

        if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return fileName;

        var numberedSuffixPrefix = suffix + "_";
        var numberedSuffixIndex = fileName.LastIndexOf(numberedSuffixPrefix, StringComparison.OrdinalIgnoreCase);
        if (numberedSuffixIndex >= 0)
        {
            var numberStart = numberedSuffixIndex + numberedSuffixPrefix.Length;
            if (numberStart < fileName.Length && fileName.Skip(numberStart).All(char.IsDigit))
                return fileName[..(numberedSuffixIndex + suffix.Length)];
        }

        return fileName + suffix;
    }
}
